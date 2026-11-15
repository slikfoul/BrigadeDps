using System.Text.Json;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrigadeDps;

[HarmonyPatch]
internal static class GameBridge
{
    private const string Channel = "slikfoul.BrigadeDps/state/2";
    internal const int ExplosionSource = -10, TornadoSource = -20, MiscSource = -30;
    private static readonly BattleLedger ledger = new();
    private static readonly HashSet<ulong> observedBosses = new(), livingBosses = new();
    private static PlayerStats[] nativePlayerStats = Array.Empty<PlayerStats>();
    private static RunManager run;
    private static RunOverviewPanel results;
    private static CustomMessagingManager messaging;
    private static CustomMessagingManager.HandleNamedMessageDelegate receiver;
    private static float nextRoster, nextSend, nextPanelLookup, nextError;
    private static double lastTime, lastBossDeath;
    private static bool ended;
    internal static MeterSnapshot Current = new();
    internal static bool Received { get; private set; }
    internal static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    internal static bool Running => !ended && run != null && !run.WasCollected && run.IsSpawned && run.runHasStarted;
    internal static bool ResultsOpen => results != null && !results.WasCollected && results.IsOpen && results.gameObject.activeInHierarchy;
    internal static bool Solo
    {
        get
        {
            var manager = NetworkManager.Singleton;
            return manager == null || (manager.IsServer && Current.Players.Count <= 1);
        }
    }
    private static double Clock => run != null && !run.WasCollected ? Math.Max(lastTime, run.currentRunTimeInSeconds) : lastTime;

    internal static void Tick()
    {
        try
        {
            var manager = NetworkManager.Singleton;
            var bus = manager != null && manager.IsListening ? manager.CustomMessagingManager : null;
            if (bus != null && (messaging == null || messaging.Pointer != bus.Pointer))
            {
                messaging = bus;
                receiver ??= DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(
                    new Action<ulong, FastBufferReader>(Receive));
                bus.RegisterNamedMessageHandler(Channel, receiver);
            }
            if (bus == null) messaging = null;
            if (Time.unscaledTime >= nextPanelLookup)
            {
                nextPanelLookup = Time.unscaledTime + 1;
                if (results == null || results.WasCollected) results = Object.FindObjectOfType<RunOverviewPanel>(true);
                if (run == null || run.WasCollected) run = Object.FindObjectOfType<RunManager>();
            }
            if (!IsHost) return;
            // A final boss can open the results panel before the next UI tick.
            // Finalize its killing hit even if the run timer has already stopped.
            if (ledger.BossActive && livingBosses.Count == 0)
            {
                lastTime = Clock;
                ledger.FinishBoss(lastBossDeath);
                Current = CreateSnapshot();
                Publish();
            }
            if (!Running) return;
            lastTime = Clock;
            if (Time.unscaledTime >= nextRoster)
            {
                nextRoster = Time.unscaledTime + 2;
                foreach (var player in Object.FindObjectsOfType<GameplayPlayerIdentity>())
                {
                    if (!player.IsSpawned) continue;
                    ledger.Identify(player.OwnerClientId, player.GetDisplayName(), ColorUtility.ToHtmlStringRGB(player.PlayerColor));
                }
                nativePlayerStats = Object.FindObjectsOfType<PlayerStats>().ToArray();
            }
            Current = CreateSnapshot();
            if (Time.unscaledTime >= nextSend) Publish();
        }
        catch (Exception ex) { Error("Update", ex); }
    }

    private static MeterSnapshot CreateSnapshot()
    {
        var result = ledger.Snapshot(lastTime, EntryPoint.Settings.Window.Value);
        foreach (var player in result.Players)
        {
            // Retain the last native sample if the player object is already gone
            // while the run's final snapshot is being assembled.
            var previous = Current.Players.FirstOrDefault(p => p.Id == player.Id);
            if (previous != null)
            {
                player.GameDps = previous.GameDps;
                foreach (var spell in player.Spells)
                    spell.GameDps = previous.Spells.FirstOrDefault(s => s.Spell == spell.Spell)?.GameDps;
            }
            if (ended) continue; // Keep results fixed even if the Unity clock advances before teardown.
            var native = nativePlayerStats.FirstOrDefault(p => p != null && !p.WasCollected && p.IsSpawned && p.OwnerClientId == player.Id);
            if (native == null) continue;
            try
            {
                var values = new Dictionary<int, double?>();
                foreach (var spell in native.GetEquippedSpellStats())
                {
                    var attributes = spell.Value;
                    if (attributes == null) continue;
                    values[(int)spell.Key] = DpsDisplay.NativeValue(attributes.Dps, attributes.TotalPerformedDamage, attributes.TotalTime);
                }
                player.GameDps = DpsDisplay.Sum(values.Values);
                foreach (var spell in player.Spells)
                    spell.GameDps = values.TryGetValue(spell.Spell, out var value) ? value : null;
            }
            catch (Exception ex) { Error("Native spell DPS", ex); }
        }
        return result;
    }

    private static void Publish()
    {
        nextSend = Time.unscaledTime + .5f;
        if (!IsHost || messaging == null) return;
        string json = JsonSerializer.Serialize(Current);
        var buffer = new FastBufferWriter(json.Length * 2 + 64, Allocator.Temp, -1);
        try
        {
            buffer.WriteValueSafe(json, false);
            messaging.SendNamedMessageToAll(Channel, buffer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        finally { buffer.Dispose(); }
    }

    private static void Receive(ulong sender, FastBufferReader reader)
    {
        try
        {
            if (IsHost || sender != NetworkManager.ServerClientId || reader.Length > 262144) return;
            reader.ReadValueSafe(out string json, false);
            var snapshot = JsonSerializer.Deserialize<MeterSnapshot>(json);
            if (snapshot == null || snapshot.Protocol != 2 || snapshot.Players == null || snapshot.Players.Count > 16) return;
            if (!double.IsFinite(snapshot.Seconds) || snapshot.Seconds < 0 || snapshot.Window is < 1 or > 60) return;
            if (snapshot.BossEndedAt is double bossEnd && (!double.IsFinite(bossEnd) || bossEnd < 0 || bossEnd > snapshot.Seconds)) return;
            foreach (var p in snapshot.Players)
            {
                if (p == null || p.Spells == null || p.Spells.Count > 256 || !double.IsFinite(p.Damage) || !double.IsFinite(p.Dps) ||
                    !double.IsFinite(p.BossDamage) || !double.IsFinite(p.BossDps) || p.Damage < 0 || p.BossDamage < 0) return;
                if (p.Spells.Any(s => s == null || !double.IsFinite(s.Dps) || !double.IsFinite(s.Damage) || s.Dps < 0 || s.Damage < 0)) return;
                if (p.GameDps.HasValue && (!double.IsFinite(p.GameDps.Value) || p.GameDps.Value < 0)) return;
                if (p.Spells.Any(s => s.GameDps.HasValue && (!double.IsFinite(s.GameDps.Value) || s.GameDps.Value < 0))) return;
            }
            Current = snapshot; Received = true;
        }
        catch (Exception ex) { Error("Network snapshot", ex); }
    }

    private static void NewRun(RunManager instance)
    {
        run = instance; ended = false; lastTime = 0; lastBossDeath = 0;
        ledger.Clear(); observedBosses.Clear(); livingBosses.Clear();
        nativePlayerStats = Array.Empty<PlayerStats>();
        Current = new MeterSnapshot(); Received = false; nextSend = nextRoster = 0;
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnNetworkSpawn)), HarmonyPostfix]
    private static void Spawned(RunManager __instance) => NewRun(__instance);

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRunOnClientRpc)), HarmonyPrefix]
    private static void Starting(RunManager __instance)
    {
        if (__instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute) NewRun(__instance);
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnDestroy)), HarmonyPrefix]
    private static void LeavingRun(RunManager __instance)
    {
        try
        {
            if (run == null || run.Pointer != __instance.Pointer) return;
            if (IsHost)
            {
                lastTime = Clock;
                if (ledger.BossActive) ledger.FinishBoss(livingBosses.Count == 0 ? lastBossDeath : lastTime);
                Current = CreateSnapshot();
                Publish();
            }
        }
        catch (Exception ex) { Error("Final snapshot", ex); }
        finally { ended = true; run = null; }
    }

    [HarmonyPatch(typeof(RunOverviewPanel), nameof(RunOverviewPanel.Open)), HarmonyPostfix]
    private static void ResultsShown(RunOverviewPanel __instance)
    {
        results = __instance;
        try
        {
            if (IsHost && !ended)
            {
                lastTime = Clock;
                if (ledger.BossActive) ledger.FinishBoss(livingBosses.Count == 0 ? lastBossDeath : lastTime);
                Current = CreateSnapshot();
                Publish();
            }
        }
        catch (Exception ex) { Error("Results snapshot", ex); }
        finally { ended = true; }
    }

    private static void ObserveBoss(EnemyHealthContainer enemy)
    {
        if (!IsHost || !enemy.IsBoss || !enemy.IsSpawned) return;
        ulong id = enemy.NetworkObjectId;
        if (!observedBosses.Add(id)) return;
        if (livingBosses.Count == 0) ledger.StartBoss(Clock);
        livingBosses.Add(id);
    }

    [HarmonyPatch(typeof(HealthContainer), nameof(HealthContainer.OnNetworkSpawn)), HarmonyPostfix]
    private static void HealthSpawned(HealthContainer __instance)
    {
        try { var enemy = __instance.TryCast<EnemyHealthContainer>(); if (enemy != null) ObserveBoss(enemy); }
        catch (Exception ex) { Error("Boss spawn", ex); }
    }

    [HarmonyPatch(typeof(EnemyHealthContainer), nameof(EnemyHealthContainer.Initialize)), HarmonyPostfix]
    private static void EnemyInitialized(EnemyHealthContainer __instance)
    {
        try { ObserveBoss(__instance); }
        catch (Exception ex) { Error("Boss initialization", ex); }
    }

    [HarmonyPatch(typeof(EnemyHealthContainer), nameof(EnemyHealthContainer.HealthDepleted)), HarmonyPostfix]
    private static void BossDied(EnemyHealthContainer __instance) => RetireBoss(__instance);

    [HarmonyPatch(typeof(HealthContainer), nameof(HealthContainer.OnNetworkDespawn)), HarmonyPrefix]
    private static void HealthDespawned(HealthContainer __instance) => RetireBoss(__instance);

    private static void RetireBoss(HealthContainer target)
    {
        try
        {
            if (IsHost && livingBosses.Remove(target.NetworkObjectId) && livingBosses.Count == 0)
                lastBossDeath = Clock; // defer finalization until after the killing damage has been recorded
        }
        catch (Exception ex) { Error("Boss end", ex); }
    }

    [HarmonyPatch(typeof(EnemyHealthContainer), nameof(EnemyHealthContainer.RecordDamageDealtToEnemy)), HarmonyPostfix]
    private static void DamageRecorded(EnemyHealthContainer __instance, float amount, DamageInfo damageSource)
    {
        try
        {
            if (!IsHost || !EntryPoint.Settings.Enabled.Value || !float.IsFinite(amount) || amount <= 0 || damageSource == null) return;
            ulong owner;
            int spell;
            var spellDamage = damageSource.TryCast<PlayerSpellDamageInfo>();
            if (spellDamage != null) { owner = spellDamage.OwnerClientId; spell = (int)spellDamage.Spell; }
            else
            {
                var playerDamage = damageSource.TryCast<PlayerDamageInfo>();
                var explosion = damageSource.TryCast<PlayerExplosionDamageInfo>();
                var tornado = damageSource.TryCast<InfusedTornadoDamageInfo>();
                if (playerDamage != null) { owner = playerDamage.OwnerClientId; spell = MiscSource; }
                else if (explosion != null) { owner = explosion.OwnerClientId; spell = ExplosionSource; }
                else if (tornado != null) { owner = tornado.InfusedByPlayerId; spell = TornadoSource; }
                else return;
            }
            bool boss = __instance.IsBoss;
            if (boss) ObserveBoss(__instance);
            ledger.Record(owner, spell, amount, Clock, boss);
        }
        catch (Exception ex) { Error("Damage event", ex); }
    }

    internal static void Error(string operation, Exception ex)
    {
        if (Time.unscaledTime < nextError) return;
        nextError = Time.unscaledTime + 10;
        EntryPoint.Instance.Log.LogError($"{operation}: {ex}");
    }
}

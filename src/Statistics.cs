namespace BrigadeDps;

// Pure managed statistics, independent of Unity and the transport.
public sealed class DamageSeries
{
    private readonly double[] values = new double[601];
    private readonly long[] ticks = Enumerable.Repeat(long.MinValue, 601).ToArray();
    public double Total { get; private set; }
    public Dictionary<int, DamageSeries> Spells { get; } = new();

    public void Add(double time, double damage, int spell)
    {
        if (!double.IsFinite(time) || time < 0 || !double.IsFinite(damage) || damage <= 0) return;
        AddHit(time, damage);
        if (!Spells.TryGetValue(spell, out var series)) Spells[spell] = series = new DamageSeries();
        series.AddHit(time, damage);
    }

    private void AddHit(double time, double damage)
    {
        long tick = (long)Math.Floor(time * 10);
        int slot = (int)(tick % values.Length);
        if (ticks[slot] != tick) { ticks[slot] = tick; values[slot] = 0; }
        values[slot] += damage;
        Total += damage;
    }

    public double Rate(double time, double start, int window)
    {
        window = Math.Clamp(window, 1, 60);
        long end = (long)Math.Floor(time * 10);
        double recent = 0;
        for (int i = 0; i < values.Length; i++)
            if (ticks[i] > end - window * 10 && ticks[i] <= end) recent += values[i];
        return recent / Math.Clamp(time - start, 0.1, window);
    }
}

public sealed class SpellScore
{
    public int Spell { get; set; }
    public double Damage { get; set; }
    public double Dps { get; set; }
    public double? GameDps { get; set; }
}

public enum DpsDisplayMode { Rolling, Game }

public static class DpsDisplay
{
    public static double? Select(DpsDisplayMode mode, double rolling, double? native) =>
        mode == DpsDisplayMode.Game ? native : rolling;

    public static double? NativeValue(double value, double damage, double elapsed) =>
        double.IsFinite(value) && value >= 0 ? value :
        damage == 0 && elapsed <= 0 ? 0 : null;

    public static double? Sum(IEnumerable<double?> values)
    {
        double total = 0;
        bool any = false;
        foreach (var value in values)
        {
            if (!value.HasValue || !double.IsFinite(value.Value) || value.Value < 0) return null;
            total += value.Value; any = true;
        }
        return any && double.IsFinite(total) ? total : null;
    }
}

public sealed class PlayerScore
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "FFFFFF";
    public double Damage { get; set; }
    public double Dps { get; set; }
    public double? GameDps { get; set; }
    public double BossDamage { get; set; }
    public double BossDps { get; set; }
    public List<SpellScore> Spells { get; set; } = new();
}

public sealed class MeterSnapshot
{
    public int Protocol { get; set; } = 2;
    public double Seconds { get; set; }
    public int Window { get; set; } = 10;
    public bool HasBoss { get; set; }
    public bool BossActive { get; set; }
    public double BossSeconds { get; set; }
    public double? BossEndedAt { get; set; }
    public List<PlayerScore> Players { get; set; } = new();
}

// Native totals are cumulative for the whole run. They replace display totals,
// never feed the rolling hit buffer or the separate boss encounter counters.
public sealed class GameDamageTotals
{
    private readonly Dictionary<ulong, (double Total, Dictionary<int, double> Spells)> players = new();

    public void Clear() => players.Clear();

    public bool Update(ulong player, double total, IEnumerable<KeyValuePair<int, double>> spells)
    {
        if (!double.IsFinite(total) || total < 0 || spells == null) return false;
        var values = new Dictionary<int, double>();
        foreach (var spell in spells)
        {
            if (spell.Key < 0 || !double.IsFinite(spell.Value) || spell.Value < 0) return false;
            values[spell.Key] = spell.Value;
        }
        players[player] = (total, values);
        return true;
    }

    public void Apply(MeterSnapshot snapshot)
    {
        foreach (var entry in players)
        {
            var player = snapshot.Players.FirstOrDefault(p => p.Id == entry.Key);
            if (player == null)
            {
                player = new PlayerScore { Id = entry.Key };
                snapshot.Players.Add(player);
            }
            player.Damage = entry.Value.Total;
            foreach (var native in entry.Value.Spells)
            {
                var spell = player.Spells.FirstOrDefault(s => s.Spell == native.Key);
                if (spell == null)
                {
                    spell = new SpellScore { Spell = native.Key };
                    player.Spells.Add(spell);
                }
                spell.Damage = native.Value;
            }
            player.Spells.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        }
        snapshot.Players.Sort((a, b) => b.Damage.CompareTo(a.Damage));
    }
}

public sealed class BattleLedger
{
    private readonly Dictionary<ulong, DamageSeries> all = new();
    private readonly Dictionary<ulong, DamageSeries> boss = new();
    private readonly Dictionary<ulong, (string Name, string Color)> names = new();
    private double bossStart, bossStop;
    public bool HasBoss { get; private set; }
    public bool BossActive { get; private set; }

    public void Clear()
    {
        all.Clear(); boss.Clear(); names.Clear();
        HasBoss = BossActive = false; bossStart = bossStop = 0;
    }

    public void Identify(ulong id, string name, string color)
    {
        names[id] = (name, color);
        if (!all.ContainsKey(id)) all[id] = new DamageSeries();
    }

    public void StartBoss(double time)
    {
        boss.Clear(); bossStart = time; bossStop = time;
        HasBoss = BossActive = true;
    }

    public void FinishBoss(double time)
    {
        if (!BossActive) return;
        bossStop = Math.Max(bossStart, time); BossActive = false;
    }

    public void Record(ulong id, int spell, double damage, double time, bool onBoss)
    {
        if (!all.TryGetValue(id, out var total)) all[id] = total = new DamageSeries();
        total.Add(time, damage, spell);
        if (!onBoss) return;
        if (!HasBoss) StartBoss(time);
        if (!boss.TryGetValue(id, out var target)) boss[id] = target = new DamageSeries();
        target.Add(time, damage, spell);
    }

    public MeterSnapshot Snapshot(double time, int window)
    {
        double bossNow = BossActive ? time : bossStop;
        var result = new MeterSnapshot {
            Seconds = time, Window = Math.Clamp(window, 1, 60), HasBoss = HasBoss,
            BossActive = BossActive, BossSeconds = HasBoss ? Math.Max(0, bossNow - bossStart) : 0,
            BossEndedAt = HasBoss && !BossActive ? bossStop : null
        };
        foreach (var entry in all)
        {
            names.TryGetValue(entry.Key, out var identity);
            boss.TryGetValue(entry.Key, out var target);
            result.Players.Add(new PlayerScore {
                Id = entry.Key, Name = identity.Name ?? "", Color = identity.Color ?? "FFFFFF",
                Damage = entry.Value.Total, Dps = entry.Value.Rate(time, 0, window),
                BossDamage = target?.Total ?? 0,
                // Boss damage averages over the entire encounter, independently
                // of the party's rolling window. bossNow freezes on defeat.
                BossDps = (target?.Total ?? 0) / Math.Max(0.1, result.BossSeconds),
                Spells = entry.Value.Spells.OrderByDescending(x => x.Value.Total)
                    .Select(x => new SpellScore { Spell = x.Key, Damage = x.Value.Total, Dps = x.Value.Rate(time, 0, window) }).ToList()
            });
        }
        result.Players.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return result;
    }
}

public static class VisibilityPolicy
{
    public static bool Show(bool enabled, bool running, bool resultsOpen, bool keepResults) =>
        enabled && (running || (resultsOpen && keepResults));
}

// The display timeout uses a local monotonic clock, so pauses, frozen results
// snapshots and hiding the meter do not restart or suspend the countdown.
public sealed class BossDisplayLifetime
{
    private bool hasBoss, active, finishedSeen;
    private double? lastEnd;
    private double finishedAt;

    public void Observe(MeterSnapshot state, double now)
    {
        hasBoss = state.HasBoss; active = state.BossActive;
        if (!hasBoss || active) { finishedSeen = false; lastEnd = null; return; }
        if (finishedSeen && lastEnd == state.BossEndedAt) return;
        double age = state.BossEndedAt is double end ? Math.Max(0, state.Seconds - end) : 0;
        finishedAt = now - age;
        lastEnd = state.BossEndedAt;
        finishedSeen = true;
    }

    public bool Show(double now, int holdSeconds) => hasBoss &&
        (active || (finishedSeen && holdSeconds > 0 && now - finishedAt < holdSeconds));
}

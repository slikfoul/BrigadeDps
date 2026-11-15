using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace BrigadeDps;

[BepInPlugin(Id, "Brigade DPS", "0.1.10")]
[BepInIncompatibility("girthy_mods.DamageMeter")]
public sealed class EntryPoint : BasePlugin
{
    public const string Id = "slikfoul.BrigadeDps";
    internal static EntryPoint Instance = null!;
    internal static Options Settings = null!;
    private MeterView view;
    public override void Load()
    {
        Instance = this;
        Settings = new Options(Config);
        new Harmony(Id).PatchAll(typeof(GameBridge));
        view = AddComponent<MeterView>();
        Log.LogInfo("Brigade DPS 0.1.10 loaded: F3 meter, F4 spells, F5 settings; configurable growth directions and draggable settings, per-spell DPS, RU/EN.");
    }
}

internal sealed class Options
{
    internal readonly ConfigEntry<bool> Enabled, Visible, Spells, Icons, Boss, Results, Solo, Locked, SpellNames, AbbreviateSpells;
    internal readonly ConfigEntry<float> Scale, Opacity, X, Y, Height;
    internal readonly ConfigEntry<bool> PlacementStored, AnchorRight, AnchorBottom;
    internal readonly ConfigEntry<float> EdgeMarginX, EdgeMarginY, SettingsX, SettingsY;
    internal readonly ConfigEntry<int> Window, SpellLimit, BossHoldSeconds;
    internal readonly ConfigEntry<DpsDisplayMode> DpsMode;
    internal readonly ConfigEntry<string> ToggleKey, DetailKey, SettingsKey;
    internal readonly ConfigFile File;
    internal Options(ConfigFile file)
    {
        File = file;
        Enabled = file.Bind("General", "Enabled", true, "Enable display and host damage collection.");
        Visible = file.Bind("Display", "Visible", true, "Show the panel; F3 toggles it.");
        Spells = file.Bind("Display", "Spells", true, "Show damage by spell.");
        Icons = file.Bind("Display", "Icons", true, "Show the game's spell icons.");
        Boss = file.Bind("Display", "Boss", true, "Show the current or last boss encounter.");
        BossHoldSeconds = file.Bind("Display", "BossHoldSeconds", 10, new ConfigDescription("Real-time seconds to keep defeated boss statistics visible. 0 hides them immediately. Local display preference.", new AcceptableValueRange<int>(0, 3600)));
        Results = file.Bind("Display", "Results", true, "Show only while the run results panel is open.");
        Solo = file.Bind("Display", "Solo", true, "Show during solo runs.");
        Locked = file.Bind("Window", "Locked", false, "Prevent moving the panel.");
        SpellNames = file.Bind("Display", "SpellNames", true, "Show spell names; when off, spell icons are always shown.");
        AbbreviateSpells = file.Bind("Display", "AbbreviateSpells", false, "Use the first letter of each word of the localized spell name.");
        Scale = file.Bind("Window", "Scale", 1f, new ConfigDescription("Panel scale.", new AcceptableValueRange<float>(0.6f, 1.8f)));
        Opacity = file.Bind("Window", "Opacity", 0.88f, new ConfigDescription("Panel background opacity.", new AcceptableValueRange<float>(0.1f, 1f)));
        X = file.Bind("Window", "X", 0.02f, new ConfigDescription("Horizontal position within available screen space (0..1).", new AcceptableValueRange<float>(0, 1)));
        Y = file.Bind("Window", "Y", 0.18f, new ConfigDescription("Vertical position from top within available screen space (0..1).", new AcceptableValueRange<float>(0, 1)));
        PlacementStored = file.Bind("Window", "PlacementStored", false, "Whether the saved edge placement has been initialized.");
        AnchorRight = file.Bind("Window", "AnchorRight", false, "Keep the right edge fixed and grow leftward.");
        AnchorBottom = file.Bind("Window", "AnchorBottom", false, "Keep the bottom edge fixed and grow upward.");
        SettingsX = file.Bind("SettingsWindow", "X", .5f, new ConfigDescription("Horizontal position within available screen space.", new AcceptableValueRange<float>(0, 1)));
        SettingsY = file.Bind("SettingsWindow", "Y", .5f, new ConfigDescription("Vertical position within available screen space.", new AcceptableValueRange<float>(0, 1)));
        EdgeMarginX = file.Bind("Window", "EdgeMarginX", .02f, new ConfigDescription("Distance from the chosen horizontal edge as fraction of screen width.", new AcceptableValueRange<float>(0, 1)));
        EdgeMarginY = file.Bind("Window", "EdgeMarginY", .18f, new ConfigDescription("Distance from the chosen vertical edge as fraction of screen height.", new AcceptableValueRange<float>(0, 1)));
        Height = file.Bind("Window", "MaxHeight", 0.72f, new ConfigDescription("Maximum height as fraction of the screen; scroll overflowing rows.", new AcceptableValueRange<float>(0.3f, 0.95f)));
        Window = file.Bind("General", "DpsSeconds", 10, new ConfigDescription("Rolling DPS window, controlled by host.", new AcceptableValueRange<int>(1, 60)));
        DpsMode = file.Bind("Display", "DpsMode", DpsDisplayMode.Rolling, "Rolling: recent host-defined interval. Game: each spell's native DPS since it was equipped; player row sums equipped spells. Boss DPS remains rolling. Local display preference.");
        SpellLimit = file.Bind("Display", "SpellLimit", 8, new ConfigDescription("Maximum spell rows per player.", new AcceptableValueRange<int>(1, 20)));
        ToggleKey = file.Bind("Keys", "Meter", "F3", "Unity Input System key name.");
        DetailKey = file.Bind("Keys", "Spells", "F4", "Unity Input System key name.");
        SettingsKey = file.Bind("Keys", "Settings", "F5", "Unity Input System key name.");
    }

    internal void SavePlacement(float x, float y, float width, float height, float screenWidth, float screenHeight, float padding)
    {
        var horizontal = WindowAnchorAxis.Capture(x, width, screenWidth, padding, AnchorRight.Value);
        var vertical = WindowAnchorAxis.Capture(y, height, screenHeight, padding, AnchorBottom.Value);
        bool autoSave = File.SaveOnConfigSet;
        File.SaveOnConfigSet = false;
        try
        {
            EdgeMarginX.Value = horizontal.MarginFraction;
            EdgeMarginY.Value = vertical.MarginFraction;
            X.Value = screenWidth > width ? Math.Clamp(x / (screenWidth - width), 0, 1) : 0;
            Y.Value = screenHeight > height ? Math.Clamp(y / (screenHeight - height), 0, 1) : 0;
            PlacementStored.Value = true;
        }
        finally { File.SaveOnConfigSet = autoSave; }
        File.Save();
    }

    internal void ResetPosition() { X.Value = .02f; Y.Value = .18f; Scale.Value = 1; Locked.Value = false; PlacementStored.Value = false; }
}

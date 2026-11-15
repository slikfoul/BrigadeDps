using System.Globalization;
using BepInEx.Configuration;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BrigadeDps;

public sealed class MeterView : MonoBehaviour
{
    public MeterView(IntPtr pointer) : base(pointer) { }
    private const float PanelWidth = 320, RowHeight = 25, ColumnGap = 6, RowInset = 4, FramePadding = 18;
    private static readonly Color Ink = new(.115f, .105f, .13f), Paper = new(.95f, .94f, .97f), Gold = new(.82f, .79f, .89f);
    private static readonly Color Muted = new(.62f, .68f, .68f);
    private Options cfg { [HideFromIl2Cpp] get => EntryPoint.Settings; }
    private RectTransform root, window, body, viewport, settings, settingsBody;
    private Canvas canvas;
    private ScrollRect meterScroll;
    private Image background;
    private TextMeshProUGUI settingsCaption;
    private TMP_FontAsset font;
    private Sprite stoneBorder, stoneFill;
    private Texture2D generatedFrameTexture;
    private Sprite generatedFrame;
    private Texture2D windowFrameTexture;
    private Sprite windowFrame;
    private Sprite cardStone, cardLines;
    private NativeCardFrame meterCardFrame, settingsCardFrame;
    private NativeCardDivider bossDivider;
    private readonly BossDisplayLifetime bossLifetime = new();
    private TMP_InputField bossRetentionInput;
    private readonly List<UnityAction<string>> textActions = new();
    private int dividerAfterRow = -1;
    private float nextCardSearch;
    private readonly List<UnityAction> actions = new();
    private readonly List<TableLine> lines = new();
    private readonly List<Action> settingsRefresh = new();
    private readonly List<TextMeshProUGUI> textElements = new();
    private readonly List<(TextMeshProUGUI Label, string Key)> localizedLabels = new();
    private readonly Dictionary<int, Sprite> sprites = new();
    private readonly Dictionary<int, string> spellNames = new();
    private string locale;
    private int used;
    private bool settingsOpen, dragging, settingsDragging, cursorOwned;
    private CursorLockMode savedLock;
    private bool savedVisible;
    private Vector2 dragOrigin, dragWindow;
    private Vector2 settingsDragOrigin, settingsDragWindow;
    private float nextDraw;
    private float currentHeight = 120;
    private float currentWidth = PanelWidth;
    private ConfigEntry<string> capturingKey;

    public void Update()
    {
        try
        {
            if (capturingKey != null) CaptureKey();
            else if (bossRetentionInput == null || !bossRetentionInput.isFocused)
            {
                if (KeyPressed(cfg.ToggleKey.Value)) cfg.Visible.Value = !cfg.Visible.Value;
                if (KeyPressed(cfg.DetailKey.Value)) cfg.Spells.Value = !cfg.Spells.Value;
                if (KeyPressed(cfg.SettingsKey.Value)) settingsOpen = !settingsOpen;
                if (settingsOpen && KeyPressed("Escape")) settingsOpen = false;
            }
            if (Time.unscaledTime >= nextDraw)
            {
                nextDraw = Time.unscaledTime + .2f;
                UpdateLanguage();
                GameBridge.Tick();
                bossLifetime.Observe(GameBridge.Current, Time.unscaledTimeAsDouble);
                bool eligible = VisibilityPolicy.Show(cfg.Enabled.Value, GameBridge.Running, GameBridge.ResultsOpen, cfg.Results.Value);
                if (!cfg.Solo.Value && GameBridge.Solo && !GameBridge.ResultsOpen) eligible = false;
                bool show = eligible && cfg.Visible.Value;
                if ((show || settingsOpen) && root == null) Build();
                if (root == null) return;
                window.gameObject.SetActive(show);
                settings.gameObject.SetActive(settingsOpen);
                if (show) DrawScores();
                if (settingsOpen) foreach (var refresh in settingsRefresh) refresh();
                if (!show) EndDrag();
                Place();
                UpdateCardFrames();
            }
            if (root == null) return;
            if (window.gameObject.activeSelf) Drag();
            DragSettings();
            if (!settingsOpen) capturingKey = null;
        }
        catch (Exception ex) { GameBridge.Error("Interface", ex); }
    }

    public void LateUpdate()
    {
        PlaceBossDivider();
        // Hold Alt if the game has captured the mouse. Settings always release it.
        bool alt = Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
        bool wantCursor = settingsOpen || dragging || (alt && window != null && window.gameObject.activeSelf);
        if (wantCursor)
        {
            if (!cursorOwned) { savedLock = Cursor.lockState; savedVisible = Cursor.visible; cursorOwned = true; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else ReleaseCursor();
    }

    private void ReleaseCursor()
    {
        if (!cursorOwned) return;
        Cursor.lockState = savedLock; Cursor.visible = savedVisible; cursorOwned = false;
    }

    public void OnDestroy()
    {
        EndDrag(); EndSettingsDrag(); ReleaseCursor();
        bossDivider?.DestroyArtwork();
        if (root != null) Object.Destroy(root.gameObject);
        if (generatedFrame != null) Object.Destroy(generatedFrame);
        if (generatedFrameTexture != null) Object.Destroy(generatedFrameTexture);
        if (windowFrame != null) Object.Destroy(windowFrame);
        if (windowFrameTexture != null) Object.Destroy(windowFrameTexture);
    }

    private static bool KeyPressed(string value) => Keyboard.current != null && Enum.TryParse<Key>(value, true, out var key)
        && key != Key.None && Keyboard.current[key].wasPressedThisFrame;

    private void CaptureKey()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { capturingKey = null; return; }
        foreach (var key in keyboard.allKeys)
            if (key.wasPressedThisFrame) { capturingKey.Value = key.keyCode.ToString(); capturingKey = null; break; }
    }

    private string Tr(string text) => UiText.Translate(text, locale);

    private void UpdateLanguage()
    {
        string language = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
        if (locale == language) return;
        locale = language; spellNames.Clear();
        SelectGameFont();
        foreach (var label in textElements) if (font != null) label.font = font;
        foreach (var item in localizedLabels) item.Label.text = Tr(item.Key);
    }

    private void SelectGameFont()
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (candidate.name != "TitilliumWeb-Regular SDF") continue;
            font = candidate; break;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
    }

    private void Build()
    {
        SelectGameFont();
        if (font == null) { EntryPoint.Instance.Log.LogWarning("No game font available yet."); return; }
        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.name == "ui_button_frame_default") stoneBorder = sprite;
            if (sprite.name == "ui_button_frame_background") stoneFill = sprite;
            if (sprite.name == "ui_store_card") cardStone = sprite;
            if (sprite.name == "ui_store_card_lines_white") cardLines = sprite;
        }
        if (stoneBorder == null) stoneBorder = CreateStoneFrame();
        windowFrame = CreateWindowFrame();
        root = Rect("BrigadeDps.UI", null, 0, 0, 0, 0);
        Object.DontDestroyOnLoad(root.gameObject);
        canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        window = Framed("Meter", root, PanelWidth, currentHeight);
        background = window.GetComponent<Image>();
        var scroll = Rect("Scroll", window, FramePadding, FramePadding, PanelWidth - 2 * FramePadding, 100).gameObject.AddComponent<ScrollRect>();
        meterScroll = scroll;
        viewport = Rect("Viewport", scroll.transform, 0, 0, PanelWidth - 2 * FramePadding, 100);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        body = Rect("Rows", viewport, 0, 0, PanelWidth - 2 * FramePadding, 100);
        scroll.viewport = viewport; scroll.content = body; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
        settings = Framed("Settings", root, 550, 700);
        settingsCaption = Text(settings, "BRIGADE DPS · НАСТРОЙКИ", 18, 8, 480, 32, 20, Gold);
        settingsCaption.overflowMode = TextOverflowModes.Overflow;
        Button(settings, "×", 510, 12, 28, 26, () => settingsOpen = false);
        Text(settings, "Изменения применяются и сохраняются сразу", 18, 46, 514, 25, 13, Muted);
        var settingsScroll = Rect("Scroll", settings, 14, 82, 522, 550).gameObject.AddComponent<ScrollRect>();
        var settingsViewport = Rect("Viewport", settingsScroll.transform, 0, 0, 522, 550);
        settingsViewport.gameObject.AddComponent<RectMask2D>();
        settingsViewport.gameObject.AddComponent<Image>().color = Color.clear;
        settingsBody = Rect("Options", settingsViewport, 0, 0, 522, 760);
        settingsScroll.viewport = settingsViewport; settingsScroll.content = settingsBody;
        settingsScroll.horizontal = false; settingsScroll.vertical = true;
        settingsScroll.movementType = ScrollRect.MovementType.Clamped; settingsScroll.scrollSensitivity = 28;
        BuildSettings();
        Button(settings, "Сбросить положение", 18, 648, 260, 30, () => { cfg.ResetPosition(); LayoutMeter(); Place(); });
        Button(settings, "Готово", 376, 648, 156, 30, () => settingsOpen = false);
        window.gameObject.SetActive(false); settings.gameObject.SetActive(settingsOpen);
    }

    private void BuildSettings()
    {
        float y = 0;
        Toggle("Мод включён", cfg.Enabled, ref y);
        Toggle("Показывать окно", cfg.Visible, ref y);
        Toggle("Строки заклинаний", cfg.Spells, ref y);
        Toggle("Иконки заклинаний", cfg.Icons, ref y);
        Toggle("Названия заклинаний", cfg.SpellNames, ref y);
        Toggle("Названия по первым буквам", cfg.AbbreviateSpells, ref y);
        Toggle("Статистика босса", cfg.Boss, ref y);
        BossRetentionField(ref y);
        Toggle("На экране результатов", cfg.Results, ref y);
        Toggle("В одиночной игре", cfg.Solo, ref y);
        Toggle("Закрепить окно", cfg.Locked, ref y);
        Direction("Рост по горизонтали", cfg.AnchorRight, "Вправо", "Влево", ref y);
        Direction("Рост по вертикали", cfg.AnchorBottom, "Вниз", "Вверх", ref y);
        Stepper("Масштаб", () => cfg.Scale.Value.ToString("0.0", CultureInfo.InvariantCulture),
            d => cfg.Scale.Value = Mathf.Clamp(cfg.Scale.Value + .1f * d, .6f, 1.8f), ref y);
        Stepper("Непрозрачность", () => $"{cfg.Opacity.Value:P0}",
            d => cfg.Opacity.Value = Mathf.Clamp(cfg.Opacity.Value + .1f * d, .1f, 1), ref y);
        Stepper("Предел высоты", () => $"{cfg.Height.Value:P0}",
            d => cfg.Height.Value = Mathf.Clamp(cfg.Height.Value + .05f * d, .3f, .95f), ref y);
        Stepper("Заклинаний на игрока", () => cfg.SpellLimit.Value.ToString(),
            d => cfg.SpellLimit.Value = Math.Clamp(cfg.SpellLimit.Value + d, 1, 20), ref y);
        DpsModeSetting(ref y);
        Stepper("Окно УВС (задаёт хост)", () => $"{(GameBridge.IsHost || !GameBridge.Running ? cfg.Window.Value : GameBridge.Current.Window)} {Tr("с")}",
            d => { if (GameBridge.IsHost || !GameBridge.Running) cfg.Window.Value = Math.Clamp(cfg.Window.Value + d, 1, 60); }, ref y);
        Shortcut("Показать / скрыть", cfg.ToggleKey, ref y);
        Shortcut("Строки заклинаний", cfg.DetailKey, ref y);
        Shortcut("Открыть настройки", cfg.SettingsKey, ref y);
        Text(settingsBody, UiText.Help,
            4, y + 10, 504, 110, 12, Muted);
        settingsBody.sizeDelta = new Vector2(522, y + 130);
    }

    [HideFromIl2Cpp]
    private void DpsModeSetting(ref float y)
    {
        Text(settingsBody, "УВС команды и заклинаний", 4, y, 326, 31, 15, Paper);
        var value = Button(settingsBody, "", 350, y, 150, 29, () =>
            cfg.DpsMode.Value = cfg.DpsMode.Value == DpsDisplayMode.Rolling ? DpsDisplayMode.Game : DpsDisplayMode.Rolling);
        settingsRefresh.Add(() => value.text = Tr(cfg.DpsMode.Value == DpsDisplayMode.Game ? "Как в игре" : "За интервал"));
        y += 37;
    }

    [HideFromIl2Cpp]
    private void BossRetentionField(ref float y)
    {
        Text(settingsBody, "Итоги босса после смерти (с)", 4, y, 322, 31, 15, Paper);
        Button(settingsBody, "−", 350, y, 32, 29, () => cfg.BossHoldSeconds.Value = Math.Max(0, cfg.BossHoldSeconds.Value - 1));
        var rt = Rect("BossHoldSeconds", settingsBody, 387, y, 76, 29);
        var fill = rt.gameObject.AddComponent<Image>(); fill.color = new Color(.16f, .15f, .19f, 1);
        var label = Text(rt, "", 3, 0, 70, 29, 14, Gold);
        label.alignment = TextAlignmentOptions.Center; label.richText = false;
        bossRetentionInput = rt.gameObject.AddComponent<TMP_InputField>();
        bossRetentionInput.textViewport = rt;
        bossRetentionInput.textComponent = label;
        bossRetentionInput.targetGraphic = fill;
        bossRetentionInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        bossRetentionInput.characterLimit = 4;
        bossRetentionInput.lineType = TMP_InputField.LineType.SingleLine;
        var callback = DelegateSupport.ConvertDelegate<UnityAction<string>>(new Action<string>(value => {
            if (int.TryParse(value, out int seconds)) cfg.BossHoldSeconds.Value = Math.Clamp(seconds, 0, 3600);
            bossRetentionInput.SetTextWithoutNotify(cfg.BossHoldSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }));
        textActions.Add(callback); bossRetentionInput.onEndEdit.AddListener(callback);
        Button(settingsBody, "+", 468, y, 32, 29, () => cfg.BossHoldSeconds.Value = Math.Min(3600, cfg.BossHoldSeconds.Value + 1));
        settingsRefresh.Add(() => {
            if (!bossRetentionInput.isFocused) bossRetentionInput.SetTextWithoutNotify(cfg.BossHoldSeconds.Value.ToString(CultureInfo.InvariantCulture));
        });
        y += 37;
    }

    [HideFromIl2Cpp]
    private void Toggle(string title, ConfigEntry<bool> option, ref float y)
    {
        Text(settingsBody, title, 4, y, 326, 31, 15, Paper);
        var value = Button(settingsBody, "", 350, y, 150, 29, () => {
            option.Value = !option.Value;
            if (option == cfg.SpellNames && !option.Value) cfg.Icons.Value = true;
            if (option == cfg.Icons && !option.Value) cfg.SpellNames.Value = true;
        });
        settingsRefresh.Add(() => { value.text = Tr(option.Value ? "ВКЛ" : "ВЫКЛ"); value.color = option.Value ? Paper : Muted; });
        y += 37;
    }

    [HideFromIl2Cpp]
    private void Direction(string title, ConfigEntry<bool> option, string near, string far, ref float y)
    {
        Text(settingsBody, title, 4, y, 326, 31, 15, Paper);
        var value = Button(settingsBody, "", 350, y, 150, 29, () => {
            option.Value = !option.Value;
            // Re-anchor at the current position without moving the panel.
            if (cfg.PlacementStored.Value)
            {
                float scale = EffectiveScale();
                cfg.SavePlacement(window.anchoredPosition.x, -window.anchoredPosition.y,
                    currentWidth * scale, currentHeight * scale, root.rect.width, root.rect.height, 6 * scale);
            }
        });
        settingsRefresh.Add(() => value.text = Tr(option.Value ? far : near));
        y += 37;
    }

    [HideFromIl2Cpp]
    private void Stepper(string title, Func<string> read, Action<int> change, ref float y)
    {
        Text(settingsBody, title, 4, y, 322, 31, 15, Paper);
        Button(settingsBody, "−", 350, y, 32, 29, () => change(-1));
        var value = Text(settingsBody, "", 384, y, 80, 29, 14, Gold); value.alignment = TextAlignmentOptions.Center;
        Button(settingsBody, "+", 468, y, 32, 29, () => change(1));
        settingsRefresh.Add(() => value.text = read()); y += 37;
    }

    [HideFromIl2Cpp]
    private void Shortcut(string title, ConfigEntry<string> option, ref float y)
    {
        Text(settingsBody, title, 4, y, 322, 31, 15, Paper);
        var value = Button(settingsBody, "", 350, y, 150, 29, () => capturingKey = option);
        settingsRefresh.Add(() => value.text = capturingKey == option ? Tr("Нажми клавишу…") : option.Value);
        y += 37;
    }

    private void DrawScores()
    {
        var state = GameBridge.Current;
        used = 0;
        dividerAfterRow = -1;
        if (cfg.Boss.Value && bossLifetime.Show(Time.unscaledTimeAsDouble, cfg.BossHoldSeconds.Value))
        {
            Section(state.BossActive ? "БОСС · БОЙ" : "БОСС · ИТОГ", Gold);
            double total = state.Players.Sum(p => p.BossDamage);
            foreach (var p in state.Players.OrderByDescending(p => p.BossDamage))
                AddLine(PlayerName(p), p.BossDps, p.BossDamage, Percent(p.BossDamage, total), PlayerTint(p.Color), -1, true);
            dividerAfterRow = used;
        }
        Section("КОМАНДА", Paper);
        if (!GameBridge.IsHost && !GameBridge.Received)
            Note("Ожидание хоста с Brigade DPS 0.1.2…", Gold);
        else if (state.Players.Count == 0) Note("Урон ещё не нанесён", Muted);
        double party = state.Players.Sum(p => p.Damage);
        foreach (var player in state.Players)
        {
            AddLine(PlayerName(player), DpsDisplay.Select(cfg.DpsMode.Value, player.Dps, player.GameDps), player.Damage, Percent(player.Damage, party), PlayerTint(player.Color), -1, true);
            if (!cfg.Spells.Value) continue;
            int count = 0;
            foreach (var spell in player.Spells)
            {
                if (!UiText.IsSpell(spell.Spell)) continue;
                if (count++ >= cfg.SpellLimit.Value) break;
                var name = UiText.SpellLabel(SpellName(spell.Spell), cfg.SpellNames.Value, cfg.AbbreviateSpells.Value);
                AddLine(name, DpsDisplay.Select(cfg.DpsMode.Value, spell.Dps, spell.GameDps), spell.Damage, Percent(spell.Damage, player.Damage), Paper, spell.Spell, false);
            }
        }
        for (int i = used; i < lines.Count; i++) lines[i].Root.gameObject.SetActive(false);
        LayoutMeter();
        background.color = meterCardFrame != null ? Color.clear : new Color(Ink.r, Ink.g, Ink.b, cfg.Opacity.Value);
    }

    private string PlayerName(PlayerScore player) => string.IsNullOrWhiteSpace(player.Name) ? $"{Tr("Игрок")} {player.Id + 1}" : player.Name;

    private void LayoutMeter()
    {
        float scale = EffectiveScale();
        float nameWidth = 0, dpsWidth = 0, damageWidth = 0, shareWidth = 0, noteWidth = 0;
        for (int i = 0; i < used; i++)
        {
            var row = lines[i];
            if (row.IsNote) { noteWidth = Mathf.Max(noteWidth, row.Measure(row.Name)); continue; }
            nameWidth = Mathf.Max(nameWidth, row.Measure(row.Name) + row.Indent + row.SpellInset);
            dpsWidth = Mathf.Max(dpsWidth, row.Measure(row.Dps));
            damageWidth = Mathf.Max(damageWidth, row.Measure(row.Damage));
            shareWidth = Mathf.Max(shareWidth, row.Measure(row.Share));
        }
        float numbersWidth = dpsWidth + damageWidth + shareWidth + ColumnGap * 3;
        float wantedWidth = Mathf.Max(nameWidth + numbersWidth, noteWidth) + RowInset * 2 + 2 * FramePadding;
        currentWidth = Mathf.Min(Mathf.Max(100, wantedWidth), root.rect.width / scale - 12);
        nameWidth = Mathf.Max(0, currentWidth - 2 * FramePadding - RowInset * 2 - numbersWidth);
        float contentHeight = used * RowHeight + (dividerAfterRow >= 0 ? NativeCardDivider.Height : 0);
        float maxHeight = Mathf.Max(RowHeight + 2 * FramePadding, Math.Min(root.rect.height - 12 * scale, root.rect.height * cfg.Height.Value) / scale);
        currentHeight = Mathf.Min(contentHeight + 2 * FramePadding, maxHeight);
        window.sizeDelta = new Vector2(currentWidth, currentHeight);
        body.sizeDelta = new Vector2(currentWidth - 2 * FramePadding, contentHeight);
        viewport.sizeDelta = new Vector2(currentWidth - 2 * FramePadding, currentHeight - 2 * FramePadding);
        viewport.parent.Cast<RectTransform>().sizeDelta = viewport.sizeDelta;
        for (int i = 0; i < used; i++) lines[i].Layout(currentWidth - 2 * FramePadding, nameWidth, dpsWidth, damageWidth, shareWidth);
    }

    private void Section(string title, Color color)
    {
        var row = Next(); row.Set(Tr(title), Tr("УВС"), Tr("УРОН"), "%", color, null, true, 0);
        row.Name.alignment = row.Dps.alignment = row.Damage.alignment = row.Share.alignment = TextAlignmentOptions.Center;
        row.Background.color = new Color(.27f, .30f, .28f, .55f);
    }
    private void Note(string message, Color color)
    {
        var row = Next(); row.Set(Tr(message), "", "", "", color, null, false, 0);
        row.IsNote = true;
    }
    private void AddLine(string name, double? dps, double damage, string share, Color tint, int spell, bool player)
    {
        Sprite sprite = !player && (cfg.Icons.Value || !cfg.SpellNames.Value) ? SpellIcon(spell) : null;
        // A game update may temporarily omit an icon; keep such a spell identifiable.
        if (!player && sprite == null && string.IsNullOrEmpty(name)) name = UiText.SpellLabel(SpellName(spell), true, true);
        var row = Next();
        row.Set(name, dps.HasValue ? Number(dps.Value) : "—", Number(damage), share, tint, sprite, player,
            player ? 0 : (sprite != null ? 23 : 8));
        row.SpellInset = !player && (!cfg.SpellNames.Value || cfg.AbbreviateSpells.Value) ? 8 : 0;
    }
    [HideFromIl2Cpp]
    private TableLine Next()
    {
        if (used == lines.Count) lines.Add(new TableLine(this, body));
        var row = lines[used];
        float dividerSpace = dividerAfterRow >= 0 && used >= dividerAfterRow ? NativeCardDivider.Height : 0;
        row.Root.anchoredPosition = new Vector2(0, -RowHeight * used++ - dividerSpace);
        row.Root.gameObject.SetActive(true); return row;
    }

    private Sprite SpellIcon(int spell)
    {
        if (spell < 0) return null;
        if (sprites.TryGetValue(spell, out var cached) && cached != null && !cached.WasCollected) return cached;
        var config = SpellConfigurationRepository.Get((SpellType)spell);
        var icon = config?.IconContainer?.DefaultIcon;
        if (icon != null) sprites[spell] = icon;
        return icon;
    }

    private string SpellName(int spell)
    {
        if (spellNames.TryGetValue(spell, out var value)) return value;
        string fallback = ((SpellType)spell).ToString();
        try
        {
            string translated = LocalizationSettings.StringDatabase.GetLocalizedString("SpellName_" + fallback);
            if (!string.IsNullOrWhiteSpace(translated) && !translated.Contains("No translation") && !translated.StartsWith("SpellName_"))
                return spellNames[spell] = translated;
        }
        catch { }
        return fallback;
    }

    private float EffectiveScale() => Mathf.Min(cfg.Scale.Value, (root.rect.width - 12) / 240);
    private void Place()
    {
        float scale = EffectiveScale();
        window.localScale = new Vector3(scale, scale, 1);
        float width = currentWidth * scale, height = currentHeight * scale, padding = 6 * scale;
        if (!cfg.PlacementStored.Value && window.gameObject.activeSelf)
        {
            // Migrate the old normalized position only after the real table size is known.
            cfg.SavePlacement(cfg.X.Value * Math.Max(0, root.rect.width - width),
                cfg.Y.Value * Math.Max(0, root.rect.height - height), width, height, root.rect.width, root.rect.height, padding);
        }
        if (dragging)
        {
            window.anchoredPosition = new Vector2(
                WindowAnchorAxis.Clamp(window.anchoredPosition.x, width, root.rect.width, padding),
                -WindowAnchorAxis.Clamp(-window.anchoredPosition.y, height, root.rect.height, padding));
        }
        else if (cfg.PlacementStored.Value)
        {
            var horizontal = new WindowAnchorAxis(cfg.AnchorRight.Value, cfg.EdgeMarginX.Value);
            var vertical = new WindowAnchorAxis(cfg.AnchorBottom.Value, cfg.EdgeMarginY.Value);
            window.anchoredPosition = new Vector2(horizontal.Resolve(width, root.rect.width, padding),
                -vertical.Resolve(height, root.rect.height, padding));
        }
        float settingsScale = Mathf.Min(1, Mathf.Min((root.rect.height - 24) / 700, (root.rect.width - 24) / 550));
        settings.localScale = new Vector3(settingsScale, settingsScale, 1);
        float availableX = Mathf.Max(0, root.rect.width - 550 * settingsScale - 12);
        float availableY = Mathf.Max(0, root.rect.height - 700 * settingsScale - 12);
        settings.anchoredPosition = settingsDragging
            ? new Vector2(Mathf.Clamp(settings.anchoredPosition.x, 6, 6 + availableX),
                -Mathf.Clamp(-settings.anchoredPosition.y, 6, 6 + availableY))
            : new Vector2(6 + cfg.SettingsX.Value * availableX, -(6 + cfg.SettingsY.Value * availableY));
        settings.SetAsLastSibling();
    }

    private void DragSettings()
    {
        var mouse = Mouse.current;
        if (!settingsOpen || mouse == null || !Application.isFocused) { EndSettingsDrag(); return; }
        Vector2 position = mouse.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(settings, position, null, out var local);
        bool title = local.x >= 8 && local.x < 502 && local.y <= -4 && local.y >= -44;
        if (mouse.leftButton.wasPressedThisFrame && title)
        {
            settingsDragging = true;
            settingsDragOrigin = position; settingsDragWindow = settings.anchoredPosition;
        }
        if (!settingsDragging) return;
        settings.anchoredPosition = settingsDragWindow + (position - settingsDragOrigin) / canvas.scaleFactor;
        Place();
        if (!mouse.leftButton.isPressed) EndSettingsDrag();
    }

    private void EndSettingsDrag()
    {
        if (!settingsDragging) return;
        settingsDragging = false;
        float availableX = root.rect.width - settings.rect.width * settings.localScale.x - 12;
        float availableY = root.rect.height - settings.rect.height * settings.localScale.y - 12;
        cfg.SettingsX.Value = availableX > 0 ? Mathf.Clamp01((settings.anchoredPosition.x - 6) / availableX) : .5f;
        cfg.SettingsY.Value = availableY > 0 ? Mathf.Clamp01((-settings.anchoredPosition.y - 6) / availableY) : .5f;
    }

    private void Drag()
    {
        var mouse = Mouse.current;
        if (mouse == null || cfg.Locked.Value || settingsOpen || !Application.isFocused) { EndDrag(); return; }
        Vector2 position = mouse.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(window, position, null, out var local);
        bool topRow = local.x >= 0 && local.x <= currentWidth && local.y <= 0 && local.y >= -(RowHeight + FramePadding);
        if (mouse.leftButton.wasPressedThisFrame && topRow)
        {
            dragging = true;
            dragOrigin = position; dragWindow = window.anchoredPosition;
            meterScroll.StopMovement(); meterScroll.enabled = false;
        }
        if (!dragging) return;
        var delta = (position - dragOrigin) / canvas.scaleFactor;
        window.anchoredPosition = dragWindow + delta;
        Place();
        if (!mouse.leftButton.isPressed)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        float scale = EffectiveScale();
        cfg.SavePlacement(window.anchoredPosition.x, -window.anchoredPosition.y,
            currentWidth * scale, currentHeight * scale, root.rect.width, root.rect.height, 6 * scale);
        if (meterScroll != null) meterScroll.enabled = true;
    }

    private RectTransform Framed(string name, Transform parent, float width, float height)
    {
        var rt = Rect(name, parent, 0, 0, width, height);
        rt.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .96f);
        if (windowFrame != null)
        {
            StoneEdge(rt, 2f, windowFrame);
            return rt;
        }
        Border(rt, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(-8, 2));
        Border(rt, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(-8, 2));
        Border(rt, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), new Vector2(2, -8));
        Border(rt, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-2, 0), new Vector2(2, -8));
        foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
        {
            var diamond = Rect("Corner", rt, 0, 0, 7, 7); diamond.anchorMin = diamond.anchorMax = corner;
            diamond.pivot = new Vector2(.5f, .5f); diamond.anchoredPosition = Vector2.zero;
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            var image = diamond.gameObject.AddComponent<Image>(); image.color = Gold; image.raycastTarget = false;
        }
        return rt;
    }

    private void UpdateCardFrames()
    {
        if ((cardStone == null || cardLines == null) && Time.unscaledTime >= nextCardSearch)
        {
            nextCardSearch = Time.unscaledTime + 2;
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite.name == "ui_store_card") cardStone = sprite;
                if (sprite.name == "ui_store_card_lines_white") cardLines = sprite;
            }
        }
        if (cardStone == null || cardLines == null) return;
        if (meterCardFrame == null)
        {
            meterCardFrame = new NativeCardFrame(window, cardStone, cardLines);
            settingsCardFrame = new NativeCardFrame(settings, cardStone, cardLines);
            bossDivider = new NativeCardDivider(window, cardStone, cardLines);
            window.Find("StoneFrame")?.gameObject.SetActive(false);
            settings.Find("StoneFrame")?.gameObject.SetActive(false);
            background.color = Color.clear;
            settings.GetComponent<Image>().color = Color.clear;
            EntryPoint.Instance.Log.LogInfo("Native subdued card frames ready; upper ornaments mirror the lower edge.");
        }
        meterCardFrame.Layout(cfg.Opacity.Value);
        settingsCardFrame.Layout(.96f);
        PlaceBossDivider();
    }

    private void PlaceBossDivider()
    {
        if (bossDivider == null) return;
        bossDivider.Place(window.gameObject.activeSelf && dividerAfterRow >= 0,
            currentWidth, viewport.rect.height, FramePadding, dividerAfterRow * RowHeight,
            body.anchoredPosition.y);
    }

    // Own sliced artwork for scenes where the game's button atlas is not loaded yet.
    private Sprite CreateStoneFrame()
    {
        const int size = 256;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Math.Min(x, size - 1 - x), dy = Math.Min(y, size - 1 - y);
            float edge = Math.Min(Math.Min(dx, dy), (dx + dy - 25) * .7071f);
            // Small chips remain confined to the corner slices.
            if ((dx is > 29 and < 38 && dy < 7) || (dy is > 36 and < 43 && dx < 5)) edge -= 5;
            Color c = Color.clear;
            if (edge >= 0 && edge < 34)
            {
                if (edge < 4) c = new Color(.11f, .095f, .14f);
                else if (edge < 12) c = new Color(.25f, .22f, .31f);
                else if (edge < 20) c = new Color(.39f, .35f, .46f);
                else if (edge < 25) c = new Color(.20f, .18f, .25f);
                else if (edge < 28) c = new Color(.86f, .83f, .93f);
                else c = new Color(.28f, .25f, .34f);
            }
            pixels[y * size + x] = c;
        }
        generatedFrameTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedFrameTexture.name = "BrigadeDps.StoneFrame";
        generatedFrameTexture.SetPixels(pixels); generatedFrameTexture.Apply();
        generatedFrame = Sprite.Create(generatedFrameTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(55, 55, 55, 55));
        return generatedFrame;
    }

    private Sprite CreateWindowFrame()
    {
        var artwork = FrameArtwork.CreatePixels();
        var pixels = new Color[artwork.Length];
        for (int i = 0; i < artwork.Length; i++)
        {
            uint argb = artwork[i];
            pixels[i] = new Color((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f,
                (argb & 255) / 255f, (argb >> 24) / 255f);
        }
        windowFrameTexture = new Texture2D(FrameArtwork.Size, FrameArtwork.Size, TextureFormat.RGBA32, false);
        windowFrameTexture.name = "BrigadeDps.CardFrame";
        windowFrameTexture.wrapMode = TextureWrapMode.Clamp;
        windowFrameTexture.filterMode = FilterMode.Bilinear;
        windowFrameTexture.SetPixels(pixels); windowFrameTexture.Apply();
        return Sprite.Create(windowFrameTexture, new Rect(0, 0, FrameArtwork.Size, FrameArtwork.Size),
            new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(FrameArtwork.Slice, FrameArtwork.Slice, FrameArtwork.Slice, FrameArtwork.Slice));
    }

    private void StoneEdge(RectTransform parent, float multiplier, Sprite artwork = null)
    {
        var rt = Rect("StoneFrame", parent, 0, 0, 0, 0);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-5, -5); rt.offsetMax = new Vector2(5, 5);
        var image = rt.gameObject.AddComponent<Image>(); image.sprite = artwork != null ? artwork : stoneBorder;
        image.type = Image.Type.Sliced; image.fillCenter = false; image.pixelsPerUnitMultiplier = multiplier;
        image.raycastTarget = false;
    }

    private void Border(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var rt = Rect(name, parent, 0, 0, 0, 0); rt.anchorMin = min; rt.anchorMax = max;
        rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = position; rt.sizeDelta = size;
        var image = rt.gameObject.AddComponent<Image>(); image.color = Gold; image.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rt = new GameObject(name).AddComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height);
        return rt;
    }
    private TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
    {
        var text = Rect("Label", parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = color; text.text = Tr(value);
        textElements.Add(text);
        if (UiText.Has(value)) localizedLabels.Add((text, value));
        text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    [HideFromIl2Cpp]
    private TextMeshProUGUI Button(Transform parent, string label, float x, float y, float width, float height, Action action)
    {
        var rt = Rect("Button", parent, x, y, width, height);
        var image = rt.gameObject.AddComponent<Image>(); image.color = new Color(.24f, .22f, .30f, .95f);
        if (stoneFill != null)
        {
            image.sprite = stoneFill; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 7;
            image.color = new Color(.24f, .22f, .30f, 1);
        }
        if (stoneBorder != null) StoneEdge(rt, 7);
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.20f, 1.35f); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.75f, .70f, .85f); button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        var callback = DelegateSupport.ConvertDelegate<UnityAction>(action); actions.Add(callback); button.onClick.AddListener(callback);
        var text = Text(rt, label, 3, 0, width - 6, height, 13, Paper); text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static string Percent(double part, double total) => total > 0 ? (100 * part / total).ToString("0.0", CultureInfo.InvariantCulture) : "0.0";
    private static string Number(double value) => value >= 1e6 ? (value / 1e6).ToString("0.##", CultureInfo.InvariantCulture) + "M"
        : value >= 1e4 ? (value / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "k" : value.ToString("0", CultureInfo.InvariantCulture);
    private static Color PlayerTint(string value)
    {
        try
        {
            uint rgb = Convert.ToUInt32(value, 16);
            return Color.Lerp(new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f), Color.white, .25f);
        }
        catch { return Paper; }
    }

    private sealed class TableLine
    {
        internal readonly RectTransform Root;
        internal readonly Image Background, Icon;
        internal readonly TextMeshProUGUI Name, Dps, Damage, Share;
        internal bool IsNote;
        internal float Indent, SpellInset;
        internal TableLine(MeterView owner, Transform parent)
        {
            Root = Rect("Row", parent, 0, 0, 0, RowHeight);
            Root.anchorMax = new Vector2(1, 1);
            Background = Root.gameObject.AddComponent<Image>(); Background.raycastTarget = false;
            Icon = Rect("SpellIcon", Root, RowInset, 3, 19, 19).gameObject.AddComponent<Image>();
            Icon.preserveAspect = true; Icon.raycastTarget = false;
            Name = owner.Text(Root, "", 0, 0, 1, RowHeight, 14, Paper); Name.richText = false;
            Dps = owner.Text(Root, "", 0, 0, 1, RowHeight, 14, Paper);
            Damage = owner.Text(Root, "", 0, 0, 1, RowHeight, 14, Paper);
            Share = owner.Text(Root, "", 0, 0, 1, RowHeight, 14, Paper);
            Dps.alignment = Damage.alignment = Share.alignment = TextAlignmentOptions.MidlineRight;
        }
        internal void Set(string name, string dps, string damage, string share, Color tint, Sprite sprite, bool bold, float indent)
        {
            Name.text = name; Dps.text = dps; Damage.text = damage; Share.text = share;
            Indent = indent; SpellInset = 0; IsNote = false;
            Name.alignment = TextAlignmentOptions.MidlineLeft;
            Dps.alignment = Damage.alignment = Share.alignment = TextAlignmentOptions.MidlineRight;
            Name.color = tint; Name.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            Name.fontSize = bold ? 15 : 13;
            Dps.color = Damage.color = Share.color = bold ? Paper : Muted;
            Icon.sprite = sprite; Icon.gameObject.SetActive(sprite != null);
            Background.color = bold ? new Color(.6f, .65f, .65f, .06f) : Color.clear;
        }
        internal float Measure(TextMeshProUGUI label) => string.IsNullOrEmpty(label.text) ? 0 : Mathf.Ceil(label.GetPreferredValues(label.text).x) + 2;
        internal void Layout(float width, float nameWidth, float dpsWidth, float damageWidth, float shareWidth)
        {
            Icon.rectTransform.anchoredPosition = new Vector2(RowInset + SpellInset, -3);
            Position(Name, RowInset + Indent + SpellInset, IsNote ? width - RowInset * 2 : Mathf.Max(0, nameWidth - Indent - SpellInset));
            float x = RowInset + nameWidth + ColumnGap;
            Position(Dps, x, dpsWidth); x += dpsWidth + ColumnGap;
            Position(Damage, x, damageWidth); x += damageWidth + ColumnGap;
            Position(Share, x, shareWidth);
        }
        private static void Position(TextMeshProUGUI label, float x, float width)
        {
            label.rectTransform.anchoredPosition = new Vector2(x, 0);
            label.rectTransform.sizeDelta = new Vector2(width, RowHeight);
        }
    }
}

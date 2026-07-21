using Godot;
using SpaceFactory.Application.Settings;
using SpaceFactory.Core.Settings;
using SpaceFactory.Infrastructure.Persistence;
using CoreWindowMode = SpaceFactory.Core.Settings.WindowMode;

namespace SpaceFactory.Presentation.Settings;

public partial class SettingsMenuController : CanvasLayer
{
    private enum SettingsPage
    {
        Main,
        Keyboard,
        Audio,
        Video,
    }

    private static readonly ScreenResolution[] Resolutions =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160),
    ];

    private static readonly int[] RefreshRates = [60, 75, 120, 144, 165, 240];
    private static readonly int[] FpsLimits = [0, 30, 60, 120, 144, 165, 240];
    private const int KeyboardRowsPerPage = 6;
    private const int VideoPageCount = 3;
    private readonly List<Control> _focusableControls = [];
    private IGameSettingsStore _store = null!;
    private SettingsRuntimeApplier _runtime = null!;
    private GameSettings _saved = null!;
    private GameSettings _draft = null!;
    private MarginContainer _safeArea = null!;
    private MarginContainer _frameMargin = null!;
    private VBoxContainer _content = null!;
    private Button _infoButton = null!;
    private Label _section = null!;
    private Label _status = null!;
    private ColorRect _modalShade = null!;
    private Label _modalTitle = null!;
    private Label _modalMessage = null!;
    private Label _modalCountdown = null!;
    private HBoxContainer _modalActions = null!;
    private AudioStreamPlayer _audioTestPlayer = null!;
    private SettingsPage _page;
    private GameAction? _captureAction;
    private ulong _captureReadyFrame;
    private Action? _modalCancelAction;
    private VideoSettings? _pendingPreviousVideo;
    private VideoSettings? _pendingVideo;
    private double _videoConfirmationRemaining;
    private double _audioSaveDelay;
    private int _keyboardPageIndex;
    private int _videoPageIndex;
    private bool _ready;

    public bool IsOpen => Visible;

    public bool HotbarMouseWheelEnabled => _saved.HotbarMouseWheelEnabled;

    public event Action? Closed;

    public event Action? InputBindingsChanged;

    public event Action? InfoRequested;

    public override void _Ready()
    {
        _safeArea = GetNode<MarginContainer>("SafeArea");
        _frameMargin = GetNode<MarginContainer>("SafeArea/Frame/FrameMargin");
        _content = GetNode<VBoxContainer>("SafeArea/Frame/FrameMargin/Layout/Content");
        _infoButton = GetNode<Button>("SafeArea/Frame/FrameMargin/Layout/Header/Info");
        _section = GetNode<Label>("SafeArea/Frame/FrameMargin/Layout/Header/Section");
        _status = GetNode<Label>("SafeArea/Frame/FrameMargin/Layout/Footer/Status");
        _modalShade = GetNode<ColorRect>("ModalShade");
        _modalTitle = GetNode<Label>("ModalShade/ModalCenter/ModalPanel/Margin/Content/Title");
        _modalMessage = GetNode<Label>("ModalShade/ModalCenter/ModalPanel/Margin/Content/Message");
        _modalCountdown = GetNode<Label>("ModalShade/ModalCenter/ModalPanel/Margin/Content/Countdown");
        _modalActions = GetNode<HBoxContainer>("ModalShade/ModalCenter/ModalPanel/Margin/Content/Actions");

        var theme = CreateSciFiTheme();
        GetNode<Control>("SafeArea").Theme = theme;
        _modalShade.Theme = theme;

        _store = new JsonGameSettingsStore();
        _saved = _store.Load().Normalize();
        _draft = _saved;
        var brightness = GetParent().GetNode<CanvasModulate>("World/BrightnessModulate");
        _runtime = new SettingsRuntimeApplier(GetTree().Root, brightness);
        _runtime.ApplyAll(_saved);

        _audioTestPlayer = new AudioStreamPlayer { Bus = "UI" };
        AddChild(_audioTestPlayer);
        _infoButton.Pressed += OpenInfoMenu;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        BuildMainPage();
        UpdateResponsiveLayout();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_ready)
        {
            _infoButton.Pressed -= OpenInfoMenu;
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    public override void _Process(double delta)
    {
        if (_audioSaveDelay > 0)
        {
            _audioSaveDelay -= delta;
            if (_audioSaveDelay <= 0)
            {
                _store.Save(_saved);
            }
        }

        if (_videoConfirmationRemaining <= 0)
        {
            return;
        }

        _videoConfirmationRemaining -= delta;
        if (_videoConfirmationRemaining <= 0)
        {
            RevertPendingVideo("Zeit abgelaufen – vorherige Videoeinstellungen wiederhergestellt.");
            return;
        }

        _modalCountdown.Text = $"Automatische Rücksetzung in {Mathf.CeilToInt(_videoConfirmationRemaining)} Sekunden";
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (_captureAction is not null)
        {
            HandleBindingCapture(@event);
            return;
        }

        var isPhysicalEscape = @event is InputEventKey { Pressed: true, Echo: false } keyEvent &&
            (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape);
        var isConfiguredPause = @event.IsActionPressed("pause") &&
            (@event is not InputEventKey pauseKey || !pauseKey.Echo);
        if (isPhysicalEscape || isConfiguredPause)
        {
            NavigateBackOrClose();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_modalShade.Visible || @event is not InputEventKey { Pressed: true, Echo: false } navigationKey)
        {
            return;
        }

        var physicalKey = navigationKey.PhysicalKeycode != Key.None
            ? navigationKey.PhysicalKeycode
            : navigationKey.Keycode;
        if (GetViewport().GuiGetFocusOwner() is OptionButton option && option.GetPopup().Visible)
        {
            return;
        }

        switch (physicalKey)
        {
            case Key.W:
                MoveFocus(-1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.S:
                MoveFocus(1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.A:
                AdjustFocusedControl(-1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.D:
                AdjustFocusedControl(1);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public void Open()
    {
        _draft = _saved;
        _captureAction = null;
        HideModal();
        BuildMainPage();
        Visible = true;
        FocusFirst();
    }

    public void CloseImmediately()
    {
        Visible = false;
        FlushPendingAudioSave();
        GetViewport().GuiReleaseFocus();
    }

    public void NavigateBackOrClose()
    {
        if (TryCloseTransientUi())
        {
            return;
        }

        if (_page != SettingsPage.Main)
        {
            BuildMainPage();
            return;
        }

        Visible = false;
        FlushPendingAudioSave();
        GetViewport().GuiReleaseFocus();
        Closed?.Invoke();
    }

    /// <summary>
    /// Closes the deepest settings sub-state before page navigation is allowed.
    /// This keeps Escape deterministic for key capture, dialogs and native
    /// option popups instead of accidentally leaving the complete page.
    /// </summary>
    public bool TryCloseTransientUi()
    {
        if (_captureAction is not null)
        {
            CancelBindingCapture();
            return true;
        }

        if (_modalShade.Visible)
        {
            (_modalCancelAction ?? HideModal).Invoke();
            return true;
        }

        if (GetViewport().GuiGetFocusOwner() is OptionButton option && option.GetPopup().Visible)
        {
            option.GetPopup().Hide();
            option.GrabFocus();
            return true;
        }

        return false;
    }

#if DEBUG
    public void RunConstructionSmokeTest()
    {
        Open();
        for (var page = 0; page < GetKeyboardPageCount(); page++)
        {
            _keyboardPageIndex = page;
            BuildKeyboardPage();
        }

        BuildAudioPage();
        for (var page = 0; page < VideoPageCount; page++)
        {
            _videoPageIndex = page;
            BuildVideoPage();
        }

        if (ContainsScrollContainer(GetNode("SafeArea/Frame")))
        {
            throw new InvalidOperationException("Settings pages must not contain a ScrollContainer.");
        }

        Vector2[] desktopSizes = [new(1366, 768), new(1920, 1080), new(2560, 1440)];
        foreach (var viewportSize in desktopSizes)
        {
            ValidateDesktopMetrics(viewportSize, CalculateResponsiveMetrics(viewportSize));
        }

        ShowQuitConfirmation();
        if (!TryCloseTransientUi() || _modalShade.Visible)
        {
            throw new InvalidOperationException("Escape hierarchy did not close the settings modal first.");
        }

        BuildKeyboardPage();
        NavigateBackOrClose();
        if (!Visible || _page != SettingsPage.Main)
        {
            throw new InvalidOperationException("Escape hierarchy did not return a settings subpage to the pause start page.");
        }

        var alternateResolution = _saved.Video.Resolution == Resolutions[0]
            ? Resolutions[1]
            : Resolutions[0];
        _draft = _draft with { Video = _saved.Video with { Resolution = alternateResolution } };
        ApplyVideoSettings();
        RevertPendingVideo("Smoke-Test-Rücksetzung");
        BuildMainPage();
        if (!_infoButton.Visible || _infoButton.Text != "[i]" ||
            _infoButton.CustomMinimumSize.X > 48 || _infoButton.CustomMinimumSize.Y > 48 ||
            _content.GetChildren().OfType<Button>().Any(button => button.Text.Contains("INFO", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Info must be a compact header icon instead of a full-width menu row.");
        }
        NavigateBackOrClose();
        GD.Print("SETTINGS_SMOKE_OK: compact info icon, hierarchical Escape, paged keyboard/video, no-scroll desktop layouts");
    }
#endif

    private void BuildMainPage()
    {
        BeginPage(SettingsPage.Main, "HAUPTMENÜ");
        AddSpacer(24);
        AddMainButton(
            "TASTATUREINSTELLUNGEN",
            "res://assets/ui/settings/keyboard.svg",
            OpenKeyboardPage);
        AddMainButton("AUDIO", "res://assets/ui/settings/audio.svg", BuildAudioPage);
        AddMainButton("VIDEO", "res://assets/ui/settings/video.svg", OpenVideoPage);
        AddMainButton("SPIEL VERLASSEN", "res://assets/ui/settings/exit.svg", ShowQuitConfirmation);
        RegisterFocusable(_infoButton);
        _status.Text = "Einstellungen werden lokal und dauerhaft gespeichert.";
        FocusFirst();
    }

    private void OpenKeyboardPage()
    {
        _keyboardPageIndex = 0;
        BuildKeyboardPage();
    }

    private void OpenVideoPage()
    {
        _videoPageIndex = 0;
        BuildVideoPage();
    }

    private void OpenInfoMenu()
    {
        CloseImmediately();
        InfoRequested?.Invoke();
    }

    private void BuildKeyboardPage()
    {
        BeginPage(SettingsPage.Keyboard, "TASTATUREINSTELLUNGEN");
        var pageCount = GetKeyboardPageCount();
        _keyboardPageIndex = Math.Clamp(_keyboardPageIndex, 0, pageCount - 1);
        foreach (var definition in InputActionCatalog.All
                     .Skip(_keyboardPageIndex * KeyboardRowsPerPage)
                     .Take(KeyboardRowsPerPage))
        {
            var row = CreateSettingRow();
            var label = new Label
            {
                Text = definition.DisplayName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var bindingButton = CreateButton(InputBindingFormatter.Format(_draft.Input.GetBinding(definition.Action)));
            bindingButton.CustomMinimumSize = new Vector2(230, 42);
            var action = definition.Action;
            bindingButton.Pressed += () => BeginBindingCapture(action, bindingButton);
            row.AddChild(label);
            row.AddChild(bindingButton);
            _content.AddChild(row);
            RegisterFocusable(bindingButton);
        }

        AddToggleRow(
            "Hotbar mit Mausrad wechseln",
            _draft.HotbarMouseWheelEnabled,
            enabled => _draft = _draft with { HotbarMouseWheelEnabled = enabled });

        AddPageNavigation(
            _keyboardPageIndex,
            pageCount,
            page =>
            {
                _keyboardPageIndex = page;
                BuildKeyboardPage();
            });

        var actions = CreateActionRow();
        actions.AddChild(CreateActionButton("STANDARD", ResetKeyboardDefaults));
        actions.AddChild(CreateActionButton("SPEICHERN", SaveKeyboardSettings, primary: true));
        actions.AddChild(CreateActionButton("ZURÜCK", BuildMainPage));
        _content.AddChild(actions);
        _status.Text = $"Tastenbelegungen {_keyboardPageIndex + 1} / {pageCount}";
        FocusFirst();
    }

    private void BuildAudioPage()
    {
        BeginPage(SettingsPage.Audio, "AUDIO");
        AddVolumeSlider("Gesamtlautstärke", _draft.Audio.MasterVolume,
            value => UpdateAudio(_draft.Audio with { MasterVolume = value }));
        AddVolumeSlider("Musiklautstärke", _draft.Audio.MusicVolume,
            value => UpdateAudio(_draft.Audio with { MusicVolume = value }));
        AddVolumeSlider("Soundeffekte", _draft.Audio.SoundEffectsVolume,
            value => UpdateAudio(_draft.Audio with { SoundEffectsVolume = value }));
        AddVolumeSlider("Umgebungsgeräusche", _draft.Audio.AmbientVolume,
            value => UpdateAudio(_draft.Audio with { AmbientVolume = value }));
        AddVolumeSlider("Benutzeroberflächen-Sounds", _draft.Audio.UserInterfaceVolume,
            value => UpdateAudio(_draft.Audio with { UserInterfaceVolume = value }));

        AddToggleRow("Ton vollständig stummschalten", _draft.Audio.IsMuted,
            enabled => UpdateAudio(_draft.Audio with { IsMuted = enabled }));
        AddToggleRow("Musik stummschalten", _draft.Audio.IsMusicMuted,
            enabled => UpdateAudio(_draft.Audio with { IsMusicMuted = enabled }));

        var actions = CreateActionRow();
        actions.AddChild(CreateActionButton("TEST-SOUND", PlayTestSound, primary: true));
        actions.AddChild(CreateActionButton("ZURÜCK", BuildMainPage));
        _content.AddChild(actions);
        _status.Text = "Audioänderungen werden sofort angewendet und gespeichert.";
        FocusFirst();
    }

    private void BuildVideoPage()
    {
        BeginPage(SettingsPage.Video, "VIDEO");
        _videoPageIndex = Math.Clamp(_videoPageIndex, 0, VideoPageCount - 1);
        switch (_videoPageIndex)
        {
            case 0:
                AddDisplayVideoRows();
                break;
            case 1:
                AddQualityVideoRows();
                break;
            case 2:
                AddInterfaceVideoRows();
                break;
        }

        AddPageNavigation(
            _videoPageIndex,
            VideoPageCount,
            page =>
            {
                _videoPageIndex = page;
                BuildVideoPage();
            });

        var actions = CreateActionRow();
        actions.AddChild(CreateActionButton("ANWENDEN", ApplyVideoSettings, primary: true));
        actions.AddChild(CreateActionButton("VERWERFEN", DiscardVideoChanges));
        actions.AddChild(CreateActionButton("STANDARDWERTE", RestoreVideoDefaults));
        actions.AddChild(CreateActionButton("ZURÜCK", BuildMainPage));
        _content.AddChild(actions);
        _status.Text = $"Videoeinstellungen {_videoPageIndex + 1} / {VideoPageCount}";
        FocusFirst();
    }

    private void AddDisplayVideoRows()
    {
        AddOptionRow(
            "Auflösung",
            Resolutions.Select(value => $"{value.Width} × {value.Height}").ToArray(),
            FindResolutionIndex(_draft.Video.Resolution),
            index => _draft = _draft with { Video = _draft.Video with { Resolution = Resolutions[index] } });
        AddOptionRow(
            "Fenstermodus",
            ["Fenster", "Vollbild", "Randloses Vollbild"],
            (int)_draft.Video.WindowMode,
            index => _draft = _draft with { Video = _draft.Video with { WindowMode = (CoreWindowMode)index } });
        AddOptionRow(
            "Bildwiederholrate",
            RefreshRates.Select(value => $"{value} Hz").ToArray(),
            FindClosestIndex(RefreshRates, _draft.Video.RefreshRate),
            index => _draft = _draft with { Video = _draft.Video with { RefreshRate = RefreshRates[index] } });
        AddOptionRow(
            "FPS-Limit",
            FpsLimits.Select(value => value == 0 ? "Unbegrenzt" : value.ToString()).ToArray(),
            FindClosestIndex(FpsLimits, _draft.Video.FpsLimit),
            index => _draft = _draft with { Video = _draft.Video with { FpsLimit = FpsLimits[index] } });
        AddToggleRow("V-Sync", _draft.Video.VSyncEnabled,
            enabled => _draft = _draft with { Video = _draft.Video with { VSyncEnabled = enabled } });
    }

    private void AddQualityVideoRows()
    {
        AddOptionRow(
            "Grafikqualität",
            ["Niedrig", "Mittel", "Hoch", "Ultra", "Benutzerdefiniert"],
            (int)_draft.Video.GraphicsQuality,
            index =>
            {
                _draft = _draft with { Video = _draft.Video.ApplyPreset((GraphicsQualityPreset)index) };
                BuildVideoPage();
            });
        AddQualityRow("Texturqualität", _draft.Video.TextureQuality,
            quality => SetCustomVideo(_draft.Video with { TextureQuality = quality }));
        AddQualityRow("Schattenqualität", _draft.Video.ShadowQuality,
            quality => SetCustomVideo(_draft.Video with { ShadowQuality = quality }));
        AddQualityRow("Effektqualität", _draft.Video.EffectQuality,
            quality => SetCustomVideo(_draft.Video with { EffectQuality = quality }));
        AddQualityRow("Partikeldichte", _draft.Video.ParticleDensity,
            quality => SetCustomVideo(_draft.Video with { ParticleDensity = quality }));
    }

    private void AddInterfaceVideoRows()
    {
        AddOptionRow(
            "Kantenglättung",
            ["Aus", "FXAA", "MSAA 2×", "MSAA 4×", "MSAA 8×"],
            (int)_draft.Video.AntiAliasing,
            index => SetCustomVideo(_draft.Video with { AntiAliasing = (AntiAliasingMode)index }));
        AddVideoSlider("Bildschirmhelligkeit", _draft.Video.BrightnessPercent, 25, 200, "%",
            value => _draft = _draft with { Video = _draft.Video with { BrightnessPercent = value } });
        AddVideoSlider("Benutzeroberflächen-Skalierung", _draft.Video.UserInterfaceScalePercent, 75, 150, "%",
            value => _draft = _draft with { Video = _draft.Video with { UserInterfaceScalePercent = value } });
    }

    private void BeginPage(SettingsPage page, string section)
    {
        _page = page;
        _section.Text = section;
        _infoButton.Visible = page == SettingsPage.Main;
        _focusableControls.Clear();
        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void AddMainButton(string text, string? iconPath, Action action)
    {
        var button = CreateButton($"  {text}                                      ›");
        button.CustomMinimumSize = new Vector2(0, 76);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            button.Icon = GD.Load<Texture2D>(iconPath);
            button.ExpandIcon = true;
        }

        button.Alignment = HorizontalAlignment.Left;
        button.Pressed += action;
        _content.AddChild(button);
        RegisterFocusable(button);
    }

    private HBoxContainer CreateSettingRow()
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 42) };
        row.AddThemeConstantOverride("separation", 18);
        return row;
    }

    private HBoxContainer CreateActionRow()
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 50),
            Alignment = BoxContainer.AlignmentMode.End,
        };
        row.AddThemeConstantOverride("separation", 12);
        return row;
    }

    private int GetKeyboardPageCount() =>
        Math.Max(1, (int)Math.Ceiling(InputActionCatalog.All.Count / (double)KeyboardRowsPerPage));

    private void AddPageNavigation(int currentPage, int pageCount, Action<int> selectPage)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 42),
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 12);

        var previous = CreateButton("‹  ZURÜCK");
        previous.CustomMinimumSize = new Vector2(142, 40);
        previous.Disabled = currentPage <= 0;
        previous.Pressed += () => selectPage(Math.Max(0, currentPage - 1));

        var label = new Label
        {
            Text = $"SEITE {currentPage + 1} / {pageCount}",
            CustomMinimumSize = new Vector2(150, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var next = CreateButton("WEITER  ›");
        next.CustomMinimumSize = new Vector2(142, 40);
        next.Disabled = currentPage >= pageCount - 1;
        next.Pressed += () => selectPage(Math.Min(pageCount - 1, currentPage + 1));

        row.AddChild(previous);
        row.AddChild(label);
        row.AddChild(next);
        _content.AddChild(row);
        RegisterFocusable(previous);
        RegisterFocusable(next);
    }

    private Button CreateButton(string text) => new()
    {
        Text = text,
        FocusMode = Control.FocusModeEnum.All,
        MouseDefaultCursorShape = Control.CursorShape.PointingHand,
    };

    private Button CreateActionButton(string text, Action action, bool primary = false)
    {
        var button = CreateButton(text);
        button.CustomMinimumSize = new Vector2(150, 42);
        if (primary)
        {
            button.AddThemeColorOverride("font_color", new Color(0.8f, 0.98f, 1));
        }

        button.Pressed += action;
        RegisterFocusable(button);
        return button;
    }

    private void AddSpacer(float height)
    {
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore });
    }

    private void BeginBindingCapture(GameAction action, Button button)
    {
        _captureAction = action;
        _captureReadyFrame = Engine.GetProcessFrames() + 1;
        button.Text = "TASTE DRÜCKEN …";
        _status.Text = "Neue Taste drücken – Escape bricht die Eingabe ab.";
    }

    private void HandleBindingCapture(InputEvent @event)
    {
        if (Engine.GetProcessFrames() < _captureReadyFrame)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
            {
                CancelBindingCapture();
                GetViewport().SetInputAsHandled();
                return;
            }

            var code = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
            CompleteBindingCapture(InputBinding.Key((long)code));
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            CompleteBindingCapture(InputBinding.MouseButton((long)mouseEvent.ButtonIndex));
            GetViewport().SetInputAsHandled();
        }
    }

    private void CompleteBindingCapture(InputBinding binding)
    {
        if (_captureAction is not { } action)
        {
            return;
        }

        _captureAction = null;
        var permanentOwner = InputActionCatalog.FindPermanentBindingOwner(binding);
        if (permanentOwner is { } owner && owner != action)
        {
            var ownerName = InputActionCatalog.Get(owner).DisplayName;
            ShowModal(
                "RESERVIERTE BELEGUNG",
                $"{InputBindingFormatter.Format(binding)} bleibt als feste Pfeiltasten-Alternative für „{ownerName}“ reserviert.",
                [new ModalAction("OK", () =>
                {
                    HideModal();
                    BuildKeyboardPage();
                }, true)],
                () =>
                {
                    HideModal();
                    BuildKeyboardPage();
                });
            return;
        }

        GameAction? conflictingAction = null;
        foreach (var pair in _draft.Input.Bindings)
        {
            if (pair.Key != action && pair.Value == binding)
            {
                conflictingAction = pair.Key;
                break;
            }
        }

        if (conflictingAction is not { } conflict)
        {
            _draft = _draft with { Input = _draft.Input.WithBinding(action, binding) };
            BuildKeyboardPage();
            _status.Text = "Neue Belegung vorgemerkt. Zum Übernehmen SPEICHERN wählen.";
            return;
        }

        var oldBinding = _draft.Input.GetBinding(action);
        var conflictName = InputActionCatalog.Get(conflict).DisplayName;
        ShowModal(
            "TASTENKONFLIKT",
            $"{InputBindingFormatter.Format(binding)} wird bereits für „{conflictName}“ verwendet.\nSollen die beiden Belegungen getauscht werden?",
            [
                new ModalAction("BELEGUNGEN TAUSCHEN", () =>
                {
                    var swapped = _draft.Input
                        .WithBinding(conflict, oldBinding)
                        .WithBinding(action, binding);
                    _draft = _draft with { Input = swapped };
                    HideModal();
                    BuildKeyboardPage();
                    _status.Text = "Belegungen getauscht. Zum Übernehmen SPEICHERN wählen.";
                }, true),
                new ModalAction("ABBRECHEN", () =>
                {
                    HideModal();
                    BuildKeyboardPage();
                }),
            ],
            () =>
            {
                HideModal();
                BuildKeyboardPage();
            });
    }

    private void CancelBindingCapture()
    {
        _captureAction = null;
        BuildKeyboardPage();
        _status.Text = "Tastenbelegung nicht geändert.";
    }

    private void ResetKeyboardDefaults()
    {
        _draft = _draft with
        {
            Input = InputSettings.CreateDefault(),
            HotbarMouseWheelEnabled = true,
        };
        BuildKeyboardPage();
        _status.Text = "Standardbelegungen vorgemerkt. Zum Übernehmen SPEICHERN wählen.";
    }

    private void SaveKeyboardSettings()
    {
        if (_draft.Input.HasConflicts)
        {
            _status.Text = "Speichern nicht möglich: Doppelte Tastenbelegungen vorhanden.";
            return;
        }

        _saved = _saved with
        {
            Input = _draft.Input.Normalize(),
            HotbarMouseWheelEnabled = _draft.HotbarMouseWheelEnabled,
        };
        _draft = _draft with
        {
            Input = _saved.Input,
            HotbarMouseWheelEnabled = _saved.HotbarMouseWheelEnabled,
        };
        _runtime.ApplyInput(_saved.Input);
        _store.Save(_saved);
        InputBindingsChanged?.Invoke();
        BuildKeyboardPage();
        _status.Text = "Tastenbelegungen gespeichert und sofort aktiviert.";
    }

    private void AddVolumeSlider(string labelText, int value, Action<int> changed)
    {
        var row = CreateSettingRow();
        var label = new Label { Text = labelText, CustomMinimumSize = new Vector2(300, 0), VerticalAlignment = VerticalAlignment.Center };
        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
        };
        var valueLabel = new Label { Text = $"{value}%", CustomMinimumSize = new Vector2(64, 0), HorizontalAlignment = HorizontalAlignment.Right };
        slider.ValueChanged += newValue =>
        {
            var rounded = Mathf.RoundToInt(newValue);
            valueLabel.Text = $"{rounded}%";
            changed(rounded);
        };
        row.AddChild(label);
        row.AddChild(slider);
        row.AddChild(valueLabel);
        _content.AddChild(row);
        RegisterFocusable(slider);
    }

    private void AddVideoSlider(
        string labelText,
        int value,
        int minimum,
        int maximum,
        string suffix,
        Action<int> changed)
    {
        var row = CreateSettingRow();
        var label = new Label { Text = labelText, CustomMinimumSize = new Vector2(300, 0), VerticalAlignment = VerticalAlignment.Center };
        var slider = new HSlider
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 1,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
        };
        var valueLabel = new Label { Text = $"{value}{suffix}", CustomMinimumSize = new Vector2(72, 0), HorizontalAlignment = HorizontalAlignment.Right };
        slider.ValueChanged += newValue =>
        {
            var rounded = Mathf.RoundToInt(newValue);
            valueLabel.Text = $"{rounded}{suffix}";
            changed(rounded);
        };
        row.AddChild(label);
        row.AddChild(slider);
        row.AddChild(valueLabel);
        _content.AddChild(row);
        RegisterFocusable(slider);
    }

    private void AddToggleRow(string text, bool enabled, Action<bool> changed)
    {
        var row = CreateSettingRow();
        var label = new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        var toggle = new CheckButton { ButtonPressed = enabled, Text = enabled ? "AN" : "AUS", FocusMode = Control.FocusModeEnum.All };
        toggle.Toggled += value =>
        {
            toggle.Text = value ? "AN" : "AUS";
            changed(value);
        };
        row.AddChild(label);
        row.AddChild(toggle);
        _content.AddChild(row);
        RegisterFocusable(toggle);
    }

    private void AddOptionRow(string labelText, string[] items, int selected, Action<int> changed)
    {
        var row = CreateSettingRow();
        var label = new Label { Text = labelText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        var option = new OptionButton { CustomMinimumSize = new Vector2(260, 44), FocusMode = Control.FocusModeEnum.All };
        foreach (var item in items)
        {
            option.AddItem(item);
        }

        option.Select(Math.Clamp(selected, 0, items.Length - 1));
        option.ItemSelected += index => changed((int)index);
        row.AddChild(label);
        row.AddChild(option);
        _content.AddChild(row);
        RegisterFocusable(option);
    }

    private void AddQualityRow(string label, QualityLevel quality, Action<QualityLevel> changed) =>
        AddOptionRow(label, ["Niedrig", "Mittel", "Hoch", "Ultra"], (int)quality,
            index => changed((QualityLevel)index));

    private void UpdateAudio(AudioSettings audio)
    {
        _draft = _draft with { Audio = audio.Normalize() };
        _saved = _saved with { Audio = _draft.Audio.Normalize() };
        _draft = _draft with { Audio = _saved.Audio };
        _runtime.ApplyAudio(_saved.Audio);
        _audioSaveDelay = 0.25;
        _status.Text = "Audioeinstellungen sofort angewendet.";
    }

    private void FlushPendingAudioSave()
    {
        if (_audioSaveDelay <= 0)
        {
            return;
        }

        _audioSaveDelay = 0;
        _store.Save(_saved);
    }

    private void PlayTestSound()
    {
        var generator = new AudioStreamGenerator { MixRate = 22_050, BufferLength = 0.24f };
        _audioTestPlayer.Stop();
        _audioTestPlayer.Stream = generator;
        _audioTestPlayer.Play();
        if (_audioTestPlayer.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
        {
            return;
        }

        const int frameCount = 4_400;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var time = frame / 22_050.0f;
            var envelope = 1.0f - (frame / (float)frameCount);
            var sample = Mathf.Sin(Mathf.Tau * (440 + (time * 240)) * time) * envelope * 0.18f;
            playback.PushFrame(new Vector2(sample, sample));
        }

        _status.Text = "UI-Testsignal abgespielt.";
    }

    private void SetCustomVideo(VideoSettings settings) =>
        _draft = _draft with { Video = settings with { GraphicsQuality = GraphicsQualityPreset.Custom } };

    private void ApplyVideoSettings()
    {
        var candidate = _draft.Video.Normalize();
        var previous = _saved.Video;
        _runtime.ApplyVideo(candidate);
        if (RequiresVideoConfirmation(previous, candidate))
        {
            _pendingPreviousVideo = previous;
            _pendingVideo = candidate;
            _videoConfirmationRemaining = 12;
            ShowModal(
                "VIDEOEINSTELLUNGEN BESTÄTIGEN",
                "Möchtest du die neue Auflösung und den Fenstermodus beibehalten?",
                [
                    new ModalAction("BEIBEHALTEN", ConfirmPendingVideo, true),
                    new ModalAction("ZURÜCKSETZEN", () => RevertPendingVideo("Videoeinstellungen verworfen.")),
                ],
                () => RevertPendingVideo("Videoeinstellungen verworfen."));
            _modalCountdown.Text = "Automatische Rücksetzung in 12 Sekunden";
            return;
        }

        CommitVideo(candidate);
        _status.Text = "Videoeinstellungen angewendet und gespeichert.";
    }

    private void ConfirmPendingVideo()
    {
        if (_pendingVideo is not { } video)
        {
            return;
        }

        CommitVideo(video);
        ClearPendingVideo();
        HideModal();
        BuildVideoPage();
        _status.Text = "Neue Videoeinstellungen bestätigt und gespeichert.";
    }

    private void RevertPendingVideo(string message)
    {
        if (_pendingPreviousVideo is { } previous)
        {
            _runtime.ApplyVideo(previous);
            _draft = _draft with { Video = previous };
        }

        ClearPendingVideo();
        HideModal();
        BuildVideoPage();
        _status.Text = message;
    }

    private void CommitVideo(VideoSettings video)
    {
        _saved = _saved with { Video = video.Normalize() };
        _draft = _draft with { Video = _saved.Video };
        _runtime.ApplyVideo(_saved.Video);
        _store.Save(_saved);
    }

    private void ClearPendingVideo()
    {
        _pendingPreviousVideo = null;
        _pendingVideo = null;
        _videoConfirmationRemaining = 0;
    }

    private void DiscardVideoChanges()
    {
        _draft = _draft with { Video = _saved.Video };
        BuildVideoPage();
        _status.Text = "Nicht angewendete Videoänderungen verworfen.";
    }

    private void RestoreVideoDefaults()
    {
        _draft = _draft with { Video = VideoSettings.Default };
        BuildVideoPage();
        _status.Text = "Standardwerte vorgemerkt. Mit ANWENDEN übernehmen.";
    }

    private void ShowQuitConfirmation()
    {
        ShowModal(
            "SPIEL WIRKLICH VERLASSEN?",
            "Bist du sicher, dass du das Spiel verlassen möchtest? Nicht gespeicherter Fortschritt könnte verloren gehen.",
            [
                new ModalAction("SPIEL VERLASSEN", QuitGame, true),
                new ModalAction("ABBRECHEN", HideModal),
            ],
            HideModal);
    }

    private void QuitGame()
    {
        FlushPendingAudioSave();
        GetTree().Quit();
    }

    private void ShowModal(string title, string message, ModalAction[] actions, Action cancelAction)
    {
        _modalTitle.Text = title;
        _modalMessage.Text = message;
        _modalCountdown.Text = string.Empty;
        _modalCancelAction = cancelAction;
        foreach (var child in _modalActions.GetChildren())
        {
            _modalActions.RemoveChild(child);
            child.QueueFree();
        }

        Button? first = null;
        foreach (var action in actions)
        {
            var button = CreateButton(action.Label);
            button.CustomMinimumSize = new Vector2(190, 48);
            if (action.Primary)
            {
                button.AddThemeColorOverride("font_color", new Color(0.72f, 0.96f, 1));
            }

            button.Pressed += action.Callback;
            _modalActions.AddChild(button);
            first ??= button;
        }

        _modalShade.Visible = true;
        first?.GrabFocus();
    }

    private void HideModal()
    {
        _modalShade.Visible = false;
        _modalCancelAction = null;
        _modalCountdown.Text = string.Empty;
        FocusFirst();
    }

    private void RegisterFocusable(Control control) => _focusableControls.Add(control);

    private void FocusFirst()
    {
        if (Visible && !_modalShade.Visible && _focusableControls.Count > 0)
        {
            _focusableControls[0].GrabFocus();
        }
    }

    private void MoveFocus(int direction)
    {
        if (_focusableControls.Count == 0)
        {
            return;
        }

        var owner = GetViewport().GuiGetFocusOwner();
        var current = owner is null ? -1 : _focusableControls.IndexOf(owner);
        var next = current < 0
            ? 0
            : Mathf.PosMod(current + direction, _focusableControls.Count);
        _focusableControls[next].GrabFocus();
    }

    private void AdjustFocusedControl(int direction)
    {
        switch (GetViewport().GuiGetFocusOwner())
        {
            case HSlider slider:
                slider.Value = Mathf.Clamp(
                    slider.Value + (slider.Step * direction),
                    slider.MinValue,
                    slider.MaxValue);
                break;
            case OptionButton option when option.ItemCount > 0:
                var next = Mathf.PosMod(option.Selected + direction, option.ItemCount);
                option.Select(next);
                option.EmitSignal(OptionButton.SignalName.ItemSelected, next);
                break;
            case CheckButton toggle:
                toggle.ButtonPressed = direction > 0;
                break;
        }
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var metrics = CalculateResponsiveMetrics(GetViewport().GetVisibleRect().Size);
        SetMargins(_safeArea, metrics.SafeMarginX, metrics.SafeMarginY);
        SetMargins(_frameMargin, metrics.FrameMarginX, metrics.FrameMarginY);
        _content.AddThemeConstantOverride("separation", metrics.ContentGap);
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize)
    {
        var width = Mathf.Max(1_024, viewportSize.X);
        var height = Mathf.Max(700, viewportSize.Y);
        var compact = width < 1_600 || height < 900;
        return new ResponsiveMetrics(
            SafeMarginX: Mathf.RoundToInt(Mathf.Clamp(width * 0.028f, 28, 72)),
            SafeMarginY: Mathf.RoundToInt(Mathf.Clamp(height * 0.028f, 18, 46)),
            FrameMarginX: compact ? 28 : 48,
            FrameMarginY: compact ? 18 : 26,
            ContentGap: compact ? 6 : 10);
    }

    private static void ValidateDesktopMetrics(Vector2 viewportSize, ResponsiveMetrics metrics)
    {
        var usableHeight = viewportSize.Y - (2 * metrics.SafeMarginY) - (2 * metrics.FrameMarginY);
        // Six key rows, the wheel toggle, pagination and the action row are the
        // tallest compact page. Header/footer reserve includes both separators.
        var tallestPage = (7 * 42) + 42 + 50 + (8 * metrics.ContentGap) + 116;
        if (tallestPage > usableHeight)
        {
            throw new InvalidOperationException(
                $"Settings layout does not fit {viewportSize.X:0}x{viewportSize.Y:0} without scrolling.");
        }
    }

    private static bool ContainsScrollContainer(Node node)
    {
        if (node is ScrollContainer)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (ContainsScrollContainer(child))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetMargins(MarginContainer container, int horizontal, int vertical)
    {
        container.AddThemeConstantOverride("margin_left", horizontal);
        container.AddThemeConstantOverride("margin_right", horizontal);
        container.AddThemeConstantOverride("margin_top", vertical);
        container.AddThemeConstantOverride("margin_bottom", vertical);
    }

    private static int FindResolutionIndex(ScreenResolution current)
    {
        for (var index = 0; index < Resolutions.Length; index++)
        {
            if (Resolutions[index] == current)
            {
                return index;
            }
        }

        return 0;
    }

    private static int FindClosestIndex(int[] values, int current)
    {
        var closestIndex = 0;
        var closestDistance = int.MaxValue;
        for (var index = 0; index < values.Length; index++)
        {
            var distance = Math.Abs(values[index] - current);
            if (distance < closestDistance)
            {
                closestIndex = index;
                closestDistance = distance;
            }
        }

        return closestIndex;
    }

    private static bool RequiresVideoConfirmation(VideoSettings previous, VideoSettings candidate) =>
        previous.Resolution != candidate.Resolution ||
        previous.WindowMode != candidate.WindowMode ||
        previous.RefreshRate != candidate.RefreshRate;

    internal static Theme CreateSciFiTheme()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", new Color(0.82f, 0.9f, 0.94f));
        theme.SetFontSize("font_size", "Label", 17);
        theme.SetColor("font_color", "Button", new Color(0.82f, 0.92f, 0.96f));
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_focus_color", "Button", Colors.White);
        theme.SetFontSize("font_size", "Button", 18);
        theme.SetStylebox("normal", "Button", CreateStyleBox(new Color(0.015f, 0.04f, 0.058f, 0.94f), new Color(0.14f, 0.38f, 0.5f), 1));
        theme.SetStylebox("hover", "Button", CreateStyleBox(new Color(0.02f, 0.12f, 0.17f, 0.98f), new Color(0.05f, 0.8f, 1), 2));
        theme.SetStylebox("focus", "Button", CreateStyleBox(new Color(0.025f, 0.15f, 0.2f, 1), new Color(0.12f, 0.9f, 1), 3));
        theme.SetStylebox("pressed", "Button", CreateStyleBox(new Color(0.02f, 0.2f, 0.26f, 1), new Color(0.35f, 0.96f, 1), 2));
        theme.SetStylebox("panel", "PanelContainer", CreateStyleBox(new Color(0.005f, 0.025f, 0.038f, 0.94f), new Color(0.05f, 0.52f, 0.7f, 0.85f), 2));
        theme.SetStylebox("normal", "LineEdit", CreateStyleBox(new Color(0.01f, 0.05f, 0.07f, 1), new Color(0.08f, 0.55f, 0.7f), 1));
        return theme;
    }

    private static StyleBoxFlat CreateStyleBox(Color background, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
    }

    private sealed record ModalAction(string Label, Action Callback, bool Primary = false);

    private readonly record struct ResponsiveMetrics(
        int SafeMarginX,
        int SafeMarginY,
        int FrameMarginX,
        int FrameMarginY,
        int ContentGap);
}

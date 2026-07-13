using Godot;

namespace SpaceFactory.Presentation.Building;

public partial class MachinePanelController : CanvasLayer
{
    private readonly List<MaterialRowWidgets> _inputRows = [];
    private readonly List<MaterialRowWidgets> _outputRows = [];
    private Control _overlay = null!;
    private MarginContainer _safeArea = null!;
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private BoxContainer _body = null!;
    private PanelContainer _recipePanel = null!;
    private Label _machineName = null!;
    private Label _statusBadge = null!;
    private Label _statusDetail = null!;
    private MachineGlyphControl _glyph = null!;
    private OptionButton _recipeSelector = null!;
    private Label _recipeSummary = null!;
    private VBoxContainer _inputs = null!;
    private VBoxContainer _outputs = null!;
    private Label _progressLabel = null!;
    private ProgressBar _progress = null!;
    private Label _requiredPower = null!;
    private Label _availablePower = null!;
    private Label _powerStatus = null!;
    private Button _activeToggle = null!;
    private Button _loadInputs = null!;
    private Button _collectOutputs = null!;
    private Button _returnInputs = null!;
    private BoxContainer _footer = null!;
    private Button _close = null!;
    private MachinePanelViewModel? _model;
    private string _renderedRecipeSignature = string.Empty;
    private string _renderedFlowSignature = string.Empty;
    private Tween? _transitionTween;
    private bool _ready;
    private bool _isOpen;
    private bool _isClosing;
    private bool _suppressEvents;

    public bool IsOpen => _isOpen || _isClosing;

    public event Action<string>? RecipeSelectionRequested;

    public event Action<bool>? ActiveStateChangeRequested;

    public event Action? LoadInputsRequested;

    public event Action? CollectOutputsRequested;

    public event Action? ReturnInputsRequested;

    public event Action? Closed;

    public override void _Ready()
    {
        _overlay = GetNode<Control>("Overlay");
        _safeArea = GetNode<MarginContainer>("Overlay/SafeArea");
        _frame = GetNode<PanelContainer>("Overlay/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin");
        _body = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body");
        _recipePanel = GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/RecipePanel");
        _machineName = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Identity/Name");
        _statusBadge = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/StatusBadge");
        _glyph = GetNode<MachineGlyphControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Glyph");
        _close = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Close");
        _recipeSelector = GetNode<OptionButton>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/RecipePanel/Margin/Layout/RecipeSelector");
        _recipeSummary = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/RecipePanel/Margin/Layout/RecipeSummary");
        _inputs = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Flow/Inputs/Rows");
        _outputs = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Flow/Outputs/Rows");
        _progressLabel = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/ProgressHeader/Value");
        _progress = GetNode<ProgressBar>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Progress");
        _requiredPower = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Energy/Required/Value");
        _availablePower = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Energy/Available/Value");
        _powerStatus = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/Energy/Status/Value");
        _statusDetail = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel/Margin/Layout/StatusDetail");
        _activeToggle = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/ActiveToggle");
        _loadInputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/LoadInputs");
        _collectOutputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/CollectOutputs");
        _returnInputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/ReturnInputs");
        _footer = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer");

        _safeArea.Theme = BuildingUiTheme.CreateTheme();
        _frame.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreateFrameStyle());
        _recipePanel.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/Body/ProductionPanel")
            .AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        ApplyProgressStyle();
        _close.Pressed += Close;
        _recipeSelector.ItemSelected += HandleRecipeSelected;
        _activeToggle.Toggled += HandleActiveToggled;
        _loadInputs.Pressed += HandleLoadInputsPressed;
        _collectOutputs.Pressed += HandleCollectOutputsPressed;
        _returnInputs.Pressed += HandleReturnInputsPressed;
        _frame.Resized += UpdateFramePivot;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        UpdateResponsiveLayout();
        UpdateFramePivot();
        Visible = false;
    }

    public override void _ExitTree()
    {
        _close.Pressed -= Close;
        _recipeSelector.ItemSelected -= HandleRecipeSelected;
        _activeToggle.Toggled -= HandleActiveToggled;
        _loadInputs.Pressed -= HandleLoadInputsPressed;
        _collectOutputs.Pressed -= HandleCollectOutputsPressed;
        _returnInputs.Pressed -= HandleReturnInputsPressed;
        _frame.Resized -= UpdateFramePivot;
        GetViewport().SizeChanged -= UpdateResponsiveLayout;
        _transitionTween?.Kill();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(MachinePanelViewModel model)
    {
        if (!_ready)
        {
            throw new InvalidOperationException("MachinePanelController must be inside the scene tree before opening.");
        }

        UpdateView(model);
        if (_isOpen && !_isClosing)
        {
            return;
        }

        _transitionTween?.Kill();
        _isOpen = true;
        _isClosing = false;
        Visible = true;
        _overlay.Modulate = new Color(1, 1, 1, 0);
        _frame.Scale = new Vector2(0.985f, 0.985f);
        _transitionTween = CreateTween().SetParallel();
        _transitionTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, "modulate", Colors.White, 0.15);
        _transitionTween.TweenProperty(_frame, "scale", Vector2.One, 0.15);
    }

    /// <summary>
    /// Applies a new gameplay snapshot. Recipe widgets are rebuilt only if the
    /// available recipe definitions changed; production ticks update labels and
    /// bars in place.
    /// </summary>
    public void UpdateView(MachinePanelViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        if (!_ready)
        {
            return;
        }

        _suppressEvents = true;
        try
        {
            _machineName.Text = model.DisplayName.ToUpperInvariant();
            _glyph.Configure(model.Glyph, model.Status != MachineUiStatus.UnderConstruction);
            RefreshRecipes(model);
            RefreshMachineState(model);
            RefreshFlow(model);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    public void Close()
    {
        if (!_isOpen || _isClosing)
        {
            return;
        }

        _isOpen = false;
        _isClosing = true;
        _transitionTween?.Kill();
        GetViewport().GuiReleaseFocus();
        _transitionTween = CreateTween().SetParallel();
        _transitionTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, "modulate", new Color(1, 1, 1, 0), 0.12);
        _transitionTween.TweenProperty(_frame, "scale", new Vector2(0.99f, 0.99f), 0.12);
        _transitionTween.SetParallel(false);
        _transitionTween.TweenCallback(Callable.From(FinishClose));
    }

    public void CloseImmediately()
    {
        if (!IsOpen)
        {
            return;
        }

        _transitionTween?.Kill();
        _isOpen = false;
        _isClosing = true;
        FinishClose();
    }

    public void RunConstructionSmokeTest()
    {
        if (!_ready)
        {
            throw new InvalidOperationException("Machine panel smoke test requires a ready scene.");
        }

        var originalModel = _model;
        try
        {
            var smoke = CreateSmokeViewModel();
            UpdateView(smoke);
            if (_recipeSelector.ItemCount != 2 || _recipeSelector.Selected != 0)
            {
                throw new InvalidOperationException("Machine panel did not render and select its recipes.");
            }

            if (_inputRows.Count != 2 || _outputRows.Count != 1)
            {
                throw new InvalidOperationException("Machine panel input/output rows are incomplete.");
            }

            if (!Mathf.IsEqualApprox((float)_progress.Value, 42) || !_activeToggle.ButtonPressed)
            {
                throw new InvalidOperationException("Machine progress and persistent active state are not represented.");
            }

            var changedTick = smoke with { ProductionProgress = 0.67f, AvailablePower = 0, Status = MachineUiStatus.WaitingForEnergy };
            var recipeSignature = _renderedRecipeSignature;
            UpdateView(changedTick);
            if (!Mathf.IsEqualApprox((float)_progress.Value, 67) || _renderedRecipeSignature != recipeSignature)
            {
                throw new InvalidOperationException("A production tick rebuilt recipes or failed to update progress in place.");
            }

            Vector2[] viewportSizes = [new(800, 600), new(1366, 768), new(1920, 1080)];
            foreach (var size in viewportSizes)
            {
                var metrics = CalculateResponsiveMetrics(size);
                if (metrics.SafeMargin < 10 || metrics.FrameMargin < 12)
                {
                    throw new InvalidOperationException($"Invalid machine panel layout at {size.X}x{size.Y}.");
                }
            }
        }
        finally
        {
            if (originalModel is not null)
            {
                UpdateView(originalModel);
            }
            else
            {
                ClearView();
            }
        }

        GD.Print("MACHINE_PANEL_UI_SMOKE_OK: recipes, inputs/outputs, live progress, energy/status, persistent start toggle");
    }

    private void RefreshRecipes(MachinePanelViewModel model)
    {
        var signature = string.Join(
            '|',
            model.Recipes.Select(recipe => $"{recipe.RecipeId}:{recipe.DisplayName}:{recipe.IsUnlocked}"));
        if (_renderedRecipeSignature != signature)
        {
            _recipeSelector.Clear();
            for (var index = 0; index < model.Recipes.Count; index++)
            {
                var recipe = model.Recipes[index];
                _recipeSelector.AddItem(recipe.IsUnlocked ? recipe.DisplayName : $"{recipe.DisplayName}  ·  GESPERRT", index);
                _recipeSelector.GetPopup().SetItemDisabled(index, !recipe.IsUnlocked);
                _recipeSelector.GetPopup().SetItemTooltip(index, recipe.IsUnlocked ? string.Empty : recipe.UnlockMessage);
            }

            _renderedRecipeSignature = signature;
        }

        var selectedIndex = FindRecipeIndex(model.Recipes, model.SelectedRecipeId);
        _recipeSelector.Select(selectedIndex);
        var selectedRecipe = GetSelectedRecipe(model);
        _recipeSummary.Text = selectedRecipe is null
            ? "Wähle ein freigeschaltetes Rezept. Die Produktion startet erst nach dem Einschalten."
            : $"ZYKLUS  {selectedRecipe.DurationSeconds:0.#} s  ·  Produktion startet nur bei vollständigem Material und freiem Ausgang.";
    }

    private void RefreshMachineState(MachinePanelViewModel model)
    {
        var status = GetStatusPresentation(model.Status);
        _statusBadge.Text = status.Label;
        _statusBadge.AddThemeColorOverride("font_color", status.Color);
        _statusDetail.Text = string.IsNullOrWhiteSpace(model.StatusDetail) ? status.Description : model.StatusDetail;
        _statusDetail.AddThemeColorOverride("font_color", status.Color.Lightened(0.14f));

        var progress = Mathf.Clamp(model.ProductionProgress, 0, 1);
        _progress.Value = progress * 100;
        _progressLabel.Text = $"{progress * 100:0}%";
        _requiredPower.Text = $"{model.RequiredPower:0.#} kW";
        _availablePower.Text = $"{model.AvailablePower:0.#} kW";
        var hasPower = model.Status != MachineUiStatus.WaitingForEnergy &&
                       (model.RequiredPower <= 0 || model.AvailablePower >= model.RequiredPower);
        _powerStatus.Text = hasPower ? "VERSORGT" : "UNTERVERSORGT";
        _powerStatus.AddThemeColorOverride("font_color", hasPower ? BuildingUiTheme.Success : BuildingUiTheme.Warning);
        _activeToggle.ButtonPressed = model.IsActive;
        _activeToggle.Text = model.IsActive ? "MASCHINE AKTIV  ·  STOPPEN" : "MASCHINE STARTEN";
        _activeToggle.Disabled = model.Status == MachineUiStatus.UnderConstruction;
    }

    private void RefreshFlow(MachinePanelViewModel model)
    {
        var recipe = GetSelectedRecipe(model);
        var flowSignature = recipe is null
            ? "none"
            : $"{recipe.RecipeId}|{string.Join(',', recipe.Inputs.Select(input => input.DisplayName))}|{string.Join(',', recipe.Outputs.Select(output => output.DisplayName))}";
        if (_renderedFlowSignature != flowSignature)
        {
            RebuildMaterialRows(_inputs, _inputRows, recipe?.Inputs.Count ?? 0, input: true);
            RebuildMaterialRows(_outputs, _outputRows, recipe?.Outputs.Count ?? 0, input: false);
            _renderedFlowSignature = flowSignature;
        }

        if (recipe is null)
        {
            ShowEmptyRows(_inputs, "KEIN REZEPT AUSGEWÄHLT");
            ShowEmptyRows(_outputs, "KEINE AUSGABE");
            return;
        }

        RemoveEmptyRows(_inputs);
        RemoveEmptyRows(_outputs);
        for (var index = 0; index < recipe.Inputs.Count; index++)
        {
            var input = recipe.Inputs[index];
            UpdateMaterialRow(
                _inputRows[index],
                input.DisplayName,
                $"{input.AvailableAmount} / {input.RequiredAmount}",
                input.Accent,
                input.IsAvailable ? BuildingUiTheme.Success : BuildingUiTheme.Failure);
        }

        for (var index = 0; index < recipe.Outputs.Count; index++)
        {
            var output = recipe.Outputs[index];
            var amount = output.Capacity > 0
                ? $"+{output.ProducedAmount}  ·  {output.StoredAmount} / {output.Capacity}"
                : $"+{output.ProducedAmount}";
            UpdateMaterialRow(
                _outputRows[index],
                output.DisplayName,
                amount,
                output.Accent,
                output.IsFull ? BuildingUiTheme.Warning : BuildingUiTheme.Success);
        }
    }

    private static void RebuildMaterialRows(
        VBoxContainer container,
        List<MaterialRowWidgets> rows,
        int count,
        bool input)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }

        rows.Clear();
        for (var index = 0; index < count; index++)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 46),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            panel.AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(
                    BuildingUiTheme.CardBackground,
                    input ? new Color(0.1f, 0.43f, 0.55f, 0.8f) : new Color(0.12f, 0.52f, 0.42f, 0.8f)));
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            var marker = new ColorRect
            {
                CustomMinimumSize = new Vector2(7, 25),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var name = new Label
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            name.AddThemeFontSizeOverride("font_size", 12);
            var amount = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            amount.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(marker);
            row.AddChild(name);
            row.AddChild(amount);
            panel.AddChild(row);
            container.AddChild(panel);
            rows.Add(new MaterialRowWidgets(marker, name, amount));
        }
    }

    private static void UpdateMaterialRow(
        MaterialRowWidgets row,
        string name,
        string amount,
        Color accent,
        Color amountColor)
    {
        row.Marker.Color = accent;
        row.Name.Text = name;
        row.Name.AddThemeColorOverride("font_color", BuildingUiTheme.Text);
        row.Amount.Text = amount;
        row.Amount.AddThemeColorOverride("font_color", amountColor);
    }

    private static void ShowEmptyRows(VBoxContainer container, string text)
    {
        if (container.GetNodeOrNull<Label>("Empty") is not null)
        {
            return;
        }

        var label = new Label
        {
            Name = "Empty",
            Text = text,
            CustomMinimumSize = new Vector2(0, 46),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        label.AddThemeFontSizeOverride("font_size", 11);
        container.AddChild(label);
    }

    private static void RemoveEmptyRows(VBoxContainer container)
    {
        var empty = container.GetNodeOrNull<Label>("Empty");
        if (empty is null)
        {
            return;
        }

        container.RemoveChild(empty);
        empty.QueueFree();
    }

    private void HandleRecipeSelected(long index)
    {
        if (_suppressEvents || _model is null || index < 0 || index >= _model.Recipes.Count)
        {
            return;
        }

        var recipe = _model.Recipes[(int)index];
        if (!recipe.IsUnlocked)
        {
            return;
        }

        _model = _model with { SelectedRecipeId = recipe.RecipeId };
        RefreshRecipes(_model);
        RefreshFlow(_model);
        RecipeSelectionRequested?.Invoke(recipe.RecipeId);
    }

    private void ClearView()
    {
        _suppressEvents = true;
        try
        {
            _model = null;
            _renderedRecipeSignature = string.Empty;
            _renderedFlowSignature = string.Empty;
            _recipeSelector.Clear();
            RebuildMaterialRows(_inputs, _inputRows, 0, input: true);
            RebuildMaterialRows(_outputs, _outputRows, 0, input: false);
            ShowEmptyRows(_inputs, "KEIN REZEPT AUSGEWÄHLT");
            ShowEmptyRows(_outputs, "KEINE AUSGABE");
            _progress.Value = 0;
            _progressLabel.Text = "0%";
            _requiredPower.Text = "0 kW";
            _availablePower.Text = "0 kW";
            _powerStatus.Text = "—";
            _activeToggle.ButtonPressed = false;
            _activeToggle.Text = "MASCHINE STARTEN";
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void HandleActiveToggled(bool active)
    {
        if (_suppressEvents || _model is null)
        {
            return;
        }

        _model = _model with { IsActive = active };
        _activeToggle.Text = active ? "MASCHINE AKTIV  ·  STOPPEN" : "MASCHINE STARTEN";
        ActiveStateChangeRequested?.Invoke(active);
    }

    private void HandleLoadInputsPressed()
    {
        if (_model is not null)
        {
            LoadInputsRequested?.Invoke();
        }
    }

    private void HandleCollectOutputsPressed()
    {
        if (_model is not null)
        {
            CollectOutputsRequested?.Invoke();
        }
    }

    private void HandleReturnInputsPressed()
    {
        if (_model is not null)
        {
            ReturnInputsRequested?.Invoke();
        }
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var metrics = CalculateResponsiveMetrics(GetViewport().GetVisibleRect().Size);
        SetMargins(_safeArea, metrics.SafeMargin);
        SetMargins(_frameMargin, metrics.FrameMargin);
        _body.Vertical = metrics.Stacked;
        _footer.Vertical = metrics.Stacked || GetViewport().GetVisibleRect().Size.X < 1_500;
        _body.AddThemeConstantOverride("separation", metrics.Stacked ? 12 : 18);
        _recipePanel.CustomMinimumSize = metrics.Stacked ? new Vector2(0, 228) : new Vector2(metrics.RecipeWidth, 0);
        _recipePanel.SizeFlagsHorizontal = metrics.Stacked ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize)
    {
        var width = Mathf.Max(640, viewportSize.X);
        var height = Mathf.Max(480, viewportSize.Y);
        var safeMargin = Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(width * 0.075f, height * 0.09f), 18, 86));
        var frameMargin = width < 1000 ? 14 : 22;
        var stacked = width < 950;
        var recipeWidth = width < 1400 ? 300 : 340;
        return new ResponsiveMetrics(stacked, safeMargin, frameMargin, recipeWidth);
    }

    private static void SetMargins(MarginContainer container, int margin)
    {
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
    }

    private static MachineRecipeViewModel? GetSelectedRecipe(MachinePanelViewModel model)
    {
        if (model.SelectedRecipeId is null)
        {
            return null;
        }

        return model.Recipes.FirstOrDefault(
            recipe => string.Equals(recipe.RecipeId, model.SelectedRecipeId, StringComparison.Ordinal));
    }

    private static int FindRecipeIndex(IReadOnlyList<MachineRecipeViewModel> recipes, string? recipeId)
    {
        if (recipeId is null)
        {
            return -1;
        }

        for (var index = 0; index < recipes.Count; index++)
        {
            if (string.Equals(recipes[index].RecipeId, recipeId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static StatusPresentation GetStatusPresentation(MachineUiStatus status) => status switch
    {
        MachineUiStatus.UnderConstruction => new("IM BAU", "Die Konstruktion wird abgeschlossen.", BuildingUiTheme.Accent),
        MachineUiStatus.SwitchedOff => new("AUSGESCHALTET", "Maschine ist ausgeschaltet.", BuildingUiTheme.TextMuted),
        MachineUiStatus.Ready => new("BEREIT", "Bereit für den nächsten Produktionszyklus.", BuildingUiTheme.Success),
        MachineUiStatus.Producing => new("PRODUZIERT", "Produktionszyklus läuft.", BuildingUiTheme.Accent),
        MachineUiStatus.WaitingForMaterials => new("WARTET AUF MATERIAL", "Fehlende Eingabematerialien werden benötigt.", BuildingUiTheme.Warning),
        MachineUiStatus.WaitingForEnergy => new("WARTET AUF ENERGIE", "Das lokale Stromnetz liefert zu wenig Leistung.", BuildingUiTheme.Warning),
        MachineUiStatus.OutputFull => new("AUSGABE VOLL", "Leere den Ausgabespeicher, um fortzufahren.", BuildingUiTheme.Warning),
        MachineUiStatus.Blocked => new("BLOCKIERT", "Die Maschine kann momentan nicht arbeiten.", BuildingUiTheme.Failure),
        _ => new("UNBEKANNT", "Kein Status verfügbar.", BuildingUiTheme.TextMuted),
    };

    private void ApplyProgressStyle()
    {
        _progress.AddThemeStyleboxOverride(
            "background",
            BuildingUiTheme.CreatePanelStyle(new Color(0.003f, 0.018f, 0.026f, 1), new Color(0.05f, 0.27f, 0.34f, 0.9f)));
        _progress.AddThemeStyleboxOverride(
            "fill",
            BuildingUiTheme.CreatePanelStyle(new Color(0.03f, 0.52f, 0.68f, 0.95f), BuildingUiTheme.Accent));
    }

    private void UpdateFramePivot()
    {
        _frame.PivotOffset = _frame.Size * 0.5f;
    }

    private void FinishClose()
    {
        if (!_isClosing)
        {
            return;
        }

        _isClosing = false;
        Visible = false;
        _overlay.Modulate = Colors.White;
        _frame.Scale = Vector2.One;
        Closed?.Invoke();
    }

    private static MachinePanelViewModel CreateSmokeViewModel()
    {
        var recipe = new MachineRecipeViewModel(
            "iron_ingot",
            "Eisenbarren",
            [
                new MachineMaterialViewModel("Zerkleinertes Eisenerz", 2, 7, new Color(0.46f, 0.51f, 0.53f)),
                new MachineMaterialViewModel("Kohlenstoff", 1, 0, new Color(0.22f, 0.23f, 0.24f)),
            ],
            [new MachineOutputViewModel("Eisenbarren", 1, 12, 200, new Color(0.65f, 0.69f, 0.71f))],
            4.5f);
        var lockedRecipe = new MachineRecipeViewModel(
            "titanium_ingot",
            "Titanbarren",
            [],
            [],
            8,
            IsUnlocked: false,
            UnlockMessage: "Erweiterte Metallverarbeitung erforderlich");
        return new MachinePanelViewModel(
            "smelter",
            "Schmelzer",
            MachineGlyph.Smelter,
            [recipe, lockedRecipe],
            recipe.RecipeId,
            MachineUiStatus.Producing,
            "Produktionszyklus läuft.",
            true,
            0.42f,
            12,
            18);
    }

    private readonly record struct MaterialRowWidgets(ColorRect Marker, Label Name, Label Amount);

    private readonly record struct StatusPresentation(string Label, string Description, Color Color);

    private readonly record struct ResponsiveMetrics(bool Stacked, int SafeMargin, int FrameMargin, int RecipeWidth);
}

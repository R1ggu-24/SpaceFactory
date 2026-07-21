using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Production;
using SpaceFactory.Presentation.InventoryUI;

namespace SpaceFactory.Presentation.Building;

public partial class MachinePanelController : CanvasLayer
{
    public const string PersonalInventoryId = "machine_personal";

    private readonly List<MaterialRowWidgets> _inputRows = [];
    private readonly List<OutputSlotWidgets> _outputSlots = [];
    private Control _overlay = null!;
    private InventoryWorldDropSurface _worldDropSurface = null!;
    private MarginContainer _safeArea = null!;
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private BoxContainer _body = null!;
    private VBoxContainer _machineColumn = null!;
    private InventoryPanelControl _personalInventoryPanel = null!;
    private PanelContainer _recipePanel = null!;
    private PanelContainer _productionPanel = null!;
    private Label _machineName = null!;
    private Label _statusBadge = null!;
    private Label _statusDetail = null!;
    private MachineGlyphControl _glyph = null!;
    private OptionButton _recipeSelector = null!;
    private Label _recipeSummary = null!;
    private HBoxContainer _flow = null!;
    private VBoxContainer _inputGroup = null!;
    private VBoxContainer _outputGroup = null!;
    private Label _flowArrow = null!;
    private GridContainer _inputs = null!;
    private GridContainer _outputs = null!;
    private HBoxContainer _progressHeader = null!;
    private Label _progressLabel = null!;
    private ProgressBar _progress = null!;
    private Label _energyTitle = null!;
    private HBoxContainer _energy = null!;
    private Label _requiredPower = null!;
    private Label _availablePower = null!;
    private Label _powerStatus = null!;
    private PanelContainer _generatorTank = null!;
    private Label _generatorTankReadout = null!;
    private Button _fillGeneratorTank = null!;
    private Button _drainGeneratorTank = null!;
    private Button _activeToggle = null!;
    private Button _loadInputs = null!;
    private Button _collectOutputs = null!;
    private Button _returnInputs = null!;
    private BoxContainer _footer = null!;
    private Label _footerHint = null!;
    private Button _close = null!;
    private InventoryTrashDropTarget _machineTrash = null!;
    private SlotInventory? _personalInventory;
    private ItemPresentationCatalog _itemPresentations =
        ItemPresentationCatalog.Create([], DefaultProductionItemCatalog.Instance);
    private int? _selectedPersonalSlotIndex;
    private MachinePanelViewModel? _model;
    private string _renderedRecipeSignature = string.Empty;
    private string _renderedFlowSignature = string.Empty;
    private Tween? _transitionTween;
    private bool _ready;
    private bool _isOpen;
    private bool _isClosing;
    private bool _suppressEvents;
    private Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? _previewMachineTransfer;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _machineTransferRequested;
    private Func<InventorySlotAddress, bool>? _deleteMachineStackRequested;

    public bool IsOpen => _isOpen || _isClosing;

    public event Action<string>? RecipeSelectionRequested;

    public event Action<bool>? ActiveStateChangeRequested;

    public event Action? LoadInputsRequested;

    public event Action? CollectOutputsRequested;

    public event Action? ReturnInputsRequested;

    public event Action? GeneratorTankFillRequested;

    public event Action? GeneratorTankDrainRequested;

    public event Action? PersonalInventoryChanged;

    public event Action<InventorySlotAddress, Vector2>? WorldDropRequested;

    public event Action? Closed;

    public override void _Ready()
    {
        _overlay = GetNode<Control>("Overlay");
        _worldDropSurface = GetNode<InventoryWorldDropSurface>("Overlay/WorldDropSurface");
        _safeArea = GetNode<MarginContainer>("Overlay/SafeArea");
        _frame = GetNode<PanelContainer>("Overlay/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin");
        _body = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body");
        _personalInventoryPanel = GetNode<InventoryPanelControl>(
            "Overlay/SafeArea/Frame/FrameMargin/Layout/Body/PersonalInventory");
        _recipePanel = GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/RecipePanel");
        _productionPanel = GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel");
        _machineName = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Identity/Name");
        _statusBadge = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/StatusBadge");
        _glyph = GetNode<MachineGlyphControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Glyph");
        _close = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Close");
        _recipeSelector = GetNode<OptionButton>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/RecipePanel/Margin/Layout/RecipeSelector");
        _recipeSummary = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/RecipePanel/Margin/Layout/RecipeSummary");
        _flow = GetNode<HBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow");
        _inputGroup = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow/Inputs");
        _outputGroup = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow/Outputs");
        _flowArrow = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow/Arrow");
        _inputs = GetNode<GridContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow/Inputs/Rows");
        _outputs = GetNode<GridContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Flow/Outputs/Rows");
        _progressHeader = GetNode<HBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/ProgressHeader");
        _progressLabel = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/ProgressHeader/Value");
        _progress = GetNode<ProgressBar>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Progress");
        _energyTitle = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/EnergyTitle");
        _energy = GetNode<HBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Energy");
        _requiredPower = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Energy/Required/Value");
        _availablePower = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Energy/Available/Value");
        _powerStatus = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/Energy/Status/Value");
        _generatorTank = GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/GeneratorTank");
        _generatorTankReadout = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/GeneratorTank/Margin/Row/Readout");
        _fillGeneratorTank = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/GeneratorTank/Margin/Row/Fill");
        _drainGeneratorTank = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/GeneratorTank/Margin/Row/Drain");
        _statusDetail = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel/Margin/Layout/StatusDetail");
        _activeToggle = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/ActiveToggle");
        _loadInputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/LoadInputs");
        _collectOutputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/CollectOutputs");
        _returnInputs = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/ReturnInputs");
        _footer = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer");
        _footerHint = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/Hint");

        _safeArea.Theme = BuildingUiTheme.CreateTheme();
        _frame.AddThemeStyleboxOverride("panel", BuildingUiTheme.CreateFrameStyle());
        _recipePanel.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        GetNode<PanelContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Body/ProductionPanel")
            .AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(BuildingUiTheme.PanelBackground, BuildingUiTheme.AccentMuted));
        _generatorTank.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(
                new Color(0.004f, 0.03f, 0.041f, 0.98f),
                new Color(0.08f, 0.54f, 0.66f, 0.9f)));
        ApplyProgressStyle();
        _close.Pressed += Close;
        _recipeSelector.ItemSelected += HandleRecipeSelected;
        _activeToggle.Toggled += HandleActiveToggled;
        _loadInputs.Pressed += HandleLoadInputsPressed;
        _collectOutputs.Pressed += HandleCollectOutputsPressed;
        _returnInputs.Pressed += HandleReturnInputsPressed;
        _fillGeneratorTank.Pressed += HandleGeneratorTankFillPressed;
        _drainGeneratorTank.Pressed += HandleGeneratorTankDrainPressed;
        _worldDropSurface.WorldDropRequested += HandleWorldDropRequested;
        BuildTwoColumnLayout();
        _frame.Resized += UpdateFramePivot;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _ready = true;
        if (_personalInventory is not null)
        {
            ConfigurePersonalInventoryPanel();
        }

        UpdateResponsiveLayout();
        UpdateFramePivot();
        Visible = false;
    }

    /// <summary>
    /// Connects the already existing astronaut inventory to the machine window.
    /// The panel intentionally exposes only inventory-local rearranging here;
    /// material loading and output collection continue to use the established,
    /// transactional machine transfer commands in the footer.
    /// </summary>
    public void ConfigurePersonalInventory(
        SlotInventory personalInventory,
        ItemPresentationCatalog itemPresentations,
        Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? previewMachineTransfer = null,
        Func<InventorySlotAddress, InventorySlotAddress, bool>? machineTransferRequested = null,
        Func<InventorySlotAddress, bool>? deleteMachineStackRequested = null)
    {
        ArgumentNullException.ThrowIfNull(personalInventory);
        ArgumentNullException.ThrowIfNull(itemPresentations);
        _personalInventory = personalInventory;
        _itemPresentations = itemPresentations;
        _previewMachineTransfer = previewMachineTransfer;
        _machineTransferRequested = machineTransferRequested;
        _deleteMachineStackRequested = deleteMachineStackRequested;
        if (_ready)
        {
            ConfigurePersonalInventoryPanel();
            UpdateResponsiveLayout();
        }
    }

    public override void _ExitTree()
    {
        _close.Pressed -= Close;
        _recipeSelector.ItemSelected -= HandleRecipeSelected;
        _activeToggle.Toggled -= HandleActiveToggled;
        _loadInputs.Pressed -= HandleLoadInputsPressed;
        _collectOutputs.Pressed -= HandleCollectOutputsPressed;
        _returnInputs.Pressed -= HandleReturnInputsPressed;
        _fillGeneratorTank.Pressed -= HandleGeneratorTankFillPressed;
        _drainGeneratorTank.Pressed -= HandleGeneratorTankDrainPressed;
        _worldDropSurface.WorldDropRequested -= HandleWorldDropRequested;
        if (_machineTrash is not null)
        {
            _machineTrash.DeleteRequested -= HandleMachineDeleteRequested;
        }
        _frame.Resized -= UpdateFramePivot;
        GetViewport().SizeChanged -= UpdateResponsiveLayout;
        _transitionTween?.Kill();
    }

    private void HandleWorldDropRequested(InventorySlotAddress address, Vector2 screenPosition) =>
        WorldDropRequested?.Invoke(address, screenPosition);

    private void BuildTwoColumnLayout()
    {
        _machineColumn = new VBoxContainer
        {
            Name = "MachineColumn",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 2,
        };
        _machineColumn.AddThemeConstantOverride("separation", 8);
        _body.AddChild(_machineColumn);
        _recipePanel.Reparent(_machineColumn);
        _productionPanel.Reparent(_machineColumn);
        _recipePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _recipePanel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        _productionPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _productionPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _personalInventoryPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _personalInventoryPanel.SizeFlagsStretchRatio = 1;

        // The selected recipe remains visible, while explanatory duplicate text and
        // permanent footer/status prose no longer consume valuable vertical space.
        _recipeSummary.Visible = false;
        _statusDetail.Visible = false;
        _footerHint.Visible = false;
        GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Identity/Eyebrow").Visible = false;

        _machineTrash = new InventoryTrashDropTarget
        {
            Name = "MachineTrash",
            CustomMinimumSize = new Vector2(
                InventoryUiConfiguration.CompactTrashTargetSize,
                InventoryUiConfiguration.CompactTrashTargetSize),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = "Maschineninhalt dauerhaft löschen",
        };
        _generatorTank.GetParent().AddChild(_machineTrash);
        _machineTrash.DeleteRequested += HandleMachineDeleteRequested;
    }

    private void HandleMachineDeleteRequested(InventorySlotAddress address)
    {
        if (!address.InventoryId.StartsWith("machine_input:", StringComparison.Ordinal) &&
            !address.InventoryId.StartsWith("machine_output:", StringComparison.Ordinal))
        {
            return;
        }

        if (_deleteMachineStackRequested?.Invoke(address) == true)
        {
            PersonalInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Closes the deepest machine-panel sub-state without leaving the panel.
    /// </summary>
    public bool TryCloseTransientUi()
    {
        var popup = _recipeSelector.GetPopup();
        if (!popup.Visible)
        {
            return false;
        }

        popup.Hide();
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
        {
            if (!TryCloseTransientUi())
            {
                Close();
            }
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
            _recipePanel.Visible = model.Recipes.Count > 1;
            var displaysProductionMetrics = model.Glyph != MachineGlyph.Storage;
            _progressHeader.Visible = displaysProductionMetrics;
            _progress.Visible = displaysProductionMetrics;
            _energyTitle.Visible = displaysProductionMetrics;
            _energy.Visible = displaysProductionMetrics;
            _generatorTank.Visible = model.HasGeneratorFuelTankControls;
            _machineTrash.Visible = model.InputSlots is { Count: > 0 } || model.OutputSlots is { Count: > 0 };
            if (_personalInventory is not null)
            {
                _personalInventoryPanel.Refresh();
            }

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

            if (_inputRows.Count != 2 || _outputSlots.Count != 1 ||
                !_inputRows[0].Icon.Visible || !_outputSlots[0].Icon.Visible)
            {
                throw new InvalidOperationException("Machine panel inputs or physical output slots are incomplete.");
            }

            if (_inputRows[0].Panel.MouseFilter != Control.MouseFilterEnum.Stop ||
                _outputSlots[0].Panel.MouseFilter != Control.MouseFilterEnum.Stop ||
                !_inputRows[0].Panel.CanStartDrag || !_outputSlots[0].Panel.CanStartDrag)
            {
                throw new InvalidOperationException("Machine input/output slots are not active world-drop drag sources.");
            }

            if (_personalInventory is not null &&
                (!_personalInventoryPanel.Visible || _personalInventoryPanel.SlotControlCount != _personalInventory.SlotCount))
            {
                throw new InvalidOperationException("Machine panel did not render the configured personal inventory.");
            }

            if (!_machineTrash.Visible ||
                _machineTrash.CustomMinimumSize != new Vector2(
                    InventoryUiConfiguration.CompactTrashTargetSize,
                    InventoryUiConfiguration.CompactTrashTargetSize) ||
                _machineTrash.GetChildCount() != 0 ||
                GetChildren().OfType<ConfirmationDialog>().Any())
            {
                throw new InvalidOperationException(
                    "Machine trash must reuse the compact inventory target and delete without a confirmation dialog.");
            }

            var electrolysisRecipe = new MachineRecipeViewModel(
                "electrolysis",
                "Wasserelektrolyse",
                [new MachineMaterialViewModel("Wasser", 2, 4, new Color(0.2f, 0.62f, 0.94f), ProductionItemIds.Water)],
                [
                    new MachineOutputViewModel("Wasserstoff", 2, 0, 200, new Color(0.34f, 0.85f, 0.92f), ProductionItemIds.Hydrogen),
                    new MachineOutputViewModel("Sauerstoff", 1, 8, 200, new Color(0.58f, 0.86f, 1), ProductionItemIds.Oxygen),
                ],
                4);
            UpdateView(smoke with
            {
                Recipes = [electrolysisRecipe],
                SelectedRecipeId = electrolysisRecipe.RecipeId,
                OutputSlots =
                [
                    new MachineInventorySlotViewModel(0, null, string.Empty, 0, 200, BuildingUiTheme.TextMuted),
                    new MachineInventorySlotViewModel(1, ProductionItemIds.Oxygen, "Sauerstoff", 8, 200, new Color(0.58f, 0.86f, 1)),
                ],
            });
            if (_outputSlots.Count != 2 || !_outputSlots.All(slot => slot.Icon.Visible) ||
                _outputSlots[0].Amount.Text != "0 / 200")
            {
                throw new InvalidOperationException("Multi-output recipes do not keep every expected icon and empty output visible.");
            }

            var nuclearRecipe = new MachineRecipeViewModel(
                "nuclear",
                "Nuklearzyklus",
                [],
                [
                    new MachineOutputViewModel("Energiezelle", 1, 1, 1, BuildingUiTheme.Success, ProductionItemIds.NuclearFuelCell),
                    new MachineOutputViewModel("Radioaktiver Abfall", 1, 0, 200, BuildingUiTheme.Warning,
                        ProductionItemIds.RadioactiveWaste, IsWaste: true, IsRadioactive: true),
                ],
                8);
            UpdateView(smoke with { Recipes = [nuclearRecipe], SelectedRecipeId = nuclearRecipe.RecipeId, OutputSlots = [] });
            if (_outputSlots.Count != 2 || !_outputSlots[1].SlotLabel.Text.Contains("RADIOAKTIV", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Radioactive recipe waste is not marked in its dedicated output slot.");
            }

            UpdateView(smoke);

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

            var generatorFillSmoke = smoke with
            {
                HasGeneratorFuelTankControls = true,
                GeneratorFuelSeconds = 120,
                GeneratorFuelCapacitySeconds = 720,
                LoadedFilledFuelContainers = 1,
                LoadedEmptyFuelContainers = 0,
                GeneratorTankTransferSlotOccupied = true,
            };
            UpdateView(generatorFillSmoke);
            if (!_generatorTank.Visible ||
                !_generatorTankReadout.Text.Contains("120 / 720", StringComparison.Ordinal) ||
                _fillGeneratorTank.Disabled || !_drainGeneratorTank.Disabled)
            {
                throw new InvalidOperationException("Fuel generator fill controls are missing or inconsistent.");
            }

            UpdateView(generatorFillSmoke with
            {
                LoadedFilledFuelContainers = 0,
                LoadedEmptyFuelContainers = 1,
            });
            if (!_fillGeneratorTank.Disabled || _drainGeneratorTank.Disabled)
            {
                throw new InvalidOperationException("Fuel generator drain controls are missing or inconsistent.");
            }

            var storageRecipe = new MachineRecipeViewModel(
                "storage_contents",
                "Lagerinhalt (30/30 Slots)",
                Enumerable.Range(1, 30)
                    .Select(index => new MachineMaterialViewModel(
                        $"Material {index:00}",
                        0,
                        index,
                        new Color(0.28f, 0.62f, 0.7f)))
                    .ToArray(),
                [],
                0);
            var storageSmoke = smoke with
            {
                Glyph = MachineGlyph.Storage,
                Recipes = [storageRecipe],
                SelectedRecipeId = storageRecipe.RecipeId,
                RequiredPower = 0,
                AvailablePower = 0,
                OutputSlots = [],
            };
            UpdateView(storageSmoke);
            if (_recipePanel.Visible || _outputGroup.Visible || _progress.Visible || _energy.Visible ||
                _inputRows.Count != 30 || _inputs.Columns < 3)
            {
                throw new InvalidOperationException("Compact storage layout does not expose all material rows without scrolling.");
            }

            if (GetNodeOrNull<ScrollContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll") is not null)
            {
                throw new InvalidOperationException("Machine panel still contains a body scroll container.");
            }

            Vector2[] viewportSizes = [new(1366, 768), new(1920, 1080), new(2560, 1440)];
            foreach (var size in viewportSizes)
            {
                var metrics = CalculateResponsiveMetrics(size);
                var usableWidth = size.X - (2 * (metrics.SafeMargin + metrics.FrameMargin));
                var inventoryWidth = (6 * metrics.InventorySlotSize) + (5 * metrics.InventoryGap) + 20;
                var minimumBodyWidth = (inventoryWidth * 3) + metrics.BodySeparation;
                var usableHeight = size.Y - (2 * (metrics.SafeMargin + metrics.FrameMargin)) - 122;
                var inventoryHeight = (4 * metrics.InventorySlotSize) + (3 * metrics.InventoryGap) + 81;
                if (metrics.InventorySlotSize != InventoryUiConfiguration.InventorySlotSize ||
                    metrics.InventoryGap != InventoryUiConfiguration.InventorySlotGap ||
                    metrics.SafeMargin < 12 || metrics.FrameMargin < 10 ||
                    usableWidth < minimumBodyWidth || usableHeight < inventoryHeight)
                {
                    throw new InvalidOperationException($"Invalid machine panel layout at {size.X}x{size.Y}.");
                }
            }

            if (!Mathf.IsEqualApprox(_personalInventoryPanel.SizeFlagsStretchRatio, 1) ||
                !Mathf.IsEqualApprox(_machineColumn.SizeFlagsStretchRatio, 2))
            {
                throw new InvalidOperationException("Machine panel does not preserve its one-third/two-thirds column ratio.");
            }
            if (_recipeSummary.Visible || _statusDetail.Visible || _footerHint.Visible)
            {
                throw new InvalidOperationException("Redundant machine help/status strips are still visible.");
            }

            _recipeSelector.GetPopup().Show();
            if (!TryCloseTransientUi() || _recipeSelector.GetPopup().Visible)
            {
                throw new InvalidOperationException(
                    "Escape hierarchy did not close the recipe popup before the machine panel.");
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

        GD.Print("MACHINE_PANEL_UI_SMOKE_OK: personal inventory, physical icon output slots, compact generator tank controls, scrollbar-free desktop layout, live production and power state");
    }

    private void RefreshRecipes(MachinePanelViewModel model)
    {
        var signature = string.Join(
            '|',
            model.Recipes.Select(recipe =>
                $"{recipe.RecipeId}:{recipe.DisplayName}:{recipe.DurationSeconds:0.###}:{recipe.IsUnlocked}"));
        if (_renderedRecipeSignature != signature)
        {
            _recipeSelector.Clear();
            for (var index = 0; index < model.Recipes.Count; index++)
            {
                var recipe = model.Recipes[index];
                var timedName = $"{recipe.DisplayName}  ·  {recipe.DurationSeconds:0.#} s";
                _recipeSelector.AddItem(recipe.IsUnlocked ? timedName : $"{timedName}  ·  GESPERRT", index);
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
        if (model.HasGeneratorFuelTankControls)
        {
            var secondsPerContainer = ProductionConfiguration.FuelGeneratorSecondsPerContainer;
            var slotReadout = model.GeneratorTankTransferSlotHasWrongContent
                ? "FALSCHER INHALT"
                : $"{model.LoadedFilledFuelContainers} VOLL / {model.LoadedEmptyFuelContainers} LEER";
            _generatorTankReadout.Text =
                $"TANK  {model.GeneratorFuelSeconds:0.#} / {model.GeneratorFuelCapacitySeconds:0.#} s" +
                $"  ·  TANK-SLOT 01: {slotReadout}";
            _fillGeneratorTank.Disabled = !model.GeneratorTankTransferSlotOccupied ||
                                           (!model.GeneratorTankTransferSlotHasWrongContent &&
                                            model.LoadedFilledFuelContainers <= 0) ||
                                           model.GeneratorFuelSeconds + secondsPerContainer >
                                           model.GeneratorFuelCapacitySeconds + 0.001f;
            _drainGeneratorTank.Disabled = !model.GeneratorTankTransferSlotOccupied ||
                                            (!model.GeneratorTankTransferSlotHasWrongContent &&
                                             model.LoadedEmptyFuelContainers <= 0) ||
                                            model.GeneratorFuelSeconds + 0.001f < secondsPerContainer;
        }
    }

    private void RefreshFlow(MachinePanelViewModel model)
    {
        var recipe = GetSelectedRecipe(model);
        var outputSlots = ResolveOutputSlots(model, recipe);
        var inputOnly = recipe is not null && outputSlots.Count == 0;
        _flow.Visible = model.Recipes.Count > 0;
        _inputGroup.Visible = model.Recipes.Count > 0;
        _flowArrow.Visible = model.Recipes.Count > 0 && !inputOnly;
        _outputGroup.Visible = model.Recipes.Count > 0 && !inputOnly;
        _inputs.Columns = CalculateMaterialColumns(recipe?.Inputs.Count ?? 0, inputOnly);
        _outputs.Columns = CalculateOutputColumns(outputSlots.Count);
        var flowSignature = recipe is null
            ? "none"
            : $"{recipe.RecipeId}|{string.Join(',', recipe.Inputs.Select(input => input.DisplayName))}|slots:{outputSlots.Count}";
        if (_renderedFlowSignature != flowSignature)
        {
            RebuildMaterialRows(_inputs, _inputRows, recipe?.Inputs.Count ?? 0, input: true);
            RebuildOutputSlots(outputSlots.Count);
            _renderedFlowSignature = flowSignature;
        }

        if (recipe is null)
        {
            ShowEmptyRows(_inputs, "KEIN REZEPT AUSGEWÄHLT");
            return;
        }

        RemoveEmptyRows(_inputs);
        var claimedInputSlots = new HashSet<int>();
        for (var index = 0; index < recipe.Inputs.Count; index++)
        {
            var input = recipe.Inputs[index];
            var physicalInput = model.InputSlots?
                .FirstOrDefault(slot => !slot.IsEmpty &&
                                        slot.ItemId == input.ItemId &&
                                        claimedInputSlots.Add(slot.SlotIndex));
            physicalInput ??= model.InputSlots?
                .FirstOrDefault(slot => slot.IsEmpty && claimedInputSlots.Add(slot.SlotIndex));
            ItemPresentationViewModel? inputItem = input.ItemId is { } inputItemId
                ? _itemPresentations.GetOrCreateFallback(inputItemId, physicalInput?.MaximumAmount ?? 200)
                : null;
            UpdateMaterialRow(
                _inputRows[index],
                inputItem,
                input.DisplayName,
                $"{input.AvailableAmount} / {input.RequiredAmount}",
                input.Accent,
                input.IsAvailable ? BuildingUiTheme.Success : BuildingUiTheme.Failure);
            if (physicalInput?.ItemId is { } physicalItemId)
            {
                inputItem = _itemPresentations.GetOrCreateFallback(physicalItemId, physicalInput.MaximumAmount);
            }
            _inputRows[index].Panel.ConfigureDrag(
                $"machine_input:{model.MachineId}",
                physicalInput?.SlotIndex ?? -1,
                inputItem,
                physicalInput?.Amount ?? 0);
            _inputRows[index].Panel.ConfigureTransfer(_previewMachineTransfer, TransferMachineSlot);
        }

        for (var index = 0; index < outputSlots.Count; index++)
        {
            UpdateOutputSlot(_outputSlots[index], outputSlots[index]);
        }
    }

    private static IReadOnlyList<MachineInventorySlotViewModel> ResolveOutputSlots(
        MachinePanelViewModel model,
        MachineRecipeViewModel? recipe)
    {
        if (recipe is null)
        {
            return [];
        }

        var physicalSlots = model.OutputSlots ?? [];
        var claimedSlots = new HashSet<int>();
        return recipe.Outputs.Select((output, index) =>
        {
            var physical = physicalSlots.FirstOrDefault(slot =>
                !slot.IsEmpty && slot.ItemId == output.ItemId && claimedSlots.Add(slot.SlotIndex));
            physical ??= physicalSlots.FirstOrDefault(slot =>
                slot.IsEmpty && slot.IsWaste == output.IsWaste && claimedSlots.Add(slot.SlotIndex));
            return new MachineInventorySlotViewModel(
                physical?.SlotIndex ?? index,
                output.ItemId,
                output.DisplayName,
                output.StoredAmount,
                Math.Max(1, output.Capacity),
                output.Accent,
                output.IsWaste,
                output.IsRadioactive);
        }).ToArray();
    }

    private int CalculateMaterialColumns(int itemCount, bool fullWidth)
    {
        if (itemCount <= 3)
        {
            return 1;
        }

        if (!fullWidth)
        {
            return 2;
        }

        return GetViewport().GetVisibleRect().Size.X >= 1_100 ? 4 : 3;
    }

    private static int CalculateOutputColumns(int slotCount) => slotCount switch
    {
        <= 2 => 1,
        _ => 2,
    };

    private static void RebuildMaterialRows(
        GridContainer container,
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
            var panel = new MachineOutputDropControl
            {
                CustomMinimumSize = new Vector2(0, 44),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            panel.AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(
                    BuildingUiTheme.CardBackground,
                    input ? new Color(0.1f, 0.43f, 0.55f, 0.8f) : new Color(0.12f, 0.52f, 0.42f, 0.8f)));
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            var iconFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(36, 36),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            iconFrame.AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(
                    new Color(0.003f, 0.025f, 0.035f, 0.98f),
                    new Color(0.08f, 0.35f, 0.42f, 0.9f)));
            var icon = new ResourceIconControl { MouseFilter = Control.MouseFilterEnum.Ignore };
            iconFrame.AddChild(icon);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 3;
            icon.OffsetTop = 3;
            icon.OffsetRight = -3;
            icon.OffsetBottom = -3;
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
            row.AddChild(iconFrame);
            row.AddChild(name);
            row.AddChild(amount);
            panel.AddChild(row);
            container.AddChild(panel);
            rows.Add(new MaterialRowWidgets(panel, icon, name, amount));
        }
    }

    private static void UpdateMaterialRow(
        MaterialRowWidgets row,
        ItemPresentationViewModel? item,
        string name,
        string amount,
        Color accent,
        Color amountColor)
    {
        row.Icon.Configure(item);
        row.Name.Text = name;
        row.Name.AddThemeColorOverride("font_color", BuildingUiTheme.Text);
        row.Amount.Text = amount;
        row.Amount.AddThemeColorOverride("font_color", amountColor);
        row.Panel.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(
                new Color(0.003f, 0.024f, 0.033f, 0.96f),
                amountColor == BuildingUiTheme.Failure
                    ? BuildingUiTheme.Failure
                    : accent.Lightened(0.08f)));
        // Item names use the shared 0.2-second hover presenter. A native tooltip here would
        // race it with a different delay and produce overlapping labels.
        row.Panel.TooltipText = string.Empty;
    }

    private void RebuildOutputSlots(int count)
    {
        foreach (var child in _outputs.GetChildren())
        {
            _outputs.RemoveChild(child);
            child.QueueFree();
        }

        _outputSlots.Clear();
        for (var index = 0; index < count; index++)
        {
            var panel = new MachineOutputDropControl
            {
                CustomMinimumSize = new Vector2(118, 62),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", 6);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_right", 7);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 7);
            var iconFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(48, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            iconFrame.AddThemeStyleboxOverride(
                "panel",
                BuildingUiTheme.CreatePanelStyle(
                    new Color(0.003f, 0.025f, 0.035f, 0.98f),
                    new Color(0.08f, 0.35f, 0.42f, 0.9f)));
            var icon = new ResourceIconControl
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            iconFrame.AddChild(icon);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 4;
            icon.OffsetTop = 4;
            icon.OffsetRight = -4;
            icon.OffsetBottom = -4;

            var labels = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            labels.AddThemeConstantOverride("separation", 1);
            var slotLabel = new Label
            {
                Text = $"SLOT {index + 1:00}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            slotLabel.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
            slotLabel.AddThemeFontSizeOverride("font_size", 8);
            var name = new Label
            {
                Text = "FREI",
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            name.AddThemeColorOverride("font_color", BuildingUiTheme.Text);
            name.AddThemeFontSizeOverride("font_size", 10);
            var amount = new Label
            {
                Text = "0 / 200",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            amount.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
            amount.AddThemeFontSizeOverride("font_size", 12);
            labels.AddChild(slotLabel);
            labels.AddChild(name);
            labels.AddChild(amount);
            row.AddChild(iconFrame);
            row.AddChild(labels);
            margin.AddChild(row);
            panel.AddChild(margin);
            _outputs.AddChild(panel);
            _outputSlots.Add(new OutputSlotWidgets(panel, icon, slotLabel, name, amount));
        }
    }

    private void UpdateOutputSlot(OutputSlotWidgets widgets, MachineInventorySlotViewModel slot)
    {
        var maximum = Math.Max(1, slot.MaximumAmount);
        ItemPresentationViewModel? item = null;
        if (slot.ItemId is { } itemId)
        {
            item = _itemPresentations.GetOrCreateFallback(itemId, maximum);
        }

        widgets.Icon.Configure(item);
        widgets.Panel.ConfigureDrag(
            $"machine_output:{_model?.MachineId ?? string.Empty}",
            slot.SlotIndex,
            item,
            slot.Amount);
        widgets.Panel.ConfigureTransfer(_previewMachineTransfer, TransferMachineSlot);
        var hasDefinedOutput = slot.ItemId is not null;
        widgets.SlotLabel.Visible = hasDefinedOutput;
        widgets.Name.Visible = hasDefinedOutput;
        widgets.Amount.Visible = hasDefinedOutput;
        widgets.SlotLabel.Text = slot.IsRadioactive
            ? "⚠ RADIOAKTIV"
            : slot.IsWaste
                ? $"ABFALL {slot.SlotIndex + 1:00}"
            : $"SLOT {slot.SlotIndex + 1:00}";
        widgets.Name.Text = hasDefinedOutput ? slot.DisplayName.ToUpperInvariant() : string.Empty;
        widgets.Amount.Text = hasDefinedOutput ? $"{slot.Amount} / {maximum}" : string.Empty;
        widgets.Amount.AddThemeColorOverride(
            "font_color",
            slot.IsFull ? BuildingUiTheme.Warning : slot.IsEmpty ? BuildingUiTheme.TextMuted : BuildingUiTheme.Success);
        var border = slot.IsRadioactive
            ? new Color(1f, 0.34f, 0.2f, 1f)
            : slot.IsWaste
                ? new Color(0.86f, 0.62f, 0.18f, 0.95f)
            : slot.IsFull
                ? BuildingUiTheme.Warning
                : slot.IsEmpty
                    ? new Color(0.07f, 0.27f, 0.33f, 0.8f)
                    : slot.Accent.Lightened(0.12f);
        widgets.Panel.AddThemeStyleboxOverride(
            "panel",
            BuildingUiTheme.CreatePanelStyle(
                slot.IsEmpty ? new Color(0.004f, 0.022f, 0.031f, 0.94f) : BuildingUiTheme.CardBackground,
                border));
        widgets.Panel.TooltipText = string.Empty;
    }

    private static void ShowEmptyRows(GridContainer container, string text)
    {
        if (container.GetNodeOrNull<Label>("Empty") is not null)
        {
            return;
        }

        var label = new Label
        {
            Name = "Empty",
            Text = text,
            CustomMinimumSize = new Vector2(0, 38),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", BuildingUiTheme.TextMuted);
        label.AddThemeFontSizeOverride("font_size", 11);
        container.AddChild(label);
    }

    private static void RemoveEmptyRows(GridContainer container)
    {
        var empty = container.GetNodeOrNull<Label>("Empty");
        if (empty is null)
        {
            return;
        }

        container.RemoveChild(empty);
        empty.QueueFree();
    }

    private void ConfigurePersonalInventoryPanel()
    {
        if (_personalInventory is null)
        {
            _personalInventoryPanel.Visible = false;
            return;
        }

        _personalInventoryPanel.Configure(
            PersonalInventoryId,
            "PERSÖNLICHES INVENTAR",
            6,
            _personalInventory,
            _itemPresentations,
            PreviewPersonalInventoryTransfer,
            TransferWithinPersonalInventory,
            SelectPersonalInventorySlot);
        _personalInventoryPanel.SetSelectedSlot(_selectedPersonalSlotIndex);
        _personalInventoryPanel.Visible = true;
    }

    private InventoryTransferResult PreviewPersonalInventoryTransfer(
        InventorySlotAddress source,
        InventorySlotAddress target)
    {
        if (_personalInventory is null)
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        if (source.InventoryId != PersonalInventoryId || target.InventoryId != PersonalInventoryId)
        {
            return _previewMachineTransfer?.Invoke(source, target) ??
                   InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        return InventoryTransfer.Preview(
            _personalInventory,
            source.SlotIndex,
            _personalInventory,
            target.SlotIndex);
    }

    private bool TransferWithinPersonalInventory(
        InventorySlotAddress source,
        InventorySlotAddress target)
    {
        if (_personalInventory is null)
        {
            return false;
        }

        if (source.InventoryId != PersonalInventoryId || target.InventoryId != PersonalInventoryId)
        {
            return TransferMachineSlot(source, target);
        }

        var result = InventoryTransfer.Transfer(
            _personalInventory,
            source.SlotIndex,
            _personalInventory,
            target.SlotIndex);
        _personalInventoryPanel.Refresh();
        _personalInventoryPanel.PlayTransferFeedback(source.SlotIndex, result.Succeeded);
        if (result.Succeeded)
        {
            PersonalInventoryChanged?.Invoke();
        }

        return result.Succeeded;
    }

    private bool TransferMachineSlot(InventorySlotAddress source, InventorySlotAddress target)
    {
        var succeeded = _machineTransferRequested?.Invoke(source, target) ?? false;
        if (!succeeded)
        {
            return false;
        }

        _personalInventoryPanel.Refresh();
        PersonalInventoryChanged?.Invoke();
        return true;
    }

    private void SelectPersonalInventorySlot(InventorySlotAddress address)
    {
        if (address.InventoryId != PersonalInventoryId)
        {
            return;
        }

        _selectedPersonalSlotIndex = address.SlotIndex;
        _personalInventoryPanel.SetSelectedSlot(address.SlotIndex);
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
            RebuildOutputSlots(0);
            ShowEmptyRows(_inputs, "KEIN REZEPT AUSGEWÄHLT");
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

    private void HandleGeneratorTankFillPressed()
    {
        if (_model?.HasGeneratorFuelTankControls == true)
        {
            GeneratorTankFillRequested?.Invoke();
        }
    }

    private void HandleGeneratorTankDrainPressed()
    {
        if (_model?.HasGeneratorFuelTankControls == true)
        {
            GeneratorTankDrainRequested?.Invoke();
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
        _body.Vertical = false;
        _footer.Vertical = false;
        _footerHint.Visible = false;
        _body.AddThemeConstantOverride("separation", metrics.BodySeparation);
        _recipePanel.CustomMinimumSize = Vector2.Zero;
        _recipePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (_personalInventory is not null)
        {
            _personalInventoryPanel.SetLayoutMetrics(
                6,
                metrics.InventorySlotSize,
                metrics.InventoryGap,
                compact: true);
            _personalInventoryPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        }

        if (_model is not null)
        {
            RefreshFlow(_model);
        }
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize)
    {
        var width = Mathf.Max(640, viewportSize.X);
        var height = Mathf.Max(480, viewportSize.Y);
        var safeMargin = width < 1_600
            ? 16
            : Mathf.RoundToInt(Mathf.Clamp(Mathf.Min(width * 0.03f, height * 0.04f), 20, 42));
        var frameMargin = width < 1_600 ? 10 : 14;
        var recipeWidth = width < 900 ? 140 : width < 1_200 ? 180 : width < 1_500 ? 220 : 260;
        var bodySeparation = width < 900 ? 8 : 12;
        var showFooterHint = width >= 1_150;
        const float inventorySlotSize = InventoryUiConfiguration.InventorySlotSize;
        const int inventoryGap = InventoryUiConfiguration.InventorySlotGap;
        return new ResponsiveMetrics(
            safeMargin,
            frameMargin,
            recipeWidth,
            bodySeparation,
            showFooterHint,
            inventorySlotSize,
            inventoryGap);
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
                new MachineMaterialViewModel("Zerkleinertes Eisenerz", 2, 7, new Color(0.46f, 0.51f, 0.53f), ProductionItemIds.CrushedIronOre),
                new MachineMaterialViewModel("Kohlenstoff", 1, 0, new Color(0.22f, 0.23f, 0.24f), ProductionItemIds.Carbon),
            ],
            [new MachineOutputViewModel(
                "Eisenbarren",
                1,
                12,
                200,
                new Color(0.65f, 0.69f, 0.71f),
                ProductionItemIds.IronIngot)],
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
            18,
            [
                new MachineInventorySlotViewModel(
                    0,
                    ProductionItemIds.IronIngot,
                    "Eisenbarren",
                    12,
                    200,
                    new Color(0.65f, 0.69f, 0.71f)),
                new MachineInventorySlotViewModel(1, null, string.Empty, 0, 200, BuildingUiTheme.TextMuted),
                new MachineInventorySlotViewModel(2, null, string.Empty, 0, 200, BuildingUiTheme.TextMuted),
                new MachineInventorySlotViewModel(3, null, string.Empty, 0, 200, BuildingUiTheme.TextMuted),
            ],
            [
                new MachineInventorySlotViewModel(
                    0,
                    ProductionItemIds.CrushedIronOre,
                    "Zerkleinertes Eisenerz",
                    7,
                    200,
                    new Color(0.46f, 0.51f, 0.53f)),
            ]);
    }

    private readonly record struct MaterialRowWidgets(
        MachineOutputDropControl Panel,
        ResourceIconControl Icon,
        Label Name,
        Label Amount);

    private readonly record struct OutputSlotWidgets(
        MachineOutputDropControl Panel,
        ResourceIconControl Icon,
        Label SlotLabel,
        Label Name,
        Label Amount);

    private readonly record struct StatusPresentation(string Label, string Description, Color Color);

    private readonly record struct ResponsiveMetrics(
        int SafeMargin,
        int FrameMargin,
        int RecipeWidth,
        int BodySeparation,
        bool ShowFooterHint,
        float InventorySlotSize,
        int InventoryGap);
}

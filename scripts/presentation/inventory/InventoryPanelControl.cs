using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryPanelControl : PanelContainer
{
    private const string SlotScenePath = "res://scenes/ui/inventory/InventorySlot.tscn";

    private readonly List<InventorySlotControl> _slotControls = [];
    private MarginContainer _margin = null!;
    private VBoxContainer _layout = null!;
    private HBoxContainer _header = null!;
    private Label _title = null!;
    private Label _summary = null!;
    private HSeparator _rule = null!;
    private PanelContainer _fuelModule = null!;
    private Label _fuelAmount = null!;
    private ProgressBar _fuelProgress = null!;
    private Button _refuelButton = null!;
    private Button _drainButton = null!;
    private InventoryTrashDropTarget _trashTarget = null!;
    private CenterContainer _gridViewport = null!;
    private GridContainer _grid = null!;
    private SlotInventory? _inventory;
    private ItemPresentationCatalog? _items;
    private Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? _previewTransfer;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _transferRequested;
    private Action<InventorySlotAddress>? _selectionRequested;
    private string _inventoryId = string.Empty;
    private int? _selectedSlotIndex;
    private double _currentFuel;
    private double _fuelCapacity;
    private bool _fuelStateConfigured;
    private ShipFuelType _fuelType = ShipFuelConfiguration.NewGameFuelType;
    private double _remainingBoostSeconds;
    private double _boostSpeedMultiplier = 1;
    private bool _toolColumnPresentation;

    public SlotInventory Inventory => _inventory ??
        throw new InvalidOperationException("The inventory panel has not been configured.");

    public int SlotControlCount => _slotControls.Count;

    public bool FuelControlsVisible => Visible && _fuelModule.Visible;

    public bool DeleteTargetVisible => Visible && _trashTarget.Visible;

    public bool UsesCompactToolColumn => _toolColumnPresentation;

    public bool HeaderVisible => _header.Visible || _rule.Visible;

    public bool HasUnobstructedDragSurfaces =>
        _slotControls.Count > 0 && _slotControls.All(slot => slot.HasUnobstructedDragSurface);

    public bool HasLegacySlotMarkers => _slotControls.Any(slot => slot.HasLegacyItemMarkers);

    public float PresentedSlotSize => _slotControls.Count == 0 ? 0 : _slotControls[0].PresentedSlotSize;

    public event Action? RefuelRequested;
    public event Action? DrainFuelRequested;
    public event Action<InventorySlotAddress>? DeleteRequested;
    public event Action<InventoryItemContextRequest, Vector2>? ContextRequested;

    public override void _Ready()
    {
        _margin = GetNode<MarginContainer>("Margin");
        _layout = GetNode<VBoxContainer>("Margin/Layout");
        _header = GetNode<HBoxContainer>("Margin/Layout/Header");
        _title = GetNode<Label>("Margin/Layout/Header/Title");
        _summary = GetNode<Label>("Margin/Layout/Header/Summary");
        _rule = GetNode<HSeparator>("Margin/Layout/Rule");
        _fuelModule = GetNode<PanelContainer>("Margin/Layout/FuelModule");
        _fuelAmount = GetNode<Label>("Margin/Layout/FuelModule/Margin/Layout/Readout/FuelAmount");
        _fuelProgress = GetNode<ProgressBar>("Margin/Layout/FuelModule/Margin/Layout/Readout/FuelProgress");
        _refuelButton = GetNode<Button>("Margin/Layout/FuelModule/Margin/Layout/TankButtons/RefuelButton");
        _drainButton = GetNode<Button>("Margin/Layout/FuelModule/Margin/Layout/TankButtons/DrainButton");
        _trashTarget = GetNode<InventoryTrashDropTarget>("Margin/Layout/TrashTarget");
        _gridViewport = GetNode<CenterContainer>("Margin/Layout/GridViewport");
        _grid = GetNode<GridContainer>("Margin/Layout/GridViewport/Grid");
        _refuelButton.Pressed += OnRefuelPressed;
        _drainButton.Pressed += OnDrainPressed;
        _trashTarget.DeleteRequested += HandleDeleteRequested;
        RefreshFuelStyle();
        RefreshPanelStyle();
    }

    public override void _ExitTree()
    {
        if (_refuelButton is not null)
        {
            _refuelButton.Pressed -= OnRefuelPressed;
            _drainButton.Pressed -= OnDrainPressed;
            _trashTarget.DeleteRequested -= HandleDeleteRequested;
        }
    }

    public void Configure(
        string inventoryId,
        string title,
        int columns,
        SlotInventory inventory,
        ItemPresentationCatalog items,
        Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult> previewTransfer,
        Func<InventorySlotAddress, InventorySlotAddress, bool> transferRequested,
        Action<InventorySlotAddress> selectionRequested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryId);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(previewTransfer);
        ArgumentNullException.ThrowIfNull(transferRequested);
        ArgumentNullException.ThrowIfNull(selectionRequested);

        _inventoryId = inventoryId;
        _toolColumnPresentation = inventoryId == InventoryMenuController.ToolInventoryId;
        _inventory = inventory;
        _items = items;
        _previewTransfer = previewTransfer;
        _transferRequested = transferRequested;
        _selectionRequested = selectionRequested;
        _title.Text = title;
        _header.Visible = !_toolColumnPresentation;
        _rule.Visible = !_toolColumnPresentation;
        _fuelModule.Visible = !_toolColumnPresentation &&
                              inventoryId == InventoryMenuController.ShipInventoryId;
        _trashTarget.Visible = false;
        UpdateFuelStateVisuals();
        BuildSlots();
        if (_toolColumnPresentation)
        {
            SetToolColumnLayoutMetrics(
                InventoryUiConfiguration.InventorySlotSize,
                InventoryUiConfiguration.InventorySlotGap);
        }
        else
        {
            SetLayoutMetrics(
                columns,
                InventoryUiConfiguration.InventorySlotSize,
                InventoryUiConfiguration.InventorySlotGap,
                compact: false);
        }
        RefreshPanelStyle();
        Refresh();
    }

    public void Refresh()
    {
        if (_inventory is null || _items is null)
        {
            return;
        }

        for (var index = 0; index < _slotControls.Count; index++)
        {
            var slot = _inventory.GetSlot(index);
            _slotControls[index].Refresh(slot, FindItem(slot));
            _slotControls[index].SetSelected(index == _selectedSlotIndex);
        }

        _summary.Text = $"{_inventory.UsedSlotCount:00} / {_inventory.SlotCount:00} BELEGT";
    }

    public void SetFuelTankState(double currentFuel, double capacity) => SetFuelTankState(
        currentFuel,
        capacity,
        _fuelType,
        currentFuel / ShipFuelConfiguration.ConsumptionPerBoostSecond,
        _boostSpeedMultiplier);

    public void SetFuelTankState(
        double currentFuel,
        double capacity,
        ShipFuelType fuelType,
        double remainingBoostSeconds,
        double boostSpeedMultiplier)
    {
        if (!double.IsFinite(currentFuel) || !double.IsFinite(capacity) || capacity <= 0 ||
            !Enum.IsDefined(fuelType) || !double.IsFinite(remainingBoostSeconds) ||
            remainingBoostSeconds < 0 || !double.IsFinite(boostSpeedMultiplier) ||
            boostSpeedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Fuel tank values must be finite and capacity must be positive.");
        }

        _currentFuel = Math.Clamp(currentFuel, 0, capacity);
        _fuelCapacity = capacity;
        _fuelType = fuelType;
        _remainingBoostSeconds = remainingBoostSeconds;
        _boostSpeedMultiplier = boostSpeedMultiplier;
        _fuelStateConfigured = true;
        if (IsNodeReady())
        {
            UpdateFuelStateVisuals();
        }
    }

    public void SetLayoutMetrics(int columns, float slotSize, int gap, bool compact)
    {
        if (_inventory is null)
        {
            return;
        }

        columns = Math.Clamp(columns, 1, _inventory.SlotCount);
        if (_toolColumnPresentation)
        {
            SetToolColumnLayoutMetrics(slotSize, gap);
            return;
        }

        // Slot targets and their rhythm remain constant across supported resolutions.
        // Responsive compaction is restricted to panel chrome and outer margins.
        slotSize = Mathf.Clamp(slotSize, InventoryUiConfiguration.InventorySlotSize, 84);
        gap = Math.Clamp(gap, InventoryUiConfiguration.InventorySlotGap, 10);
        var rows = Mathf.CeilToInt(_inventory.SlotCount / (float)columns);
        var horizontalMargin = compact
            ? InventoryUiConfiguration.InventoryPanelMarginCompact
            : InventoryUiConfiguration.InventoryPanelMarginRegular;
        var verticalMargin = compact ? 7 : 10;
        var gridWidth = (columns * slotSize) + ((columns - 1) * gap);
        var gridHeight = (rows * slotSize) + ((rows - 1) * gap);
        var panelChromeHeight = compact ? 54 : 64;

        _grid.Columns = columns;
        _grid.AddThemeConstantOverride("h_separation", gap);
        _grid.AddThemeConstantOverride("v_separation", gap);
        _gridViewport.CustomMinimumSize = new Vector2(gridWidth, gridHeight);
        _margin.AddThemeConstantOverride("margin_left", horizontalMargin);
        _margin.AddThemeConstantOverride("margin_right", horizontalMargin);
        _margin.AddThemeConstantOverride("margin_top", verticalMargin);
        _margin.AddThemeConstantOverride("margin_bottom", verticalMargin);
        _title.AddThemeFontSizeOverride("font_size", compact ? 15 : 17);
        CustomMinimumSize = new Vector2(
            gridWidth + (horizontalMargin * 2),
            gridHeight + panelChromeHeight + (verticalMargin * 2));

        foreach (var slotControl in _slotControls)
        {
            slotControl.SetSlotSize(slotSize, compact);
        }
    }

    /// <summary>
    /// Presents the four tool-only slots as a slim companion column aligned to the
    /// four astronaut rows, without a second card, title, summary or trash target.
    /// </summary>
    public void SetToolColumnLayoutMetrics(float inventorySlotSize, int inventoryGap)
    {
        if (_inventory is null || !_toolColumnPresentation)
        {
            return;
        }

        var toolSize = InventoryUiConfiguration.ToolSlotSize;
        var rowCount = Math.Max(1, _inventory.SlotCount);
        var verticalMargin = Mathf.Max(0, Mathf.RoundToInt((inventorySlotSize - toolSize) * 0.5f));
        var toolGap = rowCount <= 1
            ? 0
            : Mathf.RoundToInt(inventorySlotSize + inventoryGap - toolSize);
        var toolGridHeight = (rowCount * toolSize) + ((rowCount - 1) * toolGap);

        _grid.Columns = 1;
        _grid.AddThemeConstantOverride("h_separation", InventoryUiConfiguration.InventorySlotGap);
        _grid.AddThemeConstantOverride("v_separation", toolGap);
        _gridViewport.CustomMinimumSize = new Vector2(toolSize, toolGridHeight);
        _margin.AddThemeConstantOverride("margin_left", 2);
        _margin.AddThemeConstantOverride("margin_right", 2);
        _margin.AddThemeConstantOverride("margin_top", verticalMargin);
        _margin.AddThemeConstantOverride("margin_bottom", verticalMargin);
        _layout.AddThemeConstantOverride("separation", 0);
        CustomMinimumSize = new Vector2(toolSize + 4, toolGridHeight + (2 * verticalMargin));
        foreach (var slotControl in _slotControls)
        {
            slotControl.SetSlotSize(toolSize, compact: true);
        }
    }

    public void SetSelectedSlot(int? slotIndex)
    {
        _selectedSlotIndex = slotIndex;
        for (var index = 0; index < _slotControls.Count; index++)
        {
            _slotControls[index].SetSelected(index == slotIndex);
        }
    }

    public void SetShortcutLabels(IReadOnlyList<string>? labels)
    {
        for (var index = 0; index < _slotControls.Count; index++)
        {
            _slotControls[index].SetShortcutLabel(
                labels is not null && index < labels.Count ? labels[index] : null);
        }
    }

    public void SetDeleteTargetVisible(bool visible) => _trashTarget.Visible = visible;

    public void SetHeaderVisible(bool visible)
    {
        var show = visible && !_toolColumnPresentation;
        _header.Visible = show;
        _rule.Visible = show;
    }

    public void PlayTransferFeedback(int slotIndex, bool succeeded)
    {
        if (slotIndex < 0 || slotIndex >= _slotControls.Count)
        {
            return;
        }

        _slotControls[slotIndex].PlayTransferFeedback(succeeded);
    }

    private void BuildSlots()
    {
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _slotControls.Clear();
        var slotScene = GD.Load<PackedScene>(SlotScenePath) ??
            throw new InvalidOperationException($"Inventory slot scene is missing at {SlotScenePath}.");
        foreach (var slot in Inventory.Slots)
        {
            var control = slotScene.Instantiate<InventorySlotControl>();
            _grid.AddChild(control);
            control.Configure(
                _inventoryId,
                slot,
                FindItem(slot),
                _previewTransfer ?? throw new InvalidOperationException("Transfer preview handler is missing."),
                _transferRequested ?? throw new InvalidOperationException("Transfer handler is missing."),
                _selectionRequested ?? throw new InvalidOperationException("Selection handler is missing."),
                HandleContextRequested);
            _slotControls.Add(control);
        }
    }

    private ItemPresentationViewModel? FindItem(InventorySlot slot)
    {
        if (slot.ItemId is not { } itemId || _items is null)
        {
            return null;
        }

        return _items.GetOrCreateFallback(itemId, slot.MaximumAmount);
    }

    private void OnRefuelPressed()
    {
        if (_inventoryId != InventoryMenuController.ShipInventoryId || !_fuelStateConfigured ||
            _currentFuel >= _fuelCapacity)
        {
            return;
        }

        RefuelRequested?.Invoke();
    }

    private void OnDrainPressed()
    {
        if (_inventoryId == InventoryMenuController.ShipInventoryId && _fuelStateConfigured)
        {
            DrainFuelRequested?.Invoke();
        }
    }

    private void HandleDeleteRequested(InventorySlotAddress address) => DeleteRequested?.Invoke(address);

    private void HandleContextRequested(InventoryItemContextRequest request, Vector2 position) =>
        ContextRequested?.Invoke(request, position);

    private void UpdateFuelStateVisuals()
    {
        if (_fuelModule is null)
        {
            return;
        }

        if (!_fuelStateConfigured)
        {
            _fuelAmount.Text = "TANKDATEN NICHT VERFÜGBAR";
            _fuelProgress.MinValue = 0;
            _fuelProgress.MaxValue = 1;
            _fuelProgress.Value = 0;
            _refuelButton.Disabled = true;
            _drainButton.Disabled = true;
            return;
        }

        _fuelProgress.MinValue = 0;
        _fuelProgress.MaxValue = _fuelCapacity;
        _fuelProgress.Value = _currentFuel;
        var percent = Mathf.RoundToInt((float)(_currentFuel / _fuelCapacity * 100));
        _fuelAmount.Text =
            $"{ShipFuelConfiguration.GetDisplayName(_fuelType).ToUpperInvariant()}  ·  " +
            $"{_currentFuel:0} / {_fuelCapacity:0}  ·  {percent}%\n" +
            $"BOOST RESTZEIT {_remainingBoostSeconds:0} s  ·  GESCHWINDIGKEIT x{_boostSpeedMultiplier:0.00}";
        _refuelButton.Disabled = _currentFuel >= _fuelCapacity - 0.000_001;
        _drainButton.Disabled = _currentFuel + 0.000_001 < ShipFuelConfiguration.FuelPerContainer;
        _refuelButton.TooltipText = _refuelButton.Disabled
            ? "Der Raumschifftank ist vollständig gefüllt."
            : "Ausgewählten gefüllten Behälter aus Inventar oder Lager verwenden.";
        _drainButton.TooltipText = _drainButton.Disabled
            ? "Tank leer oder Restmenge kleiner als ein Behälter."
            : "Ausgewählten leeren Behälter mit Tankinhalt füllen.";
    }

    private void RefreshFuelStyle()
    {
        _fuelModule.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.006f, 0.042f, 0.055f, 0.94f),
            BorderColor = new Color(0.08f, 0.44f, 0.57f, 0.82f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        });
        _fuelProgress.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.002f, 0.014f, 0.021f, 0.95f),
            BorderColor = new Color(0.07f, 0.28f, 0.34f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        });
        _fuelProgress.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.72f, 0.9f, 0.92f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        });
        _refuelButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(
            new Color(0.018f, 0.1f, 0.13f, 0.96f),
            new Color(0.09f, 0.58f, 0.73f, 0.9f)));
        _refuelButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(
            new Color(0.025f, 0.16f, 0.2f, 0.98f),
            new Color(0.18f, 0.82f, 1, 1)));
        _refuelButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(
            new Color(0.02f, 0.2f, 0.24f, 1),
            new Color(0.28f, 0.9f, 1, 1)));
        _refuelButton.AddThemeStyleboxOverride("disabled", CreateButtonStyle(
            new Color(0.018f, 0.045f, 0.054f, 0.82f),
            new Color(0.15f, 0.27f, 0.3f, 0.65f)));
        _drainButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(
            new Color(0.018f, 0.1f, 0.13f, 0.96f),
            new Color(0.09f, 0.58f, 0.73f, 0.9f)));
        _drainButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(
            new Color(0.025f, 0.16f, 0.2f, 0.98f),
            new Color(0.18f, 0.82f, 1, 1)));
        _drainButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(
            new Color(0.02f, 0.2f, 0.24f, 1),
            new Color(0.28f, 0.9f, 1, 1)));
        _drainButton.AddThemeStyleboxOverride("disabled", CreateButtonStyle(
            new Color(0.018f, 0.045f, 0.054f, 0.82f),
            new Color(0.15f, 0.27f, 0.3f, 0.65f)));
    }

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 5,
        ContentMarginBottom = 5,
    };

    private void RefreshPanelStyle()
    {
        if (_toolColumnPresentation)
        {
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            return;
        }

        var shipPanel = _inventoryId == InventoryMenuController.ShipInventoryId;
        var accent = shipPanel
            ? new Color(0.12f, 0.58f, 0.74f, 0.88f)
            : new Color(0.08f, 0.68f, 0.86f, 0.9f);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.004f, 0.021f, 0.032f, 0.975f),
            BorderColor = accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ShadowColor = new Color(0, 0, 0, 0.34f),
            ShadowSize = 5,
        });
    }
}

using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryMenuController : CanvasLayer
{
    public const string AstronautInventoryId = "astronaut";
    public const string ShipInventoryId = "ship";

    private readonly record struct ResponsiveMetrics(
        bool Stacked,
        int ShipColumns,
        float SlotSize,
        int Gap,
        int SafeMarginX,
        int SafeMarginY,
        int FrameMarginX,
        int FrameMarginY,
        int PanelSeparation,
        bool Compact,
        float AvailableBodyWidth,
        float AvailableBodyHeight,
        float RequiredBodyWidth,
        float RequiredBodyHeight);

    private Control _overlay = null!;
    private MarginContainer _safeArea = null!;
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private ScrollContainer _bodyScroll = null!;
    private BoxContainer _header = null!;
    private BoxContainer _footer = null!;
    private InventoryPanelControl _astronautPanel = null!;
    private InventoryPanelControl _shipPanel = null!;
    private BoxContainer _inventoryColumns = null!;
    private Label _mode = null!;
    private Label _selection = null!;
    private Label _status = null!;
    private Label _footerHint = null!;
    private SlotInventory? _astronautInventory;
    private SlotInventory? _shipInventory;
    private ItemPresentationCatalog? _items;
    private InventorySlotAddress? _selectedAddress;
    private Tween? _transitionTween;
    private Tween? _statusTween;
    private bool _ready;
    private bool _initialized;
    private bool _isOpen;
    private bool _isClosing;
    private bool _hasFuelTankState;
    private double _currentFuel;
    private double _fuelCapacity;

    public bool IsOpen => _isOpen || _isClosing;

    public bool IsShipStorageVisible => IsOpen && _shipPanel.Visible;

    public event Action? Closed;

    public event Action? RefuelRequested;

    public override void _Ready()
    {
        _overlay = GetNode<Control>("Overlay");
        _safeArea = GetNode<MarginContainer>("Overlay/SafeArea");
        _frame = GetNode<PanelContainer>("Overlay/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin");
        _bodyScroll = GetNode<ScrollContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll");
        _header = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header");
        _footer = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer");
        _astronautPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/AstronautInventory");
        _shipPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/ShipInventory");
        _inventoryColumns = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns");
        _mode = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Meta/Mode");
        _selection = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Meta/Selection");
        _status = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/Status");
        _footerHint = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Footer/Hint");
        _shipPanel.RefuelRequested += HandleRefuelRequested;

        _safeArea.Theme = CreateSciFiTheme();
        ApplyFrameStyle();
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _frame.Resized += UpdateFramePivot;
        _ready = true;
        if (_astronautInventory is not null && _shipInventory is not null && _items is not null)
        {
            ConfigurePanels();
        }

        UpdateResponsiveLayout();
        UpdateFramePivot();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_ready)
        {
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
            _frame.Resized -= UpdateFramePivot;
            _shipPanel.RefuelRequested -= HandleRefuelRequested;
        }

        _transitionTween?.Kill();
        _statusTween?.Kill();
    }

    public void Initialize(
        SlotInventory astronautInventory,
        SlotInventory shipInventory,
        IReadOnlyList<ResourceDefinition> resources,
        ProductionItemCatalog? productionItems = null)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(shipInventory);
        ArgumentNullException.ThrowIfNull(resources);
        if (astronautInventory.SlotCount != InventoryConfiguration.AstronautSlotCount)
        {
            throw new ArgumentException(
                $"Astronaut inventory must contain {InventoryConfiguration.AstronautSlotCount} slots.",
                nameof(astronautInventory));
        }

        if (shipInventory.SlotCount != InventoryConfiguration.ShipSlotCount)
        {
            throw new ArgumentException(
                $"Ship inventory must contain {InventoryConfiguration.ShipSlotCount} slots.",
                nameof(shipInventory));
        }

        _astronautInventory = astronautInventory;
        _shipInventory = shipInventory;
        _items = ItemPresentationCatalog.Create(resources, productionItems);
        if (_ready)
        {
            ConfigurePanels();
        }
    }

    /// <summary>Updates the ship-inventory fuel readout without owning fuel gameplay logic.</summary>
    public void SetFuelTankState(double currentFuel, double capacity)
    {
        if (!double.IsFinite(currentFuel) || !double.IsFinite(capacity) || capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "Fuel tank values must be finite and capacity must be positive.");
        }

        _currentFuel = Math.Clamp(currentFuel, 0, capacity);
        _fuelCapacity = capacity;
        _hasFuelTankState = true;
        if (_ready)
        {
            _shipPanel.SetFuelTankState(_currentFuel, _fuelCapacity);
        }
    }

    /// <summary>Displays feedback supplied by an external inventory-related service.</summary>
    public void ShowExternalStatus(string message, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ShowStatus(message, succeeded ? TransferFeedback.Success : TransferFeedback.Failure);
    }

    /// <summary>Opens only the personal inventory. Ship storage stays inaccessible.</summary>
    public void OpenAstronautInventory() => Open(includeShipStorage: false);

    /// <summary>Opens both inventories. The caller must only use this while controlling the ship.</summary>
    public void OpenShipInventory() => Open(includeShipStorage: true);

    public void Open(bool includeShipStorage)
    {
        EnsureInitialized();
        SetInventoryMode(includeShipStorage);
        Refresh();
        UpdateResponsiveLayout();

        if (_isOpen && !_isClosing)
        {
            return;
        }

        _transitionTween?.Kill();
        _isClosing = false;
        _isOpen = true;
        Visible = true;
        _overlay.Modulate = new Color(1, 1, 1, 0);
        _frame.Scale = new Vector2(0.985f, 0.985f);
        _transitionTween = CreateTween();
        _transitionTween.SetParallel();
        _transitionTween.SetEase(Tween.EaseType.Out);
        _transitionTween.SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, new NodePath("modulate"), Colors.White, 0.16);
        _transitionTween.TweenProperty(_frame, new NodePath("scale"), Vector2.One, 0.16);
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
        _transitionTween = CreateTween();
        _transitionTween.SetParallel();
        _transitionTween.SetEase(Tween.EaseType.In);
        _transitionTween.SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.TweenProperty(_overlay, new NodePath("modulate"), new Color(1, 1, 1, 0), 0.13);
        _transitionTween.TweenProperty(_frame, new NodePath("scale"), new Vector2(0.99f, 0.99f), 0.13);
        _transitionTween.SetParallel(false);
        _transitionTween.TweenCallback(Callable.From(FinishClose));
    }

    /// <summary>
    /// Hides the inventory synchronously when another primary overlay replaces it.
    /// </summary>
    public void CloseImmediately()
    {
        if (!IsOpen)
        {
            return;
        }

        _transitionTween?.Kill();
        _isOpen = false;
        _isClosing = true;
        GetViewport().GuiReleaseFocus();
        FinishClose();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            return;
        }

        _astronautPanel.Refresh();
        _shipPanel.Refresh();
        RefreshSelection();
    }

    /// <summary>
    /// Verifies both inventory modes and the three supported desktop layouts
    /// without changing the actual viewport size.
    /// </summary>
    public void RunConstructionSmokeTest()
    {
        EnsureInitialized();
        if (_astronautPanel.SlotControlCount != InventoryConfiguration.AstronautSlotCount ||
            _shipPanel.SlotControlCount != InventoryConfiguration.ShipSlotCount)
        {
            throw new InvalidOperationException("Inventory UI did not construct all 20 / 50 slot controls.");
        }

        if (_bodyScroll.Get("horizontal_scroll_mode").AsInt32() != 3)
        {
            throw new InvalidOperationException("The inventory's outer horizontal scrollbar must stay disabled.");
        }

        if (_items is null ||
            !_items.TryGet(ProductionItemIds.FuelContainer, out var fuelContainer) ||
            fuelContainer is not { Glyph: ItemIconGlyph.Container, IsContainer: true } ||
            !_items.TryGet(ProductionItemIds.IronOre, out var ironOre) ||
            ironOre is not { UsesResourceIcon: true })
        {
            throw new InvalidOperationException(
                "Inventory item presentation mapping did not preserve resources and production containers.");
        }

        Vector2[] desktopSizes =
        [
            new(1366, 768),
            new(1920, 1080),
            new(2560, 1440),
        ];
        foreach (var size in desktopSizes)
        {
            ValidateDesktopMetrics(CalculateResponsiveMetrics(size, includeShipStorage: false), size, "astronaut");
            ValidateDesktopMetrics(CalculateResponsiveMetrics(size, includeShipStorage: true), size, "ship");
        }

        var originalShipVisibility = _shipPanel.Visible;
        SetInventoryMode(includeShipStorage: false);
        if (_shipPanel.Visible || _shipPanel.FuelControlsVisible)
        {
            throw new InvalidOperationException("Ship storage and fuel controls must stay hidden in astronaut mode.");
        }

        SetInventoryMode(includeShipStorage: true);
        if (!_shipPanel.Visible || !_shipPanel.FuelControlsVisible)
        {
            throw new InvalidOperationException("Ship storage and fuel controls must be visible in ship mode.");
        }

        SetInventoryMode(originalShipVisibility);
        GD.Print("INVENTORY_UI_SMOKE_OK: 20/50 slots; resources/products/containers; ship fuel controls; 1366x768, 1920x1080, 2560x1440");
    }

    private void ConfigurePanels()
    {
        _astronautPanel.Configure(
            AstronautInventoryId,
            "ASTRONAUT",
            5,
            _astronautInventory!,
            _items!,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        _shipPanel.Configure(
            ShipInventoryId,
            "RAUMSCHIFF-LAGER",
            10,
            _shipInventory!,
            _items!,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        if (_hasFuelTankState)
        {
            _shipPanel.SetFuelTankState(_currentFuel, _fuelCapacity);
        }

        _initialized = true;
        UpdateResponsiveLayout();
    }

    private void SetInventoryMode(bool includeShipStorage)
    {
        _shipPanel.Visible = includeShipStorage;
        _mode.Text = includeShipStorage ? "RAUMSCHIFF  /  TRANSFERMODUS" : "ASTRONAUT  /  PERSÖNLICH";
        UpdateFooterHint(GetViewport().GetVisibleRect().Size.X < 900);
        ShowStatus(
            includeShipStorage
                ? "Stapel zwischen Astronaut und Raumschiff-Lager verschieben."
                : "Persönliches Inventar · Raumschiff-Lager nicht verbunden.",
            TransferFeedback.Neutral);

        if (!includeShipStorage && _selectedAddress is { InventoryId: ShipInventoryId })
        {
            _selectedAddress = null;
        }

        RefreshSelection();
        UpdateResponsiveLayout();
    }

    private InventoryTransferResult PreviewTransferStack(InventorySlotAddress source, InventorySlotAddress target)
    {
        if (!TryGetInventory(source.InventoryId, out var sourceInventory) ||
            !TryGetInventory(target.InventoryId, out var targetInventory) ||
            (!_shipPanel.Visible &&
             (source.InventoryId == ShipInventoryId || target.InventoryId == ShipInventoryId)))
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        return InventoryTransfer.Preview(
            sourceInventory,
            source.SlotIndex,
            targetInventory,
            target.SlotIndex);
    }

    private bool TransferStack(InventorySlotAddress source, InventorySlotAddress target)
    {
        if (!TryGetInventory(source.InventoryId, out var sourceInventory) ||
            !TryGetInventory(target.InventoryId, out var targetInventory))
        {
            ShowStatus("Transfer abgelehnt: unbekanntes Inventar.", TransferFeedback.Failure);
            return false;
        }

        if (!_shipPanel.Visible &&
            (source.InventoryId == ShipInventoryId || target.InventoryId == ShipInventoryId))
        {
            ShowStatus("Das Raumschiff-Lager ist momentan nicht zugänglich.", TransferFeedback.Failure);
            return false;
        }

        var result = InventoryTransfer.Transfer(
            sourceInventory,
            source.SlotIndex,
            targetInventory,
            target.SlotIndex);
        _selectedAddress = target;
        Refresh();
        SetSelectedVisual(target);
        GetPanel(target.InventoryId)?.PlayTransferFeedback(target.SlotIndex, result.Succeeded);
        if (result.Succeeded && source != target)
        {
            GetPanel(source.InventoryId)?.PlayTransferFeedback(source.SlotIndex, succeeded: true);
        }

        ShowStatus(
            DescribeTransfer(result),
            result.Succeeded ? TransferFeedback.Success : TransferFeedback.Failure);
        return result.Succeeded;
    }

    private void SelectSlot(InventorySlotAddress address)
    {
        if (!TryGetInventory(address.InventoryId, out _) ||
            (!_shipPanel.Visible && address.InventoryId == ShipInventoryId))
        {
            return;
        }

        _selectedAddress = address;
        SetSelectedVisual(address);
        RefreshSelection();
    }

    private void SetSelectedVisual(InventorySlotAddress? address)
    {
        _astronautPanel.SetSelectedSlot(address is { InventoryId: AstronautInventoryId } astronaut
            ? astronaut.SlotIndex
            : null);
        _shipPanel.SetSelectedSlot(address is { InventoryId: ShipInventoryId } ship
            ? ship.SlotIndex
            : null);
    }

    private void RefreshSelection()
    {
        SetSelectedVisual(_selectedAddress);
        if (_selectedAddress is not { } address ||
            !TryGetInventory(address.InventoryId, out var inventory) ||
            address.SlotIndex < 0 ||
            address.SlotIndex >= inventory.SlotCount)
        {
            _selection.Text = "AUSWAHL  —  KEIN GEGENSTAND";
            return;
        }

        var slot = inventory.GetSlot(address.SlotIndex);
        if (slot.IsEmpty)
        {
            _selection.Text = $"AUSWAHL  —  LEERER SLOT {slot.Index + 1:00}";
            return;
        }

        var itemName = slot.ItemId is { } itemId && _items is not null
            ? _items.GetOrCreateFallback(itemId, slot.MaximumAmount).DisplayName
            : slot.ItemId?.Value ?? "UNBEKANNT";
        _selection.Text = $"AUSWAHL  —  {itemName.ToUpperInvariant()}  ·  {slot.Amount} / {slot.MaximumAmount}";
    }

    private void HandleRefuelRequested()
    {
        if (!_shipPanel.Visible || !IsOpen)
        {
            return;
        }

        RefuelRequested?.Invoke();
    }

    private bool TryGetInventory(string inventoryId, out SlotInventory inventory)
    {
        inventory = inventoryId switch
        {
            AstronautInventoryId when _astronautInventory is not null => _astronautInventory,
            ShipInventoryId when _shipInventory is not null => _shipInventory,
            _ => null!,
        };
        return inventory is not null;
    }

    private InventoryPanelControl? GetPanel(string inventoryId) => inventoryId switch
    {
        AstronautInventoryId => _astronautPanel,
        ShipInventoryId => _shipPanel,
        _ => null,
    };

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        ApplyResponsiveMetrics(CalculateResponsiveMetrics(viewportSize, _shipPanel.Visible));
        var narrowHeader = viewportSize.X < 700;
        var narrowFooter = viewportSize.X < 900;
        _header.Set("vertical", narrowHeader);
        _footer.Set("vertical", narrowFooter);
        _mode.HorizontalAlignment = narrowHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _selection.HorizontalAlignment = narrowHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _footerHint.HorizontalAlignment = narrowFooter ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        UpdateFooterHint(narrowFooter);
    }

    private void UpdateFooterHint(bool narrow)
    {
        if (narrow)
        {
            _footerHint.Text = _shipPanel.Visible
                ? "ZIEHEN: TRANSFER  |  I / ESC: SCHLIESSEN"
                : "KLICK: AUSWAHL  |  I / ESC: SCHLIESSEN";
            return;
        }

        _footerHint.Text = _shipPanel.Visible
            ? "LINKSKLICK + ZIEHEN   TRANSFER    |    I / ESC   SCHLIESSEN"
            : "LINKSKLICK   AUSWÄHLEN    |    I / ESC   SCHLIESSEN";
    }

    private void ApplyResponsiveMetrics(ResponsiveMetrics metrics)
    {
        SetMargins(_safeArea, metrics.SafeMarginX, metrics.SafeMarginY);
        SetMargins(_frameMargin, metrics.FrameMarginX, metrics.FrameMarginY);
        _inventoryColumns.Set("vertical", metrics.Stacked);
        _inventoryColumns.AddThemeConstantOverride("separation", metrics.PanelSeparation);
        _bodyScroll.ScrollHorizontal = 0;

        _astronautPanel.SizeFlagsStretchRatio = 5;
        _shipPanel.SizeFlagsStretchRatio = metrics.ShipColumns;
        _astronautPanel.SetLayoutMetrics(5, metrics.SlotSize, metrics.Gap, metrics.Compact);
        _shipPanel.SetLayoutMetrics(metrics.ShipColumns, metrics.SlotSize, metrics.Gap, metrics.Compact);
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(Vector2 viewportSize, bool includeShipStorage)
    {
        var width = Mathf.Max(480, viewportSize.X);
        var height = Mathf.Max(360, viewportSize.Y);
        var compact = width < 1500 || height < 850;
        var safeMarginX = width < 900
            ? 12
            : Mathf.Clamp(Mathf.RoundToInt(width * 0.045f), 28, 120);
        var safeMarginY = height < 560
            ? 8
            : Mathf.Clamp(Mathf.RoundToInt(height * 0.05f), 18, 72);
        var frameMarginX = compact ? 12 : 20;
        var frameMarginY = compact ? 8 : 14;
        var panelSeparation = compact ? 10 : 14;
        var gap = compact ? 5 : 7;
        var availableWidth = width - (2 * (safeMarginX + frameMarginX));
        var headerFooterBudget = compact ? 120 : 134;
        var availableHeight = height - (2 * (safeMarginY + frameMarginY)) - headerFooterBudget;
        var stacked = includeShipStorage && width < 1050;
        var shipColumns = includeShipStorage && width >= 1600 ? 10 : 8;
        var panelMarginX = compact ? 10 : 14;
        var panelChrome = compact
            ? includeShipStorage ? 132 : 81
            : includeShipStorage ? 145 : 96;
        var maximumSlotSize = width >= 2200 ? 84 : width >= 1600 ? 78 : 68;

        float slotSize;
        float requiredWidth;
        float requiredHeight;
        if (!includeShipStorage)
        {
            const int astronautColumns = 5;
            const int astronautRows = 4;
            var preferredPanelWidth = Mathf.Min(760, availableWidth);
            var widthBound = (preferredPanelWidth - (2 * panelMarginX) - ((astronautColumns - 1) * gap)) /
                             astronautColumns;
            var heightBound = (availableHeight - panelChrome - ((astronautRows - 1) * gap)) / astronautRows;
            slotSize = Mathf.Clamp(Mathf.Min(widthBound, heightBound), 42, maximumSlotSize);
            requiredWidth = (astronautColumns * slotSize) + ((astronautColumns - 1) * gap) + (2 * panelMarginX);
            requiredHeight = (astronautRows * slotSize) + ((astronautRows - 1) * gap) + panelChrome;
        }
        else if (!stacked)
        {
            const int astronautColumns = 5;
            const int astronautRows = 4;
            var shipRows = Mathf.CeilToInt(50 / (float)shipColumns);
            var widthBound = (availableWidth - panelSeparation - (4 * panelMarginX) -
                              (((astronautColumns - 1) + (shipColumns - 1)) * gap)) /
                             (astronautColumns + shipColumns);
            var largestRowCount = Math.Max(astronautRows, shipRows);
            var heightBound = (availableHeight - panelChrome - ((largestRowCount - 1) * gap)) /
                              largestRowCount;
            slotSize = Mathf.Clamp(Mathf.Min(widthBound, heightBound), 38, maximumSlotSize);
            var astronautWidth = (astronautColumns * slotSize) + ((astronautColumns - 1) * gap) +
                                 (2 * panelMarginX);
            var shipWidth = (shipColumns * slotSize) + ((shipColumns - 1) * gap) + (2 * panelMarginX);
            requiredWidth = astronautWidth + shipWidth + panelSeparation;
            requiredHeight = (largestRowCount * slotSize) + ((largestRowCount - 1) * gap) + panelChrome;
        }
        else
        {
            const int astronautColumns = 5;
            const int astronautRows = 4;
            var shipRows = Mathf.CeilToInt(50 / (float)shipColumns);
            var widthBound = (availableWidth - 18 - (2 * panelMarginX) - ((shipColumns - 1) * gap)) /
                             shipColumns;
            slotSize = Mathf.Clamp(widthBound, 38, Math.Min(64, maximumSlotSize));
            var astronautWidth = (astronautColumns * slotSize) + ((astronautColumns - 1) * gap) +
                                 (2 * panelMarginX);
            var shipWidth = (shipColumns * slotSize) + ((shipColumns - 1) * gap) + (2 * panelMarginX);
            requiredWidth = Math.Max(astronautWidth, shipWidth);
            var astronautHeight = (astronautRows * slotSize) + ((astronautRows - 1) * gap) + panelChrome;
            var shipHeight = (shipRows * slotSize) + ((shipRows - 1) * gap) + panelChrome;
            requiredHeight = astronautHeight + shipHeight + panelSeparation;
        }

        return new ResponsiveMetrics(
            stacked,
            shipColumns,
            slotSize,
            gap,
            safeMarginX,
            safeMarginY,
            frameMarginX,
            frameMarginY,
            panelSeparation,
            compact,
            availableWidth,
            availableHeight,
            requiredWidth,
            requiredHeight);
    }

    private static void ValidateDesktopMetrics(ResponsiveMetrics metrics, Vector2 size, string mode)
    {
        if (metrics.RequiredBodyWidth > metrics.AvailableBodyWidth + 0.5f)
        {
            throw new InvalidOperationException(
                $"Inventory {mode} layout overflows horizontally at {size.X}x{size.Y}.");
        }

        if (!metrics.Stacked && metrics.RequiredBodyHeight > metrics.AvailableBodyHeight + 0.5f)
        {
            throw new InvalidOperationException(
                $"Inventory {mode} layout requires vertical scrolling at {size.X}x{size.Y}.");
        }
    }

    private static void SetMargins(MarginContainer container, int horizontal, int vertical)
    {
        container.AddThemeConstantOverride("margin_left", horizontal);
        container.AddThemeConstantOverride("margin_right", horizontal);
        container.AddThemeConstantOverride("margin_top", vertical);
        container.AddThemeConstantOverride("margin_bottom", vertical);
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

    private enum TransferFeedback
    {
        Neutral,
        Success,
        Failure,
    }

    private void ShowStatus(string message, TransferFeedback feedback)
    {
        if (!_ready)
        {
            return;
        }

        _status.Text = message;
        _statusTween?.Kill();
        _status.Modulate = feedback switch
        {
            TransferFeedback.Success => new Color(0.55f, 1, 0.78f, 1),
            TransferFeedback.Failure => new Color(1, 0.58f, 0.58f, 1),
            _ => Colors.White,
        };
        if (feedback == TransferFeedback.Neutral)
        {
            return;
        }

        _statusTween = CreateTween();
        _statusTween.SetEase(Tween.EaseType.Out);
        _statusTween.SetTrans(Tween.TransitionType.Cubic);
        _statusTween.TweenInterval(0.7);
        _statusTween.TweenProperty(_status, new NodePath("modulate"), Colors.White, 0.28);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InventoryMenuController.Initialize must be called before opening the menu.");
        }
    }

    private static string DescribeTransfer(InventoryTransferResult result)
    {
        if (result.Succeeded)
        {
            if (result.MovedAmount == 0)
            {
                return "Quelle und Ziel sind identisch.";
            }

            var action = result.Swapped ? "Stapel getauscht" : "Transfer erfolgreich";
            var remainder = result.RemainingAmount > 0 ? $" · {result.RemainingAmount} verbleiben" : string.Empty;
            return $"{action}: {result.MovedAmount} Einheiten{remainder}.";
        }

        return result.Failure switch
        {
            InventoryTransferFailure.SourceEmpty => "Transfer nicht möglich: Quellslot ist leer.",
            InventoryTransferFailure.TargetStackFull => "Transfer nicht möglich: Zielstapel ist voll.",
            InventoryTransferFailure.IncompatibleStacks => "Transfer nicht möglich: Stapel sind nicht kompatibel.",
            InventoryTransferFailure.StackLimitExceeded => "Transfer nicht möglich: maximal 200 Einheiten je Slot.",
            InventoryTransferFailure.InsufficientItems => "Transfer nicht möglich: zu wenige Einheiten vorhanden.",
            _ => "Transfer konnte nicht ausgeführt werden.",
        };
    }

    private void ApplyFrameStyle()
    {
        _frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.002f, 0.012f, 0.019f, 0.955f),
            BorderColor = new Color(0.04f, 0.39f, 0.52f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 2,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ShadowColor = new Color(0, 0, 0, 0.54f),
            ShadowSize = 12,
        });
    }

    private static Theme CreateSciFiTheme()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", new Color(0.78f, 0.9f, 0.94f));
        theme.SetFontSize("font_size", "Label", 14);
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine
        {
            Color = new Color(0.05f, 0.4f, 0.52f, 0.56f),
            Thickness = 1,
        });
        return theme;
    }
}

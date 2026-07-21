using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Settings;
using SpaceFactory.Core.Ships.Fuel;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryMenuController : CanvasLayer
{
    public enum ShipFuelTransferDirection
    {
        Fill,
        Drain,
    }

    public const string AstronautInventoryId = "astronaut";
    public const string HotbarInventoryId = "hotbar";
    public const string ToolInventoryId = "tools";
    public const string ShipInventoryId = "ship";
    public const string StorageInventoryId = "storage";

    private enum InventoryViewMode
    {
        Personal,
        ShipStorage,
        ContainerStorage,
    }

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
    private InventoryWorldDropSurface _worldDropSurface = null!;
    private MarginContainer _safeArea = null!;
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private MarginContainer _bodyScroll = null!;
    private BoxContainer _header = null!;
    private InventoryPanelControl _astronautPanel = null!;
    private InventoryPanelControl _toolPanel = null!;
    private InventoryPanelControl _hotbarPanel = null!;
    private InventoryPanelControl _shipPanel = null!;
    private VBoxContainer _personalColumn = null!;
    private BoxContainer _personalInventoryRow = null!;
    private VSeparator _toolDivider = null!;
    private BoxContainer _inventoryColumns = null!;
    private VBoxContainer _storageActions = null!;
    private Button _storeAll = null!;
    private Button _takeAll = null!;
    private Label _title = null!;
    private Label _mode = null!;
    private InventoryItemContextMenu _itemContextMenu = null!;
    private PanelContainer _toast = null!;
    private Label _toastMessage = null!;
    private SlotInventory? _astronautInventory;
    private SlotInventory? _hotbarInventory;
    private ToolInventoryState? _toolState;
    private SlotInventory? _shipInventory;
    private ItemPresentationCatalog? _items;
    private ProductionItemCatalog _productionItems = DefaultProductionItemCatalog.Instance;
    private InventorySlotAddress? _selectedAddress;
    private Tween? _transitionTween;
    private Tween? _statusTween;
    private bool _ready;
    private bool _initialized;
    private bool _isOpen;
    private bool _isClosing;
    private bool _hasFuelTankState;
    private InventoryViewMode _viewMode;
    private SlotInventory? _activeStorageInventory;
    private string _activeStorageInventoryId = ShipInventoryId;
    private string _activeStorageTitle = "RAUMSCHIFF-LAGER";
    private int _activeStorageColumns = 8;
    private Func<ItemId, bool>? _activeStorageAcceptance;
    private double _currentFuel;
    private double _fuelCapacity;
    private ShipFuelType _fuelType = ShipFuelConfiguration.NewGameFuelType;
    private double _remainingBoostSeconds;
    private double _boostSpeedMultiplier = 1;
    private int _activeHotbarSlotIndex;

    public bool IsOpen => _isOpen || _isClosing;

    public bool IsShipStorageVisible => IsOpen && _shipPanel.Visible &&
                                        _viewMode == InventoryViewMode.ShipStorage;

    public bool IsContainerStorageVisible => IsOpen && _shipPanel.Visible &&
                                             _viewMode == InventoryViewMode.ContainerStorage;

    public bool IsPersonalInventoryVisible => IsOpen && _personalColumn.Visible;

    public bool IsEmbeddedHotbarVisible => IsOpen && _hotbarPanel.Visible;

    public bool IsToolInventoryVisible => IsOpen && _toolPanel.Visible;

    public event Action? Closed;

    public event Action<ShipFuelTransferDirection, InventorySlotAddress>? ShipFuelTransferRequested;

    public event Action? InventoryChanged;

    public event Action<InventorySlotAddress, Vector2>? WorldDropRequested;

    public event Action<int>? HotbarItemActivationRequested;

    public event Action<int>? ToolItemActivationRequested;

    public override void _Ready()
    {
        _overlay = GetNode<Control>("Overlay");
        _worldDropSurface = GetNode<InventoryWorldDropSurface>("Overlay/InputShield");
        _safeArea = GetNode<MarginContainer>("Overlay/SafeArea");
        _frame = GetNode<PanelContainer>("Overlay/SafeArea/Frame");
        _frameMargin = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin");
        _bodyScroll = GetNode<MarginContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll");
        _header = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header");
        _personalColumn = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn");
        _personalInventoryRow = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn/PersonalInventoryRow");
        _astronautPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn/PersonalInventoryRow/AstronautInventory");
        _toolDivider = GetNode<VSeparator>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn/PersonalInventoryRow/ToolDivider");
        _toolPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn/PersonalInventoryRow/ToolInventory");
        _hotbarPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/PersonalColumn/HotbarInventory");
        _shipPanel = GetNode<InventoryPanelControl>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/ShipInventory");
        _inventoryColumns = GetNode<BoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns");
        _storageActions = GetNode<VBoxContainer>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/StorageActions");
        _storeAll = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/StorageActions/StoreAll");
        _takeAll = GetNode<Button>("Overlay/SafeArea/Frame/FrameMargin/Layout/BodyScroll/InventoryColumns/StorageActions/TakeAll");
        _title = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/TitleBlock/Title");
        _mode = GetNode<Label>("Overlay/SafeArea/Frame/FrameMargin/Layout/Header/Meta/Mode");
        _itemContextMenu = GetNode<InventoryItemContextMenu>("Overlay/ItemContextMenu");
        _toast = GetNode<PanelContainer>("Overlay/Toast");
        _toastMessage = GetNode<Label>("Overlay/Toast/Message");
        _shipPanel.RefuelRequested += HandleRefuelRequested;
        _shipPanel.DrainFuelRequested += HandleDrainFuelRequested;
        _astronautPanel.DeleteRequested += HandleDeleteRequested;
        _shipPanel.DeleteRequested += HandleDeleteRequested;
        _astronautPanel.ContextRequested += HandleContextRequested;
        _toolPanel.ContextRequested += HandleContextRequested;
        _hotbarPanel.ContextRequested += HandleContextRequested;
        _shipPanel.ContextRequested += HandleContextRequested;
        _storeAll.Pressed += HandleStoreAllPressed;
        _takeAll.Pressed += HandleTakeAllPressed;
        _itemContextMenu.ActionRequested += HandleContextActionRequested;
        _worldDropSurface.WorldDropRequested += HandleWorldDropRequested;

        _safeArea.Theme = CreateSciFiTheme();
        ApplyFrameStyle();
        ApplyToastStyle();
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        _frame.Resized += UpdateFramePivot;
        _ready = true;
        if (_astronautInventory is not null && _hotbarInventory is not null &&
            _shipInventory is not null && _items is not null)
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
            _shipPanel.DrainFuelRequested -= HandleDrainFuelRequested;
            _astronautPanel.DeleteRequested -= HandleDeleteRequested;
            _shipPanel.DeleteRequested -= HandleDeleteRequested;
            _astronautPanel.ContextRequested -= HandleContextRequested;
            _toolPanel.ContextRequested -= HandleContextRequested;
            _hotbarPanel.ContextRequested -= HandleContextRequested;
            _shipPanel.ContextRequested -= HandleContextRequested;
            _storeAll.Pressed -= HandleStoreAllPressed;
            _takeAll.Pressed -= HandleTakeAllPressed;
            _itemContextMenu.ActionRequested -= HandleContextActionRequested;
            _worldDropSurface.WorldDropRequested -= HandleWorldDropRequested;
        }

        _transitionTween?.Kill();
        _statusTween?.Kill();
    }

    private void HandleWorldDropRequested(InventorySlotAddress address, Vector2 screenPosition) =>
        WorldDropRequested?.Invoke(address, screenPosition);

    public void Initialize(
        SlotInventory astronautInventory,
        SlotInventory hotbarInventory,
        SlotInventory shipInventory,
        IReadOnlyList<ResourceDefinition> resources,
        ProductionItemCatalog? productionItems = null)
    {
        InitializeCore(
            astronautInventory,
            hotbarInventory,
            toolState: null,
            shipInventory,
            resources,
            productionItems);
    }

    public void Initialize(
        SlotInventory astronautInventory,
        SlotInventory hotbarInventory,
        ToolInventoryState toolState,
        SlotInventory shipInventory,
        IReadOnlyList<ResourceDefinition> resources,
        ProductionItemCatalog? productionItems = null)
    {
        ArgumentNullException.ThrowIfNull(toolState);
        InitializeCore(
            astronautInventory,
            hotbarInventory,
            toolState,
            shipInventory,
            resources,
            productionItems);
    }

    private void InitializeCore(
        SlotInventory astronautInventory,
        SlotInventory hotbarInventory,
        ToolInventoryState? toolState,
        SlotInventory shipInventory,
        IReadOnlyList<ResourceDefinition> resources,
        ProductionItemCatalog? productionItems)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(hotbarInventory);
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

        if (hotbarInventory.SlotCount != InventoryConfiguration.HotbarSlotCount)
        {
            throw new ArgumentException(
                $"Hotbar inventory must contain {InventoryConfiguration.HotbarSlotCount} slots.",
                nameof(hotbarInventory));
        }

        _astronautInventory = astronautInventory;
        _hotbarInventory = hotbarInventory;
        _toolState = toolState;
        _shipInventory = shipInventory;
        _activeStorageInventory = shipInventory;
        _productionItems = productionItems ?? DefaultProductionItemCatalog.Instance;
        _items = ItemPresentationCatalog.Create(resources, _productionItems);
        if (_ready)
        {
            ConfigurePanels();
        }
    }

    /// <summary>Updates the ship-inventory fuel readout without owning fuel gameplay logic.</summary>
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
            remainingBoostSeconds < 0 || !double.IsFinite(boostSpeedMultiplier) || boostSpeedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "Fuel tank values must be finite and capacity must be positive.");
        }

        _currentFuel = Math.Clamp(currentFuel, 0, capacity);
        _fuelCapacity = capacity;
        _fuelType = fuelType;
        _remainingBoostSeconds = remainingBoostSeconds;
        _boostSpeedMultiplier = boostSpeedMultiplier;
        _hasFuelTankState = true;
        if (_ready)
        {
            _shipPanel.SetFuelTankState(
                _currentFuel,
                _fuelCapacity,
                _fuelType,
                _remainingBoostSeconds,
                _boostSpeedMultiplier);
        }
    }

    public void SetActiveHotbarSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryConfiguration.HotbarSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        _activeHotbarSlotIndex = slotIndex;
        if (_ready)
        {
            _hotbarPanel.SetSelectedSlot(slotIndex);
        }
    }

    public void SetSelectedToolSlot(int slotIndex)
    {
        SelectToolSlotAndNotify(slotIndex);
    }

    /// <summary>Displays feedback supplied by an external inventory-related service.</summary>
    public void ShowExternalStatus(string message, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ShowStatus(message, succeeded ? TransferFeedback.Success : TransferFeedback.Failure);
    }

    /// <summary>Opens the personal inventory with its embedded six-slot hotbar.</summary>
    public void OpenAstronautInventory() => Open(InventoryViewMode.Personal);

    /// <summary>Opens personal inventory and ship cargo side by side, without the hotbar.</summary>
    public void OpenShipInventory()
    {
        EnsureInitialized();
        ConfigureActiveStorage(
            ShipInventoryId,
            "RAUMSCHIFF-LAGER",
            _shipInventory!,
            columns: 8,
            acceptsItem: null);
        Open(InventoryViewMode.ShipStorage);
    }

    /// <summary>
    /// Opens a placed storage machine beside the astronaut inventory. The supplied
    /// inventory remains owned by the machine, so persistence has no parallel copy.
    /// </summary>
    public void OpenStorageInventory(
        SlotInventory storageInventory,
        string storageTitle,
        Func<ItemId, bool>? acceptsItem = null,
        int? columns = null)
    {
        ArgumentNullException.ThrowIfNull(storageInventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageTitle);
        EnsureInitialized();
        ConfigureActiveStorage(
            StorageInventoryId,
            storageTitle,
            storageInventory,
            columns ?? ChooseStorageColumns(storageInventory.SlotCount),
            acceptsItem);
        Open(InventoryViewMode.ContainerStorage);
    }

    /// <summary>Compatibility entry point used by older callers.</summary>
    public void Open(bool includeShipStorage)
    {
        if (includeShipStorage)
        {
            OpenShipInventory();
            return;
        }

        OpenAstronautInventory();
    }

    private void Open(InventoryViewMode mode)
    {
        EnsureInitialized();
        SetInventoryMode(mode);
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
        _itemContextMenu?.Close();
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
        _itemContextMenu?.Close();
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

    /// <summary>
    /// Closes the topmost inventory sub-state without changing the owning menu. The central
    /// ESC hierarchy calls this before it closes the inventory itself.
    /// </summary>
    public bool TryCloseTransientUi() => _itemContextMenu?.TryClose() == true;

    public void Refresh()
    {
        if (!_initialized)
        {
            return;
        }

        _astronautPanel.Refresh();
        if (_toolState is not null)
        {
            _toolPanel.Refresh();
        }
        _hotbarPanel.Refresh();
        RefreshHotbarShortcutLabels();
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
            _hotbarPanel.SlotControlCount != InventoryConfiguration.HotbarSlotCount ||
            _shipPanel.SlotControlCount != InventoryConfiguration.ShipSlotCount ||
            (_toolState is not null &&
             _toolPanel.SlotControlCount != InventoryConfiguration.ToolSlotCount))
        {
            throw new InvalidOperationException("Inventory UI did not construct all 24 / 4 tool / 6 / 56 slot controls.");
        }

        if (_bodyScroll.MouseFilter != Control.MouseFilterEnum.Ignore)
        {
            throw new InvalidOperationException(
                "The scrollbar-free body container must not intercept slot drag gestures.");
        }

        if (!_astronautPanel.HasUnobstructedDragSurfaces ||
            !_hotbarPanel.HasUnobstructedDragSurfaces ||
            !_shipPanel.HasUnobstructedDragSurfaces ||
            (_toolState is not null && !_toolPanel.HasUnobstructedDragSurfaces))
        {
            throw new InvalidOperationException(
                "A decorative child intercepts pointer input before an inventory slot can start dragging.");
        }

        if (_astronautPanel.HasLegacySlotMarkers ||
            _hotbarPanel.HasLegacySlotMarkers ||
            _shipPanel.HasLegacySlotMarkers ||
            (_toolState is not null && _toolPanel.HasLegacySlotMarkers))
        {
            throw new InvalidOperationException("Inventory slots still expose MAX, TOOL or shortcut markers.");
        }

        if (!Mathf.IsEqualApprox((float)ItemHoverNamePresenter.DelaySeconds, 0.2f) ||
            !Mathf.IsEqualApprox(
                (float)(ItemHoverNamePresenter.VisibleSeconds + ItemHoverNamePresenter.FadeSeconds),
                1.0f))
        {
            throw new InvalidOperationException("The shared item hover timing must remain 0.2 s delay and about 1 s display/fade.");
        }

        var rawActions = InventoryItemContextRules.BuildActions(
            ProductionItemIds.IronOre,
            8,
            isTool: false,
            AstronautInventoryId);
        var toolActions = InventoryItemContextRules.BuildActions(
            ProductionItemIds.MiningTool,
            1,
            isTool: true,
            AstronautInventoryId);
        var placementActions = InventoryItemContextRules.BuildActions(
            ProductionItemIds.PowerCable,
            3,
            isTool: false,
            AstronautInventoryId);
        if (rawActions.Contains(InventoryItemContextAction.Select) ||
            !rawActions.Contains(InventoryItemContextAction.SplitStack) ||
            !rawActions.Contains(InventoryItemContextAction.TakeSingleItem) ||
            !toolActions.Contains(InventoryItemContextAction.Select) ||
            toolActions.Contains(InventoryItemContextAction.EquipToHotbar) ||
            !placementActions.Contains(InventoryItemContextAction.Select))
        {
            throw new InvalidOperationException("Item context actions do not follow their item and stack conditions.");
        }

        var contextRequest = new InventoryItemContextRequest(
            new InventorySlotAddress(AstronautInventoryId, 0),
            ProductionItemIds.IronOre,
            "Eisenerz",
            8);
        _itemContextMenu.Open(
            new InventoryItemContextMenuModel(contextRequest, rawActions),
            new Vector2(48, 48));
        if (!_itemContextMenu.IsOpen || !TryCloseTransientUi() || _itemContextMenu.IsOpen)
        {
            throw new InvalidOperationException(
                "The item context popup does not participate in the first Escape/back step.");
        }

        if (FindChildren("*", nameof(ConfirmationDialog), recursive: true, owned: false).Count > 0)
        {
            throw new InvalidOperationException(
                "Inventory deletion must be immediate and may not construct a confirmation dialog.");
        }

        if (!Mathf.IsEqualApprox(
                _astronautPanel.PresentedSlotSize,
                InventoryUiConfiguration.InventorySlotSize) ||
            !Mathf.IsEqualApprox(
                _shipPanel.PresentedSlotSize,
                InventoryUiConfiguration.InventorySlotSize) ||
            (_toolState is not null &&
             (!_toolPanel.UsesCompactToolColumn ||
              _toolPanel.PresentedSlotSize >= _astronautPanel.PresentedSlotSize)))
        {
            throw new InvalidOperationException(
                "Responsive inventory layout changed regular slot size or did not compact the tool column.");
        }

        if (_personalInventoryRow.Alignment != BoxContainer.AlignmentMode.Center ||
            _astronautPanel.SizeFlagsHorizontal.HasFlag(Control.SizeFlags.Expand) ||
            _personalInventoryRow.GetChild(0) != _astronautPanel ||
            _personalInventoryRow.GetChild(1) != _toolDivider ||
            _personalInventoryRow.GetChild(2) != _toolPanel)
        {
            throw new InvalidOperationException(
                "The tool column must sit directly beside the fixed 6x4 astronaut grid and its divider.");
        }

        if (_storeAll.Text.Length > 0 || _takeAll.Text.Length > 0 ||
            _storeAll.CustomMinimumSize.X > InventoryUiConfiguration.CompactActionButtonSize ||
            _takeAll.CustomMinimumSize.X > InventoryUiConfiguration.CompactActionButtonSize)
        {
            throw new InvalidOperationException("Bulk inventory actions must remain compact icon-only buttons.");
        }

        if (_items is null ||
            !_items.TryGet(ProductionItemIds.FuelContainer, out var fuelContainer) ||
            fuelContainer is not { Glyph: ItemIconGlyph.Container, IsContainer: true } ||
            !_items.TryGet(ProductionItemIds.IronOre, out var ironOre) ||
            ironOre is not { UsesResourceIcon: true } ||
            !_items.TryGet(ProductionItemIds.MiningTool, out var miningTool) ||
            miningTool is not { MaximumStackSize: 1, TexturePath: not null })
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
            ValidateDesktopMetrics(
                CalculateResponsiveMetrics(size, _toolState is not null, includeStorage: false, storageColumns: 0, storageSlots: 0,
                    showFuelModule: false),
                size,
                "astronaut");
            ValidateDesktopMetrics(
                CalculateResponsiveMetrics(size, _toolState is not null, includeStorage: true, storageColumns: 8,
                    storageSlots: InventoryConfiguration.ShipSlotCount, showFuelModule: true),
                size,
                "ship-combined");
        }

        var originalMode = _viewMode;
        var originalStorageId = _activeStorageInventoryId;
        var originalStorageTitle = _activeStorageTitle;
        var originalStorage = _activeStorageInventory!;
        var originalColumns = _activeStorageColumns;
        var originalAcceptance = _activeStorageAcceptance;
        SetInventoryMode(InventoryViewMode.Personal);
        if (_shipPanel.Visible || _shipPanel.FuelControlsVisible || !_personalColumn.Visible ||
            !_hotbarPanel.Visible || !_astronautPanel.DeleteTargetVisible ||
            _title.Text != "ASTRONAUT" || _mode.Visible || _astronautPanel.HeaderVisible ||
            (_toolState is not null && (!_toolPanel.Visible || !_toolDivider.Visible)))
        {
            throw new InvalidOperationException("Ship storage and fuel controls must stay hidden in astronaut mode.");
        }

        ConfigureActiveStorage(
            ShipInventoryId,
            "RAUMSCHIFF-LAGER",
            _shipInventory!,
            8,
            acceptsItem: null);
        SetInventoryMode(InventoryViewMode.ShipStorage);
        if (!_shipPanel.Visible || !_shipPanel.FuelControlsVisible || !_personalColumn.Visible ||
            _hotbarPanel.Visible || !_astronautPanel.DeleteTargetVisible || !_shipPanel.DeleteTargetVisible ||
            (_toolState is not null && (!_toolPanel.Visible || !_toolDivider.Visible)))
        {
            throw new InvalidOperationException(
                "Ship mode must show astronaut and cargo side by side without the embedded hotbar.");
        }

        var smokeStorage = new SlotInventory(30, InventoryConfiguration.MaximumStackSize);
        ConfigureActiveStorage(StorageInventoryId, "LAGERCONTAINER", smokeStorage, 6, acceptsItem: null);
        SetInventoryMode(InventoryViewMode.ContainerStorage);
        if (!_shipPanel.Visible || _shipPanel.FuelControlsVisible || !_personalColumn.Visible ||
            _hotbarPanel.Visible || (_toolState is not null && !_toolPanel.Visible) ||
            _shipPanel.SlotControlCount != 30 || !_astronautPanel.DeleteTargetVisible ||
            !_shipPanel.DeleteTargetVisible)
        {
            throw new InvalidOperationException(
                "Container mode must show astronaut and external storage without fuel controls or hotbar.");
        }

        ConfigureActiveStorage(
            originalStorageId,
            originalStorageTitle,
            originalStorage,
            originalColumns,
            originalAcceptance);
        SetInventoryMode(originalMode);
        GD.Print(
            "INVENTORY_UI_SMOKE_OK: personal 24+4 tools+6; combined astronaut/ship and astronaut/container; no desktop scrollbars");
    }

    private void ConfigurePanels()
    {
        _astronautPanel.Configure(
            AstronautInventoryId,
            "ASTRONAUT",
            6,
            _astronautInventory!,
            _items!,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        if (_toolState is not null)
        {
            _toolPanel.Configure(
                ToolInventoryId,
                "WERKZEUGE",
                1,
                _toolState.Inventory,
                _items!,
                PreviewTransferStack,
                TransferStack,
                SelectSlot);
            _toolPanel.SetSelectedSlot(_toolState.SelectedSlotIndex);
        }
        _toolPanel.Visible = _toolState is not null;
        _toolDivider.Visible = _toolState is not null;
        _hotbarPanel.Configure(
            HotbarInventoryId,
            "SCHNELLZUGRIFF",
            6,
            _hotbarInventory!,
            _items!,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        RefreshHotbarShortcutLabels();
        _shipPanel.Configure(
            _activeStorageInventoryId,
            _activeStorageTitle,
            _activeStorageColumns,
            _activeStorageInventory!,
            _items!,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        if (_hasFuelTankState)
        {
            _shipPanel.SetFuelTankState(
                _currentFuel, _fuelCapacity, _fuelType, _remainingBoostSeconds, _boostSpeedMultiplier);
        }

        _initialized = true;
        UpdateResponsiveLayout();
    }

    private void ConfigureActiveStorage(
        string inventoryId,
        string title,
        SlotInventory inventory,
        int columns,
        Func<ItemId, bool>? acceptsItem)
    {
        _activeStorageInventoryId = inventoryId;
        _activeStorageTitle = title;
        _activeStorageInventory = inventory;
        _activeStorageColumns = Math.Clamp(columns, 1, inventory.SlotCount);
        _activeStorageAcceptance = acceptsItem;
        if (!_ready || _items is null)
        {
            return;
        }

        _shipPanel.Configure(
            inventoryId,
            title,
            _activeStorageColumns,
            inventory,
            _items,
            PreviewTransferStack,
            TransferStack,
            SelectSlot);
        if (inventoryId == ShipInventoryId && _hasFuelTankState)
        {
            _shipPanel.SetFuelTankState(
                _currentFuel, _fuelCapacity, _fuelType, _remainingBoostSeconds, _boostSpeedMultiplier);
        }
    }

    private void SetInventoryMode(InventoryViewMode mode)
    {
        _viewMode = mode;
        var storageVisible = mode != InventoryViewMode.Personal;
        _personalColumn.Visible = true;
        _astronautPanel.Visible = true;
        _toolPanel.Visible = _toolState is not null;
        _hotbarPanel.Visible = !storageVisible;
        _shipPanel.Visible = storageVisible;
        _storageActions.Visible = storageVisible;
        _astronautPanel.SetDeleteTargetVisible(true);
        _shipPanel.SetDeleteTargetVisible(storageVisible);
        _title.Text = mode switch
        {
            InventoryViewMode.ShipStorage => "LAGER",
            InventoryViewMode.ContainerStorage => "LAGER",
            _ => "ASTRONAUT",
        };
        _mode.Text = string.Empty;
        _mode.Visible = false;
        _astronautPanel.SetHeaderVisible(storageVisible);
        _shipPanel.SetHeaderVisible(storageVisible);
        _hotbarPanel.SetHeaderVisible(true);
        _itemContextMenu.Close();
        ShowStatus(string.Empty, TransferFeedback.Neutral);

        if (_selectedAddress is { } selected && !IsInventoryAccessible(selected.InventoryId))
        {
            _selectedAddress = null;
        }

        RefreshSelection();
        UpdateResponsiveLayout();
    }

    private static int ChooseStorageColumns(int slotCount) => slotCount switch
    {
        <= 12 => 4,
        <= 30 => 6,
        <= 42 => 7,
        _ => 8,
    };

    private InventoryTransferResult PreviewTransferStack(InventorySlotAddress source, InventorySlotAddress target)
    {
        if (!TryGetInventory(source.InventoryId, out var sourceInventory) ||
            !TryGetInventory(target.InventoryId, out var targetInventory) ||
            !IsInventoryAccessible(source.InventoryId) ||
            !IsInventoryAccessible(target.InventoryId) ||
            !CanTransferWithToolRules(source, sourceInventory, target, targetInventory) ||
            !CanTransferWithStorageRules(source, sourceInventory, target, targetInventory))
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        return IsCrossInventoryStorageTransfer(source, target)
            ? InventoryTransfer.PreviewPrioritizingExistingStacks(
                sourceInventory,
                source.SlotIndex,
                targetInventory,
                target.SlotIndex)
            : InventoryTransfer.Preview(
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

        if (!IsInventoryAccessible(source.InventoryId) ||
            !IsInventoryAccessible(target.InventoryId))
        {
            ShowStatus("Diese Inventare sind momentan nicht miteinander verbunden.", TransferFeedback.Failure);
            return false;
        }

        if (!CanTransferWithStorageRules(source, sourceInventory, target, targetInventory))
        {
            ShowStatus("Dieser Gegenstand ist mit dem geöffneten Lager nicht kompatibel.", TransferFeedback.Failure);
            return false;
        }

        if (!CanTransferWithToolRules(source, sourceInventory, target, targetInventory))
        {
            ShowStatus("Werkzeuge gehören in das Inventar oder die Werkzeugleiste.", TransferFeedback.Failure);
            return false;
        }

        var result = IsCrossInventoryStorageTransfer(source, target)
            ? InventoryTransfer.TransferPrioritizingExistingStacks(
                sourceInventory,
                source.SlotIndex,
                targetInventory,
                target.SlotIndex)
            : InventoryTransfer.Transfer(
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
        if (result.Succeeded && source != target)
        {
            InventoryChanged?.Invoke();
        }

        return result.Succeeded;
    }

    private void SelectSlot(InventorySlotAddress address)
    {
        if (!TryGetInventory(address.InventoryId, out _) ||
            !IsInventoryAccessible(address.InventoryId))
        {
            return;
        }

        if (address.InventoryId == ToolInventoryId && _toolState is not null)
        {
            SelectToolSlotAndNotify(address.SlotIndex);
        }

        _selectedAddress = address;
        SetSelectedVisual(address);
        RefreshSelection();
    }

    private void SelectToolSlotAndNotify(int slotIndex)
    {
        if (_toolState is null)
        {
            return;
        }

        var changed = _toolState.SelectSlot(slotIndex);
        if (_ready)
        {
            _toolPanel.SetSelectedSlot(_toolState.SelectedSlotIndex);
        }

        if (changed)
        {
            // Selection is persistent gameplay state: the hand mirror and equipped tool must
            // change in the same synchronous path as the highlighted inventory slot.
            InventoryChanged?.Invoke();
        }
    }

    private void SetSelectedVisual(InventorySlotAddress? address)
    {
        _astronautPanel.SetSelectedSlot(address is { InventoryId: AstronautInventoryId } astronaut
            ? astronaut.SlotIndex
            : null);
        _hotbarPanel.SetSelectedSlot(_activeHotbarSlotIndex);
        _toolPanel.SetSelectedSlot(_toolState?.SelectedSlotIndex);
        _shipPanel.SetSelectedSlot(address is { } storage && IsActiveStorageId(storage.InventoryId)
            ? storage.SlotIndex
            : null);
    }

    private void RefreshSelection()
    {
        SetSelectedVisual(_selectedAddress);
    }

    private void HandleRefuelRequested()
    {
        if (_viewMode != InventoryViewMode.ShipStorage || !_shipPanel.Visible || !IsOpen)
        {
            return;
        }

        if (_selectedAddress is not { } selected ||
            selected.InventoryId is not (ShipInventoryId or AstronautInventoryId))
        {
            ShowStatus("Kein passender Behälter", TransferFeedback.Failure);
            return;
        }

        ShipFuelTransferRequested?.Invoke(ShipFuelTransferDirection.Fill, selected);
    }

    private void HandleDrainFuelRequested()
    {
        if (_viewMode != InventoryViewMode.ShipStorage || !_shipPanel.Visible || !IsOpen)
        {
            return;
        }

        if (_selectedAddress is not { } selected ||
            selected.InventoryId is not (ShipInventoryId or AstronautInventoryId))
        {
            ShowStatus("Kein passender Behälter", TransferFeedback.Failure);
            return;
        }

        ShipFuelTransferRequested?.Invoke(ShipFuelTransferDirection.Drain, selected);
    }

    private void HandleStoreAllPressed() =>
        TransferAll(_astronautInventory!, _activeStorageInventory!, _activeStorageAcceptance);

    private void HandleTakeAllPressed() =>
        TransferAll(_activeStorageInventory!, _astronautInventory!, targetAcceptance: null);

    private void TransferAll(
        SlotInventory source,
        SlotInventory target,
        Func<ItemId, bool>? targetAcceptance)
    {
        if (_viewMode == InventoryViewMode.Personal || !IsOpen)
        {
            return;
        }

        var result = InventoryBulkTransfer.TransferAllThatFits(source, target, targetAcceptance);
        Refresh();
        if (result.Succeeded)
        {
            ShowStatus($"{result.MovedItemCount} Gegenstände verschoben", TransferFeedback.Success);
            InventoryChanged?.Invoke();
            return;
        }

        ShowStatus(
            result.Failure == InventoryBulkTransferFailure.TargetFull
                ? "Kein freier Platz im Ziel"
                : "Nichts zu verschieben",
            TransferFeedback.Failure);
    }

    private void HandleContextRequested(InventoryItemContextRequest request, Vector2 screenPosition)
    {
        if (!TryResolveCurrentContextRequest(request, out _, out var itemId, out var amount))
        {
            _itemContextMenu.Close();
            return;
        }

        var tool = IsTool(itemId);
        var actions = InventoryItemContextRules.BuildActions(
            itemId,
            amount,
            tool,
            request.Address.InventoryId);

        _itemContextMenu.Open(new InventoryItemContextMenuModel(request, actions), screenPosition);
    }

    private void HandleContextActionRequested(
        InventoryItemContextAction action,
        InventoryItemContextRequest request) =>
        ExecuteContextAction(action, request, allowClosedHotbar: false);

    /// <summary>
    /// Executes the same validated context action for the persistent HUD hotbar. The HUD owns
    /// only its popup; all inventory mutations and world-drop routing stay centralized here.
    /// </summary>
    public void ExecuteHotbarContextAction(
        InventoryItemContextAction action,
        InventoryItemContextRequest request) =>
        ExecuteContextAction(action, request, allowClosedHotbar: true);

    private void ExecuteContextAction(
        InventoryItemContextAction action,
        InventoryItemContextRequest request,
        bool allowClosedHotbar)
    {
        if (!TryResolveCurrentContextRequest(
                request,
                out var source,
                out var itemId,
                out var amount,
                allowClosedHotbar))
        {
            ShowStatus("Gegenstand ist nicht mehr verfügbar", TransferFeedback.Failure);
            return;
        }

        switch (action)
        {
            case InventoryItemContextAction.Select:
                if (IsTool(itemId))
                {
                    SelectToolFromContext(request.Address, source, itemId);
                }
                else if (InventoryItemContextRules.IsPlaceable(itemId))
                {
                    MoveToHotbarAndActivate(request.Address, source, itemId);
                }
                break;
            case InventoryItemContextAction.SplitStack:
                ApplyContextMove(
                    request.Address,
                    InventoryContextActions.SplitStack(source, request.Address.SlotIndex, itemId));
                break;
            case InventoryItemContextAction.TakeSingleItem:
                ApplyContextMove(
                    request.Address,
                    InventoryContextActions.TakeSingleItem(source, request.Address.SlotIndex, itemId));
                break;
            case InventoryItemContextAction.DropStack:
                if (amount > 0)
                {
                    WorldDropRequested?.Invoke(request.Address, GetViewport().GetMousePosition());
                }
                break;
            case InventoryItemContextAction.EquipToHotbar:
                if (!IsTool(itemId))
                {
                    MoveToHotbarAndActivate(request.Address, source, itemId);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private bool TryResolveCurrentContextRequest(
        InventoryItemContextRequest request,
        out SlotInventory inventory,
        out ItemId itemId,
        out int amount,
        bool allowClosedHotbar = false)
    {
        inventory = null!;
        itemId = default;
        amount = 0;
        var accessibleHudHotbar = allowClosedHotbar &&
                                  request.Address.InventoryId == HotbarInventoryId;
        if ((!IsOpen && !accessibleHudHotbar) ||
            (!accessibleHudHotbar && !IsInventoryAccessible(request.Address.InventoryId)) ||
            !TryGetInventory(request.Address.InventoryId, out inventory) ||
            request.Address.SlotIndex < 0 || request.Address.SlotIndex >= inventory.SlotCount)
        {
            return false;
        }

        var current = inventory.GetSlot(request.Address.SlotIndex);
        if (current.ItemId != request.ExpectedItemId || current.Amount <= 0)
        {
            return false;
        }

        itemId = request.ExpectedItemId;
        amount = current.Amount;
        return true;
    }

    private void ApplyContextMove(
        InventorySlotAddress sourceAddress,
        InventoryContextActions.MoveResult result)
    {
        if (!result.Succeeded || result.TargetSlotIndex is not { } targetIndex)
        {
            ShowStatus(
                result.Transfer.Failure == InventoryTransferFailure.TargetStackFull
                    ? "Kein freier Slot"
                    : "Aktion nicht möglich",
                TransferFeedback.Failure);
            return;
        }

        _selectedAddress = new InventorySlotAddress(sourceAddress.InventoryId, targetIndex);
        Refresh();
        GetPanel(sourceAddress.InventoryId)?.PlayTransferFeedback(targetIndex, succeeded: true);
        InventoryChanged?.Invoke();
    }

    private void SelectToolFromContext(
        InventorySlotAddress sourceAddress,
        SlotInventory source,
        ItemId itemId)
    {
        if (_toolState is null)
        {
            ShowStatus("Keine Werkzeugslots verfügbar", TransferFeedback.Failure);
            return;
        }

        int targetIndex;
        var moved = false;
        if (sourceAddress.InventoryId == ToolInventoryId)
        {
            targetIndex = sourceAddress.SlotIndex;
        }
        else
        {
            var result = InventoryContextActions.MoveToFirstFreeOrFallback(
                source,
                sourceAddress.SlotIndex,
                itemId,
                _toolState.Inventory,
                _toolState.SelectedSlotIndex);
            if (!result.Succeeded || result.TargetSlotIndex is not { } movedTo)
            {
                ShowStatus("Werkzeugslot nicht verfügbar", TransferFeedback.Failure);
                return;
            }

            targetIndex = movedTo;
            moved = true;
        }

        var changed = _toolState.SelectSlot(targetIndex);
        changed |= _toolState.ActivateHandMode();
        _selectedAddress = new InventorySlotAddress(ToolInventoryId, targetIndex);
        Refresh();
        if (moved || changed)
        {
            InventoryChanged?.Invoke();
        }
        ToolItemActivationRequested?.Invoke(targetIndex);
    }

    private void MoveToHotbarAndActivate(
        InventorySlotAddress sourceAddress,
        SlotInventory source,
        ItemId itemId)
    {
        int targetIndex;
        var moved = false;
        if (sourceAddress.InventoryId == HotbarInventoryId)
        {
            targetIndex = sourceAddress.SlotIndex;
        }
        else
        {
            var result = InventoryContextActions.MoveToFirstFreeOrFallback(
                source,
                sourceAddress.SlotIndex,
                itemId,
                _hotbarInventory!,
                fallbackSlotIndex: 0);
            if (!result.Succeeded || result.TargetSlotIndex is not { } movedTo)
            {
                ShowStatus("Schnellzugriff blockiert", TransferFeedback.Failure);
                return;
            }

            targetIndex = movedTo;
            moved = true;
        }

        _activeHotbarSlotIndex = targetIndex;
        _selectedAddress = new InventorySlotAddress(HotbarInventoryId, targetIndex);
        Refresh();
        if (moved)
        {
            InventoryChanged?.Invoke();
        }
        HotbarItemActivationRequested?.Invoke(targetIndex);
    }

    private void HandleDeleteRequested(InventorySlotAddress address)
    {
        if (!IsInventoryAccessible(address.InventoryId) ||
            !TryGetInventory(address.InventoryId, out var inventory))
        {
            return;
        }

        DeleteStack(address);
    }

    private void DeleteStack(InventorySlotAddress address)
    {
        if (!TryGetInventory(address.InventoryId, out var inventory) ||
            !IsInventoryAccessible(address.InventoryId))
        {
            return;
        }

        var slot = inventory.GetSlot(address.SlotIndex);
        if (slot.ItemId is not { } itemId ||
            !inventory.RemoveFromSlot(address.SlotIndex, itemId, slot.Amount).Succeeded)
        {
            return;
        }

        _selectedAddress = null;
        Refresh();
        ShowStatus("Gegenstand gelöscht", TransferFeedback.Success);
        InventoryChanged?.Invoke();
    }


    private bool TryGetInventory(string inventoryId, out SlotInventory inventory)
    {
        inventory = inventoryId switch
        {
            AstronautInventoryId when _astronautInventory is not null => _astronautInventory,
            HotbarInventoryId when _hotbarInventory is not null => _hotbarInventory,
            ToolInventoryId when _toolState is not null => _toolState.Inventory,
            ShipInventoryId when _shipInventory is not null => _shipInventory,
            StorageInventoryId when _activeStorageInventory is not null => _activeStorageInventory,
            _ => null!,
        };
        return inventory is not null;
    }

    private InventoryPanelControl? GetPanel(string inventoryId) => inventoryId switch
    {
        AstronautInventoryId => _astronautPanel,
        HotbarInventoryId => _hotbarPanel,
        ToolInventoryId => _toolPanel,
        ShipInventoryId => _shipPanel,
        StorageInventoryId => _shipPanel,
        _ => null,
    };

    private bool IsInventoryAccessible(string inventoryId) => _viewMode switch
    {
        InventoryViewMode.ShipStorage => inventoryId is AstronautInventoryId or ToolInventoryId or ShipInventoryId,
        InventoryViewMode.ContainerStorage => inventoryId is AstronautInventoryId or ToolInventoryId or StorageInventoryId,
        _ => inventoryId is AstronautInventoryId or ToolInventoryId or HotbarInventoryId,
    };

    private bool CanTransferWithToolRules(
        InventorySlotAddress source,
        SlotInventory sourceInventory,
        InventorySlotAddress target,
        SlotInventory targetInventory)
    {
        var sourceSlot = sourceInventory.GetSlot(source.SlotIndex);
        if (sourceSlot.ItemId is not { } sourceItem)
        {
            return false;
        }

        if (!targetInventory.AcceptsItem(sourceItem) ||
            (target.InventoryId == HotbarInventoryId && IsTool(sourceItem)))
        {
            return false;
        }

        var targetSlot = targetInventory.GetSlot(target.SlotIndex);
        return targetSlot.IsEmpty || targetSlot.ItemId == sourceItem ||
               targetSlot.ItemId is { } swappedItem && sourceInventory.AcceptsItem(swappedItem) &&
               (source.InventoryId != HotbarInventoryId || !IsTool(swappedItem));
    }

    private bool IsTool(ItemId itemId) =>
        _productionItems.TryGet(itemId, out var definition) &&
        definition is { Category: ProductionItemCategory.Tool };

    private bool IsCrossInventoryStorageTransfer(
        InventorySlotAddress source,
        InventorySlotAddress target) =>
        source.InventoryId != target.InventoryId &&
        (IsActiveStorageId(source.InventoryId) || IsActiveStorageId(target.InventoryId));

    private bool CanTransferWithStorageRules(
        InventorySlotAddress source,
        SlotInventory sourceInventory,
        InventorySlotAddress target,
        SlotInventory targetInventory)
    {
        if (_activeStorageAcceptance is null || !IsCrossInventoryStorageTransfer(source, target))
        {
            return true;
        }

        if (IsActiveStorageId(target.InventoryId))
        {
            var enteringItem = sourceInventory.GetSlot(source.SlotIndex).ItemId;
            return enteringItem is { } itemId && _activeStorageAcceptance(itemId);
        }

        // A drop from storage onto a different occupied astronaut stack performs a
        // swap. Validate the item that would enter the specialised storage as well.
        var sourceSlot = sourceInventory.GetSlot(source.SlotIndex);
        var targetSlot = targetInventory.GetSlot(target.SlotIndex);
        return targetSlot.IsEmpty || targetSlot.ItemId == sourceSlot.ItemId ||
               targetSlot.ItemId is { } swappedItem && _activeStorageAcceptance(swappedItem);
    }

    private bool IsActiveStorageId(string inventoryId) =>
        inventoryId == _activeStorageInventoryId &&
        _viewMode != InventoryViewMode.Personal;

    private void RefreshHotbarShortcutLabels()
    {
        _hotbarPanel.SetShortcutLabels(null);
    }

    private void UpdateResponsiveLayout()
    {
        if (!_ready)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        ApplyResponsiveMetrics(CalculateResponsiveMetrics(
            viewportSize,
            _toolState is not null,
            _shipPanel.Visible,
            _activeStorageColumns,
            _activeStorageInventory?.SlotCount ?? 0,
            _activeStorageInventoryId == ShipInventoryId));
        var narrowHeader = viewportSize.X < 700;
        _header.Set("vertical", narrowHeader);
        _mode.HorizontalAlignment = narrowHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void ApplyResponsiveMetrics(ResponsiveMetrics metrics)
    {
        SetMargins(_safeArea, metrics.SafeMarginX, metrics.SafeMarginY);
        SetMargins(_frameMargin, metrics.FrameMarginX, metrics.FrameMarginY);
        _inventoryColumns.Set("vertical", false);
        _inventoryColumns.AddThemeConstantOverride("separation", metrics.PanelSeparation);
        _personalColumn.AddThemeConstantOverride("separation", metrics.PanelSeparation);
        _personalInventoryRow.AddThemeConstantOverride(
            "separation",
            InventoryUiConfiguration.InventorySlotGap + 1);
        _astronautPanel.SetLayoutMetrics(6, metrics.SlotSize, metrics.Gap, metrics.Compact);
        if (_toolState is not null)
        {
            _toolPanel.SetToolColumnLayoutMetrics(metrics.SlotSize, metrics.Gap);
            _toolDivider.CustomMinimumSize = new Vector2(
                1,
                (4 * metrics.SlotSize) + (3 * metrics.Gap));
        }
        _hotbarPanel.SetLayoutMetrics(6, metrics.SlotSize, metrics.Gap, metrics.Compact);
        _shipPanel.SetLayoutMetrics(_activeStorageColumns, metrics.SlotSize, metrics.Gap, metrics.Compact);
    }

    private static ResponsiveMetrics CalculateResponsiveMetrics(
        Vector2 viewportSize,
        bool includeTools,
        bool includeStorage,
        int storageColumns,
        int storageSlots,
        bool showFuelModule)
    {
        var width = Mathf.Max(1366, viewportSize.X);
        var height = Mathf.Max(768, viewportSize.Y);
        var compact = width < 1500 || height < 850;
        var safeMarginX = compact
            ? 18
            : Mathf.Clamp(Mathf.RoundToInt(width * 0.035f), 34, 100);
        var safeMarginY = compact
            ? 8
            : Mathf.Clamp(Mathf.RoundToInt(height * 0.035f), 20, 58);
        var frameMarginX = compact ? 10 : 18;
        var frameMarginY = compact ? 6 : 12;
        var panelSeparation = compact ? 8 : 12;
        const int gap = InventoryUiConfiguration.InventorySlotGap;
        const float slotSize = InventoryUiConfiguration.InventorySlotSize;
        var availableWidth = width - (2 * (safeMarginX + frameMarginX));
        var headerBudget = compact ? 50 : 58;
        var availableHeight = height - (2 * (safeMarginY + frameMarginY)) - headerBudget;
        var panelMarginX = compact
            ? InventoryUiConfiguration.InventoryPanelMarginCompact
            : InventoryUiConfiguration.InventoryPanelMarginRegular;
        var personalPanelChrome = compact ? 90 : 100;
        var shipPanelChrome = compact ? 168 : 178;

        float requiredWidth;
        float requiredHeight;
        if (!includeStorage)
        {
            var astronautGridWidth = (6 * slotSize) + (5 * gap);
            var astronautPanelWidth = astronautGridWidth + (2 * panelMarginX);
            var toolWidth = includeTools
                ? InventoryUiConfiguration.ToolSlotSize + 5 + (2 * panelSeparation)
                : 0;
            requiredWidth = astronautPanelWidth + toolWidth;

            var astronautHeight = (4 * slotSize) + (3 * gap) + personalPanelChrome;
            var hotbarHeight = slotSize + personalPanelChrome;
            requiredHeight = astronautHeight + panelSeparation + hotbarHeight;
        }
        else
        {
            const int astronautColumns = 6;
            const int astronautRows = 4;
            storageColumns = Math.Clamp(storageColumns, 1, Math.Max(1, storageSlots));
            var storageRows = Mathf.CeilToInt(storageSlots / (float)storageColumns);
            var storageChrome = showFuelModule ? shipPanelChrome : personalPanelChrome;
            var astronautWidth = (astronautColumns * slotSize) +
                                 ((astronautColumns - 1) * gap) + (2 * panelMarginX);
            var toolWidth = includeTools
                ? InventoryUiConfiguration.ToolSlotSize + 5 + (2 * panelSeparation)
                : 0;
            var personalWidth = astronautWidth + toolWidth;
            var storageWidth = (storageColumns * slotSize) +
                               ((storageColumns - 1) * gap) + (2 * panelMarginX);
            var actionWidth = InventoryUiConfiguration.CompactActionButtonSize;
            requiredWidth = personalWidth + storageWidth + actionWidth + (2 * panelSeparation);
            var astronautHeight = (astronautRows * slotSize) +
                                  ((astronautRows - 1) * gap) + personalPanelChrome;
            var storageHeight = (storageRows * slotSize) +
                                ((storageRows - 1) * gap) + storageChrome;
            requiredHeight = Mathf.Max(astronautHeight, storageHeight);
        }

        return new ResponsiveMetrics(
            false,
            includeStorage ? storageColumns : 0,
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
        if (!Mathf.IsEqualApprox(metrics.SlotSize, InventoryUiConfiguration.InventorySlotSize) ||
            metrics.Gap != InventoryUiConfiguration.InventorySlotGap)
        {
            throw new InvalidOperationException(
                $"Inventory {mode} changed slot size or spacing at {size.X}x{size.Y}.");
        }

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
        _toast.Visible = false;
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

        _statusTween?.Kill();
        if (feedback == TransferFeedback.Neutral || string.IsNullOrWhiteSpace(message))
        {
            _toast.Visible = false;
            _toastMessage.Text = string.Empty;
            return;
        }

        _toastMessage.Text = message;
        _toastMessage.Modulate = feedback switch
        {
            TransferFeedback.Success => new Color(0.55f, 1, 0.78f, 1),
            TransferFeedback.Failure => new Color(1, 0.58f, 0.58f, 1),
            _ => Colors.White,
        };
        _toast.Visible = true;
        _toast.Modulate = Colors.White;

        _statusTween = CreateTween();
        _statusTween.SetEase(Tween.EaseType.Out);
        _statusTween.SetTrans(Tween.TransitionType.Cubic);
        _statusTween.TweenInterval(feedback == TransferFeedback.Failure ? 1.25 : 0.85);
        _statusTween.TweenProperty(_toast, new NodePath("modulate:a"), 0.0f, 0.22);
        _statusTween.TweenCallback(Callable.From(() => _toast.Visible = false));
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
            InventoryTransferFailure.ItemNotAccepted => "Transfer nicht möglich: Gegenstandstyp ist für diesen Slot ungültig.",
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

    private void ApplyToastStyle()
    {
        _toast.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.004f, 0.025f, 0.036f, 0.97f),
            BorderColor = new Color(0.1f, 0.48f, 0.61f, 0.92f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            ShadowColor = new Color(0, 0, 0, 0.42f),
            ShadowSize = 5,
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
        theme.SetStylebox("separator", "VSeparator", new StyleBoxLine
        {
            Color = new Color(0.42f, 0.46f, 0.49f, 0.72f),
            Thickness = 1,
            Vertical = true,
        });
        return theme;
    }
}

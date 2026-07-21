using Godot;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Player;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Settings;
using SpaceFactory.Core.Ships.Docking;
using SpaceFactory.Core.Ships.Fuel;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Exploration;
using SpaceFactory.Core.Hazards;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;
using SpaceFactory.Infrastructure.Data;
using SpaceFactory.Infrastructure.Persistence;
using SpaceFactory.Presentation.Building;
using SpaceFactory.Presentation.Info;
using SpaceFactory.Presentation.InventoryUI;
using SpaceFactory.Presentation.Player;
using SpaceFactory.Presentation.Settings;
using SpaceFactory.Presentation.Ship;
using SpaceFactory.Presentation.UI;
using SpaceFactory.Presentation.World;
using SpaceFactory.Presentation.WorldMap;

namespace SpaceFactory.Presentation.Bootstrap;

public partial class GameRoot : Node
{
    private const long Seed = 741029384;
    private const int SectorSize = 5000;
    private const int StreamingRadius = 2;
    private const int MinimapScanCandidateRadius = 3;
    private const double MinimapScanRadiusWorld = CircularMinimapControl.DefaultScanRadius;
    private const int UnpilotedShipStreamingRadius = 1;
    private const double StreamingCheckIntervalSeconds = 0.2;
    private const double DockingCheckIntervalSeconds = 0.1;
    private const double PowerMenuRefreshIntervalSeconds = 0.25;
    private static readonly WorldGenerationSettings GenerationSettings = new(
        SectorSize,
        0,
        8,
        new Dictionary<AsteroidSize, double>
        {
            [AsteroidSize.Tiny] = 0.20,
            [AsteroidSize.Small] = 0.45,
            [AsteroidSize.Medium] = 0.25,
            [AsteroidSize.Large] = 0.09,
            [AsteroidSize.Huge] = 0.01,
        },
        MinimumCometSpacing: 180,
        FieldCellSizeInSectors: 16,
        FieldSpawnChance: WorldGenerationDefaults.FieldSpawnChance,
        MinimumFieldRadiusInSectors: 1.5,
        MaximumFieldRadiusInSectors: 3.8,
        MinimumFieldGapInSectors: 3,
        MaximumFieldGapInSectors: 7,
        LoneCometChancePerSector: WorldGenerationDefaults.LoneCometChancePerSector,
        ExtremeCometSeparationInSectors: 6,
        EnableStartingDiscoveryField: true,
        StartingFieldCenterSectorX: 2.75,
        StartingFieldCenterSectorY: 0.5,
        StartingFieldMajorRadiusInSectors: 2.2,
        StartingFieldMinorRadiusInSectors: 1.45,
        StartingFieldIntensity: 0.85,
        StartingSafeRadius: 900,
        StartingSafeCenterX: SectorSize / 2.0,
        StartingSafeCenterY: SectorSize / 2.0);
    private readonly SectorContentGenerator _sectorContentGenerator = new(
        new DeterministicWorldGenerator(),
        new ResourceDepositGenerator());
    private readonly Dictionary<SectorCoordinate, SectorView> _loadedSectors = [];
    private readonly Dictionary<SectorCoordinate, GeneratedSectorContent> _preparedSectors = [];
    private readonly HashSet<SectorCoordinate> _currentlyScannedSectors = [];
    private readonly ExplorationMapService _explorationMap = new(SectorSize, Seed);
    private RadiationExposureLevel _lastRadiationLevel;
    private readonly SlotInventory _astronautInventory = CreatePlayerInventory(InventoryConfiguration.AstronautSlotCount);
    private readonly HotbarState _hotbarState = new(CreatePlayerInventory(InventoryConfiguration.HotbarSlotCount));
    private readonly ToolInventoryState _toolInventoryState = ToolInventoryState.Create();
    private readonly SlotInventory _shipInventory = CreatePlayerInventory(InventoryConfiguration.ShipSlotCount);
    private readonly ShipInteractionPressGate _shipInteractionPressGate = new();
    private readonly ShipDockingPressGate _shipDockingPressGate = new();
    private readonly ShipDockingConfiguration _dockingConfiguration = ShipDockingConfiguration.Default;
    private Node2D _sectorContainer = null!;
    private PlayerShipController _ship = null!;
    private OnFootPlayerController _onFootPlayer = null!;
    private WorldMapController _worldMap = null!;
    private ResourceHud _resourceHud = null!;
    private HotbarController _hotbar = null!;
    private InventoryMenuController _inventoryMenu = null!;
    private SettingsMenuController _settingsMenu = null!;
    private InfoMenuController _infoMenu = null!;
    private BuildMenuController _buildMenu = null!;
    private MachinePanelController _machinePanel = null!;
    private CanvasLayer _powerMenuLayer = null!;
    private PowerMenuControl _powerMenu = null!;
    private FactoryRuntimeController _factory = null!;
    private IReadOnlyList<ResourceDefinition> _resourceCatalog = [];
    private JsonResourceStateStore _resourceStateStore = null!;
    private SectorCoordinate _currentSector;
    private SectorCoordinate _streamingCenter;
    private SectorCoordinate _streamedShipSector;
    private double _streamingCheckElapsed;
    private double _dockingCheckElapsed;
    private double _powerMenuRefreshElapsed;
    private PlayerControlMode _controlMode = PlayerControlMode.Ship;
    private PrimaryUiMode _primaryUiMode = PrimaryUiMode.None;
    private ShipInteractionAction _availableShipInteraction;
    private ShipDockingDecision _availableDockingDecision =
        ShipDockingDecision.Blocked(ShipDockingBlockReason.NoCandidate);
    private ShipDockingContext _availableDockingContext;
    private AsteroidView? _availableDockingComet;
    private Vector2? _availableSafeExitPosition;
    private ShipDockingAction _displayedDockingAction;
    private MachineState? _availableMachineInteraction;
    private PowerInteractionTarget? _availablePowerInteraction;
    private PowerInteractionTarget? _displayedPowerInteraction;
    private MachineState? _machinePanelTarget;
    private MachineState? _inventoryStorageTarget;
    private PowerInteractionTarget? _powerMenuTarget;
    private MachineInstanceId? _displayedMachineInteractionId;

    public ExplorationMapService ExplorationMap => _explorationMap;

    public override void _Ready()
    {
        _sectorContainer = GetNode<Node2D>("World/Sectors");
        _ship = GetNode<PlayerShipController>("World/PlayerShip");
        _onFootPlayer = GetNode<OnFootPlayerController>("World/OnFootPlayer");
        _worldMap = GetNode<WorldMapController>("WorldMap/Controller");
        _resourceHud = GetNode<ResourceHud>("ResourceHud");
        _hotbar = GetNode<HotbarController>("Hotbar");
        _inventoryMenu = GetNode<InventoryMenuController>("InventoryMenu");
        _settingsMenu = GetNode<SettingsMenuController>("SettingsMenu");
        _infoMenu = GetNode<InfoMenuController>("InfoMenu");
        _buildMenu = GetNode<BuildMenuController>("BuildMenu");
        _machinePanel = GetNode<MachinePanelController>("MachinePanel");
        _factory = GetNode<FactoryRuntimeController>("World/FactoryRuntime");
        _powerMenuLayer = new CanvasLayer
        {
            Name = "PowerMenuLayer",
            Layer = 80,
        };
        AddChild(_powerMenuLayer);
        _powerMenu = new PowerMenuControl { Name = "PowerMenu" };
        _powerMenu.Closed += HandlePowerMenuClosed;
        _powerMenu.NetworkEnabledChangeRequested += HandlePowerNetworkEnabledChanged;
        _powerMenu.PortEnabledChangeRequested += HandlePowerPortEnabledChanged;
        _powerMenu.SourceEnabledChangeRequested += HandlePowerSourceEnabledChanged;
        _powerMenu.DisconnectPortRequested += HandlePowerPortDisconnectRequested;
        _powerMenuLayer.AddChild(_powerMenu);
        _inventoryMenu.Closed += HandleInventoryClosed;
        _inventoryMenu.ShipFuelTransferRequested += HandleShipFuelTransferRequested;
        _inventoryMenu.InventoryChanged += HandleInventoryChanged;
        _inventoryMenu.WorldDropRequested += HandleWorldDropRequested;
        _inventoryMenu.HotbarItemActivationRequested += HandleInventoryHotbarActivationRequested;
        _inventoryMenu.ToolItemActivationRequested += HandleInventoryToolActivationRequested;
        _hotbar.ContextActionRequested += HandleHotbarContextActionRequested;
        _worldMap.MapVisibilityChanged += HandleMapVisibilityChanged;
        _worldMap.MapOpenRequested += HandleMapOpenRequested;
        _settingsMenu.Closed += HandleSettingsClosed;
        _settingsMenu.InputBindingsChanged += HandleInputBindingsChanged;
        _settingsMenu.InfoRequested += HandleInfoRequested;
        _infoMenu.BackRequested += HandleInfoBackRequested;
        _buildMenu.MachineSelected += HandleBuildMachineSelected;
        _buildMenu.Closed += HandleBuildMenuClosed;
        _machinePanel.RecipeSelectionRequested += HandleMachineRecipeSelected;
        _machinePanel.ActiveStateChangeRequested += HandleMachineActiveChanged;
        _machinePanel.LoadInputsRequested += HandleMachineLoadInputsRequested;
        _machinePanel.CollectOutputsRequested += HandleMachineCollectOutputsRequested;
        _machinePanel.ReturnInputsRequested += HandleMachineReturnInputsRequested;
        _machinePanel.GeneratorTankFillRequested += HandleGeneratorTankFillRequested;
        _machinePanel.GeneratorTankDrainRequested += HandleGeneratorTankDrainRequested;
        _machinePanel.PersonalInventoryChanged += HandleInventoryChanged;
        _machinePanel.WorldDropRequested += HandleWorldDropRequested;
        _machinePanel.Closed += HandleMachinePanelClosed;
        _factory.FactoryStateChanged += HandleFactoryStateChanged;
        _factory.BuildCatalogChanged += HandleBuildCatalogChanged;
        _factory.MachineInteractionRequested += OpenMachinePanel;
        _ship.BoostFuelUnavailable += HandleBoostFuelUnavailable;
        _resourceCatalog = new JsonResourceCatalogLoader().Load("res://data/resources/resource_definitions.json");
        _resourceStateStore = new JsonResourceStateStore();
        _worldMap.SetDataSource(_explorationMap);
        _worldMap.SetResourceCatalog(_resourceCatalog);
        _inventoryMenu.Initialize(
            _astronautInventory,
            _hotbarState.Inventory,
            _toolInventoryState,
            _shipInventory,
            _resourceCatalog,
            DefaultProductionItemCatalog.Instance);
        var itemPresentation = ItemPresentationCatalog.Create(
            _resourceCatalog,
            DefaultProductionItemCatalog.Instance);
        _machinePanel.ConfigurePersonalInventory(
            _astronautInventory,
            itemPresentation,
            _factory.PreviewOpenMachineSlotTransfer,
            _factory.TransferOpenMachineSlot,
            _factory.DeleteOpenMachineStack);
        _hotbar.Initialize(
            _hotbarState,
            _toolInventoryState,
            itemPresentation,
            HandleHotbarSlotSelected,
            ActivateHandSlot);
        _factory.Initialize(
            _astronautInventory,
            _hotbarState.Inventory,
            _toolInventoryState,
            itemPresentation,
            new JsonFactoryStateStore(),
            () => _ship.FuelTank.CurrentFuel,
            () => _ship.FuelTank.CurrentFuelType,
            _ship.RestoreFuel,
            () => _hotbarState.ActiveSlotIndex,
            RestoreActiveHotbarSlot,
            message => _resourceHud.ShowMessage(message));
        _factory.AttachShipInventory(_shipInventory);
        _factory.AttachShipPower(_ship, SectorSize);
        _factory.AttachWorldItemContext(
            () => _controlMode == PlayerControlMode.OnFoot
                ? _onFootPlayer.GlobalPosition
                : _ship.GlobalPosition,
            () => _controlMode == PlayerControlMode.OnFoot
                ? _onFootPlayer.Velocity
                : _ship.Velocity,
            () => _controlMode == PlayerControlMode.OnFoot && _primaryUiMode == PrimaryUiMode.None);
        _ship.FuelChanged += HandleShipFuelChanged;
        RefreshBuildMenuCatalog();
        _buildMenu.SetBuildActionLabel(InputBindingFormatter.FormatAction("build_menu"));
        RefreshShipFuelInventoryUi();
        _onFootPlayer.Initialize(
            _astronautInventory,
            _resourceCatalog,
            _resourceHud,
            IsGameplayInputBlocked,
            IsMovementInputBlocked);
        RefreshHotbarAndEquipment();
        SetControlMode(PlayerControlMode.Ship, updateUi: false);
        var initialSector = ToSectorCoordinate(_ship.GlobalPosition);
        LoadAround(initialSector, GetStreamingCenter(initialSector));
        UpdateUi();
#if DEBUG
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            RunHeadlessSmokeTest();
        }
#endif
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_factory))
        {
            _factory.SaveNow();
            _factory.FactoryStateChanged -= HandleFactoryStateChanged;
            _factory.BuildCatalogChanged -= HandleBuildCatalogChanged;
            _factory.MachineInteractionRequested -= OpenMachinePanel;
        }

        if (GodotObject.IsInstanceValid(_machinePanel))
        {
            _machinePanel.PersonalInventoryChanged -= HandleInventoryChanged;
            _machinePanel.WorldDropRequested -= HandleWorldDropRequested;
            _machinePanel.GeneratorTankFillRequested -= HandleGeneratorTankFillRequested;
            _machinePanel.GeneratorTankDrainRequested -= HandleGeneratorTankDrainRequested;
        }

        if (GodotObject.IsInstanceValid(_powerMenu))
        {
            _powerMenu.Closed -= HandlePowerMenuClosed;
            _powerMenu.NetworkEnabledChangeRequested -= HandlePowerNetworkEnabledChanged;
            _powerMenu.PortEnabledChangeRequested -= HandlePowerPortEnabledChanged;
            _powerMenu.SourceEnabledChangeRequested -= HandlePowerSourceEnabledChanged;
            _powerMenu.DisconnectPortRequested -= HandlePowerPortDisconnectRequested;
        }

        if (GodotObject.IsInstanceValid(_worldMap))
        {
            _worldMap.MapOpenRequested -= HandleMapOpenRequested;
        }

        if (GodotObject.IsInstanceValid(_inventoryMenu))
        {
            _inventoryMenu.InventoryChanged -= HandleInventoryChanged;
            _inventoryMenu.WorldDropRequested -= HandleWorldDropRequested;
            _inventoryMenu.ShipFuelTransferRequested -= HandleShipFuelTransferRequested;
            _inventoryMenu.HotbarItemActivationRequested -= HandleInventoryHotbarActivationRequested;
            _inventoryMenu.ToolItemActivationRequested -= HandleInventoryToolActivationRequested;
        }

        if (GodotObject.IsInstanceValid(_hotbar))
        {
            _hotbar.ContextActionRequested -= HandleHotbarContextActionRequested;
        }

        if (GodotObject.IsInstanceValid(_ship))
        {
            _ship.BoostFuelUnavailable -= HandleBoostFuelUnavailable;
            _ship.FuelChanged -= HandleShipFuelChanged;
        }
    }

    public override void _Process(double delta)
    {
        _shipInteractionPressGate.Advance(delta);
        _shipDockingPressGate.Advance(delta);
        _worldMap.SetShipState(
            _ship.GlobalPosition,
            _ship.Rotation,
            _controlMode == PlayerControlMode.Ship);
        _resourceHud.SetShipFuel(
            _ship.FuelTank.CurrentFuel,
            _controlMode == PlayerControlMode.Ship);
        var isOnFoot = _controlMode == PlayerControlMode.OnFoot;
        var radiationRate = _factory.UpdatePlayerRadiation(
            delta,
            isOnFoot ? _onFootPlayer.GlobalPosition : _ship.GlobalPosition,
            isOnFoot);
        var radiationLevel = _factory.RadiationExposure.Level;
        _onFootPlayer.SetRadiationEffects(
            _factory.RadiationExposure.MovementMultiplier,
            _factory.RadiationExposure.MiningEfficiencyMultiplier);
        _resourceHud.SetRadiation(
            _factory.RadiationExposure.AccumulatedDose,
            radiationRate,
            _factory.RadiationExposure.Integrity,
            radiationLevel,
            isOnFoot && (_factory.RadiationExposure.AccumulatedDose > 0.05 || radiationRate > 0.001));
        if (isOnFoot && radiationLevel != _lastRadiationLevel &&
            radiationLevel != RadiationExposureLevel.Safe)
        {
            _resourceHud.ShowMessage(
                radiationLevel == RadiationExposureLevel.Critical
                    ? "WARNUNG: Kritische Strahlenbelastung"
                    : "Warnung: Erhöhte Strahlenbelastung",
                3.2);
        }

        _lastRadiationLevel = radiationLevel;
        if (_inventoryMenu.IsOpen && _controlMode == PlayerControlMode.Ship)
        {
            RefreshShipFuelInventoryUi();
        }

        if (_primaryUiMode == PrimaryUiMode.PowerMenu)
        {
            _powerMenuRefreshElapsed += delta;
            if (_powerMenuRefreshElapsed >= PowerMenuRefreshIntervalSeconds)
            {
                _powerMenuRefreshElapsed = 0;
                RefreshPowerMenu();
            }
        }

        if (_primaryUiMode == PrimaryUiMode.PauseMenu)
        {
            CancelActiveDismantling();
            return;
        }

        UpdateDismantlingInteraction(delta);

        _dockingCheckElapsed += delta;
        RefreshDockingAvailability();

        RefreshShipInteractionState();
        _streamingCheckElapsed += delta;
        if (_streamingCheckElapsed < StreamingCheckIntervalSeconds)
        {
            return;
        }

        _streamingCheckElapsed = 0;
        var activePosition = _onFootPlayer.IsControlActive ? _onFootPlayer.GlobalPosition : _ship.GlobalPosition;
        var coordinate = new SectorCoordinate(
            Mathf.FloorToInt(activePosition.X / SectorSize),
            Mathf.FloorToInt(activePosition.Y / SectorSize));
        var streamingCenter = GetStreamingCenter(coordinate);
        var shipCoordinate = ToSectorCoordinate(_ship.GlobalPosition);
        if (coordinate != _currentSector ||
            streamingCenter != _streamingCenter ||
            (_controlMode == PlayerControlMode.OnFoot && shipCoordinate != _streamedShipSector))
        {
            LoadAround(coordinate, streamingCenter);
            UpdateUi();
        }

        if (_controlMode == PlayerControlMode.Ship)
        {
            EnsureScannedAround(_ship.GlobalPosition);
            MarkNearbyCometsVisited(_ship.GlobalPosition);
        }
    }

    public override void _Input(InputEvent @event)
    {
        var releasedInteraction = false;
        if (@event.IsActionReleased("ship_interaction"))
        {
            _shipInteractionPressGate.Release();
            releasedInteraction = true;
        }

        if (@event.IsActionReleased("ship_docking"))
        {
            _shipDockingPressGate.TryConsume(isPressed: false);
            releasedInteraction = true;
        }

        if (releasedInteraction)
        {
            return;
        }

        if (!IsSingleActionPress(@event))
        {
            return;
        }

        if (TryHandleMenuBackInput(@event))
        {
            return;
        }

        if (_primaryUiMode == PrimaryUiMode.PauseMenu)
        {
            return;
        }

        // _Input runs before Control._GuiInput. Leave pointer events over an interactive Control
        // untouched so hotbar slots, the minimap and menus receive the click instead of placing
        // or dismantling an object in the world behind them.
        if (@event is InputEventMouseButton { Pressed: true } pointerEvent &&
            IsPointerOverInteractiveUi() &&
            !CanSelectHotbarWithWheelOverHud(pointerEvent))
        {
            return;
        }

        if (_controlMode == PlayerControlMode.OnFoot && _primaryUiMode == PrimaryUiMode.None)
        {
            if (@event.IsActionPressed(InputActionCatalog.Get(GameAction.ActivateHandSlot).InputMapAction))
            {
                ActivateHandSlot();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_toolInventoryState.IsHandModeActive &&
                @event.IsActionPressed(InputActionCatalog.Get(GameAction.PreviousTool).InputMapAction))
            {
                SelectRelativeTool(previous: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_toolInventoryState.IsHandModeActive &&
                @event.IsActionPressed(InputActionCatalog.Get(GameAction.NextTool).InputMapAction))
            {
                SelectRelativeTool(previous: false);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_settingsMenu.HotbarMouseWheelEnabled &&
                (!_factory.IsPlacementActive || _factory.IsHotbarPlacementActive) &&
                @event is InputEventMouseButton
                {
                    Pressed: true,
                    ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
                } wheel)
            {
                SelectRelativeHotbar(wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (_controlMode == PlayerControlMode.OnFoot &&
            _primaryUiMode == PrimaryUiMode.None &&
            TryGetPressedHotbarSlot(@event, out var hotbarSlotIndex))
        {
            SelectHotbarSlot(hotbarSlotIndex);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_factory.IsPlacementActive && _primaryUiMode == PrimaryUiMode.None)
        {
            if (@event.IsActionPressed(InputActionCatalog.Get(GameAction.RotateBuilding).InputMapAction))
            {
                _factory.RotatePlacement(1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("build_menu"))
            {
                _factory.CancelPlacement();
                SetPrimaryUiMode(PrimaryUiMode.BuildMenu);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true } placementMouse)
            {
                switch (placementMouse.ButtonIndex)
                {
                    case MouseButton.Left:
                        if (_factory.TryPlaceSelectedMachineAtViewportPosition(placementMouse.Position))
                        {
                            RefreshHotbarAndEquipment();
                        }
                        GetViewport().SetInputAsHandled();
                        return;
                    case MouseButton.Right:
                        _factory.CancelPlacement();
                        _resourceHud.ShowMessage("Platzierung abgebrochen");
                        GetViewport().SetInputAsHandled();
                        return;
                }
            }
        }

        if (_controlMode == PlayerControlMode.OnFoot &&
            _primaryUiMode == PrimaryUiMode.None &&
            !_factory.IsPlacementActive &&
            GetActiveToolItemId() == ProductionItemIds.MachineDismantlingTool &&
            @event.IsActionPressed(InputActionCatalog.Get(GameAction.UseMiningTool).InputMapAction))
        {
            if (_factory.TryBeginDismantlingAtViewportPosition(
                    @event is InputEventMouseButton dismantleMouse
                        ? dismantleMouse.Position
                        : GetViewport().GetMousePosition(),
                    _onFootPlayer.GlobalPosition,
                    ProductionItemIds.MachineDismantlingTool))
            {
                UpdateDismantlingVisual();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("build_menu"))
        {
            if (_controlMode != PlayerControlMode.OnFoot)
            {
                _resourceHud.ShowMessage("Bauen ist nur als Astronaut möglich");
            }
            else
            {
                SetPrimaryUiMode(
                    _primaryUiMode == PrimaryUiMode.BuildMenu
                        ? PrimaryUiMode.None
                        : PrimaryUiMode.BuildMenu);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("inventory"))
        {
            SetPrimaryUiMode(
                _primaryUiMode == PrimaryUiMode.Inventory
                    ? PrimaryUiMode.None
                    : PrimaryUiMode.Inventory);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("open_map"))
        {
            if (_controlMode != PlayerControlMode.Ship)
            {
                _resourceHud.ShowMessage("Karte nur im Raumschiff verfügbar");
            }
            else
            {
                SetPrimaryUiMode(
                    _primaryUiMode == PrimaryUiMode.Map
                        ? PrimaryUiMode.None
                        : PrimaryUiMode.Map);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ship_docking") &&
            _controlMode == PlayerControlMode.Ship &&
            _primaryUiMode == PrimaryUiMode.None)
        {
            RefreshDockingAvailability(force: true);
            if (_shipDockingPressGate.TryConsume(isPressed: true))
            {
                ExecuteDockingAction();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ship_interaction") && _primaryUiMode == PrimaryUiMode.None)
        {
            if (_controlMode == PlayerControlMode.Ship)
            {
                RefreshDockingAvailability(force: true);
            }
            RefreshShipInteractionState();
            if (_availableShipInteraction != ShipInteractionAction.None ||
                _availableMachineInteraction is not null ||
                _availablePowerInteraction is not null)
            {
                if (_shipInteractionPressGate.TryPress())
                {
                    ExecuteShipInteraction();
                }

                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void LoadAround(SectorCoordinate currentSector, SectorCoordinate streamingCenter)
    {
        _currentSector = currentSector;
        _streamingCenter = streamingCenter;
        var required = new HashSet<SectorCoordinate>();
        for (var y = streamingCenter.Y - StreamingRadius; y <= streamingCenter.Y + StreamingRadius; y++)
        {
            for (var x = streamingCenter.X - StreamingRadius; x <= streamingCenter.X + StreamingRadius; x++)
            {
                required.Add(new SectorCoordinate(x, y));
            }
        }

        _streamedShipSector = ToSectorCoordinate(_ship.GlobalPosition);
        if (_controlMode == PlayerControlMode.OnFoot)
        {
            for (var y = _streamedShipSector.Y - UnpilotedShipStreamingRadius;
                 y <= _streamedShipSector.Y + UnpilotedShipStreamingRadius;
                 y++)
            {
                for (var x = _streamedShipSector.X - UnpilotedShipStreamingRadius;
                     x <= _streamedShipSector.X + UnpilotedShipStreamingRadius;
                     x++)
                {
                    required.Add(new SectorCoordinate(x, y));
                }
            }
        }

        foreach (var coordinate in required)
        {
            EnsureSectorLoaded(coordinate);
        }

        foreach (var coordinate in _loadedSectors.Keys.Where(key => !required.Contains(key)).ToArray())
        {
            _factory.UnregisterSector(_loadedSectors[coordinate]);
            _loadedSectors[coordinate].QueueFree();
            _loadedSectors.Remove(coordinate);
        }

        if (_controlMode == PlayerControlMode.Ship)
        {
            EnsureScannedAround(_ship.GlobalPosition);
        }
        else
        {
            _currentlyScannedSectors.Clear();
            _explorationMap.SetChunkStates(_loadedSectors.Keys, _currentlyScannedSectors);
            TrimPreparedSectors(_loadedSectors.Keys);
        }

        UpdateCollisionDetails(currentSector);
    }

    private void EnsureSectorLoaded(SectorCoordinate coordinate)
    {
        if (_loadedSectors.ContainsKey(coordinate))
        {
            return;
        }

        var sector = new SectorView { Name = $"Sector_{coordinate.X}_{coordinate.Y}" };
        sector.ResourceExhausted += resourceId => _explorationMap.MarkResourceMissing(resourceId);
        _sectorContainer.AddChild(sector);
        var content = GetOrPrepareSector(coordinate);
        sector.Display(
            content,
            SectorSize,
            _resourceCatalog,
            _resourceStateStore);
        _loadedSectors.Add(coordinate, sector);
        _factory.RegisterSector(sector);
    }

    private GeneratedSectorContent GetOrPrepareSector(SectorCoordinate coordinate)
    {
        if (_preparedSectors.TryGetValue(coordinate, out var content))
        {
            return content;
        }

        content = _sectorContentGenerator.Generate(CreateRequest(coordinate), _resourceCatalog);
        _preparedSectors.Add(coordinate, content);
        return content;
    }

    private void ScanSectorForMap(GeneratedSectorContent content)
    {
        if (_explorationMap.GetChunkStatus(content.Sector.Coordinate) != ChunkDiscoveryStatus.Unknown)
        {
            return;
        }

        _explorationMap.Scan(content);
        _factory.SynchronizeResourceDiscoveries(
            content.ResourceDepositsByComet.Values
                .SelectMany(deposits => deposits)
                .Select(deposit => deposit.ResourceId));
        foreach (var deposit in content.ResourceDepositsByComet.Values.SelectMany(deposits => deposits))
        {
            if (_resourceStateStore.GetRemainingAmount(deposit) <= 0)
            {
                _explorationMap.MarkResourceMissing(deposit.Id);
            }
        }
    }

    private void EnsureScannedAround(Vector2 shipPosition)
    {
        var retained = _loadedSectors.Keys.ToHashSet();
        var scanCenter = new SectorCoordinate(
            Mathf.FloorToInt(shipPosition.X / SectorSize),
            Mathf.FloorToInt(shipPosition.Y / SectorSize));
        var requiredScannedSectors = new HashSet<SectorCoordinate>();
        for (var y = scanCenter.Y - MinimapScanCandidateRadius;
             y <= scanCenter.Y + MinimapScanCandidateRadius;
             y++)
        {
            for (var x = scanCenter.X - MinimapScanCandidateRadius;
                 x <= scanCenter.X + MinimapScanCandidateRadius;
                 x++)
            {
                var coordinate = new SectorCoordinate(x, y);
                if (!SectorIntersectsScanRadius(coordinate, shipPosition, MinimapScanRadiusWorld))
                {
                    continue;
                }

                retained.Add(coordinate);
                requiredScannedSectors.Add(coordinate);
                if (!_currentlyScannedSectors.Contains(coordinate) &&
                    _explorationMap.GetChunkStatus(coordinate) == ChunkDiscoveryStatus.Unknown)
                {
                    ScanSectorForMap(GetOrPrepareSector(coordinate));
                }
            }
        }

        _currentlyScannedSectors.Clear();
        _currentlyScannedSectors.UnionWith(requiredScannedSectors);
        _explorationMap.SetChunkStates(_loadedSectors.Keys, _currentlyScannedSectors);
        TrimPreparedSectors(retained);
    }

    private static bool SectorIntersectsScanRadius(
        SectorCoordinate coordinate,
        Vector2 scanCenter,
        double scanRadius)
    {
        var minimumX = coordinate.X * (double)SectorSize;
        var minimumY = coordinate.Y * (double)SectorSize;
        var closestX = Math.Clamp(scanCenter.X, minimumX, minimumX + SectorSize);
        var closestY = Math.Clamp(scanCenter.Y, minimumY, minimumY + SectorSize);
        var deltaX = scanCenter.X - closestX;
        var deltaY = scanCenter.Y - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= scanRadius * scanRadius;
    }

    private void MarkNearbyCometsVisited(Vector2 shipPosition)
    {
        var worldPosition = new WorldPosition(shipPosition.X, shipPosition.Y);
        foreach (var comet in _explorationMap
                     .GetScannedChunksAround(worldPosition, SectorSize * 1.5)
                     .SelectMany(chunk => chunk.Comets)
                     .Where(comet => comet.Exists && !comet.IsVisited))
        {
            var deltaX = comet.WorldPosition.X - shipPosition.X;
            var deltaY = comet.WorldPosition.Y - shipPosition.Y;
            var visitRadius = comet.Radius + 500;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= visitRadius * visitRadius)
            {
                _explorationMap.MarkCometVisited(comet.Id);
            }
        }
    }

    private void TrimPreparedSectors(IEnumerable<SectorCoordinate> retainedCoordinates)
    {
        var retained = retainedCoordinates.ToHashSet();
        foreach (var coordinate in _preparedSectors.Keys.Where(key => !retained.Contains(key)).ToArray())
        {
            _preparedSectors.Remove(coordinate);
        }
    }

    private static SectorGenerationRequest CreateRequest(SectorCoordinate coordinate) => new(
        new WorldSeed(Seed),
        coordinate,
        GenerationSettings);

    private SectorCoordinate GetStreamingCenter(SectorCoordinate currentSector)
    {
        if (!_ship.IsControlActive || _ship.Velocity.LengthSquared() < 1)
        {
            return currentSector;
        }

        var direction = _ship.Velocity.Normalized();
        return new SectorCoordinate(
            currentSector.X + Math.Sign(direction.X),
            currentSector.Y + Math.Sign(direction.Y));
    }

    private void UpdateUi()
    {
        _worldMap.SetCurrentSector(_currentSector);
    }

    private void UpdateCollisionDetails(SectorCoordinate currentSector)
    {
        foreach (var pair in _loadedSectors)
        {
            var distance = Math.Max(
                Math.Abs(pair.Key.X - currentSector.X),
                Math.Abs(pair.Key.Y - currentSector.Y));
            pair.Value.SetDetailedCollisions(distance <= 1);
        }
    }

    private void TryExitShip()
    {
        if (_availableShipInteraction != ShipInteractionAction.ExitShip)
        {
            return;
        }

        var safePosition = _availableSafeExitPosition;
        if (safePosition is null)
        {
            return;
        }

        var astronautVelocity = ShipDriftRules.CalculateAstronautExitVelocity(
            new ShipVelocity(_ship.Velocity.X, _ship.Velocity.Y),
            _dockingConfiguration);
        _onFootPlayer.GlobalPosition = safePosition.Value;
        _onFootPlayer.Rotation = _ship.Rotation;
        SetControlMode(PlayerControlMode.OnFoot);
        _onFootPlayer.ApplyInheritedVelocity(new Vector2(
            (float)astronautVelocity.X,
            (float)astronautVelocity.Y));
    }

    private void TryEnterShip()
    {
        if (_availableShipInteraction != ShipInteractionAction.EnterShip)
        {
            return;
        }

        SetControlMode(PlayerControlMode.Ship);
    }

    private void ExecuteShipInteraction()
    {
        if (_availablePowerInteraction is { } powerTarget)
        {
            OpenPowerMenu(powerTarget);
            return;
        }

        if (_availableMachineInteraction is { } machine)
        {
            OpenMachinePanel(machine);
            return;
        }

        switch (_availableShipInteraction)
        {
            case ShipInteractionAction.EnterShip:
                TryEnterShip();
                break;
            case ShipInteractionAction.ExitShip:
                TryExitShip();
                break;
        }
    }

    private void RefreshDockingAvailability(bool force = false)
    {
        if (!force && _dockingCheckElapsed < DockingCheckIntervalSeconds)
        {
            return;
        }

        _dockingCheckElapsed = 0;
        _availableDockingComet = null;
        _availableSafeExitPosition = _controlMode == PlayerControlMode.Ship &&
                                     _ship.IsControlActive &&
                                     _primaryUiMode == PrimaryUiMode.None
            ? FindSafeExitPosition()
            : null;
        ShipDockingCandidate? candidate = null;
        var maySearch = _controlMode == PlayerControlMode.Ship &&
            _ship.IsControlActive &&
            _primaryUiMode == PrimaryUiMode.None &&
            !_ship.IsAttached &&
            _ship.Velocity.Length() <= _dockingConfiguration.MaximumAttachmentSpeed;
        if (maySearch && TryFindDockingCandidate(out var foundCandidate, out var comet))
        {
            candidate = foundCandidate;
            _availableDockingComet = comet;
        }

        _availableDockingContext = new ShipDockingContext(
            IsShipControlled: _controlMode == PlayerControlMode.Ship &&
                              _ship.IsControlActive &&
                              _primaryUiMode == PrimaryUiMode.None,
            IsPauseMenuOpen: _primaryUiMode == PrimaryUiMode.PauseMenu,
            ShipSpeed: _ship.Velocity.Length(),
            Candidate: candidate);
        _availableDockingDecision = ShipDockingRules.Evaluate(
            _ship.DockingState,
            _availableDockingContext,
            _dockingConfiguration);
        if (_availableDockingDecision.Action == ShipDockingAction.Detach &&
            _factory.HasShipPowerConnections)
        {
            // Keep the contextual hint and H action on the exact same effective decision.
            // A cabled ship must first disconnect its live networks before detaching.
            _availableDockingDecision = ShipDockingDecision.Blocked(
                ShipDockingBlockReason.ActivePowerConnections);
        }
    }

    private bool TryFindDockingCandidate(
        out ShipDockingCandidate candidate,
        out AsteroidView comet)
    {
        candidate = default;
        comet = null!;
        var searchRadius = PlayerShipController.DockingHullReach +
                           (float)_dockingConfiguration.MaximumAttachmentDistance +
                           48.0f;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = searchRadius },
            Transform = new Transform2D(0, _ship.GlobalPosition),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Exclude = new Godot.Collections.Array<Rid> { _ship.GetRid() },
        };
        var bestSurfaceDistance = double.PositiveInfinity;
        foreach (var result in GetViewport().World2D.DirectSpaceState.IntersectShape(query, 24))
        {
            var collider = result["collider"].AsGodotObject() as AsteroidView;
            if (!GodotObject.IsInstanceValid(collider) || !collider!.SupportsShipDocking ||
                !collider.TryGetClosestSurfacePoint(
                    _ship.GlobalPosition,
                    out var surfacePoint,
                    out var outwardNormal,
                    out var centerDistance))
            {
                continue;
            }

            var surfaceDistance = Math.Max(0, centerDistance - PlayerShipController.DockingHullReach);
            if (surfaceDistance >= bestSurfaceDistance)
            {
                continue;
            }

            var attachmentPosition = surfacePoint +
                                     (outwardNormal * PlayerShipController.DockingCenterClearance);
            var attachmentRotation = outwardNormal.Angle() + (Mathf.Pi * 0.5f);
            var localAttachment = collider.ToLocal(attachmentPosition);
            var hasFreeSurface = HasFreeDockingPose(attachmentPosition, attachmentRotation);
            var hasSafeExit = HasSafeExitPositionAtPose(attachmentPosition, attachmentRotation);
            if (!hasFreeSurface || !hasSafeExit)
            {
                continue;
            }

            candidate = new ShipDockingCandidate(
                collider.CometId,
                surfaceDistance,
                hasFreeSurface,
                hasSafeExit,
                new WorldPosition(localAttachment.X, localAttachment.Y),
                Mathf.Wrap(attachmentRotation - collider.GlobalRotation, -Mathf.Pi, Mathf.Pi));
            comet = collider;
            bestSurfaceDistance = surfaceDistance;
        }

        return comet is not null;
    }

    private bool HasFreeDockingPose(
        Vector2 attachmentPosition,
        float attachmentRotation)
    {
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = PlayerShipController.DockingClearanceSize },
            Transform = new Transform2D(attachmentRotation, attachmentPosition),
            CollisionMask = 1u |
                            ResourceDepositView.ResourceCollisionLayer |
                            MachineView.MachineCollisionLayer,
            CollideWithAreas = true,
            CollideWithBodies = true,
            Exclude = new Godot.Collections.Array<Rid> { _ship.GetRid() },
        };
        return GetViewport().World2D.DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private bool HasSafeExitPositionAtPose(Vector2 shipPosition, float shipRotation)
    {
        var cockpitPosition = shipPosition +
                              new Vector2(0, PlayerShipController.CockpitEntryOffsetY)
                                  .Rotated(shipRotation);
        return EnumerateExitCandidates(cockpitPosition, shipRotation)
            .Any(candidate => IsExitPositionClear(candidate, shipPosition, shipRotation));
    }

    private void ExecuteDockingAction()
    {
        if (!_availableDockingDecision.IsAllowed)
        {
            return;
        }

        if (_availableDockingDecision.Action == ShipDockingAction.Attach &&
            !GodotObject.IsInstanceValid(_availableDockingComet))
        {
            RefreshDockingAvailability(force: true);
            return;
        }

        if (_availableDockingDecision.Action == ShipDockingAction.Detach &&
            _factory.HasShipPowerConnections)
        {
            _resourceHud.ShowMessage("Stromkabel zuerst trennen");
            return;
        }

        var decision = _ship.ExecuteDocking(_availableDockingContext, _availableDockingComet);
        if (!decision.IsAllowed)
        {
            return;
        }

        _factory.SynchronizeShipPowerDocking();

        if (decision.Action == ShipDockingAction.Attach &&
            _ship.DockingState.AttachedCometId is { } cometId)
        {
            _explorationMap.MarkCometVisited(cometId);
            _resourceHud.ShowMessage("Raumschiff am Kometen befestigt");
        }
        else
        {
            _resourceHud.ShowMessage("Raumschiff vom Kometen gelöst");
        }

        RefreshDockingAvailability(force: true);
        RefreshShipInteractionState(forcePromptUpdate: true);
    }

    private void SetControlMode(PlayerControlMode mode, bool updateUi = true)
    {
        if (_factory.IsPlacementActive && mode != PlayerControlMode.OnFoot)
        {
            _factory.CancelPlacement();
        }

        if ((mode == PlayerControlMode.OnFoot && _primaryUiMode == PrimaryUiMode.Map) ||
            (mode == PlayerControlMode.Ship &&
             _primaryUiMode is PrimaryUiMode.BuildMenu or PrimaryUiMode.MachinePanel or PrimaryUiMode.PowerMenu))
        {
            SetPrimaryUiMode(PrimaryUiMode.None);
        }

        _controlMode = mode;
        _ship.SetControlActive(mode == PlayerControlMode.Ship);
        _onFootPlayer.SetControlActive(mode == PlayerControlMode.OnFoot);
        _worldMap.SetShipState(
            _ship.GlobalPosition,
            _ship.Rotation,
            mode == PlayerControlMode.Ship);
        if (_loadedSectors.Count > 0)
        {
            if (mode == PlayerControlMode.Ship)
            {
                EnsureScannedAround(_ship.GlobalPosition);
            }
            else
            {
                _currentlyScannedSectors.Clear();
                _explorationMap.SetChunkStates(_loadedSectors.Keys, _currentlyScannedSectors);
                TrimPreparedSectors(_loadedSectors.Keys);
            }
        }

        RefreshDockingAvailability(force: true);
        RefreshShipInteractionState();
        if (updateUi)
        {
            UpdateUi();
        }

        _resourceHud.SetShipFuel(_ship.FuelTank.CurrentFuel, mode == PlayerControlMode.Ship);
        UpdateHotbarVisibility();
    }

    private void RefreshShipInteractionState(bool forcePromptUpdate = false)
    {
        var inputBlocked = IsGameplayInputBlocked();
        var availableAction = ShipInteractionRules.GetAvailableAction(
            _controlMode,
            _onFootPlayer.IsMining,
            inputBlocked,
            new WorldPosition(_onFootPlayer.GlobalPosition.X, _onFootPlayer.GlobalPosition.Y),
            new WorldPosition(_ship.CockpitEntryPosition.X, _ship.CockpitEntryPosition.Y),
            _ship.CockpitEntryRadius);
        if (availableAction == ShipInteractionAction.ExitShip && _availableSafeExitPosition is null)
        {
            availableAction = ShipInteractionAction.None;
        }
        MachineState? availableMachine = null;
        PowerInteractionTarget? availablePower = null;
        if (_controlMode == PlayerControlMode.OnFoot && !_onFootPlayer.IsMining && !inputBlocked)
        {
            var bestDistance = availableAction == ShipInteractionAction.EnterShip
                ? _onFootPlayer.GlobalPosition.DistanceSquaredTo(_ship.CockpitEntryPosition)
                : float.PositiveInfinity;
            var machineCandidate = _factory.FindNearestInteractiveMachine(_onFootPlayer.GlobalPosition);
            if (machineCandidate is not null)
            {
                var machineDistance = _factory.GetMachineDistanceSquared(
                    machineCandidate,
                    _onFootPlayer.GlobalPosition);
                if (machineDistance < bestDistance)
                {
                    bestDistance = machineDistance;
                    availableAction = ShipInteractionAction.None;
                    availableMachine = machineCandidate;
                }
            }

            var powerCandidate = _factory.FindNearestPowerInteraction(_onFootPlayer.GlobalPosition);
            if (powerCandidate is { } resolvedPowerCandidate)
            {
                var powerDistance = _factory.GetPowerInteractionDistanceSquared(
                    resolvedPowerCandidate,
                    _onFootPlayer.GlobalPosition);
                if (powerDistance < bestDistance)
                {
                    availableAction = ShipInteractionAction.None;
                    availableMachine = null;
                    availablePower = resolvedPowerCandidate;
                }
            }
        }

        var dockingAction = _availableDockingDecision.IsAllowed
            ? _availableDockingDecision.Action
            : ShipDockingAction.None;
        var machineId = availableMachine?.InstanceId;
        if (!forcePromptUpdate &&
            availableAction == _availableShipInteraction &&
            dockingAction == _displayedDockingAction &&
            machineId == _displayedMachineInteractionId &&
            Equals(availablePower, _displayedPowerInteraction))
        {
            return;
        }

        _availableShipInteraction = availableAction;
        _availableMachineInteraction = availableMachine;
        _availablePowerInteraction = availablePower;
        _displayedMachineInteractionId = machineId;
        _displayedPowerInteraction = availablePower;
        _displayedDockingAction = dockingAction;
        var interactionBinding = InputBindingFormatter.FormatAction("ship_interaction");
        string? prompt;
        if (dockingAction != ShipDockingAction.None)
        {
            var dockingBinding = InputBindingFormatter.FormatAction("ship_docking");
            prompt = dockingAction == ShipDockingAction.Attach
                ? $"{dockingBinding} – Am Kometen befestigen"
                : $"{dockingBinding} – Vom Kometen lösen";
        }
        else if (availablePower is not null)
        {
            prompt = $"{interactionBinding} – Stromnetz öffnen";
        }
        else if (availableMachine is not null)
        {
            prompt = $"{interactionBinding} – Maschine öffnen";
        }
        else
        {
            prompt = availableAction switch
            {
                ShipInteractionAction.EnterShip => $"{interactionBinding} – Einsteigen",
                ShipInteractionAction.ExitShip => $"{interactionBinding} – Aussteigen",
                _ => null,
            };
        }

        _resourceHud.SetInteractionPrompt(prompt);
    }

    private void SetPrimaryUiMode(PrimaryUiMode mode)
    {
        if (_primaryUiMode == mode)
        {
            return;
        }

        if (mode != PrimaryUiMode.None)
        {
            CancelActiveDismantling();
        }

        if (mode == PrimaryUiMode.Map && _controlMode != PlayerControlMode.Ship)
        {
            _resourceHud.ShowMessage("Karte nur im Raumschiff verfügbar");
            return;
        }

        if (mode == PrimaryUiMode.BuildMenu && _controlMode != PlayerControlMode.OnFoot)
        {
            _resourceHud.ShowMessage("Bauen ist nur als Astronaut möglich");
            return;
        }

        if (mode == PrimaryUiMode.MachinePanel && _machinePanelTarget is null)
        {
            return;
        }

        if (mode == PrimaryUiMode.PowerMenu &&
            (_controlMode != PlayerControlMode.OnFoot || _powerMenuTarget is null))
        {
            return;
        }

        if (_factory.IsPlacementActive && mode != PrimaryUiMode.None)
        {
            _factory.CancelPlacement();
        }

        var previousMode = _primaryUiMode;
        _primaryUiMode = mode;
        _shipInteractionPressGate.Reset();
        _shipDockingPressGate.SuppressUntilReleased();
        _onFootPlayer.InterruptCurrentAction();

        if (previousMode == PrimaryUiMode.PauseMenu && mode != PrimaryUiMode.PauseMenu)
        {
            _settingsMenu.CloseImmediately();
            _infoMenu.CloseImmediately();
        }

        if (previousMode == PrimaryUiMode.Inventory)
        {
            if (mode == PrimaryUiMode.None)
            {
                _inventoryMenu.Close();
            }
            else
            {
                _inventoryMenu.CloseImmediately();
            }

            _inventoryStorageTarget = null;
        }
        else if (mode != PrimaryUiMode.Inventory && _inventoryMenu.IsOpen)
        {
            _inventoryMenu.CloseImmediately();
        }

        if (previousMode == PrimaryUiMode.Map)
        {
            _worldMap.Close();
        }

        if (previousMode == PrimaryUiMode.BuildMenu)
        {
            if (mode == PrimaryUiMode.None)
            {
                _buildMenu.Close();
            }
            else
            {
                _buildMenu.CloseImmediately();
            }
        }
        else if (mode != PrimaryUiMode.BuildMenu && _buildMenu.IsOpen)
        {
            _buildMenu.CloseImmediately();
        }

        if (previousMode == PrimaryUiMode.MachinePanel)
        {
            if (mode == PrimaryUiMode.None)
            {
                _machinePanel.Close();
            }
            else
            {
                _machinePanel.CloseImmediately();
            }

            _factory.CloseMachine();
            _machinePanelTarget = null;
        }
        else if (mode != PrimaryUiMode.MachinePanel && _machinePanel.IsOpen)
        {
            _machinePanel.CloseImmediately();
            _factory.CloseMachine();
            _machinePanelTarget = null;
        }

        if (previousMode == PrimaryUiMode.PowerMenu)
        {
            _powerMenu.Close();
            _factory.ClosePowerTarget();
            _powerMenuTarget = null;
            _powerMenuRefreshElapsed = 0;
        }
        else if (mode != PrimaryUiMode.PowerMenu && _powerMenu.IsOpen)
        {
            _powerMenu.Close();
            _factory.ClosePowerTarget();
            _powerMenuTarget = null;
            _powerMenuRefreshElapsed = 0;
        }

        switch (mode)
        {
            case PrimaryUiMode.None:
                GetTree().Paused = false;
                break;
            case PrimaryUiMode.Inventory:
                GetTree().Paused = false;
                if (_inventoryStorageTarget is { } storage)
                {
                    _onFootPlayer.StopMovementImmediately();
                    _inventoryMenu.OpenStorageInventory(
                        storage.InputInventory,
                        storage.Definition.DisplayName.ToUpperInvariant(),
                        itemId => MachineInventoryAcceptanceRules.CanStore(storage.Definition, itemId));
                }
                else if (_controlMode == PlayerControlMode.Ship)
                {
                    _inventoryMenu.OpenShipInventory();
                }
                else
                {
                    _onFootPlayer.StopMovementImmediately();
                    _inventoryMenu.OpenAstronautInventory();
                }

                break;
            case PrimaryUiMode.Map:
                GetTree().Paused = false;
                _worldMap.Open();
                break;
            case PrimaryUiMode.BuildMenu:
                GetTree().Paused = false;
                _onFootPlayer.StopMovementImmediately();
                RefreshBuildMenuCatalog();
                _buildMenu.SetBuildActionLabel(InputBindingFormatter.FormatAction("build_menu"));
                _buildMenu.Open();
                break;
            case PrimaryUiMode.MachinePanel:
                GetTree().Paused = false;
                _onFootPlayer.StopMovementImmediately();
                _machinePanel.Open(_factory.CreateMachinePanelViewModel(_machinePanelTarget!));
                break;
            case PrimaryUiMode.PowerMenu:
                GetTree().Paused = false;
                _onFootPlayer.StopMovementImmediately();
                var powerModel = _factory.RefreshOpenPowerViewModel();
                if (powerModel is null)
                {
                    _primaryUiMode = PrimaryUiMode.None;
                    _factory.ClosePowerTarget();
                    _powerMenuTarget = null;
                    break;
                }

                _powerMenuRefreshElapsed = 0;
                _powerMenu.Open(powerModel);
                break;
            case PrimaryUiMode.PauseMenu:
                GetTree().Paused = true;
                _infoMenu.CloseImmediately();
                _settingsMenu.Open();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        RefreshDockingAvailability(force: true);
        RefreshShipInteractionState(forcePromptUpdate: true);
        UpdateHotbarVisibility();
    }

    private void HandleInventoryClosed()
    {
        if (_primaryUiMode == PrimaryUiMode.Inventory)
        {
            _primaryUiMode = PrimaryUiMode.None;
        }

        _inventoryStorageTarget = null;
        RefreshShipInteractionState(forcePromptUpdate: true);
        UpdateHotbarVisibility();
    }

    private void HandleInventoryChanged()
    {
        RefreshHotbarAndEquipment();
        _factory.MarkInventoryChanged();
    }

    private void HandleWorldDropRequested(InventorySlotAddress source, Vector2 screenPosition)
    {
        if (source.InventoryId == InventoryMenuController.StorageInventoryId &&
            _inventoryStorageTarget is { } storage)
        {
            source = new InventorySlotAddress(
                $"machine_input:{storage.InstanceId.Value}",
                source.SlotIndex);
        }

        var worldPosition = GetViewport().GetCanvasTransform().AffineInverse() * screenPosition;
        if (_factory.TryDropInventoryStack(source, worldPosition))
        {
            RefreshHotbarAndEquipment();
            RefreshMachinePanel();
            _inventoryMenu.Refresh();
        }
    }

    private void HandleHotbarSlotSelected(int slotIndex) => SelectHotbarSlot(slotIndex);

    private void HandleHotbarContextActionRequested(
        InventoryItemContextAction action,
        InventoryItemContextRequest request) =>
        _inventoryMenu.ExecuteHotbarContextAction(action, request);

    private void HandleInventoryHotbarActivationRequested(int slotIndex)
    {
        if (_primaryUiMode == PrimaryUiMode.Inventory)
        {
            SetPrimaryUiMode(PrimaryUiMode.None);
        }

        SelectHotbarSlot(slotIndex);
    }

    private void HandleInventoryToolActivationRequested(int slotIndex)
    {
        if (_primaryUiMode == PrimaryUiMode.Inventory)
        {
            SetPrimaryUiMode(PrimaryUiMode.None);
        }

        _toolInventoryState.SelectSlot(slotIndex);
        ActivateHandSlot();
    }

    private void SelectHotbarSlot(int slotIndex)
    {
        CancelActiveDismantling();
        _toolInventoryState.DeactivateHandMode();
        var changed = _hotbarState.SelectSlot(slotIndex);
        if (changed && _factory.IsPlacementActive)
        {
            _factory.CancelPlacement();
        }

        RefreshHotbarAndEquipment();
        _factory.MarkInventoryChanged();
        if (!_factory.IsPlacementActive &&
            _controlMode == PlayerControlMode.OnFoot &&
            _primaryUiMode == PrimaryUiMode.None &&
            _hotbarState.ActiveItemId is { } itemId)
        {
            _factory.StartPlacementFromHotbarSlot(slotIndex, itemId);
        }
    }

    private void SelectRelativeHotbar(int direction)
    {
        const int handSelectionIndex = InventoryConfiguration.HotbarSlotCount;
        var current = _toolInventoryState.IsHandModeActive
            ? handSelectionIndex
            : _hotbarState.ActiveSlotIndex;
        var selectionCount = InventoryConfiguration.HotbarSlotCount + 1;
        var next = (current + direction + selectionCount) % selectionCount;
        if (next == handSelectionIndex)
        {
            ActivateHandSlot();
            return;
        }

        SelectHotbarSlot(next);
    }

    private void ActivateHandSlot()
    {
        CancelActiveDismantling();
        _factory.CancelPlacement();
        _toolInventoryState.ActivateHandMode();
        RefreshHotbarAndEquipment();
        _factory.MarkInventoryChanged();
    }

    private void SelectRelativeTool(bool previous)
    {
        CancelActiveDismantling();
        var changed = previous
            ? _toolInventoryState.SelectPreviousTool()
            : _toolInventoryState.SelectNextTool();
        if (!changed)
        {
            return;
        }

        RefreshHotbarAndEquipment();
        _factory.MarkInventoryChanged();
    }

    private void RestoreActiveHotbarSlot(int slotIndex)
    {
        _hotbarState.SelectSlot(slotIndex);
        RefreshHotbarAndEquipment();
    }

    private void RefreshHotbarAndEquipment()
    {
        if (GodotObject.IsInstanceValid(_hotbar))
        {
            _hotbar.Refresh();
        }

        if (GodotObject.IsInstanceValid(_inventoryMenu))
        {
            _inventoryMenu.SetActiveHotbarSlot(_hotbarState.ActiveSlotIndex);
            _inventoryMenu.Refresh();
        }

        if (GodotObject.IsInstanceValid(_onFootPlayer))
        {
            _onFootPlayer.SetActiveTool(GetActiveToolItemId());
        }
    }

    private ItemId? GetActiveToolItemId() => _toolInventoryState.EquippedToolId;

    private void UpdateDismantlingInteraction(double deltaSeconds)
    {
        if (!_factory.IsDismantling)
        {
            _onFootPlayer.ClearDismantlingEffect();
            return;
        }

        var validGameplayState = _controlMode == PlayerControlMode.OnFoot &&
                                 _primaryUiMode == PrimaryUiMode.None &&
                                 !_factory.IsPlacementActive;
        var completed = _factory.AdvanceDismantling(
            deltaSeconds,
            GetViewport().GetMousePosition(),
            _onFootPlayer.GlobalPosition,
            validGameplayState ? GetActiveToolItemId() : null,
            validGameplayState && Input.IsActionPressed(
                InputActionCatalog.Get(GameAction.UseMiningTool).InputMapAction));
        if (completed)
        {
            RefreshHotbarAndEquipment();
        }

        UpdateDismantlingVisual();
    }

    private void UpdateDismantlingVisual()
    {
        if (_factory.IsDismantling &&
            _factory.DismantlingTargetWorldPosition is { } targetPosition)
        {
            _onFootPlayer.SetDismantlingEffect(targetPosition, _factory.DismantlingProgress);
            return;
        }

        _onFootPlayer.ClearDismantlingEffect();
    }

    private void CancelActiveDismantling()
    {
        _factory.CancelDismantling();
        if (GodotObject.IsInstanceValid(_onFootPlayer))
        {
            _onFootPlayer.ClearDismantlingEffect();
        }
    }

    private void UpdateHotbarVisibility()
    {
        if (GodotObject.IsInstanceValid(_hotbar))
        {
            _hotbar.SetGameplayVisible(
                _controlMode == PlayerControlMode.OnFoot &&
                _primaryUiMode == PrimaryUiMode.None);
        }
    }

    private void HandleBuildMachineSelected(string machineId)
    {
        if (_controlMode != PlayerControlMode.OnFoot || !_factory.StartPlacement(machineId))
        {
            return;
        }

        SetPrimaryUiMode(PrimaryUiMode.None);
    }

    private void HandleBuildMenuClosed()
    {
        if (_primaryUiMode == PrimaryUiMode.BuildMenu)
        {
            _primaryUiMode = PrimaryUiMode.None;
        }

        RefreshShipInteractionState(forcePromptUpdate: true);
        UpdateHotbarVisibility();
    }

    private void OpenMachinePanel(MachineState machine)
    {
        if (_controlMode != PlayerControlMode.OnFoot || _primaryUiMode != PrimaryUiMode.None)
        {
            return;
        }

        if (machine.Definition.Kind == MachineKind.Storage)
        {
            _inventoryStorageTarget = machine;
            SetPrimaryUiMode(PrimaryUiMode.Inventory);
            return;
        }

        _machinePanelTarget = machine;
        SetPrimaryUiMode(PrimaryUiMode.MachinePanel);
    }

    private void OpenPowerMenu(PowerInteractionTarget target)
    {
        if (_controlMode != PlayerControlMode.OnFoot || _primaryUiMode != PrimaryUiMode.None)
        {
            return;
        }

        _powerMenuTarget = target;
        _factory.OpenPowerTarget(target);
        if (_factory.RefreshOpenPowerViewModel() is null)
        {
            _factory.ClosePowerTarget();
            _powerMenuTarget = null;
            return;
        }

        SetPrimaryUiMode(PrimaryUiMode.PowerMenu);
    }

    private void HandlePowerMenuClosed()
    {
        if (_primaryUiMode == PrimaryUiMode.PowerMenu)
        {
            _primaryUiMode = PrimaryUiMode.None;
        }

        _factory.ClosePowerTarget();
        _powerMenuTarget = null;
        _powerMenuRefreshElapsed = 0;
        RefreshDockingAvailability(force: true);
        RefreshShipInteractionState(forcePromptUpdate: true);
    }

    private void HandlePowerNetworkEnabledChanged(bool enabled)
    {
        _factory.SetOpenPowerNetworkEnabled(enabled);
        RefreshPowerMenu();
    }

    private void HandlePowerPortEnabledChanged(string portId, bool enabled)
    {
        _factory.SetOpenPowerPortEnabled(portId, enabled);
        RefreshPowerMenu();
    }

    private void HandlePowerSourceEnabledChanged(string sourceId, bool enabled)
    {
        _factory.SetOpenPowerSourceEnabled(sourceId, enabled);
        RefreshPowerMenu();
    }

    private void HandlePowerPortDisconnectRequested(string portId)
    {
        _factory.DisconnectOpenPowerPort(portId);
        _inventoryMenu.Refresh();
        RefreshPowerMenu();
    }

    private void RefreshPowerMenu()
    {
        if (_primaryUiMode != PrimaryUiMode.PowerMenu || !_powerMenu.IsOpen)
        {
            return;
        }

        var model = _factory.RefreshOpenPowerViewModel();
        if (model is null)
        {
            SetPrimaryUiMode(PrimaryUiMode.None);
            return;
        }

        _powerMenu.UpdateView(model);
    }

    private void HandleMachinePanelClosed()
    {
        if (_primaryUiMode == PrimaryUiMode.MachinePanel)
        {
            _primaryUiMode = PrimaryUiMode.None;
        }

        _factory.CloseMachine();
        _machinePanelTarget = null;
        RefreshShipInteractionState(forcePromptUpdate: true);
    }

    private void HandleMachineRecipeSelected(string recipeId)
    {
        _factory.SelectMachineRecipe(recipeId);
        RefreshMachinePanel();
    }

    private void HandleMachineActiveChanged(bool active)
    {
        _factory.SetOpenMachineActive(active);
        RefreshMachinePanel();
    }

    private void HandleMachineLoadInputsRequested()
    {
        _factory.LoadOpenMachineInputs();
        RefreshMachinePanel();
        _inventoryMenu.Refresh();
    }

    private void HandleMachineCollectOutputsRequested()
    {
        _factory.CollectOpenMachineOutputs();
        RefreshMachinePanel();
        _inventoryMenu.Refresh();
    }

    private void HandleMachineReturnInputsRequested()
    {
        _factory.ReturnOpenMachineInputs();
        RefreshMachinePanel();
        _inventoryMenu.Refresh();
    }

    private void HandleGeneratorTankFillRequested()
    {
        _factory.FillOpenGeneratorTank();
        RefreshMachinePanel();
    }

    private void HandleGeneratorTankDrainRequested()
    {
        _factory.DrainOpenGeneratorTank();
        RefreshMachinePanel();
    }

    private void HandleFactoryStateChanged()
    {
        RefreshMachinePanel();
        if (_primaryUiMode == PrimaryUiMode.Inventory &&
            _inventoryStorageTarget is not null && _inventoryMenu.IsOpen)
        {
            _inventoryMenu.Refresh();
        }
    }

    private void HandleBuildCatalogChanged()
    {
        if (_buildMenu.IsOpen)
        {
            RefreshBuildMenuCatalog();
        }
    }

    private void RefreshMachinePanel()
    {
        if (_primaryUiMode != PrimaryUiMode.MachinePanel || !_machinePanel.IsOpen)
        {
            return;
        }

        var model = _factory.RefreshOpenMachineViewModel();
        if (model is not null)
        {
            _machinePanel.UpdateView(model);
        }
    }

    private void RefreshBuildMenuCatalog() =>
        _buildMenu.SetMachineCatalog(_factory.CreateBuildMenuViewModels());

    private void HandleShipFuelTransferRequested(
        InventoryMenuController.ShipFuelTransferDirection direction,
        InventorySlotAddress address)
    {
        if (_controlMode != PlayerControlMode.Ship || !_inventoryMenu.IsShipStorageVisible)
        {
            return;
        }

        var containerInventory = address.InventoryId switch
        {
            InventoryMenuController.AstronautInventoryId => _astronautInventory,
            InventoryMenuController.ShipInventoryId => _shipInventory,
            _ => null,
        };
        if (containerInventory is null ||
            address.SlotIndex < 0 || address.SlotIndex >= containerInventory.SlotCount)
        {
            _inventoryMenu.ShowExternalStatus("Kein passender Behälter", succeeded: false);
            return;
        }

        var result = direction == InventoryMenuController.ShipFuelTransferDirection.Fill
            ? ShipRefuelService.TransferFilledContainerFromSlot(
                _ship.FuelTank, containerInventory, address.SlotIndex)
            : ShipRefuelService.TransferTankToContainerInSlot(
                _ship.FuelTank, containerInventory, address.SlotIndex);
        var message = result.Succeeded
            ? direction == InventoryMenuController.ShipFuelTransferDirection.Fill
                ? "Tank aufgef\u00fcllt"
                : "Tank geleert"
            : result.Failure switch
            {
                ShipRefuelFailure.TankCannotFitFullContainer => "Tank voll",
                ShipRefuelFailure.TankEmpty => "Tank leer",
                ShipRefuelFailure.TankContainsDifferentFuel or
                    ShipRefuelFailure.SelectedSlotDoesNotContainFuel => "Falscher Inhalt",
                ShipRefuelFailure.SelectedSlotDoesNotContainEmptyContainer => "Kein passender Beh\u00e4lter",
                ShipRefuelFailure.TankCannotFillContainer => "Zu wenig Treibstoff",
                ShipRefuelFailure.NoSpaceForReturnedContainers => "Kein Platz im Inventar",
                _ => "Transfer nicht m\u00f6glich",
            };
        _inventoryMenu.ShowExternalStatus(message, result.Succeeded);
        if (result.Succeeded)
        {
            _factory.MarkFuelChanged();
            _factory.MarkInventoryChanged();
            _inventoryMenu.Refresh();
        }

        RefreshShipFuelInventoryUi();
        _resourceHud.SetShipFuel(_ship.FuelTank.CurrentFuel, visible: true);
    }

    private void RefreshShipFuelInventoryUi()
    {
        _inventoryMenu.SetFuelTankState(
            _ship.FuelTank.CurrentFuel,
            _ship.FuelTank.Capacity,
            _ship.FuelTank.CurrentFuelType,
            _ship.FuelTank.RemainingBoostSeconds,
            _ship.FuelTank.BoostSpeedMultiplier);
    }

    private void HandleBoostFuelUnavailable() => _resourceHud.ShowMessage("Kein Treibstoff");

    private void HandleShipFuelChanged(double currentFuel)
    {
        _ = currentFuel;
        _factory.MarkFuelChanged();
    }

    private void HandleMapVisibilityChanged(bool isVisible)
    {
        if (!isVisible && _primaryUiMode == PrimaryUiMode.Map)
        {
            _primaryUiMode = PrimaryUiMode.None;
            RefreshShipInteractionState(forcePromptUpdate: true);
            UpdateHotbarVisibility();
        }
    }

    private void HandleMapOpenRequested()
    {
        if (_controlMode == PlayerControlMode.Ship &&
            _primaryUiMode != PrimaryUiMode.PauseMenu)
        {
            SetPrimaryUiMode(PrimaryUiMode.Map);
        }
    }

    private void HandleSettingsClosed()
    {
        if (_primaryUiMode == PrimaryUiMode.PauseMenu)
        {
            _primaryUiMode = PrimaryUiMode.None;
        }

        GetTree().Paused = false;
        RefreshShipInteractionState();
        UpdateHotbarVisibility();
    }

    private void HandleInfoRequested()
    {
        if (_primaryUiMode != PrimaryUiMode.PauseMenu)
        {
            return;
        }

        _settingsMenu.CloseImmediately();
        _infoMenu.Open();
    }

    private void HandleInfoBackRequested()
    {
        if (_primaryUiMode == PrimaryUiMode.PauseMenu)
        {
            _settingsMenu.Open();
        }
    }

    private void HandleInputBindingsChanged()
    {
        _shipInteractionPressGate.Reset();
        _shipDockingPressGate.Reset();
        _worldMap.RefreshBinding();
        _buildMenu.SetBuildActionLabel(InputBindingFormatter.FormatAction("build_menu"));
        RefreshHotbarAndEquipment();
        RefreshShipInteractionState(forcePromptUpdate: true);
    }

#if DEBUG
    private async void RunHeadlessSmokeTest()
    {
        var originalControlMode = _controlMode;
        var originalShipPosition = _ship.GlobalPosition;
        var originalShipRotation = _ship.GlobalRotation;
        var originalShipVelocity = _ship.Velocity;
        var originalAttachedCometId = _ship.DockingState.AttachedCometId;
        var originalRelativeAttachment = _ship.DockingState.RelativeAttachmentPosition;
        var originalAttachmentRotation = _ship.DockingState.AttachmentRotationRadians;
        var originalLandingLegProgress = _ship.DockingState.LandingLegProgress;
        var originalAttachedComet = originalAttachedCometId is null
            ? null
            : _loadedSectors.Values
                .SelectMany(sector => sector.Comets)
                .FirstOrDefault(comet =>
                    string.Equals(comet.CometId, originalAttachedCometId, StringComparison.Ordinal));
        _factory.DebugSetPersistenceSuppressed(true);
        if (_ship.IsAttached)
        {
            RequireSmokeCondition(
                originalAttachedComet is not null && !_factory.HasShipPowerConnections,
                "Headless runtime isolation requires the persisted attached comet and no live ship cables.");
            _ship.RestoreFreePose(originalShipPosition, originalShipRotation);
            _factory.SynchronizeShipPowerDocking();
        }

        var isolatedSmokePosition = new Vector2(SectorSize * 0.5f, SectorSize * 0.5f);
        _ship.RestoreFreePose(isolatedSmokePosition, 0);
        var isolatedSmokeSector = ToSectorCoordinate(isolatedSmokePosition);
        LoadAround(isolatedSmokeSector, isolatedSmokeSector);

        try
        {
        _inventoryMenu.RunConstructionSmokeTest();
        _hotbar.RunConstructionSmokeTest();
        _buildMenu.RunConstructionSmokeTest();
        _machinePanel.RunConstructionSmokeTest();
        _powerMenu.RunLayoutSmokeTest();
        var defaultFactoryState = FactoryStateData.CreateDefault();
        var hasConfiguredHighPerformanceTestCargo = defaultFactoryState.ShipInventory.Any(slot =>
            slot.ItemId == ShipFuelConfiguration.HighPerformanceTestCargo.ItemId.Value &&
            slot.Amount == ShipFuelConfiguration.HighPerformanceTestCargo.Amount);
        var highPerformanceTestCargoMatchesConfiguration =
            ShipFuelConfiguration.IncludeHighPerformanceTestTankInNewGame
                ? hasConfiguredHighPerformanceTestCargo
                : !defaultFactoryState.ShipInventory.Any(slot =>
                    slot.ItemId == ShipFuelConfiguration.HighPerformanceTestCargo.ItemId.Value);
        RequireSmokeCondition(
            defaultFactoryState.ShipInventory.Any(slot =>
                slot.ItemId == ProductionItemIds.PowerCable.Value &&
                slot.Amount == LogisticsConfiguration.StartingPowerCableCount) &&
            defaultFactoryState.ShipInventory.Any(slot =>
                slot.ItemId == ProductionItemIds.ConveyorBelt.Value &&
                slot.Amount == LogisticsConfiguration.StartingConveyorBeltCount) &&
            defaultFactoryState.ShipInventory.Any(slot =>
                slot.ItemId == ProductionItemIds.TransportPipe.Value &&
                slot.Amount == LogisticsConfiguration.StartingTransportPipeCount) &&
            highPerformanceTestCargoMatchesConfiguration &&
            defaultFactoryState.ShipFuelType == ShipFuelType.Standard &&
            Math.Abs(defaultFactoryState.ShipFuel - ShipFuelConfiguration.TankCapacity) < 0.001 &&
            defaultFactoryState.HotbarInventory.Count == 0 &&
            defaultFactoryState.AstronautInventory.Count == 0 &&
            defaultFactoryState.ToolInventory.SequenceEqual(new[]
            {
                new InventorySlotState(0, ProductionItemIds.MiningTool.Value, 1),
                new InventorySlotState(1, ProductionItemIds.MachineDismantlingTool.Value, 1),
            }) &&
            defaultFactoryState.SelectedToolSlotIndex == 0 && defaultFactoryState.IsHandModeActive,
            "A new game must contain the connection kit, both dedicated tools and the configured fuel cargo.");
        var persistedJson = FactoryStateJsonCodec.Serialize(defaultFactoryState);
        RequireSmokeCondition(
            FactoryStateJsonCodec.TryDeserialize(persistedJson, out var restoredFactoryState, out _) &&
            restoredFactoryState.Version == FactoryStateData.CurrentVersion,
            "The factory persistence codec must round-trip a valid state.");
        var multipleMiningToolsState = defaultFactoryState with
        {
            ToolInventory = defaultFactoryState.ToolInventory
                .Append(new InventorySlotState(2, ProductionItemIds.MiningTool.Value, 1))
                .ToArray(),
        };
        var multipleMiningToolsJson = FactoryStateJsonCodec.Serialize(multipleMiningToolsState);
        RequireSmokeCondition(
            FactoryStateJsonCodec.TryDeserialize(
                multipleMiningToolsJson,
                out var restoredMultipleMiningToolsState,
                out _) &&
            restoredMultipleMiningToolsState.ToolInventory.Count(slot =>
                slot.ItemId == ProductionItemIds.MiningTool.Value) == 2,
            "Persistence must accept multiple independently crafted non-stackable mining tools.");
        GD.Print("FACTORY_PERSISTENCE_ROUNDTRIP_OK: versioned machine/research/fuel snapshot");
        GD.Print($"STARTER_CONNECTION_KIT_OK: {LogisticsConfiguration.StartingPowerCableCount} power cables, " +
                 $"{LogisticsConfiguration.StartingConveyorBeltCount} conveyor belts, " +
                 $"{LogisticsConfiguration.StartingTransportPipeCount} transport pipes");
        _factory.RunPowerCablePresentationSmokeTest();
        _factory.DebugRunDroppedItemRuntimeSmokeTest();

        SetControlMode(PlayerControlMode.Ship);
        _Input(new InputEventAction { Action = "build_menu", Pressed = true });
        RequireSmokeCondition(
            !_buildMenu.IsOpen && _primaryUiMode == PrimaryUiMode.None,
            "The build menu must stay unavailable while the ship is controlled.");
        _Input(new InputEventAction { Action = "inventory", Pressed = true });
        RequireSmokeCondition(
            _inventoryMenu.IsOpen && _inventoryMenu.IsShipStorageVisible &&
            _inventoryMenu.IsPersonalInventoryVisible &&
            !_inventoryMenu.IsEmbeddedHotbarVisible &&
            _primaryUiMode == PrimaryUiMode.Inventory && !GetTree().Paused,
            "Ship mode must open astronaut inventory and 56-slot cargo side by side without the hotbar.");
        var shipModeBeforeBlockedInteraction = _controlMode;
        _Input(new InputEventAction { Action = "ship_interaction", Pressed = true });
        RequireSmokeCondition(
            _controlMode == shipModeBeforeBlockedInteraction,
            "Ship interaction must stay blocked while inventory is open.");

        _Input(new InputEventAction { Action = "open_map", Pressed = true });
        RequireSmokeCondition(
            !_inventoryMenu.IsOpen && _worldMap.IsOpen &&
            _primaryUiMode == PrimaryUiMode.Map && !GetTree().Paused,
            "Opening the map must synchronously replace the inventory without pausing gameplay.");
        _worldMap.SetShipFollowing(true);
        RequireSmokeCondition(
            _worldMap.IsFollowingShip,
            "The ship button must enable persistent world-map following.");
        _Input(new InputEventAction { Action = "open_map", Pressed = true });
        RequireSmokeCondition(
            !_worldMap.IsOpen && _primaryUiMode == PrimaryUiMode.None,
            "A second map action must close the world map.");
        _worldMap.RequestOpenFromMinimap();
        RequireSmokeCondition(
            _worldMap.IsOpen && _primaryUiMode == PrimaryUiMode.Map,
            "The minimap must request the same central map-opening path as the M action.");
        _Input(new InputEventAction { Action = "open_map", Pressed = true });

        SetControlMode(PlayerControlMode.OnFoot);
        RequireSmokeCondition(_hotbar.Visible, "The six-slot hotbar must be visible while controlling the astronaut.");
        RequireSmokeCondition(
            _toolInventoryState.IsHandModeActive && _onFootPlayer.IsMiningToolEquipped,
            "A new astronaut must start in hand mode with the mining tool selected.");
        var secondHotbarAction = InputActionCatalog.Get(InputActionCatalog.HotbarActions[1]).InputMapAction;
        _Input(new InputEventAction { Action = secondHotbarAction, Pressed = true });
        RequireSmokeCondition(
            _hotbarState.ActiveSlotIndex == 1 && !_toolInventoryState.IsHandModeActive &&
            !_onFootPlayer.IsMiningToolEquipped,
            "Selecting a normal hotbar slot must leave hand mode and unequip the mining tool.");
        _Input(new InputEventAction
        {
            Action = InputActionCatalog.Get(GameAction.ActivateHandSlot).InputMapAction,
            Pressed = true,
        });
        RequireSmokeCondition(
            _toolInventoryState.IsHandModeActive && _onFootPlayer.IsMiningToolEquipped,
            "The configurable hand action must equip the selected dedicated tool.");
        _Input(new InputEventAction
        {
            Action = InputActionCatalog.Get(GameAction.NextTool).InputMapAction,
            Pressed = true,
        });
        RequireSmokeCondition(
            _toolInventoryState.SelectedSlotIndex == 1 && !_onFootPlayer.IsMiningToolEquipped,
            "The configurable next-tool action must cycle to the dismantling tool.");
        _Input(new InputEventAction
        {
            Action = InputActionCatalog.Get(GameAction.PreviousTool).InputMapAction,
            Pressed = true,
        });
        RequireSmokeCondition(
            _toolInventoryState.SelectedSlotIndex == 0 && _onFootPlayer.IsMiningToolEquipped,
            "The configurable previous-tool action must cycle back to the mining tool.");
        _inventoryMenu.SetSelectedToolSlot(1);
        RequireSmokeCondition(
            _toolInventoryState.SelectedSlotIndex == 1 &&
            _toolInventoryState.IsHandModeActive &&
            _onFootPlayer.IsDismantlingToolEquipped,
            "Selecting a tool inventory slot must synchronously update the equipped hand tool.");
        _inventoryMenu.SetSelectedToolSlot(0);
        RequireSmokeCondition(
            _toolInventoryState.SelectedSlotIndex == 0 && _onFootPlayer.IsMiningToolEquipped,
            "Selecting the mining-tool slot must synchronously restore the equipped mining tool.");

        // Build-menu previews retain their selected object when the regular wheel is used.
        // Physical hotbar placement still participates in the normal wheel cycle.
        _factory.StartPlacement(MachineDefinitionIds.BasicGenerator.Value);
        RequireSmokeCondition(
            _factory.IsPlacementActive && IsGameplayInputBlocked() && !IsMovementInputBlocked(),
            "Placement must block mining and interactions while keeping astronaut movement active.");
        _Input(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelDown,
            Pressed = true,
            Position = new Vector2(10, 10),
        });
        RequireSmokeCondition(
            _factory.IsPlacementActive && !_factory.IsHotbarPlacementActive &&
            _toolInventoryState.IsHandModeActive,
            "The wheel must not replace a build-menu object with a hotbar selection.");
        _factory.CancelPlacement();
        _Input(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelDown,
            Pressed = true,
            Position = new Vector2(10, 10),
        });
        RequireSmokeCondition(
            !_factory.IsPlacementActive && !_toolInventoryState.IsHandModeActive,
            "The regular wheel cycle must continue to a normal hotbar slot outside build-menu placement.");
        _Input(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelUp,
            Pressed = true,
            Position = new Vector2(10, 10),
        });
        RequireSmokeCondition(
            _toolInventoryState.IsHandModeActive,
            "The mouse-wheel selection cycle must include the dedicated hand slot.");

        _Input(new InputEventAction { Action = "build_menu", Pressed = true });
        RequireSmokeCondition(
            _buildMenu.IsOpen && _primaryUiMode == PrimaryUiMode.BuildMenu && !GetTree().Paused,
            "The configured build action must open the build menu only on foot without pausing.");
        _Input(new InputEventAction { Action = "build_menu", Pressed = true });
        await ToSignal(GetTree().CreateTimer(0.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        RequireSmokeCondition(
            !_buildMenu.IsOpen && _primaryUiMode == PrimaryUiMode.None,
            "A second configured build action must close the build menu.");
        _Input(new InputEventAction { Action = "inventory", Pressed = true });
        RequireSmokeCondition(
            _inventoryMenu.IsOpen && !_inventoryMenu.IsShipStorageVisible &&
            _inventoryMenu.IsPersonalInventoryVisible && _inventoryMenu.IsEmbeddedHotbarVisible &&
            !_hotbar.Visible &&
            _primaryUiMode == PrimaryUiMode.Inventory && !GetTree().Paused,
            "On-foot mode must expose only the 24-slot inventory plus embedded hotbar.");
        _Input(new InputEventAction { Action = "inventory", Pressed = true });
        await ToSignal(GetTree().CreateTimer(0.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        RequireSmokeCondition(
            !_inventoryMenu.IsOpen && _primaryUiMode == PrimaryUiMode.None && _hotbar.Visible,
            "A second inventory action must close the personal inventory and restore the hotbar.");
        _Input(new InputEventAction { Action = "inventory", Pressed = true });
        _Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await ToSignal(GetTree().CreateTimer(0.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        RequireSmokeCondition(
            !_inventoryMenu.IsOpen && _primaryUiMode == PrimaryUiMode.None && !GetTree().Paused,
            "The first Escape from inventory must return to gameplay without opening pause.");
        _Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        RequireSmokeCondition(
            _primaryUiMode == PrimaryUiMode.PauseMenu && GetTree().Paused,
            "A second Escape from gameplay must open the configured pause menu.");
        _settingsMenu.NavigateBackOrClose();
        RequireSmokeCondition(
            _primaryUiMode == PrimaryUiMode.None && !GetTree().Paused && _hotbar.Visible,
            "Closing pause must restore astronaut gameplay and the standalone hotbar.");
        _Input(new InputEventAction { Action = "open_map", Pressed = true });
        RequireSmokeCondition(
            !_worldMap.IsOpen && _primaryUiMode == PrimaryUiMode.None,
            "The world map must remain unavailable while on foot.");

        SetControlMode(PlayerControlMode.Ship);
        GD.Print("FACTORY_UI_STATE_SMOKE_OK: ship-blocked B, on-foot B toggle, unpaused menu state");
        GD.Print("MAP_UI_STATE_SMOKE_OK: M/I exclusivity, ship-only map, live gameplay, Escape close");
        await RunBoostSmokeTest();
        await RunDockingCandidateInputSmokeTest();
        await RunDockingAndDriftSmokeTest();
        RunSettingsSmokeTest();
        }
        finally
        {
            Input.ActionRelease("ship_boost");
            Input.ActionRelease("move_up");
            SetPrimaryUiMode(PrimaryUiMode.None);
            _ship.RestoreFreePose(originalShipPosition, originalShipRotation);
            _factory.SynchronizeShipPowerDocking();
            var originalSector = ToSectorCoordinate(originalShipPosition);
            LoadAround(originalSector, originalSector);
            var restoredAttachedComet = originalAttachedCometId is null
                ? null
                : _loadedSectors.Values
                    .SelectMany(sector => sector.Comets)
                    .FirstOrDefault(comet =>
                        string.Equals(comet.CometId, originalAttachedCometId, StringComparison.Ordinal));
            if (originalAttachedCometId is not null &&
                restoredAttachedComet is not null &&
                GodotObject.IsInstanceValid(restoredAttachedComet))
            {
                _ship.RestoreAttachedPose(
                    restoredAttachedComet,
                    originalAttachedCometId,
                    new Vector2(
                        (float)originalRelativeAttachment.X,
                        (float)originalRelativeAttachment.Y),
                    (float)originalAttachmentRotation,
                    originalLandingLegProgress);
                _factory.SynchronizeShipPowerDocking();
            }

            _ship.Velocity = originalShipVelocity;
            SetControlMode(originalControlMode);
            _factory.DebugSetPersistenceSuppressed(false);
        }
    }

    private async Task RunBoostSmokeTest()
    {
        RequireSmokeCondition(
            Mathf.IsEqualApprox(_ship.MovementSpeed, PlayerShipController.NormalFlightSpeed) &&
            Mathf.IsEqualApprox(
                PlayerShipController.NormalFlightSpeed * PlayerShipController.BoostMultiplier,
                PlayerShipController.BoostFlightSpeed),
            "Ship flight must use the central 975 normal and 2600 boost target speeds.");

        var startPosition = _ship.GlobalPosition;
        var startRotation = _ship.Rotation;
        var startFuel = _ship.FuelTank.CurrentFuel;
        _ship.RestoreFuel(ShipFuelConfiguration.TankCapacity);
        var boostFuelBefore = _ship.FuelTank.CurrentFuel;
        StaticBody2D? collisionWall = null;
        try
        {
            for (var frame = 0; frame < 2; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Input.ActionPress("move_up");
            Input.ActionPress("ship_boost");
            for (var frame = 0; frame < 24; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            RequireSmokeCondition(
                _ship.IsBoostActive &&
                _ship.Velocity.Length() > _ship.MovementSpeed &&
                _ship.Velocity.Length() <= _ship.BoostMovementSpeed + 0.5f &&
                _ship.FuelTank.CurrentFuel < boostFuelBefore,
                "Held Shift must accelerate within its target and consume tank fuel only while moving.");

            collisionWall = new StaticBody2D
            {
                CollisionLayer = 1,
                CollisionMask = 0,
                GlobalPosition = _ship.GlobalPosition + (Vector2.Up * 400),
            };
            collisionWall.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(1_200, 20) },
            });
            _ship.GetParent().AddChild(collisionWall);
            for (var frame = 0; frame < 24; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            RequireSmokeCondition(
                _ship.GlobalPosition.Y > collisionWall.GlobalPosition.Y,
                "Boost movement must not tunnel through a static comet-like collision surface.");

            collisionWall.QueueFree();
            collisionWall = null;
            for (var frame = 0; frame < 16; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            _Input(new InputEventAction { Action = "inventory", Pressed = true });
            for (var frame = 0; frame < 2; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            RequireSmokeCondition(
                !GetTree().Paused && _ship.IsBoostActive && _ship.Velocity.Length() > _ship.MovementSpeed,
                $"Ship steering and held boost must remain active behind the ship inventory " +
                $"(paused={GetTree().Paused}, boost={_ship.IsBoostActive}, speed={_ship.Velocity.Length():0.0}).");
            Input.ActionRelease("ship_boost");
            Input.ActionRelease("move_up");
            _Input(new InputEventAction { Action = "inventory", Pressed = true });
            await ToSignal(GetTree().CreateTimer(0.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            RequireSmokeCondition(!_ship.IsBoostActive, "Released Shift must keep boost disabled after closing a menu.");
        }
        finally
        {
            Input.ActionRelease("ship_boost");
            Input.ActionRelease("move_up");
            if (GodotObject.IsInstanceValid(collisionWall))
            {
                collisionWall!.QueueFree();
            }

            GetTree().Paused = false;
            _ship.GlobalPosition = startPosition;
            _ship.Rotation = startRotation;
            _ship.Velocity = Vector2.Zero;
            _ship.RestoreFuel(startFuel);
        }

        GD.Print("SHIP_BOOST_SMOKE_OK: 975/2600 speed, live inventory steering, held/released Shift, swept collision");
    }

    private async Task RunDockingCandidateInputSmokeTest()
    {
        var startPosition = _ship.GlobalPosition;
        var startRotation = _ship.Rotation;
        var comet = new AsteroidView
        {
            Name = "DockingCandidateSmokeComet",
            GlobalPosition = startPosition + new Vector2(0, 1_000),
        };
        comet.Configure(new AsteroidDefinition(
            "smoke:dockable-comet",
            new WorldPosition(0, 0),
            500,
            AsteroidSize.Large,
            "smoke_rock",
            new ItemId("iron_ore"),
            91_337,
            0,
            0.35,
            0.30,
            [],
            SurfaceProfile: null));
        _ship.GetParent().AddChild(comet);
        try
        {
            RequireSmokeCondition(
                comet.TryGetClosestSurfacePoint(
                    _ship.GlobalPosition,
                    out _,
                    out var outwardNormal,
                    out var initialSurfaceDistance),
                "The docking smoke comet must expose its polygon surface.");
            var desiredCenterDistance = PlayerShipController.DockingHullReach + 60;
            comet.GlobalPosition += outwardNormal * (initialSurfaceDistance - desiredCenterDistance);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            _ship.Velocity = new Vector2((float)_dockingConfiguration.MaximumAttachmentSpeed + 10, 0);
            RefreshDockingAvailability(force: true);
            RequireSmokeCondition(
                !_availableDockingDecision.IsAllowed,
                "Docking must remain blocked above the configured maximum speed.");

            _ship.Velocity = Vector2.Zero;
            RefreshDockingAvailability(force: true);
            RequireSmokeCondition(
                _availableDockingDecision.Action == ShipDockingAction.Attach &&
                _availableDockingDecision.IsAllowed,
                "A nearby free polygon surface must become a valid docking candidate.");

            _shipDockingPressGate.Reset();
            _Input(new InputEventAction { Action = "ship_docking", Pressed = true });
            RequireSmokeCondition(
                _ship.IsAttached && _ship.DockingState.AttachedCometId == comet.CometId,
                "The configured docking action must attach to the selected live comet.");
            _factory.RunPowerCableRuntimeSmokeTest(comet);

            SetControlMode(PlayerControlMode.OnFoot);
            _onFootPlayer.GlobalPosition = _ship.GetWorldPowerPortAnchor(ShipPowerPortId.A);
            _onFootPlayer.ApplyInheritedVelocity(Vector2.Zero);
            RefreshShipInteractionState(forcePromptUpdate: true);
            RequireSmokeCondition(
                _availablePowerInteraction is
                {
                    NodeId: var nodeId,
                    PortId: var portId,
                } &&
                nodeId == new MachineInstanceId("player_ship") &&
                portId == MachinePortIds.ShipPowerA,
                "The same nearby ship socket condition used by the prompt must expose connector A to E.");
            _shipInteractionPressGate.Reset();
            _Input(new InputEventAction { Action = "ship_interaction", Pressed = true });
            RequireSmokeCondition(
                _primaryUiMode == PrimaryUiMode.PowerMenu &&
                _powerMenu.IsOpen &&
                _powerMenu.Visible &&
                _powerMenu.GetParent() == _powerMenuLayer &&
                !GetTree().Paused,
                "E at the visible power prompt must open the live non-pausing power window on its UI layer.");
            _Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            RequireSmokeCondition(
                _primaryUiMode == PrimaryUiMode.None && !_powerMenu.IsOpen && !_powerMenu.Visible,
                "Escape must close the live power window again.");
            SetControlMode(PlayerControlMode.Ship);
            RefreshDockingAvailability(force: true);
            GD.Print(
                "POWER_MENU_INTERACTION_SMOKE_OK: shared E condition, unconnected ship network, visible UI layer, Escape close");

            _Input(new InputEventAction { Action = "ship_docking", Pressed = true });
            RequireSmokeCondition(
                _ship.IsAttached,
                "A repeated held docking press must not detach in the same input cycle.");

            _Input(new InputEventAction { Action = "ship_docking", Pressed = false });
            await ToSignal(GetTree().CreateTimer(0.22), SceneTreeTimer.SignalName.Timeout);
            _Input(new InputEventAction { Action = "ship_docking", Pressed = true });
            RequireSmokeCondition(
                !_ship.IsAttached,
                "A released, subsequent docking press must detach the ship.");
            _Input(new InputEventAction { Action = "ship_docking", Pressed = false });
        }
        finally
        {
            if (_ship.IsAttached)
            {
                _ship.ExecuteDocking(
                    new ShipDockingContext(true, false, 0, null),
                    candidateComet: null);
            }

            _shipDockingPressGate.Reset();
            _ship.GlobalPosition = startPosition;
            _ship.Rotation = startRotation;
            _ship.Velocity = Vector2.Zero;
            comet.QueueFree();
        }

        GD.Print("SHIP_DOCKING_CANDIDATE_SMOKE_OK: polygon surface, distance/speed gate, H press edge");
    }

    private async Task RunDockingAndDriftSmokeTest()
    {
        var startPosition = _ship.GlobalPosition;
        var startRotation = _ship.Rotation;
        var anchor = new Node2D
        {
            Name = "DockingSmokeAnchor",
            GlobalPosition = startPosition,
        };
        _ship.GetParent().AddChild(anchor);
        StaticBody2D? resourceBlocker = null;
        try
        {
            var insideHullExit = startPosition + new Vector2(0, -35).Rotated(startRotation);
            var clearHullExit = startPosition + new Vector2(0, -320).Rotated(startRotation);
            RequireSmokeCondition(
                !PlayerShipController.IsCircleClearOfHull(
                    insideHullExit,
                    28,
                    startPosition,
                    startRotation) &&
                PlayerShipController.IsCircleClearOfHull(
                    clearHullExit,
                    28,
                    startPosition,
                    startRotation),
                "Safe exits must reject the ship interior while accepting clear space outside the hull.");

            var relativePosition = anchor.ToLocal(_ship.GlobalPosition);
            var candidate = new ShipDockingCandidate(
                "smoke:comet",
                SurfaceDistance: 20,
                HasFreeSurface: true,
                HasSafeExitPosition: true,
                RelativeAttachmentPosition: new WorldPosition(relativePosition.X, relativePosition.Y),
                AttachmentRotationRadians: _ship.GlobalRotation - anchor.GlobalRotation);
            var attachContext = new ShipDockingContext(
                IsShipControlled: true,
                IsPauseMenuOpen: false,
                ShipSpeed: 0,
                Candidate: candidate);
            var attachDecision = _ship.ExecuteDocking(attachContext, anchor);
            RequireSmokeCondition(
                attachDecision.Action == ShipDockingAction.Attach && _ship.IsAttached,
                "A valid low-speed surface candidate must attach the ship.");

            Input.ActionPress("move_up");
            Input.ActionPress("ship_boost");
            for (var frame = 0; frame < 10; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            RequireSmokeCondition(
                _ship.Velocity.IsZeroApprox() && !_ship.IsBoostActive &&
                _ship.DockingState.LandingLegProgress > 0,
                "An attached ship must ignore flight/boost input and deploy its landing legs.");
            anchor.GlobalPosition += new Vector2(32, 18);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            RequireSmokeCondition(
                _ship.GlobalPosition.IsEqualApprox(anchor.ToGlobal(relativePosition)),
                "An attached ship must preserve its transform relative to the comet.");

            var detachContext = new ShipDockingContext(
                IsShipControlled: true,
                IsPauseMenuOpen: false,
                ShipSpeed: 0,
                Candidate: null);
            var detachDecision = _ship.ExecuteDocking(detachContext, candidateComet: null);
            Input.ActionRelease("ship_boost");
            Input.ActionRelease("move_up");
            for (var frame = 0; frame < 20; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            RequireSmokeCondition(
                detachDecision.Action == ShipDockingAction.Detach && !_ship.IsAttached &&
                _ship.DockingState.AreLandingLegsRetracted,
                "Detaching must release the ship and retract the landing legs.");

            _ship.Velocity = new Vector2(400, 0);
            _ship.SetControlActive(false);
            var driftStart = _ship.GlobalPosition;
            RequireSmokeCondition(
                Mathf.IsEqualApprox(
                    _ship.Velocity.Length(),
                    (float)_dockingConfiguration.SafeExitDriftSpeed),
                "Unpiloted drift must be clamped to the configured safe speed.");
            for (var frame = 0; frame < 4; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            var astronautVelocity = ShipDriftRules.CalculateAstronautExitVelocity(
                new ShipVelocity(_ship.Velocity.X, _ship.Velocity.Y),
                _dockingConfiguration);
            RequireSmokeCondition(
                _ship.GlobalPosition.X > driftStart.X &&
                Math.Abs(astronautVelocity.Speed -
                         (_dockingConfiguration.SafeExitDriftSpeed *
                          _dockingConfiguration.AstronautVelocityInheritance)) < 0.1,
                "The unpiloted ship must drift with collisions while the astronaut inherits its configured share.");

            resourceBlocker = new StaticBody2D
            {
                Name = "DriftResourceCollisionSmoke",
                CollisionLayer = ResourceDepositView.ResourceCollisionLayer,
                CollisionMask = 0,
            };
            resourceBlocker.AddChild(new CollisionShape2D
            {
                Shape = new CircleShape2D { Radius = 80 },
            });
            _ship.GetParent().AddChild(resourceBlocker);
            _ship.GlobalPosition = startPosition;
            _ship.GlobalRotation = 0;
            resourceBlocker.GlobalPosition = startPosition + new Vector2(380, 0);
            _ship.Velocity = new Vector2((float)_dockingConfiguration.SafeExitDriftSpeed, 0);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var resourceCollisionObserved = false;
            for (var frame = 0; frame < 90; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                resourceCollisionObserved |= _ship.GetSlideCollisionCount() > 0;
            }

            RequireSmokeCondition(
                resourceCollisionObserved,
                "An unpiloted drifting ship must be blocked by a physical resource deposit.");
        }
        finally
        {
            Input.ActionRelease("ship_boost");
            Input.ActionRelease("move_up");
            if (_ship.IsAttached)
            {
                _ship.ExecuteDocking(
                    new ShipDockingContext(true, false, 0, null),
                    candidateComet: null);
            }

            _ship.SetControlActive(true);
            _ship.GlobalPosition = startPosition;
            _ship.Rotation = startRotation;
            _ship.Velocity = Vector2.Zero;
            resourceBlocker?.QueueFree();
            anchor.QueueFree();
        }

        GD.Print("SHIP_DOCKING_DRIFT_SMOKE_OK: attach/detach, legs, anchored pose, control lock, safe drift, resource collision");
    }

    private void RunSettingsSmokeTest()
    {
        _settingsMenu.RunConstructionSmokeTest();
        _infoMenu.RunConstructionSmokeTest();
        SetPrimaryUiMode(PrimaryUiMode.PauseMenu);
        RequireSmokeCondition(
            GetTree().Paused && _primaryUiMode == PrimaryUiMode.PauseMenu,
            "Only the pause/settings menu may pause the scene tree.");
        HandleInfoRequested();
        RequireSmokeCondition(
            GetTree().Paused && _infoMenu.IsOpen && !_settingsMenu.IsOpen,
            "Info must open from the pause menu without resuming gameplay.");
        _infoMenu._Input(new InputEventAction { Action = "pause", Pressed = true });
        RequireSmokeCondition(
            GetTree().Paused && !_infoMenu.IsOpen && _settingsMenu.IsOpen,
            "Escape from info must return to the still-paused settings main page.");
        _settingsMenu._Input(new InputEventAction { Action = "pause", Pressed = true });
        RequireSmokeCondition(
            !GetTree().Paused && _primaryUiMode == PrimaryUiMode.None,
            "The configured pause action must also close the pause menu.");
    }

    private static void RequireSmokeCondition(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Headless smoke test failed: {message}");
        }
    }
#endif

    private static SlotInventory CreatePlayerInventory(int slotCount) => new(
        slotCount,
        InventoryConfiguration.MaximumStackSize,
        ResolvePlayerItemStackSize);

    private static int ResolvePlayerItemStackSize(ItemId itemId) =>
        DefaultProductionItemCatalog.Instance.TryGet(itemId, out var item) && item is not null
            ? item.MaximumStackSize
            : InventoryConfiguration.MaximumStackSize;

    private bool IsPointerOverInteractiveUi()
    {
        var hoveredControl = GetViewport().GuiGetHoveredControl();
        if (hoveredControl is null)
        {
            return false;
        }

        Node[] worldInputBlockers =
        [
            _hotbar,
            _worldMap,
            _inventoryMenu,
            _buildMenu,
            _machinePanel,
            _powerMenuLayer,
            _settingsMenu,
            _infoMenu,
        ];
        return worldInputBlockers.Any(node =>
            GodotObject.IsInstanceValid(node) && node.IsAncestorOf(hoveredControl));
    }

    private bool CanSelectHotbarWithWheelOverHud(InputEventMouseButton pointerEvent)
    {
        if (pointerEvent.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown) ||
            _controlMode != PlayerControlMode.OnFoot ||
            _primaryUiMode != PrimaryUiMode.None ||
            !_settingsMenu.HotbarMouseWheelEnabled ||
            !GodotObject.IsInstanceValid(_hotbar))
        {
            return false;
        }

        var hoveredControl = GetViewport().GuiGetHoveredControl();
        return hoveredControl is not null && _hotbar.IsAncestorOf(hoveredControl);
    }

    private static bool TryGetPressedHotbarSlot(InputEvent @event, out int slotIndex)
    {
        for (var index = 0; index < InputActionCatalog.HotbarActions.Count; index++)
        {
            var inputAction = InputActionCatalog.Get(InputActionCatalog.HotbarActions[index]).InputMapAction;
            if (@event.IsActionPressed(inputAction))
            {
                slotIndex = index;
                return true;
            }
        }

        slotIndex = -1;
        return false;
    }

    private static bool IsSingleActionPress(InputEvent @event) =>
        @event is not InputEventKey keyEvent || !keyEvent.Echo;

    private bool IsGameplayInputBlocked() =>
        _primaryUiMode != PrimaryUiMode.None ||
        _factory.IsPlacementActive ||
        _worldMap.IsOpen ||
        _settingsMenu.IsOpen ||
        _infoMenu.IsOpen ||
        _inventoryMenu.IsOpen ||
        _buildMenu.IsOpen ||
        _machinePanel.IsOpen;

    private bool IsMovementInputBlocked() =>
        _primaryUiMode != PrimaryUiMode.None ||
        _worldMap.IsOpen ||
        _settingsMenu.IsOpen ||
        _infoMenu.IsOpen ||
        _inventoryMenu.IsOpen ||
        _buildMenu.IsOpen ||
        _machinePanel.IsOpen;

    private static bool IsEscapePress(InputEvent @event) =>
        @event is InputEventKey { Pressed: true, Echo: false } keyEvent &&
        (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape);

    /// <summary>
    /// Applies one central back-navigation order: transient child, current live
    /// menu, placement, then pause. Pause/settings/info own their deeper page
    /// hierarchy and therefore receive the input unchanged.
    /// </summary>
    private bool TryHandleMenuBackInput(InputEvent @event)
    {
        if (!IsEscapePress(@event) && !@event.IsActionPressed("pause"))
        {
            return false;
        }

        if (_primaryUiMode == PrimaryUiMode.PauseMenu)
        {
            return false;
        }

        var closedTransientUi = _hotbar.TryCloseTransientUi() || _primaryUiMode switch
        {
            PrimaryUiMode.Inventory => _inventoryMenu.TryCloseTransientUi(),
            PrimaryUiMode.MachinePanel => _machinePanel.TryCloseTransientUi(),
            _ => false,
        };
        if (closedTransientUi)
        {
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (_primaryUiMode != PrimaryUiMode.None)
        {
            SetPrimaryUiMode(PrimaryUiMode.None);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (_factory.IsPlacementActive)
        {
            _factory.CancelPlacement();
            _resourceHud.ShowMessage("Platzierung abgebrochen");
            GetViewport().SetInputAsHandled();
            return true;
        }

        SetPrimaryUiMode(PrimaryUiMode.PauseMenu);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private Vector2? FindSafeExitPosition()
    {
        foreach (var candidate in EnumerateExitCandidates(_ship.CockpitEntryPosition, _ship.Rotation))
        {
            if (IsExitPositionClear(candidate, _ship.GlobalPosition, _ship.GlobalRotation))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<Vector2> EnumerateExitCandidates(
        Vector2 cockpitPosition,
        float shipRotation)
    {
        var distances = new[]
        {
            _ship.CockpitExitClearance,
            _ship.CockpitExitClearance + 22,
            _ship.CockpitExitClearance + 44,
        };
        for (var distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
        {
            for (var directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                var angle = shipRotation + (Mathf.Tau * directionIndex / 8.0f);
                yield return cockpitPosition + (Vector2.FromAngle(angle) * distances[distanceIndex]);
            }
        }
    }

    private bool IsExitPositionClear(
        Vector2 candidate,
        Vector2 shipPosition,
        float shipRotation)
    {
        // The astronaut capsule is 54 units high. A 28-unit circle therefore
        // conservatively covers the full body plus one unit of safety margin.
        const float astronautClearanceRadius = 28;
        if (!PlayerShipController.IsCircleClearOfHull(
                candidate,
                astronautClearanceRadius,
                shipPosition,
                shipRotation))
        {
            return false;
        }

        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = astronautClearanceRadius },
            Transform = new Transform2D(0, candidate),
            CollisionMask = 1u |
                            ResourceDepositView.ResourceCollisionLayer |
                            MachineView.MachineCollisionLayer,
            CollideWithAreas = true,
            CollideWithBodies = true,
            Exclude = new Godot.Collections.Array<Rid> { _ship.GetRid() },
        };
        return GetViewport().World2D.DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private static SectorCoordinate ToSectorCoordinate(Vector2 worldPosition) => new(
        Mathf.FloorToInt(worldPosition.X / SectorSize),
        Mathf.FloorToInt(worldPosition.Y / SectorSize));
}

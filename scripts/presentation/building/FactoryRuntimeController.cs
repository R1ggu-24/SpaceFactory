using Godot;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Hazards;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;
using SpaceFactory.Core.Ships.Docking;
using SpaceFactory.Core.Ships.Fuel;
using SpaceFactory.Infrastructure.Persistence;
using SpaceFactory.Presentation.InventoryUI;
using SpaceFactory.Presentation.Ship;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Owns the live, chunk-aware presentation of the Godot-independent factory
/// state. Only loaded comet machines receive nodes and full simulation ticks.
/// </summary>
public partial class FactoryRuntimeController : Node2D
{
    public const double SimulationIntervalSeconds = 0.1;
    public const double AutosaveIntervalSeconds = 1.0;
    public const double MaximumOfflineSimulationSeconds = 60 * 60;
    public const double OfflineSimulationStepSeconds = 10;
    private static bool _persistenceSmokeCompleted;
    private static readonly MachineInstanceId PlayerShipNodeId = new("player_ship");

    private readonly MachineCatalog _machineCatalog = DefaultMachineCatalog.Instance;
    private readonly RecipeCatalog _recipeCatalog = DefaultRecipeCatalog.Instance;
    private readonly ResearchCatalog _researchCatalog = DefaultResearchCatalog.Instance;
    private readonly ConnectionTypeCatalog _connectionTypes = DefaultConnectionTypeCatalog.Instance;
    private readonly MachinePortCatalog _machinePorts = DefaultMachinePortCatalog.Instance;
    private readonly MachineConnectionNetwork _connectionNetwork = new();
    private readonly ConnectedPowerGridSimulation _powerSimulation;
    private readonly Dictionary<MachineInstanceId, MachineState> _machines = [];
    private readonly Dictionary<string, List<MachineState>> _machinesByComet = new(StringComparer.Ordinal);
    private readonly Dictionary<MachineInstanceId, MachineView> _machineViews = [];
    private readonly Dictionary<MachineConnectionId, MachineConnectionView> _connectionViews = [];
    private readonly Dictionary<MachineConnectionId, PowerCablePresentationView> _powerCableViews = [];
    private readonly Dictionary<string, AsteroidView> _loadedComets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResourceDepositView> _loadedResourceSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DroppedItemStateData> _droppedItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DroppedItemView> _droppedItemViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastSimulatedUtc = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PowerNetworkTickResult> _lastPowerResults = new(StringComparer.Ordinal);
    private readonly Dictionary<PowerNetworkId, ConnectedPowerGridTickResult> _lastPowerGridResults = [];
    private readonly Dictionary<MachineInstanceId, double> _lastAvailablePowerByMachine = [];
    private readonly Dictionary<MachineInstanceId, ResearchId> _pendingResearch = [];
    private readonly DismantlingProgressState _dismantlingProgress = new();
    private readonly PlacementRotationState _placementRotationState = new();

    private SlotInventory _astronautInventory = null!;
    private SlotInventory _hotbarInventory = null!;
    private ToolInventoryState _toolInventoryState = null!;
    private SlotInventory? _shipInventory;
    private IReadOnlyList<InventorySlotState> _pendingShipInventory = [];
    private IReadOnlyList<MachineConnectionSnapshot> _pendingShipConnections = [];
    private ShipPowerState _persistedShipPower = ShipPowerState.Default;
    private ShipDockingStateData _persistedShipDocking = ShipDockingStateData.Detached;
    private IReadOnlyList<InventorySlotState> _lastAstronautInventory = [];
    private IReadOnlyList<InventorySlotState> _lastHotbarInventory = [];
    private IReadOnlyList<InventorySlotState> _lastToolInventory = [];
    private IReadOnlyList<InventorySlotState> _lastShipInventory = [];
    private int _lastActiveHotbarSlotIndex;
    private int _lastSelectedToolSlotIndex;
    private bool _lastHandModeActive;
    private ItemPresentationCatalog _itemPresentation = null!;
    private IFactoryStateStore _stateStore = null!;
    private Func<double> _shipFuelProvider = null!;
    private Func<ShipFuelType> _shipFuelTypeProvider = null!;
    private Func<int> _activeHotbarSlotProvider = null!;
    private Action<int> _restoreActiveHotbarSlot = null!;
    private Action<double, ShipFuelType> _restoreShipFuel = null!;
    private Action<string> _showMessage = null!;
    private ResearchState _research = new();
    private RadiationExposureState _radiationExposure = new();
    private FirstBasicGeneratorState _firstBasicGenerator = new();
    private MachinePlacementPreview _placementPreview = null!;
    private MachineConnectionPlacementPreview _connectionPlacementPreview = null!;
    private PowerCablePlacementPreview _powerCablePlacementPreview = null!;
    private PowerInteractionTarget? _powerCableSourceTarget;
    private PowerInteractionTarget? _powerCableCandidateTarget;
    private HotbarPlacementSource? _hotbarPlacementSource;
    private PlayerShipController? _ship;
    private ShipPowerNode? _shipPowerNode;
    private int _sectorSize;
    private bool _awaitingShipDockingRestore;
    private MachineInstanceId? _activeResearchStation;
    private MachineState? _openMachine;
    private PowerInteractionTarget? _openPowerTarget;
    private double _simulationElapsed;
    private double _autosaveElapsed;
    private bool _initialized;
    private bool _dirty;
    private bool _debugPersistenceSuppressed;
    private DismantlingTarget? _dismantlingTarget;
    private Func<Vector2>? _dropOwnerPositionProvider;
    private Func<Vector2>? _dropOwnerVelocityProvider;
    private Func<bool>? _dropPickupActiveProvider;
    private double _droppedItemRefreshElapsed;

    public FactoryRuntimeController()
    {
        _powerSimulation = new ConnectedPowerGridSimulation(_connectionNetwork, _recipeCatalog);
    }

    public bool IsPlacementActive =>
        _placementPreview?.IsActive == true ||
        _connectionPlacementPreview?.IsActive == true ||
        _powerCablePlacementPreview?.IsActive == true;

    /// <summary>
    /// Distinguishes physical quick-access placement from build-menu placement so
    /// the central input router can preserve their different wheel behaviour.
    /// </summary>
    public bool IsHotbarPlacementActive =>
        IsPlacementActive && _hotbarPlacementSource is not null;

    public bool IsDismantling => _dismantlingTarget is not null && _dismantlingProgress.IsActive;

    public float DismantlingProgress => (float)_dismantlingProgress.Progress;

    public Vector2? DismantlingTargetWorldPosition => TryGetDismantlingTargetWorldPosition(out var position)
        ? position
        : null;

    public MachinePlacementFailureReason PlacementFailure =>
        _placementPreview?.CurrentFailure ?? MachinePlacementFailureReason.PlacementNotActive;

    public IReadOnlyCollection<MachineState> Machines => _machines.Values.ToArray();

    public IReadOnlyCollection<MachineConnection> Connections => _connectionNetwork.Connections;

    public IReadOnlyCollection<DroppedItemStateData> DroppedItems => _droppedItems.Values.ToArray();

    public ResearchState Research => _research;

    public RadiationExposureState RadiationExposure => _radiationExposure;

    public double UpdatePlayerRadiation(double deltaSeconds, Vector2 playerWorldPosition, bool isOnFoot)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 || !playerWorldPosition.IsFinite())
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var sources = isOnFoot
            ? CreateLoadedRadiationSources().ToList()
            : [];
        if (isOnFoot)
        {
            var carriedStrength = GetInventoryRadiationStrength(
                                      _astronautInventory,
                                      DefaultProductionItemCatalog.Instance) +
                                  GetInventoryRadiationStrength(
                                      _hotbarInventory,
                                      DefaultProductionItemCatalog.Instance) +
                                  GetInventoryRadiationStrength(
                                      _toolInventoryState.Inventory,
                                      DefaultProductionItemCatalog.Instance);
            if (carriedStrength > 0)
            {
                sources.Add(new RadiationSource(
                    "astronaut-carried-radioactive-material",
                    playerWorldPosition.X,
                    playerWorldPosition.Y,
                    carriedStrength));
            }
        }
        var suitProtection = _astronautInventory.GetAmount(ProductionItemIds.NuclearRadiationSuit) > 0 ||
                             _hotbarInventory.GetAmount(ProductionItemIds.NuclearRadiationSuit) > 0 ||
                             _toolInventoryState.Inventory.GetAmount(ProductionItemIds.NuclearRadiationSuit) > 0
            ? RadiationConfiguration.NuclearSuitProtection
            : _astronautInventory.GetAmount(ProductionItemIds.ImprovedRadiationSuit) > 0 ||
              _hotbarInventory.GetAmount(ProductionItemIds.ImprovedRadiationSuit) > 0 ||
              _toolInventoryState.Inventory.GetAmount(ProductionItemIds.ImprovedRadiationSuit) > 0
                ? RadiationConfiguration.ImprovedSuitProtection
                : RadiationConfiguration.StandardSuitProtection;
        _radiationExposure.SetSuitProtection(suitProtection);
        var previousDose = _radiationExposure.AccumulatedDose;
        var rate = _radiationExposure.Advance(
            deltaSeconds,
            playerWorldPosition.X,
            playerWorldPosition.Y,
            sources);
        if (Math.Abs(previousDose - _radiationExposure.AccumulatedDose) > 0.001)
        {
            MarkDirty();
        }

        return rate;
    }

    public void SynchronizeResourceDiscoveries(IEnumerable<SpaceFactory.Core.Items.ItemId> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);
        var changed = false;
        foreach (var resourceId in resourceIds.Distinct())
        {
            changed |= _research.DiscoverResource(resourceId);
        }

        if (!changed)
        {
            return;
        }

        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
    }

    public bool HasShipPowerConnections =>
        _pendingShipConnections.Count > 0 ||
        _connectionNetwork.GetConnectionsForMachine(PlayerShipNodeId).Count > 0;

#if DEBUG
    /// <summary>
    /// Exposes the persisted research owner to the headless integration smoke without
    /// making research-station rebinding part of the release API.
    /// </summary>
    public string? DebugActiveResearchStationId => _activeResearchStation?.Value;

    public double DebugGetResearchDemandForComet(string cometId) =>
        GetResearchDemandForComet(cometId);

    public void DebugSetPersistenceSuppressed(bool suppressed) =>
        _debugPersistenceSuppressed = suppressed;

    public void DebugRunDroppedItemRuntimeSmokeTest()
    {
        var sourceSlot = _astronautInventory.Slots.FirstOrDefault(slot => slot.IsEmpty)?.Index ?? -1;
        if (sourceSlot < 0 ||
            !_astronautInventory.AddToSlot(sourceSlot, ProductionItemIds.CopperWire, 3).Succeeded)
        {
            throw new InvalidOperationException("Dropped-item smoke could not prepare an isolated source stack.");
        }

        var priorIds = _droppedItems.Keys.ToHashSet(StringComparer.Ordinal);
        var owner = _dropOwnerPositionProvider?.Invoke() ?? Vector2.Zero;
        if (!TryDropInventoryStack(
                new InventorySlotAddress(InventoryMenuController.AstronautInventoryId, sourceSlot),
                owner + new Vector2(520, 75)))
        {
            _astronautInventory.RemoveFromSlot(sourceSlot, ProductionItemIds.CopperWire, 3);
            throw new InvalidOperationException("Dropped-item smoke could not create a safe inertial world stack.");
        }

        var created = _droppedItems.Values.Single(item => !priorIds.Contains(item.Id));
        if (created.ItemId != ProductionItemIds.CopperWire.Value || created.Amount != 3 ||
            new Vector2((float)created.VelocityX, (float)created.VelocityY).Length() >
            WorldItemDropConfiguration.MaximumInheritedSpeed + 0.01 ||
            !_droppedItemViews.ContainsKey(created.Id))
        {
            throw new InvalidOperationException("Dropped-item smoke observed invalid item, inertia or presentation state.");
        }

        RemoveDroppedItem(created.Id);
        if (!_astronautInventory.AddToSlot(sourceSlot, ProductionItemIds.CopperWire, 3).Succeeded ||
            !_astronautInventory.RemoveFromSlot(sourceSlot, ProductionItemIds.CopperWire, 3).Succeeded)
        {
            throw new InvalidOperationException("Dropped-item smoke could not restore its isolated inventory state.");
        }

        GD.Print("DROPPED_ITEM_RUNTIME_SMOKE_OK: atomic source, safe spawn, inherited bounded inertia, visible view, cleanup");
    }
#endif

    public event Action<MachineState>? MachineInteractionRequested;

    public event Action? FactoryStateChanged;

    public event Action? BuildCatalogChanged;

    public override void _Ready()
    {
        if (!_persistenceSmokeCompleted &&
            (OS.HasFeature("headless") ||
             DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase)))
        {
            FactoryStateJsonCodec.RunSchemaV6SmokeTest();
            _persistenceSmokeCompleted = true;
            GD.Print(
                "FACTORY_PERSISTENCE_V6_SMOKE_OK: discoveries, extraction bindings, batteries, radiation, inventories, connections, migrations");
        }

        _placementPreview = new MachinePlacementPreview { Name = "MachinePlacementPreview" };
        AddChild(_placementPreview);
        _placementPreview.ConfigureWorldSources(
            () => _loadedComets.Values.Where(GodotObject.IsInstanceValid).ToArray(),
            () => _machineViews.Values.Where(GodotObject.IsInstanceValid).ToArray(),
            () => _loadedResourceSources.Values.Where(GodotObject.IsInstanceValid).ToArray());
        _connectionPlacementPreview = new MachineConnectionPlacementPreview
        {
            Name = "MachineConnectionPlacementPreview",
        };
        AddChild(_connectionPlacementPreview);
        _powerCablePlacementPreview = new PowerCablePlacementPreview
        {
            Name = "PowerCablePlacementPreview",
        };
        AddChild(_powerCablePlacementPreview);
    }

#if DEBUG
    public void RunPowerCablePresentationSmokeTest()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Power cable presentation smoke requires an initialized runtime.");
        }

        var firstAnchor = new Node2D
        {
            Name = "PowerCableSmokeAnchorA",
            Position = new Vector2(-120, -80),
        };
        var secondAnchor = new Node2D
        {
            Name = "PowerCableSmokeAnchorB",
            Position = new Vector2(160, 95),
        };
        AddChild(firstAnchor);
        AddChild(secondAnchor);
        var first = new PowerCableVisualEndpoint(
            "smoke/source",
            () => firstAnchor.GlobalPosition,
            () => GodotObject.IsInstanceValid(firstAnchor));
        var second = new PowerCableVisualEndpoint(
            "smoke/target",
            () => secondAnchor.GlobalPosition,
            () => GodotObject.IsInstanceValid(secondAnchor));
        PowerCablePresentationView? cable = null;
        try
        {
            _powerCablePlacementPreview.Begin(
                (float)PowerGridConfiguration.MaximumCableLengthWorldUnits);
            _powerCablePlacementPreview.SetAvailableEndpoints([first, second]);
            _powerCablePlacementPreview.SetSource(first);
            _powerCablePlacementPreview.SetCandidate(second, isCompatible: true);
            if (!_powerCablePlacementPreview.IsActive ||
                !_powerCablePlacementPreview.Visible ||
                _powerCablePlacementPreview.AvailableEndpointCount != 2 ||
                !_powerCablePlacementPreview.IsCandidateValid)
            {
                throw new InvalidOperationException(
                    "The live cable preview did not expose both highlighted sockets and a valid route.");
            }

            cable = new PowerCablePresentationView();
            cable.Configure("power-cable-presentation-smoke", this, first, second);
            cable.SetEndpointTracking(enabled: true);
            cable.SetVisualState(enabled: true, energized: true, utilization: 0.5f);
            if (!cable.IsInsideTree() || !cable.Visible || cable.GetParent() != this ||
                cable.FirstEndpoint != first || cable.SecondEndpoint != second)
            {
                throw new InvalidOperationException("The visible live power cable was not attached to the world canvas.");
            }

            cable.QueueRedraw();
        }
        finally
        {
            _powerCablePlacementPreview.Cancel();
            cable?.QueueFree();
            firstAnchor.QueueFree();
            secondAnchor.QueueFree();
        }

        GD.Print(
            "POWER_CABLE_PRESENTATION_SMOKE_OK: item mode, two highlighted sockets, curved preview, visible world cable");
    }

    public void RunPowerCableRuntimeSmokeTest(AsteroidView comet)
    {
        ArgumentNullException.ThrowIfNull(comet);
        if (!_initialized || _ship is not { IsAttached: true } || _shipPowerNode is null ||
            !string.Equals(_shipPowerNode.CometId, comet.CometId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The real power cable runtime smoke requires the player ship attached to its test comet.");
        }

        var originalInventory = _astronautInventory;
        var originalHotbarInventory = _hotbarInventory;
        var originalLastInventory = _lastAstronautInventory;
        var originalLastHotbarInventory = _lastHotbarInventory;
        var originalDirty = _dirty;
        var originalAutosaveElapsed = _autosaveElapsed;
        var hadLoadedComet = _loadedComets.TryGetValue(comet.CometId, out var previousComet);
        var debugInventory = new SlotInventory(InventoryConfiguration.AstronautSlotCount);

        var generator = CreatePowerCableSmokeMachine(
            "power-smoke-generator",
            MachineDefinitionIds.BasicGenerator,
            comet.CometId,
            new Vector2(-150, -30));
        var pole = CreatePowerCableSmokeMachine(
            "power-smoke-pole",
            MachineDefinitionIds.PowerPole,
            comet.CometId,
            new Vector2(0, 70));
        var consumer = CreatePowerCableSmokeMachine(
            "power-smoke-consumer",
            MachineDefinitionIds.Crusher,
            comet.CometId,
            new Vector2(160, -20));
        var smokeMachines = new[] { generator, pole, consumer };
        try
        {
            _astronautInventory = debugInventory;
            _loadedComets[comet.CometId] = comet;
            RunHotbarPlacementSmokeTest();
            foreach (var machine in smokeMachines)
            {
                if (!_connectionNetwork.RegisterMachine(machine))
                {
                    throw new InvalidOperationException($"Smoke machine '{machine.InstanceId}' could not be registered.");
                }

                _machines.Add(machine.InstanceId, machine);
                IndexMachine(machine);
                CreateMachineView(machine, comet);
            }

            var generatorPort = new PowerInteractionTarget(generator.InstanceId, MachinePortIds.Power);
            var polePort1 = new PowerInteractionTarget(pole.InstanceId, MachinePortIds.Power1);
            var polePort2 = new PowerInteractionTarget(pole.InstanceId, MachinePortIds.Power2);
            var polePort3 = new PowerInteractionTarget(pole.InstanceId, MachinePortIds.Power3);
            var consumerPort = new PowerInteractionTarget(consumer.InstanceId, MachinePortIds.Power);
            var shipPort = new PowerInteractionTarget(PlayerShipNodeId, MachinePortIds.ShipPowerA);
            var originalConnectionIds = _connectionNetwork.Connections
                .Select(connection => connection.Id)
                .ToHashSet();

            if (!TryResolvePowerEndpoint(generatorPort, out var generatorEndpoint) ||
                FindNearestPowerInteraction(generatorEndpoint.WorldPosition) != generatorPort ||
                FindNearestInteractiveMachine(_machineViews[generator.InstanceId].GlobalPosition) is not null ||
                FindNearestInteractiveMachine(_machineViews[consumer.InstanceId].GlobalPosition)?.InstanceId !=
                consumer.InstanceId)
            {
                throw new InvalidOperationException(
                    "The shared E target must prefer power overview access at generators while retaining consumer machine menus.");
            }

            OpenPowerTarget(generatorPort);
            var standaloneGeneratorModel = RefreshOpenPowerViewModel();
            ClosePowerTarget();
            OpenPowerTarget(polePort1);
            var standalonePoleModel = RefreshOpenPowerViewModel();
            ClosePowerTarget();
            var consumerWasRejected = false;
            try
            {
                OpenPowerTarget(consumerPort);
            }
            catch (InvalidOperationException)
            {
                consumerWasRejected = true;
            }

            if (standaloneGeneratorModel is null || standalonePoleModel is null || !consumerWasRejected)
            {
                throw new InvalidOperationException(
                    "Only standalone generators, poles and the attached ship may open the power overview.");
            }

            GD.Print(
                "POWER_MENU_SCOPE_SMOKE_OK: standalone generator/pole open, production consumer rejected");

            ConnectPowerCableForRuntimeSmoke(generatorPort, polePort1);
            ConnectPowerCableForRuntimeSmoke(polePort2, consumerPort);
            ConnectPowerCableForRuntimeSmoke(shipPort, polePort3);

            var smokeConnections = _connectionNetwork.Connections
                .Where(connection => !originalConnectionIds.Contains(connection.Id))
                .ToArray();
            var smokeSnapshots = smokeConnections
                .Select(connection => connection.CreateSnapshot())
                .ToArray();
            if (debugInventory.GetAmount(ProductionItemIds.PowerCable) != 0 ||
                smokeConnections.Length != 3 ||
                smokeConnections.Any(connection =>
                    !_powerCableViews.TryGetValue(connection.Id, out var view) ||
                    !GodotObject.IsInstanceValid(view) ||
                    !view.IsInsideTree() || !view.Visible || view.HostComet != comet))
            {
                throw new InvalidOperationException(
                    "The real cable flow did not consume three items and create three visible world cables.");
            }

            var sharedNetwork = _connectionNetwork.GetPowerComponents(comet.CometId)
                .Single(component => component.Endpoints.Contains(
                    new MachineConnectionEndpoint(pole.InstanceId, MachinePortIds.Power1)));
            if (!sharedNetwork.NodeIds.Contains(generator.InstanceId) ||
                !sharedNetwork.NodeIds.Contains(pole.InstanceId) ||
                !sharedNetwork.NodeIds.Contains(consumer.InstanceId) ||
                !sharedNetwork.NodeIds.Contains(PlayerShipNodeId) ||
                smokeConnections.Any(connection =>
                    !sharedNetwork.ConnectionIds.Contains(connection.Id)))
            {
                throw new InvalidOperationException(
                    "Generator, pole, consumer and attached ship did not join the same live power network.");
            }

            OpenPowerTarget(polePort1);
            DisconnectOpenPowerPort(MachinePortIds.Power1.Value);
            DisconnectOpenPowerPort(MachinePortIds.Power2.Value);
            DisconnectOpenPowerPort(MachinePortIds.Power3.Value);
            if (debugInventory.GetAmount(ProductionItemIds.PowerCable) != 3 ||
                smokeConnections.Any(connection => _powerCableViews.ContainsKey(connection.Id)) ||
                _connectionNetwork.Connections.Any(connection =>
                    smokeConnections.Any(smoke => smoke.Id == connection.Id)))
            {
                throw new InvalidOperationException(
                    "Disconnecting the three live cables did not return every item and remove every view.");
            }

            if (!debugInventory.Remove(ProductionItemIds.PowerCable, 3).Succeeded)
            {
                throw new InvalidOperationException("The restore smoke could not apply its saved inventory snapshot.");
            }

            foreach (var snapshot in smokeSnapshots)
            {
                var restore = _connectionNetwork.TryRestore(snapshot);
                if (!restore.Succeeded || restore.Connection is null)
                {
                    throw new InvalidOperationException("A saved power cable could not be restored into the live graph.");
                }

                CreateConnectionView(restore.Connection);
            }

            if (smokeSnapshots.Any(snapshot =>
                    !_powerCableViews.TryGetValue(snapshot.ConnectionId, out var view) ||
                    !GodotObject.IsInstanceValid(view) || !view.IsInsideTree() || !view.Visible))
            {
                throw new InvalidOperationException("Restored cable data did not recreate visible world cables.");
            }

            foreach (var snapshot in smokeSnapshots)
            {
                _connectionNetwork.RemoveConnection(snapshot.ConnectionId);
                RemoveConnectionView(snapshot.ConnectionId);
            }

            if (!debugInventory.Add(ProductionItemIds.PowerCable, 3).Succeeded)
            {
                throw new InvalidOperationException("The restore smoke could not reset its isolated inventory.");
            }

            GD.Print(
                "POWER_CABLE_RUNTIME_SMOKE_OK: Build selection, real machine sockets, 3 inventory deductions, " +
                "generator-pole-machine-ship network, visible cables, disconnect refunds, save-view restore");
            RunMachineDismantlingSmokeTest(comet);
        }
        finally
        {
            CancelPlacement();
            ClosePowerTarget();
            foreach (var connection in _connectionNetwork.Connections
                         .Where(connection => smokeMachines.Any(machine =>
                             connection.Source.MachineId == machine.InstanceId ||
                             connection.Target.MachineId == machine.InstanceId))
                         .ToArray())
            {
                _connectionNetwork.RemoveConnection(connection.Id);
                RemoveConnectionView(connection.Id);
            }

            foreach (var machine in smokeMachines)
            {
                if (_machineViews.Remove(machine.InstanceId, out var view) &&
                    GodotObject.IsInstanceValid(view))
                {
                    view.InteractionRequested -= HandleMachineInteractionRequested;
                    view.QueueFree();
                }

                _connectionNetwork.UnregisterMachine(machine.InstanceId);
                _machines.Remove(machine.InstanceId);
                RemoveIndexedMachine(machine);
            }

            if (hadLoadedComet && previousComet is not null)
            {
                _loadedComets[comet.CometId] = previousComet;
            }
            else
            {
                _loadedComets.Remove(comet.CometId);
            }

            InvalidatePowerTopology(comet.CometId);
            UpdateShipPowerPortVisuals();
            _astronautInventory = originalInventory;
            _hotbarInventory = originalHotbarInventory;
            _lastAstronautInventory = originalLastInventory;
            _lastHotbarInventory = originalLastHotbarInventory;
            _dirty = originalDirty;
            _autosaveElapsed = originalAutosaveElapsed;
        }
    }

    private void RunHotbarPlacementSmokeTest()
    {
        var originalResearch = _research;
        var originalPlacementRotation = _placementRotationState.LastRotationRadians;
        var debugHotbar = new SlotInventory(
            InventoryConfiguration.HotbarSlotCount,
            InventoryConfiguration.MaximumStackSize,
            itemId => DefaultProductionItemCatalog.Instance.Get(itemId).MaximumStackSize);
        if (!debugHotbar.AddToSlot(0, ProductionItemIds.MobileMinerKit, 2).Succeeded ||
            !debugHotbar.AddToSlot(1, ProductionItemIds.MobileMinerKit, 2).Succeeded ||
            !debugHotbar.AddToSlot(2, ProductionItemIds.PowerCable, 3).Succeeded ||
            !debugHotbar.AddToSlot(3, ProductionItemIds.TransportPipe, 2).Succeeded)
        {
            throw new InvalidOperationException("The hotbar smoke could not create two isolated placement stacks.");
        }

        _hotbarInventory = debugHotbar;
        _research = new ResearchState(
            [DefaultResearchIds.BasicAutomation, DefaultResearchIds.MiningAutomation]);
        try
        {
            if (!StartPlacement(MachineDefinitionIds.BasicGenerator.Value))
            {
                throw new InvalidOperationException(
                    "The rotation smoke could not start a build-menu machine preview.");
            }

            var initialRotation = _placementPreview.RelativeRotationRadians;
            RotatePlacement(1);
            var rememberedRotation = _placementPreview.RelativeRotationRadians;
            if (Mathf.IsEqualApprox(initialRotation, rememberedRotation))
            {
                throw new InvalidOperationException(
                    "Rotating a build-menu preview did not update the shared placement angle.");
            }

            CancelPlacement();
            if (!StartPlacementFromHotbarSlot(1, ProductionItemIds.MobileMinerKit) ||
                !_placementPreview.IsActive ||
                _placementPreview.SelectedDefinitionId != MachineDefinitionIds.MobileMiner ||
                _hotbarPlacementSource != new HotbarPlacementSource(1, ProductionItemIds.MobileMinerKit) ||
                !Mathf.IsEqualApprox(
                    rememberedRotation,
                    _placementPreview.RelativeRotationRadians))
            {
                throw new InvalidOperationException(
                    "Hotbar machine placement did not start with the last shared build rotation.");
            }

            var mobileMiner = _machineCatalog.Get(MachineDefinitionIds.MobileMiner);
            if (mobileMiner.PlacementRequirement != MachinePlacementRequirement.ResourceDeposit ||
                mobileMiner.PlacementItemId != ProductionItemIds.MobileMinerKit)
            {
                throw new InvalidOperationException(
                    "Mobile miner hotbar placement is not restricted to a resource deposit.");
            }

            if (!TryConsumeActivePlacementItem(ProductionItemIds.MobileMinerKit) ||
                debugHotbar.GetSlot(0).Amount != 2 || debugHotbar.GetSlot(1).Amount != 1)
            {
                throw new InvalidOperationException(
                    "Hotbar placement consumed an equal item outside the selected slot.");
            }

            if (!TryRestoreActivePlacementItem(ProductionItemIds.MobileMinerKit) ||
                debugHotbar.GetSlot(0).Amount != 2 || debugHotbar.GetSlot(1).Amount != 2)
            {
                throw new InvalidOperationException(
                    "A failed hotbar placement did not restore the item to its selected slot.");
            }

            CancelPlacement();
            if (IsPlacementActive || _hotbarPlacementSource is not null)
            {
                throw new InvalidOperationException(
                    "Cancelling after a hotbar slot switch left a placement mode active.");
            }

            if (!StartPlacementFromHotbarSlot(3, ProductionItemIds.TransportPipe) ||
                _connectionPlacementPreview.SelectedType is not { } initialPipeType)
            {
                throw new InvalidOperationException("The transport pipe did not start from its hotbar item.");
            }

            var gasSource = new MachineState(
                new MachineInstanceId("hotbar-gas-source-smoke"),
                _machineCatalog.Get(MachineDefinitionIds.Electrolyzer),
                constructionCompleted: true);
            var gasRecipe = _recipeCatalog.Get(DefaultRecipeIds.ElectrolyzeWater);
            if (!gasSource.SelectRecipe(gasRecipe) ||
                ResolveHotbarConnectionType(gasSource, initialPipeType).Kind != ConnectionKind.GasPipe)
            {
                throw new InvalidOperationException(
                    "A transport-pipe item did not select the gas medium from its source recipe.");
            }

            GD.Print(
                "HOTBAR_PLACEMENT_SMOKE_OK: automatic mobile-miner mode, resource-source restriction, " +
                "shared build rotation, selected-slot consume/rollback, pipe-medium resolution, silent cancel");
        }
        finally
        {
            CancelPlacement();
            _placementRotationState.Remember(
                originalPlacementRotation,
                supportsRotation: true);
            _research = originalResearch;
        }
    }

    private void RunMachineDismantlingSmokeTest(AsteroidView comet)
    {
        var machine = CreatePowerCableSmokeMachine(
            "dismantling-smoke-workbench",
            MachineDefinitionIds.Workbench,
            comet.CometId,
            new Vector2(0, -190));
        var originalInventory = _astronautInventory;
        try
        {
            if (!_connectionNetwork.RegisterMachine(machine))
            {
                throw new InvalidOperationException("The dismantling smoke machine could not be registered.");
            }

            _machines.Add(machine.InstanceId, machine);
            IndexMachine(machine);
            CreateMachineView(machine, comet);
            var view = _machineViews[machine.InstanceId];
            var activeRecipe = _recipeCatalog.Get(DefaultRecipeIds.MakeMiningTool);
            if (!machine.SelectRecipe(activeRecipe) ||
                !machine.InputInventory.Add(ProductionItemIds.IronPlate, 2).Succeeded ||
                !machine.InputInventory.Add(ProductionItemIds.CopperWire, 2).Succeeded ||
                !machine.InputInventory.Add(ProductionItemIds.IronRod, 1).Succeeded)
            {
                throw new InvalidOperationException("The dismantling smoke could not seed machine contents.");
            }
            var production = machine.TickProduction(
                activeRecipe,
                0.5,
                activeRecipe.RequiredPowerKilowatts);
            if (!machine.IsBatchInProgress || production.Status != MachineOperationStatus.Producing ||
                machine.InputInventory.TotalItemCount != 0)
            {
                throw new InvalidOperationException(
                    "The dismantling smoke could not start an in-flight production batch.");
            }

            var fullInventory = new SlotInventory(
                1,
                InventoryConfiguration.MaximumStackSize,
                itemId => DefaultProductionItemCatalog.Instance.Get(itemId).MaximumStackSize);
            if (!fullInventory.Add(
                    ProductionItemIds.IronOre,
                    InventoryConfiguration.MaximumStackSize).Succeeded)
            {
                throw new InvalidOperationException("The dismantling smoke could not create a full inventory.");
            }

            _astronautInventory = fullInventory;
            if (TryDismantleMachine(view) || !_machines.ContainsKey(machine.InstanceId) ||
                !_machineViews.ContainsKey(machine.InstanceId))
            {
                throw new InvalidOperationException(
                    "A full inventory removed a machine before its refund could be stored.");
            }

            var recoveryInventory = new SlotInventory(
                InventoryConfiguration.AstronautSlotCount,
                InventoryConfiguration.MaximumStackSize,
                itemId => DefaultProductionItemCatalog.Instance.Get(itemId).MaximumStackSize);
            _astronautInventory = recoveryInventory;
            if (!TryDismantleMachine(view) || _machines.ContainsKey(machine.InstanceId) ||
                _machineViews.ContainsKey(machine.InstanceId) ||
                recoveryInventory.GetAmount(ProductionItemIds.IronPlate) < 2)
            {
                throw new InvalidOperationException(
                    "Machine dismantling did not atomically remove the object and return its contents/materials.");
            }
            if (recoveryInventory.GetAmount(ProductionItemIds.CopperWire) < 2 ||
                recoveryInventory.GetAmount(ProductionItemIds.IronRod) < 1)
            {
                throw new InvalidOperationException(
                    "Machine dismantling lost the inputs reserved by an in-flight batch.");
            }

            GD.Print(
                "MACHINE_DISMANTLING_SMOKE_OK: full-inventory rejection, atomic removal, " +
                "build/content/in-flight refund");
        }
        finally
        {
            _astronautInventory = originalInventory;
            if (_machineViews.Remove(machine.InstanceId, out var remainingView) &&
                GodotObject.IsInstanceValid(remainingView))
            {
                remainingView.InteractionRequested -= HandleMachineInteractionRequested;
                remainingView.QueueFree();
            }

            _connectionNetwork.UnregisterMachine(machine.InstanceId);
            _machines.Remove(machine.InstanceId);
            RemoveIndexedMachine(machine);
        }
    }

    private MachineState CreatePowerCableSmokeMachine(
        string instanceId,
        MachineDefinitionId definitionId,
        string cometId,
        Vector2 localPosition)
    {
        var machine = new MachineState(
            new MachineInstanceId(instanceId),
            _machineCatalog.Get(definitionId),
            new MachinePlacement(cometId, localPosition.X, localPosition.Y, 0),
            constructionCompleted: true);
        machine.SetEnabled(true);
        return machine;
    }

    private void ConnectPowerCableForRuntimeSmoke(
        PowerInteractionTarget source,
        PowerInteractionTarget target)
    {
        var placementStarted = StartPlacementFromHotbarSlot(2, ProductionItemIds.PowerCable);
        var sourceResolved = TryResolvePowerEndpoint(source, out var sourceEndpoint);
        var targetResolved = TryResolvePowerEndpoint(target, out var targetEndpoint);
        if (!placementStarted || !sourceResolved || !targetResolved)
        {
            throw new InvalidOperationException(
                $"The real power cable flow could not start " +
                $"(placement={placementStarted}, source={sourceResolved}, target={targetResolved}).");
        }

        PushPowerCableRuntimeSmokeClick(sourceEndpoint.WorldPosition);
        if (_powerCablePlacementPreview.AvailableEndpointCount < 2 ||
            _powerCableSourceTarget != source ||
            !_powerCablePlacementPreview.IsActive)
        {
            throw new InvalidOperationException(
                "The first physical socket click did not survive screen-to-world conversion.");
        }

        PushPowerCableRuntimeSmokeClick(targetEndpoint.WorldPosition);
        if (_powerCablePlacementPreview.IsActive || _powerCableSourceTarget is not null)
        {
            throw new InvalidOperationException(
                "The second physical socket click did not survive screen-to-world conversion.");
        }
    }

    private void PushPowerCableRuntimeSmokeClick(Vector2 worldPosition)
    {
        var viewport = GetViewport();
        var screenPosition = viewport.GetCanvasTransform() * worldPosition;
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = screenPosition,
            GlobalPosition = screenPosition,
        }, inLocalCoords: true);
        RefreshPowerCablePlacementCandidate(worldPosition);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = screenPosition,
            GlobalPosition = screenPosition,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = screenPosition,
            GlobalPosition = screenPosition,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, inLocalCoords: true);
    }

#endif

    public override void _Process(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        if (_powerCablePlacementPreview.IsActive)
        {
            RefreshPowerCablePlacementCandidate();
        }

        if (_connectionPlacementPreview.IsActive)
        {
            RefreshConnectionPlacementCandidate();
        }

        _droppedItemRefreshElapsed += delta;
        if (_droppedItemRefreshElapsed >= 0.2)
        {
            _droppedItemRefreshElapsed = 0;
            RefreshDroppedItems();
        }

        _simulationElapsed += delta;
        if (_simulationElapsed >= SimulationIntervalSeconds)
        {
            var elapsed = _simulationElapsed;
            _simulationElapsed = 0;
            SimulateLoadedComets(elapsed);
            if (DetectPlayerInventoryChanges())
            {
                MarkDirty();
                BuildCatalogChanged?.Invoke();
            }
        }

        if (!_dirty)
        {
            return;
        }

        _autosaveElapsed += delta;
        if (_autosaveElapsed >= AutosaveIntervalSeconds)
        {
            SaveNow();
        }
    }

    public void Initialize(
        SlotInventory astronautInventory,
        SlotInventory hotbarInventory,
        ToolInventoryState toolInventoryState,
        ItemPresentationCatalog itemPresentation,
        IFactoryStateStore stateStore,
        Func<double> shipFuelProvider,
        Func<ShipFuelType> shipFuelTypeProvider,
        Action<double, ShipFuelType> restoreShipFuel,
        Func<int> activeHotbarSlotProvider,
        Action<int> restoreActiveHotbarSlot,
        Action<string> showMessage)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(hotbarInventory);
        ArgumentNullException.ThrowIfNull(toolInventoryState);
        ArgumentNullException.ThrowIfNull(itemPresentation);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(shipFuelProvider);
        ArgumentNullException.ThrowIfNull(shipFuelTypeProvider);
        ArgumentNullException.ThrowIfNull(restoreShipFuel);
        ArgumentNullException.ThrowIfNull(activeHotbarSlotProvider);
        ArgumentNullException.ThrowIfNull(restoreActiveHotbarSlot);
        ArgumentNullException.ThrowIfNull(showMessage);
        if (_initialized)
        {
            throw new InvalidOperationException("Factory runtime is already initialized.");
        }

        _astronautInventory = astronautInventory;
        _hotbarInventory = hotbarInventory;
        _toolInventoryState = toolInventoryState;
        _itemPresentation = itemPresentation;
        _stateStore = stateStore;
        _shipFuelProvider = shipFuelProvider;
        _shipFuelTypeProvider = shipFuelTypeProvider;
        _restoreShipFuel = restoreShipFuel;
        _activeHotbarSlotProvider = activeHotbarSlotProvider;
        _restoreActiveHotbarSlot = restoreActiveHotbarSlot;
        _showMessage = showMessage;
        Restore(stateStore.Load());
        RememberPlayerInventories();
        _initialized = true;
    }

    /// <summary>
    /// Attaches the independently owned ship inventory after the main runtime initialization.
    /// This preserves the existing Initialize API while allowing its persisted v2 state to be
    /// restored before the first autosave.
    /// </summary>
    public void AttachShipInventory(SlotInventory shipInventory)
    {
        ArgumentNullException.ThrowIfNull(shipInventory);
        if (!_initialized)
        {
            throw new InvalidOperationException("Factory runtime must be initialized first.");
        }

        if (_shipInventory is not null && !ReferenceEquals(_shipInventory, shipInventory))
        {
            throw new InvalidOperationException("A different ship inventory is already attached.");
        }

        _shipInventory = shipInventory;
        InventoryStatePersistence.Restore(_shipInventory, _pendingShipInventory);
        _pendingShipInventory = [];
        RepairMissingStarterPowerCables();
        RememberPlayerInventories();
    }

    /// <summary>
    /// Supplies the current actor pose without giving dropped world items ownership of player or
    /// ship controllers. The active owner decides inherited inertia and pickup availability.
    /// </summary>
    public void AttachWorldItemContext(
        Func<Vector2> ownerPositionProvider,
        Func<Vector2> ownerVelocityProvider,
        Func<bool> pickupActiveProvider)
    {
        ArgumentNullException.ThrowIfNull(ownerPositionProvider);
        ArgumentNullException.ThrowIfNull(ownerVelocityProvider);
        ArgumentNullException.ThrowIfNull(pickupActiveProvider);
        _dropOwnerPositionProvider = ownerPositionProvider;
        _dropOwnerVelocityProvider = ownerVelocityProvider;
        _dropPickupActiveProvider = pickupActiveProvider;
        RefreshDroppedItemViews(force: true);
    }

    /// <summary>
    /// Atomically releases the complete selected stack. Inventory removal is rolled back into the
    /// exact original slot if no collision-free spawn pose or world presentation can be created.
    /// </summary>
    public bool TryDropInventoryStack(InventorySlotAddress source, Vector2 requestedWorldPosition)
    {
        if (!_initialized || !requestedWorldPosition.IsFinite() ||
            !TryResolveDropSource(source, out var inventory) ||
            !WorldItemDropTransaction.TryReserveEntireStack(inventory, source.SlotIndex, out var reservation))
        {
            return false;
        }

        string? createdDropId = null;
        try
        {
            var ownerPosition = _dropOwnerPositionProvider?.Invoke() ?? requestedWorldPosition;
            if (!TryFindSafeDropPosition(ownerPosition, requestedWorldPosition, out var spawnPosition))
            {
                if (!WorldItemDropTransaction.Rollback(inventory, reservation))
                {
                    throw new InvalidOperationException("A failed world drop could not restore its source slot.");
                }

                _showMessage("Kein sicherer Platz");
                return false;
            }

            var inherited = LimitDroppedItemVelocity(_dropOwnerVelocityProvider?.Invoke() ?? Vector2.Zero);
            if (inherited.LengthSquared() < 1)
            {
                var direction = (requestedWorldPosition - ownerPosition).Normalized();
                inherited = (direction == Vector2.Zero ? Vector2.Right : direction) *
                            (float)WorldItemDropConfiguration.StationaryDropImpulse;
            }

            var id = $"drop-{Guid.NewGuid():N}";
            createdDropId = id;
            var angularMagnitude = Mathf.Lerp(
                (float)WorldItemDropConfiguration.MinimumAngularSpeedRadians,
                (float)WorldItemDropConfiguration.MaximumAngularSpeedRadians,
                Mathf.Abs(id.GetHashCode() % 1000) / 999.0f);
            var state = new DroppedItemStateData(
                id,
                reservation.ItemId.Value,
                reservation.Amount,
                spawnPosition.X,
                spawnPosition.Y,
                0,
                inherited.X,
                inherited.Y,
                id.GetHashCode() % 2 == 0 ? angularMagnitude : -angularMagnitude);
            _droppedItems.Add(id, state);
            if (ShouldPresentDroppedItem(state) && !TryCreateDroppedItemView(state))
            {
                _droppedItems.Remove(id);
                if (!WorldItemDropTransaction.Rollback(inventory, reservation))
                {
                    throw new InvalidOperationException("A failed world drop could not restore its source slot.");
                }

                return false;
            }

            MarkInventoryChanged();
            return true;
        }
        catch
        {
            if (createdDropId is not null)
            {
                RemoveDroppedItem(createdDropId);
            }

            if (inventory.GetSlot(source.SlotIndex).IsEmpty)
            {
                if (!WorldItemDropTransaction.Rollback(inventory, reservation))
                {
                    throw new InvalidOperationException(
                        "A failed world-drop transaction could not restore its source slot.");
                }
            }

            throw;
        }
    }

    private void RepairMissingStarterPowerCables()
    {
        if (_shipInventory is null ||
            _astronautInventory.GetAmount(ProductionItemIds.PowerCable) > 0 ||
            _shipInventory.GetAmount(ProductionItemIds.PowerCable) > 0 ||
            _connectionNetwork.Connections.Any(connection =>
                connection.Kind == ConnectionKind.PowerCable) ||
            _pendingShipConnections.Any(connection =>
                connection.Kind == ConnectionKind.PowerCable))
        {
            return;
        }

        var result = _shipInventory.Add(
            ProductionItemIds.PowerCable,
            LogisticsConfiguration.StartingPowerCableCount);
        if (!result.Succeeded)
        {
            GD.PushWarning(
                "The missing starter power cables could not be repaired because the ship inventory is full.");
            return;
        }

        MarkDirty();
        GD.Print(
            $"STARTER_POWER_CABLE_REPAIR_OK: restored {LogisticsConfiguration.StartingPowerCableCount} ship-storage cables");
    }

    public void AttachShipPower(PlayerShipController ship, int sectorSize)
    {
        ArgumentNullException.ThrowIfNull(ship);
        if (!_initialized)
        {
            throw new InvalidOperationException("Factory runtime must be initialized first.");
        }

        if (sectorSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorSize));
        }

        if (_ship is not null && !ReferenceEquals(_ship, ship))
        {
            throw new InvalidOperationException("A different player ship is already attached.");
        }

        _ship = ship;
        _sectorSize = sectorSize;
        ship.RestoreFreePose(
            new Vector2(
                (float)_persistedShipDocking.GlobalPositionX,
                (float)_persistedShipDocking.GlobalPositionY),
            (float)_persistedShipDocking.GlobalRotationRadians,
            _persistedShipDocking.IsAttached ? 0 : _persistedShipDocking.LandingLegProgress);
        if (!_persistedShipDocking.IsAttached)
        {
            SynchronizeShipPowerDocking(markDirty: false);
        }
        else if (_loadedComets.TryGetValue(_persistedShipDocking.CometId ?? string.Empty, out var comet))
        {
            TryRestorePersistedShipDocking(comet);
        }

        UpdateShipPowerPortVisuals();
    }

    public void SynchronizeShipPowerDocking() => SynchronizeShipPowerDocking(markDirty: true);

    private void SynchronizeShipPowerDocking(bool markDirty)
    {
        if (_ship is null)
        {
            return;
        }

        if (markDirty)
        {
            _awaitingShipDockingRestore = false;
        }

        if (_ship.IsAttached && _ship.DockingState.AttachedCometId is { } cometId)
        {
            if (_shipPowerNode is null ||
                !string.Equals(_shipPowerNode.CometId, cometId, StringComparison.Ordinal))
            {
                if (_shipPowerNode is not null)
                {
                    if (HasShipPowerConnections)
                    {
                        throw new InvalidOperationException(
                            "The ship cannot change its power-grid comet while cables are connected.");
                    }

                    _connectionNetwork.UnregisterPowerNode(PlayerShipNodeId);
                }

                _shipPowerNode = new ShipPowerNode(
                    PlayerShipNodeId,
                    cometId,
                    _ship.FuelTank,
                    _persistedShipPower.ConnectorAEnabled,
                    _persistedShipPower.ConnectorBEnabled);
                if (!_connectionNetwork.RegisterPowerNode(_shipPowerNode))
                {
                    throw new InvalidOperationException("The ship power node could not be registered.");
                }

                RestorePendingShipConnections();
                InvalidatePowerTopology(cometId);
            }
        }
        else if (_shipPowerNode is not null)
        {
            if (HasShipPowerConnections)
            {
                throw new InvalidOperationException("Ship power cables must be disconnected before detaching.");
            }

            var detachedCometId = _shipPowerNode.CometId;
            _persistedShipPower = CaptureShipPowerState();
            _connectionNetwork.UnregisterPowerNode(PlayerShipNodeId);
            _shipPowerNode = null;
            InvalidatePowerTopology(detachedCometId);
        }

        UpdateShipPowerPortVisuals();
        if (markDirty)
        {
            MarkDirty();
            FactoryStateChanged?.Invoke();
        }
    }

    public void RegisterSector(SectorView sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        foreach (var resource in sector.Resources.Where(GodotObject.IsInstanceValid))
        {
            _loadedResourceSources[resource.DepositId] = resource;
        }

        foreach (var comet in sector.Comets)
        {
            RegisterComet(comet);
        }
    }

    public void UnregisterSector(SectorView sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        foreach (var resource in sector.Resources)
        {
            _loadedResourceSources.Remove(resource.DepositId);
        }

        var persistentStateChanged = false;
        foreach (var comet in sector.Comets)
        {
            if (!_loadedComets.Remove(comet.CometId))
            {
                continue;
            }

            if (GetMachinesOnComet(comet.CometId).Any())
            {
                _lastSimulatedUtc[comet.CometId] = DateTimeOffset.UtcNow;
                persistentStateChanged = true;
            }
            foreach (var pair in _machineViews
                         .Where(pair => pair.Value.HostComet == comet)
                         .ToArray())
            {
                pair.Value.InteractionRequested -= HandleMachineInteractionRequested;
                _machineViews.Remove(pair.Key);
            }

            foreach (var pair in _connectionViews
                         .Where(pair => pair.Value.HostComet == comet)
                         .ToArray())
            {
                _connectionViews.Remove(pair.Key);
            }

            foreach (var pair in _powerCableViews
                         .Where(pair => pair.Value.HostComet == comet)
                         .ToArray())
            {
                _powerCableViews.Remove(pair.Key);
            }

            // The binding is persistent identity, not a loaded-view reference. Removing
            // it here would allow an active project to jump to another research station.
            _lastPowerResults.Remove(comet.CometId);
        }

        if (persistentStateChanged)
        {
            MarkDirty();
        }
    }

    public IReadOnlyList<BuildMachineViewModel> CreateBuildMenuViewModels()
    {
        var machines = _machineCatalog.DirectBuildMenuEntries
            .Where(IsMachineUnlocked)
            .OrderBy(machine => machine.Category)
            .ThenBy(machine => machine.DisplayName, StringComparer.CurrentCulture)
            .Select(CreateBuildMachineViewModel)
            .ToArray();
        var connections = _connectionTypes.All
            .Where(IsConnectionUnlocked)
            .OrderBy(connection => connection.DisplayName, StringComparer.CurrentCulture)
            .Select(CreateConnectionBuildViewModel)
            .ToArray();
        return [.. machines, .. connections];
    }

    public bool StartPlacement(string machineDefinitionId)
    {
        if (ConnectionPresentationCatalog.TryGet(machineDefinitionId, out var connectionPresentation) &&
            connectionPresentation is not null &&
            _connectionTypes.TryGet(connectionPresentation.TypeId, out var connectionType) &&
            connectionType is not null)
        {
            if (!IsConnectionUnlocked(connectionType))
            {
                _showMessage("Verbindung noch nicht erforscht");
                return false;
            }

            CancelPlacement();
            if (connectionType.Kind == ConnectionKind.PowerCable)
            {
                _powerCablePlacementPreview.Begin(
                    (float)PowerGridConfiguration.MaximumCableLengthWorldUnits);
            }
            else
            {
                _connectionPlacementPreview.Start(connectionType);
            }

            return true;
        }

        var id = new MachineDefinitionId(machineDefinitionId);
        if (!_machineCatalog.TryGet(id, out var definition) || definition is null)
        {
            return false;
        }

        if (!definition.IsDirectBuildMenuEntry || !IsMachineUnlocked(definition))
        {
            _showMessage("Maschine noch nicht erforscht");
            return false;
        }

        CancelPlacement();
        var presentation = MachinePresentationCatalog.Instance.Get(id);
        _placementPreview.Start(
            id,
            HasBuildMaterials,
            (float)_placementRotationState.ResolveInitialRotation(presentation.SupportsRotation));
        return true;
    }

    public bool StartPlacementFromHotbarSlot(int slotIndex, ItemId itemId)
    {
        if (slotIndex < 0 || slotIndex >= _hotbarInventory.SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        var slot = _hotbarInventory.GetSlot(slotIndex);
        if (slot.ItemId != itemId || slot.Amount <= 0)
        {
            return false;
        }

        CancelPlacement();
        if (_machineCatalog.TryGetByPlacementItem(itemId, out var machine) && machine is not null)
        {
            _hotbarPlacementSource = new HotbarPlacementSource(slotIndex, itemId);
            var presentation = MachinePresentationCatalog.Instance.Get(machine.Id);
            _placementPreview.Start(
                machine.Id,
                HasHotbarPlacementItem,
                (float)_placementRotationState.ResolveInitialRotation(presentation.SupportsRotation));
            return true;
        }

        var connection = _connectionTypes.ForBuildItem(itemId).FirstOrDefault();
        if (connection is null)
        {
            return false;
        }

        _hotbarPlacementSource = new HotbarPlacementSource(slotIndex, itemId);
        if (connection.Kind == ConnectionKind.PowerCable)
        {
            _powerCablePlacementPreview.Begin(
                (float)PowerGridConfiguration.MaximumCableLengthWorldUnits);
        }
        else
        {
            _connectionPlacementPreview.Start(connection);
        }

        return true;
    }

    public void CancelPlacement()
    {
        _placementPreview.Cancel();
        _connectionPlacementPreview.Cancel();
        _powerCablePlacementPreview.Cancel();
        _powerCableSourceTarget = null;
        _powerCableCandidateTarget = null;
        _hotbarPlacementSource = null;
    }

    public void RotatePlacement(int direction)
    {
        if (_placementPreview.IsActive)
        {
            _placementPreview.Rotate(direction);
            _placementRotationState.Remember(
                _placementPreview.RelativeRotationRadians,
                _placementPreview.SupportsRotation);
        }
    }

    public bool TryPlaceSelectedMachine() =>
        TryPlaceSelectedMachineAtWorldPosition(GetGlobalMousePosition());

    public bool TryPlaceSelectedMachineAtViewportPosition(Vector2 viewportPosition)
    {
        if (!viewportPosition.IsFinite())
        {
            throw new ArgumentException("A placement click needs a finite viewport position.", nameof(viewportPosition));
        }

        var worldPosition = GetViewport().GetCanvasTransform().AffineInverse() * viewportPosition;
        return TryPlaceSelectedMachineAtWorldPosition(worldPosition);
    }

    private bool TryPlaceSelectedMachineAtWorldPosition(Vector2 worldPosition)
    {
        if (_powerCablePlacementPreview.IsActive)
        {
            return TryAdvancePowerCablePlacement(worldPosition);
        }

        if (_connectionPlacementPreview.IsActive)
        {
            return TryAdvanceConnectionPlacement();
        }

        _placementPreview.Refresh();
        if (!_placementPreview.TryGetPlacement(out var placement, out var comet, out var failure) ||
            placement is null || comet is null ||
            _placementPreview.SelectedDefinitionId is not { } definitionId)
        {
            _showMessage(MachinePlacementPreview.GetReasonText(failure));
            return false;
        }

        var definition = _machineCatalog.Get(definitionId);
        ResourceDepositView? extractionSource = null;
        if (definition.PlacementRequirement == MachinePlacementRequirement.ResourceDeposit)
        {
            extractionSource = FindResourceSourceForPlacement(comet, placement);
            if (extractionSource is null)
            {
                _showMessage(MachinePlacementPreview.GetReasonText(
                    MachinePlacementFailureReason.ResourceSourceRequired));
                return false;
            }

            if (_machines.Values.Any(machine =>
                    string.Equals(
                        machine.ExtractionSource?.SourceId,
                        extractionSource.DepositId,
                        StringComparison.Ordinal)))
            {
                _showMessage(MachinePlacementPreview.GetReasonText(
                    MachinePlacementFailureReason.ResourceSourceOccupied));
                return false;
            }
        }

        var costs = GetActivePlacementCosts(definition);
        if (!HasActivePlacementCosts(costs))
        {
            _showMessage("Materialien fehlen");
            return false;
        }

        var constructionCostsPaid = !_firstBasicGenerator.IsFreeBuildAvailable(definition);

        var state = new MachineState(
            new MachineInstanceId($"machine:{Guid.NewGuid():N}"),
            definition,
            placement,
            constructionCostsPaid: constructionCostsPaid);
        if (extractionSource is not null)
        {
            state.BindExtractionSource(new ExtractionSourceBinding(
                extractionSource.DepositId,
                extractionSource.Deposit.ResourceId,
                extractionSource.Purity,
                extractionSource.Deposit.BaseExtractionUnitsPerMinute));
            var extractionRecipe = _recipeCatalog.ForMachine(definition.Id)
                .SingleOrDefault(recipe =>
                    recipe.SourceResourceId == extractionSource.Deposit.ResourceId);
            if (extractionRecipe is null)
            {
                _showMessage("Erzart nicht unterstützt");
                return false;
            }

            if (!IsRecipeUnlocked(extractionRecipe))
            {
                _showMessage(GetRecipeUnlockMessage(extractionRecipe));
                return false;
            }

            if (!state.SelectRecipe(extractionRecipe))
            {
                _showMessage("Das Miner-Rezept konnte nicht ausgewählt werden");
                return false;
            }
        }

        if (definition.Kind is MachineKind.Generator or MachineKind.Research or
            MachineKind.Storage or MachineKind.Infrastructure || extractionSource is not null)
        {
            state.SetEnabled(true);
        }

        if (!TryConsumeActivePlacementCosts(costs))
        {
            _showMessage("Materialien konnten nicht abgezogen werden");
            return false;
        }

        try
        {
            _machines.Add(state.InstanceId, state);
            IndexMachine(state);
            _connectionNetwork.RegisterMachine(state);
            CreateMachineView(state, comet);
            _lastSimulatedUtc[comet.CometId] = DateTimeOffset.UtcNow;
            InvalidatePowerTopology(comet.CometId);
            _firstBasicGenerator.TryConsumeFreeBuild(definition);
        }
        catch
        {
            _machines.Remove(state.InstanceId);
            RemoveIndexedMachine(state);
            _connectionNetwork.UnregisterMachine(state.InstanceId);
            InvalidatePowerTopology(comet.CometId);

            if (!TryRestoreActivePlacementCosts(costs))
            {
                throw new InvalidOperationException("A failed construction could not restore its build costs.");
            }

            throw;
        }

        CancelPlacement();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage($"{definition.DisplayName} wird gebaut");
        return true;
    }

    public bool TryBeginDismantlingAtViewportPosition(
        Vector2 viewportPosition,
        Vector2 playerWorldPosition,
        ItemId activeToolItemId)
    {
        if (!viewportPosition.IsFinite() || !playerWorldPosition.IsFinite())
        {
            throw new ArgumentException("Dismantling coordinates must be finite.");
        }

        if (!DismantlingRules.IsDismantlingTool(activeToolItemId))
        {
            return false;
        }

        var worldPosition = ViewportToWorld(viewportPosition);
        var target = ResolveDismantlingTarget(worldPosition);
        if (target is null)
        {
            CancelDismantling();
            return false;
        }

        if (playerWorldPosition.DistanceTo(target.WorldPosition) >
            DismantlingConfiguration.InteractionRangeWorldUnits)
        {
            _showMessage("Objekt ist zu weit entfernt");
            CancelDismantling();
            return false;
        }

        if (_dismantlingTarget is { } current && current.StableId == target.StableId)
        {
            return true;
        }

        CancelDismantling();
        _dismantlingTarget = target;
        _dismantlingProgress.Begin(target.StableId, target.DurationSeconds);
        UpdateDismantlingTargetVisual((float)_dismantlingProgress.Progress);
        return true;
    }

    /// <summary>
    /// Advances the held dismantling interaction. Every invalidating condition cancels before
    /// the atomic removal/refund path is reached.
    /// </summary>
    public bool AdvanceDismantling(
        double deltaSeconds,
        Vector2 viewportPosition,
        Vector2 playerWorldPosition,
        ItemId? activeToolItemId,
        bool interactionHeld)
    {
        if (!IsDismantling)
        {
            return false;
        }

        if (!interactionHeld || activeToolItemId is not { } tool ||
            !DismantlingRules.IsDismantlingTool(tool) ||
            !viewportPosition.IsFinite() || !playerWorldPosition.IsFinite())
        {
            CancelDismantling();
            return false;
        }

        var hoveredTarget = ResolveDismantlingTarget(ViewportToWorld(viewportPosition));
        if (hoveredTarget is null || _dismantlingTarget is not { } target ||
            !string.Equals(hoveredTarget.StableId, target.StableId, StringComparison.Ordinal) ||
            !TryGetDismantlingTargetWorldPosition(out var currentTargetPosition) ||
            playerWorldPosition.DistanceTo(currentTargetPosition) >
            DismantlingConfiguration.InteractionRangeWorldUnits)
        {
            CancelDismantling();
            return false;
        }

        var completed = _dismantlingProgress.Advance(deltaSeconds);
        UpdateDismantlingTargetVisual((float)_dismantlingProgress.Progress);
        if (!completed)
        {
            return false;
        }

        var completedTarget = target;
        CancelDismantling();
        if (completedTarget.MachineId is { } machineId &&
            _machineViews.TryGetValue(machineId, out var machineView) &&
            GodotObject.IsInstanceValid(machineView))
        {
            return TryDismantleMachine(machineView);
        }

        if (completedTarget.ConnectionId is { } connectionId)
        {
            var connection = _connectionNetwork.Connections
                .FirstOrDefault(item => item.Id == connectionId);
            return connection is not null && TryDismantleConnection(connection);
        }

        return false;
    }

    public void CancelDismantling()
    {
        UpdateDismantlingTargetVisual(0);
        _dismantlingTarget = null;
        _dismantlingProgress.Cancel();
    }

    private Vector2 ViewportToWorld(Vector2 viewportPosition) =>
        GetViewport().GetCanvasTransform().AffineInverse() * viewportPosition;

    private DismantlingTarget? ResolveDismantlingTarget(Vector2 worldPosition)
    {
        var connection = FindDismantlingConnection(worldPosition);
        if (connection is not null)
        {
            return new DismantlingTarget(
                $"connection:{connection.Id.Value}",
                null,
                connection.Id,
                worldPosition,
                DismantlingConfiguration.ConnectionDismantlingDurationSeconds);
        }

        var machineView = _machineViews.Values
            .Where(view => GodotObject.IsInstanceValid(view) && view.ContainsWorldPoint(worldPosition))
            .OrderBy(view => view.GlobalPosition.DistanceSquaredTo(worldPosition))
            .FirstOrDefault();
        return machineView is null
            ? null
            : new DismantlingTarget(
                $"machine:{machineView.InstanceId.Value}",
                machineView.InstanceId,
                null,
                machineView.GlobalPosition,
                DismantlingConfiguration.MachineDismantlingDurationSeconds);
    }

    private bool TryGetDismantlingTargetWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = default;
        if (_dismantlingTarget is not { } target)
        {
            return false;
        }

        if (target.MachineId is { } machineId)
        {
            if (!_machineViews.TryGetValue(machineId, out var view) ||
                !GodotObject.IsInstanceValid(view))
            {
                return false;
            }

            worldPosition = view.GlobalPosition;
            return true;
        }

        if (target.ConnectionId is { } connectionId &&
            _connectionNetwork.Connections.Any(item => item.Id == connectionId))
        {
            worldPosition = target.WorldPosition;
            return true;
        }

        return false;
    }

    private void UpdateDismantlingTargetVisual(float progress)
    {
        if (_dismantlingTarget?.MachineId is { } machineId &&
            _machineViews.TryGetValue(machineId, out var machineView) &&
            GodotObject.IsInstanceValid(machineView))
        {
            machineView.SetDismantlingProgress(progress);
            return;
        }

        if (_dismantlingTarget?.ConnectionId is not { } connectionId)
        {
            return;
        }

        if (_connectionViews.TryGetValue(connectionId, out var connectionView) &&
            GodotObject.IsInstanceValid(connectionView))
        {
            connectionView.SetDismantlingProgress(progress);
        }

        if (_powerCableViews.TryGetValue(connectionId, out var powerCableView) &&
            GodotObject.IsInstanceValid(powerCableView))
        {
            powerCableView.SetDismantlingProgress(progress);
        }
    }

    private MachineConnection? FindDismantlingConnection(Vector2 worldPosition)
    {
        var regular = _connectionViews
            .Where(pair => GodotObject.IsInstanceValid(pair.Value))
            .Select(pair => new
            {
                pair.Key,
                Distance = pair.Value.DistanceToWorldPoint(worldPosition),
            });
        var power = _powerCableViews
            .Where(pair => GodotObject.IsInstanceValid(pair.Value))
            .Select(pair => new
            {
                pair.Key,
                Distance = pair.Value.DistanceToWorldPoint(worldPosition),
            });
        var candidate = regular.Concat(power)
            .Where(item => item.Distance <= DismantlingConfiguration.ConnectionSelectionRadiusWorldUnits)
            .OrderBy(item => item.Distance)
            .FirstOrDefault();
        return candidate is null
            ? null
            : _connectionNetwork.Connections.FirstOrDefault(item => item.Id == candidate.Key);
    }

    private bool TryDismantleConnection(MachineConnection connection)
    {
        var result = DismantlingRules.TryDismantleConnection(
            ProductionItemIds.MachineDismantlingTool,
            _astronautInventory,
            connection.Kind,
            () =>
            {
                if (!_connectionNetwork.RemoveConnection(connection.Id))
                {
                    return false;
                }

                RemoveConnectionView(connection.Id);
                var cometId = GetEndpointCometId(connection.Source) ?? GetEndpointCometId(connection.Target);
                if (cometId is not null)
                {
                    InvalidatePowerTopology(cometId);
                }

                return true;
            },
            _connectionTypes);
        return FinishDismantling(result, "Verbindung abgebaut");
    }

    private bool TryDismantleMachine(MachineView view)
    {
        if (!_machines.TryGetValue(view.InstanceId, out var machine))
        {
            return false;
        }

        var connections = _connectionNetwork.GetConnectionsForMachine(machine.InstanceId).ToArray();
        IReadOnlyList<ItemAmount> inFlightInputs = machine.IsBatchInProgress &&
                                                   machine.SelectedRecipeId is { } selectedRecipeId
            ? _recipeCatalog.Get(selectedRecipeId).Inputs
            : [];
        var additionalRecovery = machine.InputInventory.Slots
            .Concat(machine.OutputInventory.Slots)
            .Where(slot => !slot.IsEmpty)
            .Select(slot => new ItemAmount(slot.ItemId!.Value, slot.Amount))
            .Concat(inFlightInputs)
            .Concat(connections.SelectMany(connection =>
                DismantlingRules.GetConnectionRecovery(connection.Kind, _connectionTypes)))
            .ToArray();
        var result = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MachineDismantlingTool,
            _astronautInventory,
            machine.Definition,
            () => RemoveMachineForDismantling(machine, view, connections),
            additionalRecovery,
            machine.ConstructionCostsPaid);
        return FinishDismantling(result, $"{machine.Definition.DisplayName} abgebaut");
    }

    private bool RemoveMachineForDismantling(
        MachineState machine,
        MachineView view,
        IReadOnlyList<MachineConnection> connections)
    {
        foreach (var connection in connections)
        {
            _connectionNetwork.RemoveConnection(connection.Id);
            RemoveConnectionView(connection.Id);
        }

        view.InteractionRequested -= HandleMachineInteractionRequested;
        _machineViews.Remove(machine.InstanceId);
        view.QueueFree();
        _connectionNetwork.UnregisterMachine(machine.InstanceId);
        _machines.Remove(machine.InstanceId);
        RemoveIndexedMachine(machine);
        _lastAvailablePowerByMachine.Remove(machine.InstanceId);
        _pendingResearch.Remove(machine.InstanceId);
        if (_activeResearchStation == machine.InstanceId)
        {
            _activeResearchStation = null;
            _research.SetEnabled(false);
        }

        if (machine.Placement?.CometId is { } cometId)
        {
            _lastSimulatedUtc[cometId] = DateTimeOffset.UtcNow;
            InvalidatePowerTopology(cometId);
        }

        return true;
    }

    private bool FinishDismantling(DismantlingResult result, string successMessage)
    {
        if (!result.Succeeded)
        {
            _showMessage(result.Failure == DismantlingFailure.InventoryFull
                ? "Inventar voll – Objekt wurde nicht abgebaut"
                : "Objekt konnte nicht abgebaut werden");
            return false;
        }

        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage(successMessage);
        return true;
    }

    private ResourceDepositView? FindResourceSourceForPlacement(
        AsteroidView comet,
        MachinePlacement placement) =>
        _loadedResourceSources.Values
            .Where(resource => GodotObject.IsInstanceValid(resource) &&
                               resource.GetParent() == comet &&
                               resource.IsInfinite)
            .OrderBy(resource => resource.Position.DistanceSquaredTo(new Vector2(
                (float)placement.RelativePositionX,
                (float)placement.RelativePositionY)))
            .FirstOrDefault(resource => resource.Position.DistanceTo(new Vector2(
                (float)placement.RelativePositionX,
                (float)placement.RelativePositionY)) <= 1f);

    private IReadOnlyList<RadiationSource> CreateLoadedRadiationSources()
    {
        var itemCatalog = DefaultProductionItemCatalog.Instance;
        var sources = new List<RadiationSource>();
        foreach (var (machineId, view) in _machineViews)
        {
            if (!GodotObject.IsInstanceValid(view) || !_machines.TryGetValue(machineId, out var machine))
            {
                continue;
            }

            var strength = GetInventoryRadiationStrength(machine.InputInventory, itemCatalog) +
                           GetInventoryRadiationStrength(machine.OutputInventory, itemCatalog);
            if (strength <= 0)
            {
                continue;
            }

            sources.Add(new RadiationSource(
                machine.InstanceId.Value,
                view.GlobalPosition.X,
                view.GlobalPosition.Y,
                strength,
                machine.Definition.RadiationShielding));
        }

        foreach (var resource in _loadedResourceSources.Values.Where(GodotObject.IsInstanceValid))
        {
            if (!itemCatalog.TryGet(resource.Deposit.ResourceId, out var item) || item is null ||
                item.HazardKind != ItemHazardKind.Radioactive)
            {
                continue;
            }

            sources.Add(new RadiationSource(
                resource.DepositId,
                resource.GlobalPosition.X,
                resource.GlobalPosition.Y,
                item.HazardStrength * RadiationConfiguration.ResourceSourceStrengthScale));
        }

        return sources;
    }

    private static double GetInventoryRadiationStrength(
        SlotInventory inventory,
        ProductionItemCatalog itemCatalog) => inventory.Slots
        .Where(slot => !slot.IsEmpty &&
                       itemCatalog.TryGet(slot.ItemId!.Value, out var item) &&
                       item?.HazardKind == ItemHazardKind.Radioactive)
        .Sum(slot =>
        {
            var item = itemCatalog.Get(slot.ItemId!.Value);
            return item.HazardStrength *
                   (slot.Amount / 50.0) *
                   RadiationConfiguration.StoredItemStrengthScale;
        });

    private bool TryAdvanceConnectionPlacement()
    {
        var type = _connectionPlacementPreview.SelectedType;
        if (type is null)
        {
            return false;
        }

        if (!HasActivePlacementItem(type.RequiredBuildItemId))
        {
            var missingItem = _itemPresentation.GetOrCreateFallback(
                type.RequiredBuildItemId,
                _astronautInventory.MaximumStackSize);
            var message = $"{missingItem.DisplayName} fehlt";
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        var worldPosition = GetGlobalMousePosition();
        var evaluation = EvaluateConnectionPlacement(worldPosition);
        if (!evaluation.Succeeded)
        {
            _connectionPlacementPreview.SetFailure(evaluation.FailureMessage);
            _showMessage(evaluation.FailureMessage);
            return false;
        }

        type = evaluation.Type!;
        if (_connectionPlacementPreview.Source is null)
        {
            if (_connectionPlacementPreview.SelectedType?.Id != type.Id)
            {
                _connectionPlacementPreview.Start(type);
            }

            _connectionPlacementPreview.SetSource(evaluation.SourceView!);
            _showMessage("Quelle gewählt – jetzt Zielmaschine anklicken");
            return true;
        }

        var sourceState = _machines[evaluation.SourceView!.InstanceId];
        var selectedState = _machines[evaluation.TargetView!.InstanceId];
        var result = _connectionNetwork.TryConnect(
            new MachineConnectionId($"connection:{Guid.NewGuid():N}"),
            type.Id,
            new MachineConnectionEndpoint(sourceState.InstanceId, evaluation.SourcePort!.Id),
            new MachineConnectionEndpoint(selectedState.InstanceId, evaluation.TargetPort!.Id));
        if (!result.Succeeded || result.Connection is null)
        {
            var message = GetConnectionFailureText(result.Failure);
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (!TryConsumeActivePlacementItem(type.RequiredBuildItemId))
        {
            _connectionNetwork.RemoveConnection(result.Connection.Id);
            _showMessage("Verbindungselement konnte nicht entnommen werden");
            return false;
        }

        try
        {
            CreateConnectionView(result.Connection);
            var cometId = sourceState.Placement!.CometId;
            _lastSimulatedUtc[cometId] = DateTimeOffset.UtcNow;
            InvalidatePowerTopology(cometId);
        }
        catch
        {
            _connectionNetwork.RemoveConnection(result.Connection.Id);
            if (!TryRestoreActivePlacementItem(type.RequiredBuildItemId))
            {
                throw new InvalidOperationException("A failed connection could not restore its build item.");
            }

            throw;
        }

        CancelPlacement();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage($"{type.DisplayName} verbunden");
        return true;
    }

    private void RefreshConnectionPlacementCandidate()
    {
        var evaluation = EvaluateConnectionPlacement(GetGlobalMousePosition());
        _connectionPlacementPreview.SetCandidateState(
            evaluation.Succeeded,
            evaluation.FailureMessage);
    }

    private ConnectionPlacementEvaluation EvaluateConnectionPlacement(Vector2 worldPosition)
    {
        var type = _connectionPlacementPreview.SelectedType;
        if (type is null)
        {
            return ConnectionPlacementEvaluation.Failed("Platzierung nicht aktiv");
        }

        var selectedView = FindNearestConnectionMachine(worldPosition);
        if (selectedView is null)
        {
            if (_connectionPlacementPreview.Source is { } source &&
                source.GetWorldConnectionAnchor(ConnectionPresentationCatalog.GetSourceAnchor(type.Kind))
                    .DistanceTo(worldPosition) > ConnectionPresentationCatalog.MaximumConnectionLength)
            {
                return ConnectionPlacementEvaluation.Failed("Kabel zu lang");
            }

            return ConnectionPlacementEvaluation.Failed("Kein freier Anschluss");
        }

        var selectedState = _machines[selectedView.InstanceId];
        if (_connectionPlacementPreview.Source is not { } sourceView)
        {
            type = ResolveHotbarConnectionType(selectedState, type);
            var sourcePort = FindConnectionPort(selectedState, type, source: true);
            if (sourcePort is null)
            {
                return ConnectionPlacementEvaluation.Failed("Kein freier Anschluss");
            }

            var sourceEndpoint = new MachineConnectionEndpoint(selectedState.InstanceId, sourcePort.Id);
            if (_connectionNetwork.Connections.Count(connection =>
                    connection.Source == sourceEndpoint || connection.Target == sourceEndpoint) >=
                sourcePort.MaximumConnections)
            {
                return ConnectionPlacementEvaluation.Failed("Anschluss belegt");
            }

            return ConnectionPlacementEvaluation.ForSource(type, selectedView, sourcePort);
        }

        var sourceState = _machines[sourceView.InstanceId];
        var sourceConnectionPort = FindConnectionPort(sourceState, type, source: true);
        var targetConnectionPort = FindConnectionPort(selectedState, type, source: false);
        if (sourceConnectionPort is null || targetConnectionPort is null)
        {
            return ConnectionPlacementEvaluation.Failed("Falscher Anschlusstyp");
        }

        var sourceAnchor = sourceView.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetSourceAnchor(type.Kind));
        var targetAnchor = selectedView.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetTargetAnchor(type.Kind));
        if (sourceAnchor.DistanceTo(targetAnchor) > ConnectionPresentationCatalog.MaximumConnectionLength)
        {
            return ConnectionPlacementEvaluation.Failed("Kabel zu lang");
        }

        var validation = _connectionNetwork.ValidateConnection(
            type.Id,
            new MachineConnectionEndpoint(sourceState.InstanceId, sourceConnectionPort.Id),
            new MachineConnectionEndpoint(selectedState.InstanceId, targetConnectionPort.Id));
        return validation == MachineConnectionFailure.None
            ? ConnectionPlacementEvaluation.ForTarget(
                type,
                sourceView,
                selectedView,
                sourceConnectionPort,
                targetConnectionPort)
            : ConnectionPlacementEvaluation.Failed(GetConnectionFailureText(validation));
    }

    private ConnectionTypeDefinition ResolveHotbarConnectionType(
        MachineState sourceMachine,
        ConnectionTypeDefinition currentType)
    {
        if (_hotbarPlacementSource is not { } hotbarSource)
        {
            return currentType;
        }

        var compatibleTypes = _connectionTypes.ForBuildItem(hotbarSource.ItemId)
            .Where(type => FindConnectionPort(sourceMachine, type, source: true) is not null)
            .ToArray();
        if (compatibleTypes.Length <= 1)
        {
            return compatibleTypes.FirstOrDefault() ?? currentType;
        }

        if (sourceMachine.SelectedRecipeId is { } selectedRecipeId &&
            _recipeCatalog.TryGet(selectedRecipeId, out var selectedRecipe) && selectedRecipe is not null)
        {
            var outputMedia = selectedRecipe.CombinedOutputs
                .Select(output => DefaultProductionItemCatalog.Instance.Get(output.ItemId).Phase)
                .Select(phase => phase switch
                {
                    ProductionItemPhase.Solid => TransportMedium.Solid,
                    ProductionItemPhase.Liquid => TransportMedium.Liquid,
                    ProductionItemPhase.Gas => TransportMedium.Gas,
                    _ => throw new ArgumentOutOfRangeException(nameof(phase)),
                })
                .ToHashSet();
            var recipeType = compatibleTypes.FirstOrDefault(type => outputMedia.Contains(type.Medium));
            if (recipeType is not null)
            {
                return recipeType;
            }
        }

        return compatibleTypes.FirstOrDefault(type => type.Id == currentType.Id) ?? compatibleTypes[0];
    }

    private bool TryAdvancePowerCablePlacement() =>
        TryAdvancePowerCablePlacement(GetGlobalMousePosition());

    private bool TryAdvancePowerCablePlacement(Vector2 worldPosition)
    {
        RefreshPowerCablePlacementCandidate(worldPosition);
        if (_powerCableCandidateTarget is not { } selected)
        {
            var message = GetMissingPowerCableCandidateFailure(worldPosition);
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        return TryAdvancePowerCablePlacement(selected);
    }

    private string GetMissingPowerCableCandidateFailure(Vector2 worldPosition)
    {
        if (_powerCableSourceTarget is { } sourceTarget &&
            TryResolvePowerEndpoint(sourceTarget, out var source) &&
            source.WorldPosition.DistanceTo(worldPosition) >
            PowerGridConfiguration.MaximumCableLengthWorldUnits)
        {
            return "Kabel zu lang";
        }

        return "Kein freier Anschluss";
    }

    private bool TryAdvancePowerCablePlacement(PowerInteractionTarget selected)
    {
        var type = _connectionTypes.Get(ConnectionKind.PowerCable);
        if (!HasActivePlacementItem(type.RequiredBuildItemId))
        {
            const string message = "Stromkabel fehlt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (!TryResolvePowerEndpoint(selected, out var selectedEndpoint))
        {
            const string message = "Kein freier Anschluss";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (IsPowerEndpointOccupied(selected))
        {
            const string message = "Anschluss belegt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (_powerCableSourceTarget is not { } sourceTarget)
        {
            _powerCableSourceTarget = selected;
            _powerCablePlacementPreview.SetSource(selectedEndpoint.Visual);
            _showMessage("Erster Anschluss gewählt – jetzt zweiten Anschluss wählen");
            return true;
        }

        if (!TryResolvePowerEndpoint(sourceTarget, out var sourceEndpoint))
        {
            CancelPlacement();
            _showMessage("Der erste Anschluss ist nicht mehr verfügbar");
            return false;
        }

        if (sourceTarget == selected)
        {
            const string message = "Anschluss belegt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (sourceEndpoint.WorldPosition.DistanceTo(selectedEndpoint.WorldPosition) >
            PowerGridConfiguration.MaximumCableLengthWorldUnits)
        {
            const string message = "Kabel zu lang";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        var result = _connectionNetwork.TryConnect(
            new MachineConnectionId($"connection:{Guid.NewGuid():N}"),
            type.Id,
            new MachineConnectionEndpoint(sourceTarget.NodeId, sourceTarget.PortId),
            new MachineConnectionEndpoint(selected.NodeId, selected.PortId));
        if (!result.Succeeded || result.Connection is null)
        {
            var message = GetConnectionFailureText(result.Failure);
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (!TryConsumeActivePlacementItem(type.RequiredBuildItemId))
        {
            _connectionNetwork.RemoveConnection(result.Connection.Id);
            _showMessage("Stromkabel konnte nicht entnommen werden");
            return false;
        }

        try
        {
            CreateConnectionView(result.Connection);
            InvalidatePowerTopology(sourceEndpoint.CometId);
        }
        catch
        {
            _connectionNetwork.RemoveConnection(result.Connection.Id);
            if (!TryRestoreActivePlacementItem(type.RequiredBuildItemId))
            {
                throw new InvalidOperationException("A failed power cable could not restore its build item.");
            }

            throw;
        }

        CancelPlacement();
        UpdateShipPowerPortVisuals();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage("Stromkabel verbunden");
        return true;
    }

    private void RefreshPowerCablePlacementCandidate()
    {
        RefreshPowerCablePlacementCandidate(GetGlobalMousePosition());
    }

    private void RefreshPowerCablePlacementCandidate(Vector2 worldPosition)
    {
        if (!_powerCablePlacementPreview.IsActive)
        {
            return;
        }

        var endpoints = GetPowerEndpointCandidates();
        _powerCablePlacementPreview.SetAvailableEndpoints(endpoints
            .Where(endpoint =>
                _powerCableSourceTarget == endpoint.Target ||
                !IsPowerEndpointOccupied(endpoint.Target))
            .Select(endpoint => endpoint.Visual));
        var candidate = FindClosestPowerEndpoint(
            worldPosition,
            (float)PowerGridConfiguration.PortSelectionRadiusWorldUnits,
            endpoints);
        _powerCableCandidateTarget = candidate?.Target;
        if (_powerCableSourceTarget is null)
        {
            return;
        }

        var failure = GetPowerCableCandidateFailure(candidate);
        var compatible = candidate is not null && string.IsNullOrEmpty(failure);
        _powerCablePlacementPreview.SetCandidate(
            candidate?.Visual,
            compatible,
            failure);
    }

    private string GetPowerCableCandidateFailure(PowerEndpointCandidate? candidate)
    {
        if (candidate is null || _powerCableSourceTarget is null)
        {
            return string.Empty;
        }

        if (candidate.Value.Target == _powerCableSourceTarget.Value ||
            IsPowerEndpointOccupied(candidate.Value.Target))
        {
            return "Anschluss belegt";
        }

        if (!TryResolvePowerEndpoint(_powerCableSourceTarget.Value, out var source) ||
            !string.Equals(source.CometId, candidate.Value.CometId, StringComparison.Ordinal))
        {
            return "Falscher Anschlusstyp";
        }

        return source.WorldPosition.DistanceTo(candidate.Value.WorldPosition) >
               PowerGridConfiguration.MaximumCableLengthWorldUnits
            ? "Kabel zu lang"
            : string.Empty;
    }

    private MachineView? FindNearestConnectionMachine(Vector2 worldPosition)
    {
        var maximumDistanceSquared = ConnectionPresentationCatalog.MachineSelectionRadius *
                                     ConnectionPresentationCatalog.MachineSelectionRadius;
        return _machineViews.Values
            .Where(GodotObject.IsInstanceValid)
            .Select(view => new
            {
                View = view,
                DistanceSquared = view.GlobalPosition.DistanceSquaredTo(worldPosition),
            })
            .Where(candidate => candidate.DistanceSquared <= maximumDistanceSquared)
            .OrderBy(candidate => candidate.DistanceSquared)
            .Select(candidate => candidate.View)
            .FirstOrDefault();
    }

    private MachinePortDefinition? FindConnectionPort(
        MachineState machine,
        ConnectionTypeDefinition type,
        bool source) =>
        _machinePorts.ForMachine(machine.Definition.Id)
            .Where(port => port.Medium == type.Medium)
            .Where(port => !type.IsDirectional || (source ? port.CanSend : port.CanReceive))
            .OrderBy(port => port.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    public PowerInteractionTarget? FindNearestPowerInteraction(Vector2 worldPosition) =>
        FindClosestPowerEndpoint(
            worldPosition,
            (float)PowerGridConfiguration.PortInteractionRadiusWorldUnits,
            GetPowerEndpointCandidates()
                .Where(candidate => CanOpenPowerOverviewAt(candidate.Target))
                .ToArray())?.Target;

    public double GetPowerInteractionDistanceSquared(
        PowerInteractionTarget target,
        Vector2 worldPosition) =>
        CanOpenPowerOverviewAt(target) && TryResolvePowerEndpoint(target, out var endpoint)
            ? endpoint.WorldPosition.DistanceSquaredTo(worldPosition)
            : double.PositiveInfinity;

    private PowerEndpointCandidate? FindClosestPowerEndpoint(
        Vector2 worldPosition,
        float maximumDistance)
    {
        return FindClosestPowerEndpoint(worldPosition, maximumDistance, GetPowerEndpointCandidates());
    }

    private static PowerEndpointCandidate? FindClosestPowerEndpoint(
        Vector2 worldPosition,
        float maximumDistance,
        IReadOnlyList<PowerEndpointCandidate> endpoints)
    {
        PowerEndpointCandidate? best = null;
        var bestDistanceSquared = maximumDistance * maximumDistance;
        foreach (var endpoint in endpoints)
        {
            var distanceSquared = endpoint.WorldPosition.DistanceSquaredTo(worldPosition);
            if (distanceSquared <= bestDistanceSquared)
            {
                best = endpoint;
                bestDistanceSquared = distanceSquared;
            }
        }

        return best;
    }

    private IReadOnlyList<PowerEndpointCandidate> GetPowerEndpointCandidates()
    {
        var endpoints = new List<PowerEndpointCandidate>();
        foreach (var state in _machines.Values)
        {
            if (!_machineViews.TryGetValue(state.InstanceId, out var view) ||
                !GodotObject.IsInstanceValid(view))
            {
                continue;
            }

            var ports = GetMachinePowerPorts(state);
            for (var index = 0; index < ports.Count; index++)
            {
                var target = new PowerInteractionTarget(state.InstanceId, ports[index].Id);
                if (!TryResolvePowerEndpoint(target, out var endpoint))
                {
                    continue;
                }

                endpoints.Add(endpoint);
            }
        }

        if (_shipPowerNode is not null && _ship is { IsAttached: true })
        {
            foreach (var portId in MachinePortIds.ShipPowerPorts)
            {
                var target = new PowerInteractionTarget(PlayerShipNodeId, portId);
                if (!TryResolvePowerEndpoint(target, out var endpoint))
                {
                    continue;
                }

                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    private bool TryResolvePowerEndpoint(
        PowerInteractionTarget target,
        out PowerEndpointCandidate endpoint)
    {
        if (target.NodeId == PlayerShipNodeId)
        {
            if (_shipPowerNode is null || _ship is not { IsAttached: true } ship ||
                target.PortId != MachinePortIds.ShipPowerA &&
                target.PortId != MachinePortIds.ShipPowerB)
            {
                endpoint = default;
                return false;
            }

            var presentationPort = target.PortId == MachinePortIds.ShipPowerA
                ? ShipPowerPortId.A
                : ShipPowerPortId.B;
            var visual = new PowerCableVisualEndpoint(
                $"{target.NodeId.Value}/{target.PortId.Value}",
                () => ship.GetWorldPowerPortAnchor(presentationPort),
                () => GodotObject.IsInstanceValid(ship) && ship.IsAttached);
            endpoint = new PowerEndpointCandidate(
                target,
                _shipPowerNode.CometId,
                visual.GetWorldPosition(),
                visual,
                $"Raumschiff Anschluss {presentationPort}");
            return true;
        }

        if (!_machines.TryGetValue(target.NodeId, out var state) || state.Placement is null ||
            !_machineViews.TryGetValue(target.NodeId, out var view) ||
            !GodotObject.IsInstanceValid(view))
        {
            endpoint = default;
            return false;
        }

        var ports = GetMachinePowerPorts(state);
        var portIndex = ports.FindIndex(port => port.Id == target.PortId);
        if (portIndex < 0)
        {
            endpoint = default;
            return false;
        }

        var machineVisual = new PowerCableVisualEndpoint(
            $"{target.NodeId.Value}/{target.PortId.Value}",
            () => view.GetWorldPowerPortAnchor(portIndex),
            () => GodotObject.IsInstanceValid(view));
        endpoint = new PowerEndpointCandidate(
            target,
            state.Placement.CometId,
            machineVisual.GetWorldPosition(),
            machineVisual,
            state.Definition.DisplayName);
        return true;
    }

    private List<MachinePortDefinition> GetMachinePowerPorts(MachineState state) =>
        _machinePorts.ForMachine(state.Definition.Id)
            .Where(port => port.Medium == TransportMedium.Power)
            .OrderBy(port => port.Id.Value, StringComparer.Ordinal)
            .ToList();

    private bool IsPowerEndpointOccupied(PowerInteractionTarget target) =>
        _connectionNetwork.GetConnectionsForMachine(target.NodeId).Any(connection =>
            connection.Source == new MachineConnectionEndpoint(target.NodeId, target.PortId) ||
            connection.Target == new MachineConnectionEndpoint(target.NodeId, target.PortId));

    private bool CanOpenPowerOverviewAt(PowerInteractionTarget target)
    {
        if (target.NodeId == PlayerShipNodeId)
        {
            return _shipPowerNode is not null && _ship is { IsAttached: true } &&
                   (target.PortId == MachinePortIds.ShipPowerA ||
                    target.PortId == MachinePortIds.ShipPowerB);
        }

        return _machines.TryGetValue(target.NodeId, out var machine) &&
               PowerOverviewAccessRules.CanOpenAt(machine.Definition) &&
               GetMachinePowerPorts(machine).Any(port => port.Id == target.PortId);
    }

    private static string GetConnectionFailureText(MachineConnectionFailure failure) => failure switch
    {
        MachineConnectionFailure.SameMachine => "Anschluss belegt",
        MachineConnectionFailure.DifferentComets => "Kabel zu lang",
        MachineConnectionFailure.PortMediumMismatch => "Falscher Anschlusstyp",
        MachineConnectionFailure.ItemCompatibilityMismatch => "Falscher Anschlusstyp",
        MachineConnectionFailure.DirectionMismatch => "Falscher Anschlusstyp",
        MachineConnectionFailure.PortCapacityReached => "Anschluss belegt",
        MachineConnectionFailure.DuplicateEndpoints => "Anschluss belegt",
        MachineConnectionFailure.UnknownPort => "Kein freier Anschluss",
        _ => "Objekt blockiert",
    };

    public MachineState? FindNearestInteractiveMachine(Vector2 worldPosition)
    {
        MachineView? closest = null;
        var bestDistanceSquared = float.PositiveInfinity;
        foreach (var view in _machineViews.Values)
        {
            if (!GodotObject.IsInstanceValid(view) || !view.IsWithinInteractionRange(worldPosition))
            {
                continue;
            }

            if (_machines.TryGetValue(view.InstanceId, out var interactionState) &&
                PowerOverviewAccessRules.CanOpenAt(interactionState.Definition))
            {
                continue;
            }

            var distance = view.GlobalPosition.DistanceSquaredTo(worldPosition);
            if (distance >= bestDistanceSquared)
            {
                continue;
            }

            closest = view;
            bestDistanceSquared = distance;
        }

        return closest is null ? null : _machines[closest.InstanceId];
    }

    public float GetMachineDistanceSquared(MachineState state, Vector2 worldPosition) =>
        _machineViews.TryGetValue(state.InstanceId, out var view) && GodotObject.IsInstanceValid(view)
            ? view.GlobalPosition.DistanceSquaredTo(worldPosition)
            : float.PositiveInfinity;

    public void OpenPowerTarget(PowerInteractionTarget target)
    {
        if (!CanOpenPowerOverviewAt(target) || !TryResolvePowerEndpoint(target, out _))
        {
            throw new InvalidOperationException(
                "The selected endpoint is not an available central power overview access point.");
        }

        _openPowerTarget = target;
    }

    public PowerMenuViewModel? RefreshOpenPowerViewModel()
    {
        if (_openPowerTarget is not { } target ||
            !CanOpenPowerOverviewAt(target) ||
            !TryResolvePowerEndpoint(target, out var endpoint) ||
            FindPowerComponent(endpoint.CometId, target) is null)
        {
            return null;
        }

        return CreatePowerMenuViewModel(target);
    }

    public void ClosePowerTarget() => _openPowerTarget = null;

    public void SetOpenPowerNetworkEnabled(bool enabled)
    {
        if (!TryGetOpenPowerComponent(out var component))
        {
            return;
        }

        var state = _powerSimulation.GetOrCreateNetworkState(component);
        if (enabled && state.IsBreakerTripped)
        {
            _powerSimulation.ResetBreaker(state.NetworkId);
        }
        else
        {
            _powerSimulation.SetNetworkEnabled(state.NetworkId, enabled);
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void SetOpenPowerPortEnabled(string portId, bool enabled)
    {
        if (_shipPowerNode is null || _openPowerTarget is null)
        {
            return;
        }

        var id = new MachinePortId(portId);
        if (!_shipPowerNode.SetConnectorEnabled(id, enabled))
        {
            return;
        }

        _persistedShipPower = CaptureShipPowerState();
        UpdateShipPowerPortVisuals();
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void SetOpenPowerSourceEnabled(string sourceId, bool enabled)
    {
        if (_machines.Values.FirstOrDefault(machine =>
                string.Equals(machine.InstanceId.Value, sourceId, StringComparison.Ordinal)) is { } machine &&
            machine.Definition.Kind == MachineKind.Generator)
        {
            machine.SetEnabled(enabled);
            MarkDirty();
            FactoryStateChanged?.Invoke();
            return;
        }

        if (_shipPowerNode is null)
        {
            return;
        }

        var connector = _shipPowerNode.Sources.FirstOrDefault(source =>
            string.Equals(source.SourceId, sourceId, StringComparison.Ordinal));
        if (connector is not ShipPowerNode.ShipPowerConnector shipConnector)
        {
            return;
        }

        _shipPowerNode.SetConnectorEnabled(shipConnector.Endpoint.PortId, enabled);
        _persistedShipPower = CaptureShipPowerState();
        UpdateShipPowerPortVisuals();
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void DisconnectOpenPowerPort(string portId)
    {
        if (_openPowerTarget is not { } openTarget)
        {
            return;
        }

        var endpoint = new MachineConnectionEndpoint(openTarget.NodeId, new MachinePortId(portId));
        var connection = _connectionNetwork.GetConnectionsForMachine(openTarget.NodeId)
            .FirstOrDefault(candidate => candidate.Kind == ConnectionKind.PowerCable &&
                                         (candidate.Source == endpoint || candidate.Target == endpoint));
        if (connection is null)
        {
            return;
        }

        var cableItem = _connectionTypes.Get(connection.TypeId).RequiredBuildItemId;
        if (!ProductionInventoryRules.CanStoreAll(
                _astronautInventory,
                [new ItemAmount(cableItem, 1)]))
        {
            _showMessage("Inventar voll – Stromkabel kann nicht getrennt werden");
            return;
        }

        var snapshot = connection.CreateSnapshot();
        if (!_connectionNetwork.RemoveConnection(connection.Id))
        {
            return;
        }

        if (!_astronautInventory.Add(cableItem, 1).Succeeded)
        {
            var rollback = _connectionNetwork.TryRestore(snapshot);
            if (!rollback.Succeeded)
            {
                throw new InvalidOperationException("A failed cable refund could not restore its connection.");
            }

            throw new InvalidOperationException("A prevalidated cable refund failed.");
        }

        RemoveConnectionView(connection.Id);
        var cometId = GetEndpointCometId(connection.Source) ?? GetEndpointCometId(connection.Target);
        if (cometId is not null)
        {
            InvalidatePowerTopology(cometId);
        }

        UpdateShipPowerPortVisuals();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage("Stromkabel getrennt und ins Inventar gelegt");
    }

    private PowerMenuViewModel CreatePowerMenuViewModel(PowerInteractionTarget target)
    {
        if (!TryResolvePowerEndpoint(target, out var endpoint))
        {
            throw new InvalidOperationException("The selected power endpoint is no longer available.");
        }

        var component = FindPowerComponent(endpoint.CometId, target);
        if (component is null)
        {
            throw new InvalidOperationException("The selected endpoint has no power component.");
        }

        var networkId = _powerSimulation.GetNetworkId(component);
        var control = _powerSimulation.GetOrCreateNetworkState(component);
        _lastPowerGridResults.TryGetValue(networkId, out var latest);
        var metrics = latest?.Metrics ?? new PowerGridMetrics(
            0,
            0,
            0,
            0,
            0,
            0,
            control.IsBreakerTripped
                ? PowerGridStatus.BreakerTripped
                : control.IsEnabled ? PowerGridStatus.Idle : PowerGridStatus.Disabled,
            0,
            null);
        var title = target.NodeId == PlayerShipNodeId
            ? "Raumschiff-Energie"
            : _machines[target.NodeId].Definition.DisplayName;
        return new PowerMenuViewModel(
            networkId.Value,
            title,
            $"NETZ {networkId.Value[^Math.Min(8, networkId.Value.Length)..].ToUpperInvariant()}",
            control.IsEnabled && !control.IsBreakerTripped,
            true,
            MapPowerMenuStatus(metrics.Status),
            metrics.MaximumCapacityKilowatts,
            metrics.ActualProductionKilowatts,
            metrics.ActualConsumptionKilowatts,
            metrics.RequestedPowerKilowatts,
            metrics.ReserveKilowatts,
            metrics.FuelConsumptionPerMinute,
            ResolveSharedFuelRuntime(metrics),
            control.History.Samples.Select(sample => new PowerHistorySampleViewModel(
                sample.MaximumCapacityKilowatts,
                sample.ActualProductionKilowatts,
                sample.ActualConsumptionKilowatts,
                sample.RequestedPowerKilowatts)).ToArray(),
            CreatePowerSourceViewModels(component, latest),
            CreatePowerPortViewModels(target.NodeId, latest));
    }

    private bool TryGetOpenPowerComponent(out PowerConnectionComponent component)
    {
        if (_openPowerTarget is { } target &&
            TryResolvePowerEndpoint(target, out var endpoint) &&
            FindPowerComponent(endpoint.CometId, target) is { } found)
        {
            component = found;
            return true;
        }

        component = null!;
        return false;
    }

    private PowerConnectionComponent? FindPowerComponent(
        string cometId,
        PowerInteractionTarget target)
    {
        var endpoint = new MachineConnectionEndpoint(target.NodeId, target.PortId);
        return _connectionNetwork.GetPowerComponents(cometId)
            .FirstOrDefault(component => component.Endpoints.Contains(endpoint));
    }

    private IReadOnlyList<PowerSourceViewModel> CreatePowerSourceViewModels(
        PowerConnectionComponent component,
        ConnectedPowerGridTickResult? latest)
    {
        var allocations = (latest?.Dispatch.SourceAllocations ?? [])
            .ToDictionary(allocation => allocation.SourceId, StringComparer.Ordinal);
        var sources = new List<PowerSourceViewModel>();
        foreach (var machineId in component.MachineIds)
        {
            if (!_machines.TryGetValue(machineId, out var machine) ||
                machine.Definition.Kind != MachineKind.Generator)
            {
                continue;
            }

            allocations.TryGetValue(machineId.Value, out var allocation);
            var supplied = allocation?.SuppliedKilowatts ?? 0;
            var utilization = machine.Definition.GeneratedPowerKilowatts <= 0
                ? 0
                : supplied / machine.Definition.GeneratedPowerKilowatts;
            var fuelPerMinute = machine.Definition.IsFuelledGenerator && utilization > 0
                ? utilization * 60 / machine.Definition.GeneratorFuelSecondsPerItem
                : 0;
            var availableFuelSeconds = machine.GeneratorFuelSecondsRemaining;
            if (machine.Definition.GeneratorFuelItemId is { } generatorFuelId)
            {
                availableFuelSeconds += machine.InputInventory.GetAmount(generatorFuelId) *
                                        machine.Definition.GeneratorFuelSecondsPerItem;
            }
            sources.Add(new PowerSourceViewModel(
                machine.InstanceId.Value,
                machine.Definition.DisplayName,
                machine.IsEnabled,
                supplied,
                fuelPerMinute,
                fuelPerMinute > 0 ? availableFuelSeconds / Math.Max(utilization, 0.000_001) / 60 : null,
                true));
        }

        foreach (var source in _connectionNetwork.GetExternalPowerSources(component))
        {
            allocations.TryGetValue(source.SourceId, out var allocation);
            var sourceFuelPerMinute = 0.0;
            if (latest is not null && latest.Dispatch.ConsumedFuel > 0 && allocation is not null)
            {
                sourceFuelPerMinute = latest.Metrics.FuelConsumptionPerMinute *
                                      (allocation.ConsumedFuel / latest.Dispatch.ConsumedFuel);
            }

            sources.Add(new PowerSourceViewModel(
                source.SourceId,
                source.Endpoint.PortId == MachinePortIds.ShipPowerA
                    ? "Raumschiff Anschluss A"
                    : "Raumschiff Anschluss B",
                source.IsEnabled,
                allocation?.SuppliedKilowatts ?? 0,
                sourceFuelPerMinute,
                ResolveSharedFuelRuntime(latest?.Metrics),
                true));
        }

        return sources;
    }

    private IReadOnlyList<PowerPortViewModel> CreatePowerPortViewModels(
        MachineInstanceId nodeId,
        ConnectedPowerGridTickResult? selectedNetwork)
    {
        IReadOnlyList<MachinePortId> portIds;
        if (nodeId == PlayerShipNodeId)
        {
            portIds = MachinePortIds.ShipPowerPorts;
        }
        else if (_machines.TryGetValue(nodeId, out var machine))
        {
            portIds = GetMachinePowerPorts(machine).Select(port => port.Id).ToArray();
        }
        else
        {
            return [];
        }

        var result = new List<PowerPortViewModel>(portIds.Count);
        foreach (var portId in portIds)
        {
            var endpoint = new MachineConnectionEndpoint(nodeId, portId);
            var connection = _connectionNetwork.GetConnectionsForMachine(nodeId)
                .FirstOrDefault(candidate => candidate.Kind == ConnectionKind.PowerCable &&
                                             (candidate.Source == endpoint || candidate.Target == endpoint));
            var other = connection is null
                ? (MachineConnectionEndpoint?)null
                : connection.Source == endpoint ? connection.Target : connection.Source;
            var component = TryResolvePowerEndpoint(new PowerInteractionTarget(nodeId, portId), out var resolved)
                ? FindPowerComponent(resolved.CometId, new PowerInteractionTarget(nodeId, portId))
                : null;
            var networkId = component is null ? (PowerNetworkId?)null : _powerSimulation.GetNetworkId(component);
            var output = 0.0;
            if (component is not null && networkId is { } id &&
                _lastPowerGridResults.TryGetValue(id, out var networkResult))
            {
                var sourceId = nodeId == PlayerShipNodeId
                    ? _shipPowerNode?.GetConnector(portId)?.SourceId
                    : nodeId.Value;
                output = networkResult.Dispatch.SourceAllocations?
                    .FirstOrDefault(source => string.Equals(source.SourceId, sourceId, StringComparison.Ordinal))?
                    .SuppliedKilowatts ?? 0;
            }

            var shipConnector = nodeId == PlayerShipNodeId ? _shipPowerNode?.GetConnector(portId) : null;
            result.Add(new PowerPortViewModel(
                portId.Value,
                nodeId == PlayerShipNodeId
                    ? portId == MachinePortIds.ShipPowerA ? "ANSCHLUSS A" : "ANSCHLUSS B"
                    : $"ANSCHLUSS {result.Count + 1}",
                connection is not null,
                other is { } otherEndpoint ? GetPowerObjectDisplayName(otherEndpoint.MachineId) : string.Empty,
                shipConnector?.IsEnabled ?? true,
                shipConnector is not null,
                output,
                networkId is { } displayId
                    ? $"NETZ {displayId.Value[^Math.Min(6, displayId.Value.Length)..].ToUpperInvariant()}"
                    : "OHNE NETZ",
                connection is not null));
        }

        _ = selectedNetwork;
        return result;
    }

    private string GetPowerObjectDisplayName(MachineInstanceId id) =>
        id == PlayerShipNodeId
            ? "Raumschiff"
            : _machines.TryGetValue(id, out var machine)
                ? machine.Definition.DisplayName
                : id.Value;

    private double? ResolveSharedFuelRuntime(PowerGridMetrics? selectedMetrics)
    {
        if (_ship is null)
        {
            return selectedMetrics?.EstimatedFuelRuntimeMinutes;
        }

        var totalFuelPerMinute = _lastPowerGridResults.Values
            .Sum(result => result.Metrics.FuelConsumptionPerMinute);
        return totalFuelPerMinute > 0
            ? _ship.FuelTank.CurrentFuel / totalFuelPerMinute
            : selectedMetrics?.EstimatedFuelRuntimeMinutes;
    }

    private void UpdateShipPowerPortVisuals()
    {
        if (_ship is null)
        {
            return;
        }

        foreach (var pair in new[]
                 {
                     (MachinePortIds.ShipPowerA, ShipPowerPortId.A),
                     (MachinePortIds.ShipPowerB, ShipPowerPortId.B),
                 })
        {
            var connector = _shipPowerNode?.GetConnector(pair.Item1);
            var connected = _connectionNetwork.GetConnectionsForMachine(PlayerShipNodeId)
                .Any(connection =>
                    connection.Source == new MachineConnectionEndpoint(PlayerShipNodeId, pair.Item1) ||
                    connection.Target == new MachineConnectionEndpoint(PlayerShipNodeId, pair.Item1));
            var output = connector is null
                ? 0
                : _lastPowerGridResults.Values
                    .SelectMany(result => result.Dispatch.SourceAllocations ?? [])
                    .FirstOrDefault(source =>
                        string.Equals(source.SourceId, connector.SourceId, StringComparison.Ordinal))?
                    .SuppliedKilowatts ?? 0;
            var enabled = connector?.IsEnabled ?? (pair.Item2 == ShipPowerPortId.A
                ? _persistedShipPower.ConnectorAEnabled
                : _persistedShipPower.ConnectorBEnabled);
            _ship.SetPowerPortVisualState(
                pair.Item2,
                new ShipPowerPortVisualState(
                    connected,
                    enabled,
                    (float)(output / PowerGridConfiguration.ShipConnectorPowerKilowatts)));
        }
    }

    private static PowerMenuNetworkStatus MapPowerMenuStatus(PowerGridStatus status) => status switch
    {
        PowerGridStatus.Disabled => PowerMenuNetworkStatus.Offline,
        PowerGridStatus.Idle or PowerGridStatus.Online => PowerMenuNetworkStatus.Stable,
        PowerGridStatus.NoCapacity => PowerMenuNetworkStatus.Limited,
        PowerGridStatus.Overloaded => PowerMenuNetworkStatus.Overloaded,
        PowerGridStatus.BreakerTripped => PowerMenuNetworkStatus.CircuitBreakerTripped,
        _ => PowerMenuNetworkStatus.Offline,
    };

    public MachinePanelViewModel CreateMachinePanelViewModel(MachineState state)
    {
        _openMachine = state;
        if (state.Definition.Id == MachineDefinitionIds.BatteryBank)
        {
            return CreateBatteryPanelViewModel(state);
        }

        return state.Definition.Kind switch
        {
            MachineKind.Research => CreateResearchPanelViewModel(state),
            MachineKind.Storage => CreateStoragePanelViewModel(state),
            MachineKind.Generator => CreateGeneratorPanelViewModel(state),
            _ => CreateProductionPanelViewModel(state),
        };
    }

    public MachinePanelViewModel? RefreshOpenMachineViewModel() =>
        _openMachine is null ? null : CreateMachinePanelViewModel(_openMachine);

    public void CloseMachine() => _openMachine = null;

    public void SelectMachineRecipe(string recipeId)
    {
        if (_openMachine is null)
        {
            return;
        }

        if (_openMachine.Definition.Kind == MachineKind.Research)
        {
            const string prefix = "research:";
            if (!recipeId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }

            var researchId = new ResearchId(recipeId[prefix.Length..]);
            _ = _researchCatalog.Get(researchId);
            _pendingResearch[_openMachine.InstanceId] = researchId;
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!_recipeCatalog.TryGet(new RecipeId(recipeId), out var recipe) || recipe is null ||
            recipe.MachineId != _openMachine.Definition.Id || !IsRecipeUnlocked(recipe))
        {
            return;
        }

        if (!_openMachine.SelectRecipe(recipe))
        {
            _showMessage("Rezept kann während eines Zyklus nicht gewechselt werden");
            return;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void SetOpenMachineActive(bool active)
    {
        if (_openMachine is null)
        {
            return;
        }

        if (_openMachine.Definition.Kind == MachineKind.Research)
        {
            SetResearchActive(_openMachine, active);
            return;
        }

        _openMachine.SetEnabled(active);
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void LoadOpenMachineInputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        MachineInventoryTransferResult result;
        switch (_openMachine.Definition.Kind)
        {
            case MachineKind.Production when
                _openMachine.Definition.Id == MachineDefinitionIds.MobileMiner:
                var batteryAmount = Math.Min(
                    _astronautInventory.GetAmount(ProductionItemIds.MobileBatteryPack),
                    ProductionInventoryRules.GetAvailableCapacity(
                        _openMachine.InputInventory,
                        ProductionItemIds.MobileBatteryPack));
                result = batteryAmount <= 0
                    ? MachineInventoryTransferResult.Failed(
                        MachineInventoryTransferFailure.NothingToTransfer)
                    : MachineInventoryTransfer.TransferExact(
                        _astronautInventory,
                        _openMachine.InputInventory,
                        [new ItemAmount(
                            ProductionItemIds.MobileBatteryPack,
                            batteryAmount)]);
                break;
            case MachineKind.Production when _openMachine.SelectedRecipeId is { } recipeId:
                result = MachineInventoryTransfer.LoadRecipeInputs(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    _recipeCatalog.Get(recipeId));
                break;
            case MachineKind.Generator when
                _openMachine.Definition.Id == MachineDefinitionIds.FuelGenerator:
                var filledFuelId = _openMachine.Definition.GeneratorFuelItemId!.Value;
                var emptyFuelId = _openMachine.Definition.GeneratorReturnedContainerItemId!.Value;
                var transferSlot = _openMachine.InputInventory.GetSlot(
                    ProductionConfiguration.FuelGeneratorTankTransferSlotIndex);
                if (!transferSlot.IsEmpty)
                {
                    result = MachineInventoryTransferResult.Failed(
                        MachineInventoryTransferFailure.TargetFull);
                    break;
                }

                var containerToLoad = _astronautInventory.GetAmount(filledFuelId) > 0
                    ? filledFuelId
                    : _astronautInventory.GetAmount(emptyFuelId) > 0
                        ? emptyFuelId
                        : (ItemId?)null;
                if (containerToLoad is null)
                {
                    result = MachineInventoryTransferResult.Failed(
                        MachineInventoryTransferFailure.NothingToTransfer);
                    break;
                }

                var sourceSlot = _astronautInventory.Slots.First(
                    slot => slot.ItemId == containerToLoad.Value);
                var slotTransfer = InventoryTransfer.Transfer(
                    _astronautInventory,
                    sourceSlot.Index,
                    _openMachine.InputInventory,
                    ProductionConfiguration.FuelGeneratorTankTransferSlotIndex,
                    1);
                result = slotTransfer.Succeeded
                    ? MachineInventoryTransferResult.Success(1)
                    : MachineInventoryTransferResult.Failed(
                        MachineInventoryTransferFailure.TargetFull);
                break;
            case MachineKind.Generator when _openMachine.Definition.IsFuelledGenerator:
                result = MachineInventoryTransfer.TransferExact(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    [new ItemAmount(_openMachine.Definition.GeneratorFuelItemId!.Value, 1)]);
                break;
            case MachineKind.Storage:
                result = MachineInventoryTransfer.TransferAsMuchAsPossible(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    itemId => MachineInventoryAcceptanceRules.CanStore(
                        _openMachine.Definition,
                        itemId));
                break;
            case MachineKind.Research when TryGetSelectedResearch(_openMachine, out var research):
                result = MachineInventoryTransfer.TransferExact(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    research.MaterialCosts);
                break;
            default:
                _showMessage("Zuerst ein Rezept auswählen");
                return;
        }

        ShowTransferResult(result, "Material geladen");
    }

    public void CollectOpenMachineOutputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        var source = _openMachine.Definition.Kind == MachineKind.Storage
            ? _openMachine.InputInventory
            : _openMachine.OutputInventory;
        var result = _openMachine.Definition.Kind == MachineKind.Storage
            ? MachineInventoryTransfer.TransferAsMuchAsPossible(source, _astronautInventory)
            : MachineInventoryTransfer.TransferAll(source, _astronautInventory);
        ShowTransferResult(result, "Ausgabe ins Inventar übernommen");
    }

    public void ReturnOpenMachineInputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        var result = MachineInventoryTransfer.TransferAll(
            _openMachine.InputInventory,
            _astronautInventory);
        ShowTransferResult(result, "Eingabematerial ins Inventar zurückgenommen");
    }

    public InventoryTransferResult PreviewOpenMachineSlotTransfer(
        InventorySlotAddress source,
        InventorySlotAddress target)
    {
        if (!TryResolveOpenMachinePanelInventory(source, out var sourceInventory, out var sourceKind) ||
            !TryResolveOpenMachinePanelInventory(target, out var targetInventory, out var targetKind) ||
            ReferenceEquals(sourceInventory, targetInventory) ||
            !IsAllowedMachinePanelTransfer(sourceKind, targetKind))
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        var sourceSlot = sourceInventory.GetSlot(source.SlotIndex);
        if (sourceSlot.ItemId is not { } itemId ||
            targetKind == MachinePanelInventoryKind.Input && !CanOpenMachineAcceptInput(itemId))
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.ItemNotAccepted);
        }

        var targetSlot = targetInventory.GetSlot(target.SlotIndex);
        if (!targetSlot.IsEmpty && targetSlot.ItemId != itemId)
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        }

        return InventoryTransfer.PreviewPrioritizingExistingStacks(
            sourceInventory,
            source.SlotIndex,
            targetInventory,
            target.SlotIndex);
    }

    public bool TransferOpenMachineSlot(InventorySlotAddress source, InventorySlotAddress target)
    {
        var preview = PreviewOpenMachineSlotTransfer(source, target);
        if (!preview.Succeeded ||
            !TryResolveOpenMachinePanelInventory(source, out var sourceInventory, out _) ||
            !TryResolveOpenMachinePanelInventory(target, out var targetInventory, out _))
        {
            return false;
        }

        var result = InventoryTransfer.TransferPrioritizingExistingStacks(
            sourceInventory,
            source.SlotIndex,
            targetInventory,
            target.SlotIndex);
        if (!result.Succeeded)
        {
            return false;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
        return true;
    }

    public bool DeleteOpenMachineStack(InventorySlotAddress address)
    {
        if (!TryResolveOpenMachinePanelInventory(address, out var inventory, out var kind) ||
            kind == MachinePanelInventoryKind.Personal)
        {
            return false;
        }

        var slot = inventory.GetSlot(address.SlotIndex);
        if (slot.ItemId is not { } itemId ||
            !inventory.RemoveFromSlot(address.SlotIndex, itemId, slot.Amount).Succeeded)
        {
            return false;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
        return true;
    }

    private bool TryResolveOpenMachinePanelInventory(
        InventorySlotAddress address,
        out SlotInventory inventory,
        out MachinePanelInventoryKind kind)
    {
        inventory = null!;
        kind = MachinePanelInventoryKind.Personal;
        if (_openMachine is null || address.SlotIndex < 0)
        {
            return false;
        }

        if (address.InventoryId == MachinePanelController.PersonalInventoryId)
        {
            inventory = _astronautInventory;
            kind = MachinePanelInventoryKind.Personal;
        }
        else if (address.InventoryId == $"machine_input:{_openMachine.InstanceId.Value}")
        {
            inventory = _openMachine.InputInventory;
            kind = MachinePanelInventoryKind.Input;
        }
        else if (address.InventoryId == $"machine_output:{_openMachine.InstanceId.Value}")
        {
            inventory = _openMachine.OutputInventory;
            kind = MachinePanelInventoryKind.Output;
        }
        else
        {
            return false;
        }

        return address.SlotIndex < inventory.SlotCount;
    }

    private static bool IsAllowedMachinePanelTransfer(
        MachinePanelInventoryKind source,
        MachinePanelInventoryKind target) =>
        source == MachinePanelInventoryKind.Personal && target == MachinePanelInventoryKind.Input ||
        source is MachinePanelInventoryKind.Input or MachinePanelInventoryKind.Output &&
        target == MachinePanelInventoryKind.Personal;

    private bool CanOpenMachineAcceptInput(ItemId itemId)
    {
        if (_openMachine is null)
        {
            return false;
        }

        if (_openMachine.Definition.Id == MachineDefinitionIds.MobileMiner)
        {
            return itemId == ProductionItemIds.MobileBatteryPack;
        }

        if (_openMachine.Definition.IsFuelledGenerator)
        {
            return itemId == _openMachine.Definition.GeneratorFuelItemId ||
                   itemId == _openMachine.Definition.GeneratorReturnedContainerItemId;
        }

        if (_openMachine.Definition.Kind == MachineKind.Research &&
            TryGetSelectedResearch(_openMachine, out var research))
        {
            return research.MaterialCosts.Any(cost => cost.ItemId == itemId);
        }

        return _openMachine.SelectedRecipeId is { } recipeId &&
               _recipeCatalog.Get(recipeId).Inputs.Any(input => input.ItemId == itemId);
    }

    private enum MachinePanelInventoryKind
    {
        Personal,
        Input,
        Output,
    }

    public void FillOpenGeneratorTank()
    {
        if (_openMachine is null)
        {
            return;
        }

        ShowGeneratorTankTransferResult(
            GeneratorFuelTankTransfer.FillFromInput(_openMachine),
            "Tank aufgefüllt");
    }

    public void DrainOpenGeneratorTank()
    {
        if (_openMachine is null)
        {
            return;
        }

        ShowGeneratorTankTransferResult(
            GeneratorFuelTankTransfer.DrainToInputContainer(_openMachine),
            "Tank geleert");
    }

    public void MarkFuelChanged() => MarkDirty();

    public void MarkInventoryChanged()
    {
        MarkDirty();
        BuildCatalogChanged?.Invoke();
    }

    public void SaveNow()
    {
        if (!_initialized || _debugPersistenceSuppressed)
        {
            return;
        }

        CaptureDroppedItemViews();
        var snapshot = new FactoryStateData(
            FactoryStateData.CurrentVersion,
            _machines.Values
                .OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal)
                .Select(machine => machine.CreateSnapshot())
                .ToArray(),
            _connectionNetwork.CreateSnapshots()
                .Concat(_pendingShipConnections)
                .GroupBy(connection => connection.ConnectionId)
                .Select(group => group.First())
                .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
                .ToArray(),
            _research.CreateSnapshot(),
            _firstBasicGenerator.FreeGeneratorAlreadyBuilt,
            _shipFuelProvider(),
            _shipFuelTypeProvider(),
            new Dictionary<string, DateTimeOffset>(_lastSimulatedUtc, StringComparer.Ordinal),
            InventoryStatePersistence.Capture(_astronautInventory),
            InventoryStatePersistence.Capture(_hotbarInventory),
            _activeHotbarSlotProvider(),
            InventoryStatePersistence.Capture(_toolInventoryState.Inventory),
            _toolInventoryState.SelectedSlotIndex,
            _toolInventoryState.IsHandModeActive,
            CaptureShipInventory(),
            _activeResearchStation?.Value,
            CapturePowerNetworkControls(),
            CaptureShipPowerState(),
            CaptureShipDockingState())
        {
            RadiationExposure = _radiationExposure.CreateSnapshot(),
            DroppedItems = _droppedItems.Values
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
        };
        var saved = _stateStore.Save(snapshot);
        _autosaveElapsed = 0;
        if (saved)
        {
            _dirty = false;
            RememberPlayerInventories();
        }
    }

    private void Restore(FactoryStateData saved)
    {
        _research = RestoreResearchSafely(saved.Research);
        _radiationExposure = RadiationExposureState.Restore(saved.RadiationExposure);
        _firstBasicGenerator = new FirstBasicGeneratorState(saved.FirstBasicGeneratorBuilt);
        _restoreShipFuel(saved.ShipFuel, saved.ShipFuelType);
        _persistedShipPower = saved.ShipPower;
        _persistedShipDocking = saved.ShipDocking;
        _awaitingShipDockingRestore = saved.ShipDocking.IsAttached;
        RestorePowerNetworkControls(saved.PowerNetworkControls);
        InventoryStatePersistence.Restore(_astronautInventory, saved.AstronautInventory);
        InventoryStatePersistence.Restore(_hotbarInventory, saved.HotbarInventory);
        _restoreActiveHotbarSlot(saved.ActiveHotbarSlotIndex);
        InventoryStatePersistence.Restore(_toolInventoryState.Inventory, saved.ToolInventory);
        _toolInventoryState.SelectSlot(saved.SelectedToolSlotIndex);
        if (saved.IsHandModeActive)
        {
            _toolInventoryState.ActivateHandMode();
        }
        else
        {
            _toolInventoryState.DeactivateHandMode();
        }
        _pendingShipInventory = saved.ShipInventory.ToArray();
        _droppedItems.Clear();
        foreach (var item in saved.DroppedItems)
        {
            _droppedItems[item.Id] = item;
        }
        foreach (var (cometId, timestamp) in saved.LastSimulatedUtcByComet)
        {
            _lastSimulatedUtc[cometId] = timestamp;
        }

        foreach (var snapshot in saved.Machines)
        {
            if (!_machineCatalog.TryGet(snapshot.DefinitionId, out var definition) || definition is null ||
                snapshot.Placement is null)
            {
                continue;
            }

            try
            {
                if (snapshot.SelectedRecipeId is { } recipeId &&
                    (!_recipeCatalog.TryGet(recipeId, out var recipe) || recipe is null ||
                     recipe.MachineId != definition.Id))
                {
                    throw new InvalidDataException(
                        $"Selected recipe '{recipeId}' does not belong to machine '{definition.Id}'.");
                }

                var state = MachineState.Restore(snapshot, definition);
                _machines[state.InstanceId] = state;
                IndexMachine(state);
                _connectionNetwork.RegisterMachine(state);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                GD.PushWarning(
                    $"Persisted machine '{snapshot.InstanceId}' was ignored: {exception.Message}");
            }
        }

        var pendingShipConnections = new List<MachineConnectionSnapshot>();
        foreach (var connectionSnapshot in saved.Connections)
        {
            if (ConnectionTouchesShip(connectionSnapshot))
            {
                pendingShipConnections.Add(connectionSnapshot);
                continue;
            }

            try
            {
                var result = _connectionNetwork.TryRestore(connectionSnapshot);
                if (!result.Succeeded)
                {
                    throw new InvalidDataException(
                        $"Connection validation failed with {result.Failure}.");
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException or InvalidOperationException or
                    KeyNotFoundException)
            {
                GD.PushWarning(
                    $"Persisted connection '{connectionSnapshot.ConnectionId}' was ignored: {exception.Message}");
            }
        }

        _pendingShipConnections = pendingShipConnections;

        var occupiedCometIds = _machinesByComet.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var cometId in _lastSimulatedUtc.Keys
                     .Where(cometId => !occupiedCometIds.Contains(cometId))
                     .ToArray())
        {
            _lastSimulatedUtc.Remove(cometId);
        }

        // An active project may remain paused while its exact station is unavailable. It
        // must never be reassigned merely because another station happens to be loaded.
        _activeResearchStation = _research.ActiveResearchId is not null &&
                                 saved.ActiveResearchStationId is { } stationId
            ? new MachineInstanceId(stationId)
            : null;
    }

    private ResearchState RestoreResearchSafely(ResearchStateSnapshot snapshot)
    {
        try
        {
            var knownResearch = _researchCatalog.All
                .ToDictionary(definition => definition.Id);
            if (snapshot.CompletedResearch.Any(id => !knownResearch.ContainsKey(id)) ||
                snapshot.ActiveResearchId is { } activeId &&
                (!knownResearch.TryGetValue(activeId, out var activeDefinition) ||
                 snapshot.ProgressSeconds > activeDefinition.DurationSeconds))
            {
                throw new InvalidDataException("The research snapshot references unknown catalog data.");
            }

            return ResearchState.Restore(snapshot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            GD.PushWarning($"Persisted research state was reset: {exception.Message}");
            return new ResearchState();
        }
    }

    private IReadOnlyList<InventorySlotState> CaptureShipInventory() =>
        _shipInventory is null
            ? _pendingShipInventory.ToArray()
            : InventoryStatePersistence.Capture(_shipInventory);

    private IReadOnlyList<SpaceFactory.Application.Factory.PowerNetworkControlState> CapturePowerNetworkControls() =>
        _powerSimulation.CreateControlSnapshots()
            .Select(snapshot => new SpaceFactory.Application.Factory.PowerNetworkControlState(
                snapshot.NetworkId.Value,
                snapshot.IsEnabled,
                snapshot.IsBreakerTripped,
                snapshot.OverloadElapsedSeconds))
            .ToArray();

    private void RestorePowerNetworkControls(
        IReadOnlyList<SpaceFactory.Application.Factory.PowerNetworkControlState> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        _powerSimulation.RestoreControlSnapshots(controls.Select(control => new PowerNetworkControlSnapshot(
            new PowerNetworkId(control.NetworkId),
            control.IsEnabled,
            control.BreakerTripped,
            control.OverloadElapsedSeconds)));
    }

    private ShipPowerState CaptureShipPowerState()
    {
        if (_shipPowerNode is null)
        {
            return _persistedShipPower;
        }

        return new ShipPowerState(
            _shipPowerNode.ConnectorA.IsEnabled,
            _shipPowerNode.ConnectorB.IsEnabled);
    }

    private ShipDockingStateData CaptureShipDockingState()
    {
        if (_ship is null || _sectorSize <= 0)
        {
            return _persistedShipDocking;
        }

        if (_awaitingShipDockingRestore && !_ship.IsAttached)
        {
            return _persistedShipDocking;
        }

        var sectorX = Mathf.FloorToInt(_ship.GlobalPosition.X / _sectorSize);
        var sectorY = Mathf.FloorToInt(_ship.GlobalPosition.Y / _sectorSize);
        if (_ship.IsAttached && _ship.DockingState.AttachedCometId is { } cometId)
        {
            var relative = _ship.DockingState.RelativeAttachmentPosition;
            return new ShipDockingStateData(
                true,
                cometId,
                sectorX,
                sectorY,
                relative.X,
                relative.Y,
                _ship.DockingState.AttachmentRotationRadians,
                _ship.DockingState.LandingLegProgress,
                _ship.GlobalPosition.X,
                _ship.GlobalPosition.Y,
                _ship.GlobalRotation);
        }

        return new ShipDockingStateData(
            false,
            null,
            sectorX,
            sectorY,
            0,
            0,
            0,
            _ship.DockingState.LandingLegProgress,
            _ship.GlobalPosition.X,
            _ship.GlobalPosition.Y,
            _ship.GlobalRotation);
    }

    private void TryRestorePersistedShipDocking(AsteroidView comet)
    {
        if (_ship is null || !_persistedShipDocking.IsAttached || _ship.IsAttached ||
            !string.Equals(_persistedShipDocking.CometId, comet.CometId, StringComparison.Ordinal))
        {
            return;
        }

        var persistedRelativePosition = new WorldPosition(
            _persistedShipDocking.RelativePositionX,
            _persistedShipDocking.RelativePositionY);
        var radialDirection = ShipDockingRestoreRules.ResolveRadialDirection(
            persistedRelativePosition,
            _persistedShipDocking.RelativeRotationRadians);
        var direction = new Vector2((float)radialDirection.X, (float)radialDirection.Y);
        var surfaceProjected = TryProjectPersistedDockingPoseOntoCurrentSurface(
            comet,
            direction,
            out var relativePosition,
            out var relativeRotation);
        if (!surfaceProjected)
        {
            // A configured AsteroidView normally always exposes its outline. A conservative
            // outside pose keeps a corrupt/legacy save collision-free while retaining attached
            // cable identity until the player can deliberately disconnect it.
            relativePosition = direction * (float)(
                comet.Radius +
                PlayerShipController.DockingCenterClearance +
                PlayerShipController.DockingHullReach +
                32.0);
            relativeRotation = direction.Angle() + (Mathf.Pi * 0.5f);
            GD.PushWarning(
                $"Persisted docking pose on '{comet.CometId}' could not be projected onto the " +
                "current surface; a conservative outside fallback was used.");
        }

        _ship.RestoreAttachedPose(
            comet,
            comet.CometId,
            relativePosition,
            relativeRotation,
            _persistedShipDocking.LandingLegProgress);
        _awaitingShipDockingRestore = false;
        _persistedShipDocking = new ShipDockingStateData(
            true,
            comet.CometId,
            Mathf.FloorToInt(_ship.GlobalPosition.X / _sectorSize),
            Mathf.FloorToInt(_ship.GlobalPosition.Y / _sectorSize),
            relativePosition.X,
            relativePosition.Y,
            relativeRotation,
            _persistedShipDocking.LandingLegProgress,
            _ship.GlobalPosition.X,
            _ship.GlobalPosition.Y,
            _ship.GlobalRotation);
        SynchronizeShipPowerDocking(markDirty: false);
        MarkDirty();
    }

    private static bool TryProjectPersistedDockingPoseOntoCurrentSurface(
        AsteroidView comet,
        Vector2 radialDirection,
        out Vector2 relativePosition,
        out float relativeRotation)
    {
        relativePosition = default;
        relativeRotation = 0;
        if (!radialDirection.IsFinite() || radialDirection.LengthSquared() <= 0.000001f)
        {
            return false;
        }

        radialDirection = radialDirection.Normalized();
        var probeRadius = (float)(
            comet.Radius +
            PlayerShipController.DockingCenterClearance +
            PlayerShipController.DockingHullReach);
        var outsideProbe = comet.ToGlobal(radialDirection * probeRadius);
        if (!comet.TryGetClosestSurfacePoint(
                outsideProbe,
                out var surfacePoint,
                out var outwardNormal,
                out _))
        {
            return false;
        }

        // The view supplies a radial outward normal. Guard against a reversed transform or
        // malformed legacy geometry before using it to offset the full ship hull.
        var centerToSurface = surfacePoint - comet.GlobalPosition;
        if (centerToSurface.Dot(outwardNormal) < 0)
        {
            outwardNormal = -outwardNormal;
        }

        var localSurfacePoint = comet.ToLocal(surfacePoint);
        var localNormalEnd = comet.ToLocal(surfacePoint + outwardNormal);
        var localNormal = localNormalEnd - localSurfacePoint;
        if (!localNormal.IsFinite() || localNormal.LengthSquared() <= 0.000001f)
        {
            return false;
        }

        var projection = ShipDockingRestoreRules.ProjectOntoCurrentSurface(
            new WorldPosition(localSurfacePoint.X, localSurfacePoint.Y),
            new WorldPosition(localNormal.X, localNormal.Y),
            PlayerShipController.DockingCenterClearance);
        relativePosition = new Vector2(
            (float)projection.RelativeAttachmentPosition.X,
            (float)projection.RelativeAttachmentPosition.Y);
        relativeRotation = (float)projection.RelativeAttachmentRotationRadians;
        var projectedGlobalPosition = comet.ToGlobal(relativePosition);
        if (!relativePosition.IsFinite() ||
            !float.IsFinite(relativeRotation) ||
            comet.ContainsWorldPoint(projectedGlobalPosition) ||
            !comet.TryGetClosestSurfacePoint(
                projectedGlobalPosition,
                out _,
                out _,
                out var projectedSurfaceDistance))
        {
            return false;
        }

        // Leave a small numerical margin beyond the actual scaled hull. This prevents a restored
        // CharacterBody from starting in contact and jittering before the first physics frame.
        return projectedSurfaceDistance >= PlayerShipController.DockingHullReach + 2.0f;
    }

    private void RestorePendingShipConnections()
    {
        if (_shipPowerNode is null || _pendingShipConnections.Count == 0)
        {
            return;
        }

        foreach (var snapshot in _pendingShipConnections)
        {
            var result = _connectionNetwork.TryRestore(snapshot);
            if (!result.Succeeded)
            {
                GD.PushWarning(
                    $"Persisted ship cable '{snapshot.ConnectionId}' was ignored: {result.Failure}");
            }
        }

        _pendingShipConnections = [];
    }

    private static bool ConnectionTouchesShip(MachineConnectionSnapshot connection) =>
        connection.SourceMachineId == PlayerShipNodeId ||
        connection.TargetMachineId == PlayerShipNodeId;

    private bool DetectPlayerInventoryChanges()
    {
        var starterToolGranted = TryGrantMissingMachineDismantlingTool();
        var astronaut = InventoryStatePersistence.Capture(_astronautInventory);
        var hotbar = InventoryStatePersistence.Capture(_hotbarInventory);
        var tools = InventoryStatePersistence.Capture(_toolInventoryState.Inventory);
        var ship = CaptureShipInventory();
        var activeHotbarSlotIndex = _activeHotbarSlotProvider();
        var changed = starterToolGranted ||
                      !_lastAstronautInventory.SequenceEqual(astronaut) ||
                      !_lastHotbarInventory.SequenceEqual(hotbar) ||
                      !_lastToolInventory.SequenceEqual(tools) ||
                      !_lastShipInventory.SequenceEqual(ship) ||
                      _lastActiveHotbarSlotIndex != activeHotbarSlotIndex ||
                      _lastSelectedToolSlotIndex != _toolInventoryState.SelectedSlotIndex ||
                      _lastHandModeActive != _toolInventoryState.IsHandModeActive;
        _lastAstronautInventory = astronaut;
        _lastHotbarInventory = hotbar;
        _lastToolInventory = tools;
        _lastShipInventory = ship;
        _lastActiveHotbarSlotIndex = activeHotbarSlotIndex;
        _lastSelectedToolSlotIndex = _toolInventoryState.SelectedSlotIndex;
        _lastHandModeActive = _toolInventoryState.IsHandModeActive;
        return changed;
    }

    /// <summary>
    /// A completely full legacy save cannot accept the migrated tool during JSON loading. This
    /// idempotent retry grants it as soon as the player creates one free slot, without replacing
    /// anything and without duplicating a tool stored in a placed machine.
    /// </summary>
    private bool TryGrantMissingMachineDismantlingTool()
    {
        var itemId = ProductionItemIds.MachineDismantlingTool;
        if (_astronautInventory.GetAmount(itemId) > 0 ||
            _hotbarInventory.GetAmount(itemId) > 0 ||
            _toolInventoryState.Inventory.GetAmount(itemId) > 0 ||
            (_shipInventory?.GetAmount(itemId) ?? 0) > 0 ||
            _machines.Values.Any(machine =>
                machine.InputInventory.GetAmount(itemId) > 0 ||
                machine.OutputInventory.GetAmount(itemId) > 0))
        {
            return false;
        }

        if (_toolInventoryState.Inventory.Add(itemId, 1).Succeeded ||
            _astronautInventory.Add(itemId, 1).Succeeded ||
            _hotbarInventory.Add(itemId, 1).Succeeded ||
            (_shipInventory?.Add(itemId, 1).Succeeded ?? false))
        {
            GD.Print("STARTER_DISMANTLING_TOOL_REPAIR_OK: granted missing tool without replacing inventory items");
            return true;
        }

        return false;
    }

    private void RememberPlayerInventories()
    {
        _lastAstronautInventory = InventoryStatePersistence.Capture(_astronautInventory);
        _lastHotbarInventory = InventoryStatePersistence.Capture(_hotbarInventory);
        _lastToolInventory = InventoryStatePersistence.Capture(_toolInventoryState.Inventory);
        _lastShipInventory = CaptureShipInventory();
        _lastActiveHotbarSlotIndex = _activeHotbarSlotProvider();
        _lastSelectedToolSlotIndex = _toolInventoryState.SelectedSlotIndex;
        _lastHandModeActive = _toolInventoryState.IsHandModeActive;
    }

    private void RegisterComet(AsteroidView comet)
    {
        if (_loadedComets.TryGetValue(comet.CometId, out var existing) && existing == comet)
        {
            return;
        }

        _loadedComets[comet.CometId] = comet;
        TryRestorePersistedShipDocking(comet);
        var machines = GetMachinesOnComet(comet.CometId).ToArray();
        if (machines.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastSimulatedUtc.TryGetValue(comet.CometId, out var lastSimulated))
        {
            var elapsed = Math.Clamp(
                (now - lastSimulated).TotalSeconds,
                0,
                MaximumOfflineSimulationSeconds);
            if (elapsed >= SimulationIntervalSeconds)
            {
                if (SimulateOfflineComet(comet.CometId, elapsed))
                {
                    MarkDirty();
                }
            }
        }

        _lastSimulatedUtc[comet.CometId] = now;
        foreach (var state in machines)
        {
            CreateMachineView(state, comet);
        }
        foreach (var connection in _connectionNetwork.Connections
                     .Where(connection => ConnectionBelongsToComet(connection, comet.CometId)))
        {
            CreateConnectionView(connection);
        }
    }

    private void CreateMachineView(MachineState state, AsteroidView comet)
    {
        if (_machineViews.TryGetValue(state.InstanceId, out var existing) &&
            GodotObject.IsInstanceValid(existing))
        {
            existing.Refresh(state);
            return;
        }

        var view = new MachineView();
        view.Configure(state, comet);
        view.InteractionRequested += HandleMachineInteractionRequested;
        _machineViews[state.InstanceId] = view;
    }

    private void CreateConnectionView(MachineConnection connection)
    {
        if (connection.Kind == ConnectionKind.PowerCable)
        {
            CreatePowerCableView(connection);
            return;
        }

        if (_connectionViews.TryGetValue(connection.Id, out var existing) &&
            GodotObject.IsInstanceValid(existing))
        {
            return;
        }

        if (!_machines.TryGetValue(connection.Source.MachineId, out var sourceState) ||
            sourceState.Placement is null ||
            !_loadedComets.TryGetValue(sourceState.Placement.CometId, out var comet) ||
            !_machineViews.TryGetValue(connection.Source.MachineId, out var sourceView) ||
            !_machineViews.TryGetValue(connection.Target.MachineId, out var targetView) ||
            !GodotObject.IsInstanceValid(comet) || !GodotObject.IsInstanceValid(sourceView) ||
            !GodotObject.IsInstanceValid(targetView))
        {
            return;
        }

        var view = new MachineConnectionView();
        view.Configure(connection, sourceView, targetView, comet);
        _connectionViews[connection.Id] = view;
    }

    private void CreatePowerCableView(MachineConnection connection)
    {
        if (_powerCableViews.TryGetValue(connection.Id, out var existing) &&
            GodotObject.IsInstanceValid(existing))
        {
            existing.RefreshEndpoints();
            return;
        }

        var sourceTarget = new PowerInteractionTarget(
            connection.Source.MachineId,
            connection.Source.PortId);
        var targetTarget = new PowerInteractionTarget(
            connection.Target.MachineId,
            connection.Target.PortId);
        if (!TryResolvePowerEndpoint(sourceTarget, out var source) ||
            !TryResolvePowerEndpoint(targetTarget, out var target) ||
            !string.Equals(source.CometId, target.CometId, StringComparison.Ordinal) ||
            !_loadedComets.TryGetValue(source.CometId, out var comet) ||
            !GodotObject.IsInstanceValid(comet))
        {
            return;
        }

        var view = new PowerCablePresentationView();
        view.Configure(
            connection.Id.Value,
            comet,
            source.Visual,
            target.Visual);
        view.SetEndpointTracking(
            sourceTarget.NodeId == PlayerShipNodeId || targetTarget.NodeId == PlayerShipNodeId);
        _powerCableViews[connection.Id] = view;
    }

    private bool ConnectionBelongsToComet(MachineConnection connection, string cometId) =>
        string.Equals(GetEndpointCometId(connection.Source), cometId, StringComparison.Ordinal) ||
        string.Equals(GetEndpointCometId(connection.Target), cometId, StringComparison.Ordinal);

    private string? GetEndpointCometId(MachineConnectionEndpoint endpoint)
    {
        if (_machines.TryGetValue(endpoint.MachineId, out var machine))
        {
            return machine.Placement?.CometId;
        }

        return _connectionNetwork.TryGetPowerNode(endpoint.MachineId, out var node)
            ? node?.CometId
            : null;
    }

    private void RemoveConnectionView(MachineConnectionId connectionId)
    {
        if (_connectionViews.Remove(connectionId, out var connectionView) &&
            GodotObject.IsInstanceValid(connectionView))
        {
            connectionView.QueueFree();
        }

        if (_powerCableViews.Remove(connectionId, out var cableView) &&
            GodotObject.IsInstanceValid(cableView))
        {
            cableView.QueueFree();
        }
    }

    private void HandleMachineInteractionRequested(MachineView view)
    {
        if (_machines.TryGetValue(view.InstanceId, out var state))
        {
            MachineInteractionRequested?.Invoke(state);
        }
    }

    private void SimulateLoadedComets(double deltaSeconds)
    {
        var changed = false;
        foreach (var cometId in _loadedComets.Keys.ToArray())
        {
            if (!GetMachinesOnComet(cometId).Any())
            {
                continue;
            }

            changed |= SimulateComet(cometId, deltaSeconds);
            _lastSimulatedUtc[cometId] = DateTimeOffset.UtcNow;
        }

        if (!changed)
        {
            return;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    private bool SimulateComet(string cometId, double deltaSeconds)
    {
        var states = GetMachinesOnComet(cometId).ToArray();
        if (states.Length == 0)
        {
            return false;
        }

        var machineFingerprints = states.ToDictionary(
            state => state.InstanceId,
            CreateSimulationFingerprint);
        var researchFingerprint = CreateResearchFingerprint();
        var changed = false;
        foreach (var state in states)
        {
            if (!state.IsConstructionComplete)
            {
                state.AdvanceConstruction(deltaSeconds);
                changed = true;
            }

            if (state.Definition.Id == MachineDefinitionIds.AutomaticMiner)
            {
                state.SetOutputConnectionAvailable(
                    _connectionNetwork.GetConnectionsForMachine(state.InstanceId)
                        .Any(connection => connection.Kind == ConnectionKind.ConveyorBelt &&
                                           connection.Source.MachineId == state.InstanceId));
            }
        }

        var transferResults = _connectionNetwork.TickDirectedTransfers(cometId, deltaSeconds);
        foreach (var transfer in transferResults.Where(result => result.TransferResult.Succeeded))
        {
            changed = true;
            if (_connectionViews.TryGetValue(transfer.ConnectionId, out var connectionView) &&
                GodotObject.IsInstanceValid(connectionView))
            {
                connectionView.ShowActivity();
            }
        }

        var researchDemand = GetResearchDemandForComet(cometId);
        var availablePower = 0.0;
        var requestedPower = 0.0;
        var consumedEnergy = 0.0;
        var externalAllocatedPower = 0.0;
        var allocations = new List<MachinePowerAllocation>();
        foreach (var staleId in _lastPowerGridResults
                     .Where(pair => string.Equals(pair.Value.Topology.CometId, cometId, StringComparison.Ordinal))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _lastPowerGridResults.Remove(staleId);
        }

        var powerResults = _powerSimulation.Tick(
            cometId,
            deltaSeconds,
            component => researchDemand > 0 &&
                         _activeResearchStation is { } stationId &&
                         component.MachineIds.Contains(stationId)
                ? researchDemand
                : 0);
        foreach (var componentResult in powerResults)
        {
            _lastPowerGridResults[componentResult.NetworkId] = componentResult;
            var dispatch = componentResult.Dispatch;
            availablePower += dispatch.AvailablePowerKilowatts;
            requestedPower += dispatch.RequestedPowerKilowatts;
            consumedEnergy += dispatch.ConsumedEnergyKilowattSeconds;
            externalAllocatedPower += dispatch.ExternalAllocatedKilowatts;
            allocations.AddRange(dispatch.MachineAllocations);
            foreach (var machineId in componentResult.Topology.MachineIds)
            {
                _lastAvailablePowerByMachine[machineId] = dispatch.AvailablePowerKilowatts;
            }

            if (dispatch.ConsumedEnergyKilowattSeconds > 0 ||
                dispatch.ExternalAllocatedKilowatts > 0)
            {
                foreach (var connectionId in componentResult.Topology.ConnectionIds)
                {
                    if (_connectionViews.TryGetValue(connectionId, out var cableView) &&
                        GodotObject.IsInstanceValid(cableView))
                    {
                        cableView.ShowActivity();
                    }
                }
            }

            var utilization = componentResult.Metrics.MaximumCapacityKilowatts <= 0
                ? 0
                : componentResult.Metrics.ActualConsumptionKilowatts /
                  componentResult.Metrics.MaximumCapacityKilowatts;
            foreach (var connectionId in componentResult.Topology.ConnectionIds)
            {
                if (_powerCableViews.TryGetValue(connectionId, out var cableView) &&
                    GodotObject.IsInstanceValid(cableView))
                {
                    cableView.SetVisualState(
                        componentResult.Control.CanDeliverPower,
                        componentResult.Metrics.ActualConsumptionKilowatts > 0,
                        (float)Math.Clamp(utilization, 0, 1));
                }
            }
        }

        var result = new PowerNetworkTickResult(
            availablePower,
            requestedPower,
            consumedEnergy,
            allocations,
            externalAllocatedPower);
        _lastPowerResults[cometId] = result;
        UpdateShipPowerPortVisuals();
        changed |= result.ConsumedEnergyKilowattSeconds > 0;
        if (researchDemand > 0 && _research.ActiveResearchId is { } activeResearchId)
        {
            var researchTick = _research.Tick(
                _researchCatalog.Get(activeResearchId),
                deltaSeconds,
                externalAllocatedPower);
            changed |= researchTick.EnergyConsumedKilowattSeconds > 0 || researchTick.Completed;
            if (researchTick.Completed)
            {
                _activeResearchStation = null;
                BuildCatalogChanged?.Invoke();
                _showMessage("Forschung abgeschlossen");
            }
        }

        foreach (var state in states)
        {
            if (_machineViews.TryGetValue(state.InstanceId, out var view) &&
                GodotObject.IsInstanceValid(view))
            {
                view.Refresh(state);
            }
        }

        changed |= states.Any(state => machineFingerprints[state.InstanceId] != CreateSimulationFingerprint(state));
        changed |= researchFingerprint != CreateResearchFingerprint();
        return changed;
    }

    private bool SimulateOfflineComet(string cometId, double elapsedSeconds)
    {
        var remaining = elapsedSeconds;
        var changed = false;
        while (remaining >= SimulationIntervalSeconds)
        {
            var step = Math.Min(OfflineSimulationStepSeconds, remaining);
            changed |= SimulateComet(cometId, step);
            remaining -= step;
        }

        return changed;
    }

    private double GetResearchDemandForComet(string cometId)
    {
        if (_research.ActiveResearchId is not { } researchId || !_research.IsEnabled)
        {
            return 0;
        }

        if (_activeResearchStation is not { } stationId ||
            !_machines.TryGetValue(stationId, out var station) ||
            station.Definition.Kind != MachineKind.Research ||
            !string.Equals(station.Placement?.CometId, cometId, StringComparison.Ordinal) ||
            !station.IsConstructionComplete)
        {
            return 0;
        }

        return _researchCatalog.Get(researchId).RequiredPowerKilowatts;
    }

    private void InvalidatePowerTopology(string cometId)
    {
        _powerSimulation.InvalidateTopology();
        _lastPowerResults.Remove(cometId);
        foreach (var networkId in _lastPowerGridResults
                     .Where(pair => string.Equals(pair.Value.Topology.CometId, cometId, StringComparison.Ordinal))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _lastPowerGridResults.Remove(networkId);
        }

        foreach (var machine in GetMachinesOnComet(cometId))
        {
            _lastAvailablePowerByMachine.Remove(machine.InstanceId);
        }
    }

    private IReadOnlyList<MachineState> GetMachinesOnComet(string cometId) =>
        _machinesByComet.TryGetValue(cometId, out var machines)
            ? machines
            : Array.Empty<MachineState>();

    private void IndexMachine(MachineState machine)
    {
        var cometId = machine.Placement?.CometId ??
                      throw new InvalidOperationException("A placed machine needs a comet ID.");
        if (!_machinesByComet.TryGetValue(cometId, out var machines))
        {
            machines = [];
            _machinesByComet.Add(cometId, machines);
        }

        machines.Add(machine);
    }

    private void RemoveIndexedMachine(MachineState machine)
    {
        var cometId = machine.Placement?.CometId;
        if (cometId is null || !_machinesByComet.TryGetValue(cometId, out var machines))
        {
            return;
        }

        machines.Remove(machine);
        if (machines.Count == 0)
        {
            _machinesByComet.Remove(cometId);
        }
    }

    private BuildMachineViewModel CreateBuildMachineViewModel(MachineDefinition definition)
    {
        var costs = _firstBasicGenerator.GetEffectiveBuildCosts(definition)
            .Select(cost =>
            {
                var item = _itemPresentation.GetOrCreateFallback(
                    cost.ItemId,
                    _astronautInventory.MaximumStackSize);
                return new BuildCostViewModel(
                    item.DisplayName,
                    cost.Amount,
                    _astronautInventory.GetAmount(cost.ItemId),
                    item.Color);
            })
            .ToArray();
        return new BuildMachineViewModel(
            definition.Id.Value,
            definition.DisplayName,
            definition.Description,
            GetFunctionSummary(definition),
            MapCategory(definition.Category),
            MachinePresentationCatalog.Instance.Get(definition.Id).Glyph,
            costs,
            IsMachineUnlocked(definition),
            GetMachineUnlockMessage(definition));
    }

    private BuildMachineViewModel CreateConnectionBuildViewModel(ConnectionTypeDefinition definition)
    {
        var presentation = ConnectionPresentationCatalog.Get(definition.Id);
        var item = _itemPresentation.GetOrCreateFallback(
            definition.RequiredBuildItemId,
            _astronautInventory.MaximumStackSize);
        return new BuildMachineViewModel(
            definition.Id.Value,
            definition.DisplayName,
            presentation.Description,
            presentation.FunctionSummary,
            BuildMenuCategory.Logistics,
            presentation.Glyph,
            [
                new BuildCostViewModel(
                    item.DisplayName,
                    1,
                    _astronautInventory.GetAmount(definition.RequiredBuildItemId),
                    item.Color),
            ],
            IsUnlocked: true);
    }

    private MachinePanelViewModel CreateProductionPanelViewModel(MachineState state)
    {
        var recipes = _recipeCatalog.ForMachine(state.Definition.Id)
            .Where(IsRecipeUnlocked)
            .Where(recipe => !recipe.IsExtractionRecipe ||
                             state.ExtractionSource is not null &&
                             recipe.SourceResourceId == state.ExtractionSource.ResourceId)
            .Select(recipe => CreateRecipeViewModel(state, recipe))
            .ToArray();
        var selected = state.SelectedRecipeId is { } selectedId
            ? _recipeCatalog.Get(selectedId) is { } selectedRecipe && IsRecipeUnlocked(selectedRecipe)
                ? selectedRecipe
                : null
            : null;
        var model = CreateBasePanel(
            state,
            recipes,
            selected?.Id.Value,
            selected is null ? 0 : (float)(state.ProductionProgressSeconds / selected.DurationSeconds),
            selected is null ? 0 : (float)selected.RequiredPowerKilowatts);
        if (state.ExtractionSource is null)
        {
            return model;
        }

        var source = state.ExtractionSource;
        var purity = SpaceFactory.Core.World.Resources.MiningConfiguration.GetPurityDisplayName(source.Purity);
        var rate = SpaceFactory.Core.World.Resources.MiningConfiguration.GetExtractionUnitsPerMinute(
            source.BaseExtractionUnitsPerMinute,
            source.Purity) * state.Definition.SpeedMultiplier;
        return model with
        {
            StatusDetail = $"{GetStatusDetail(state.Status)} Quelle: {purity}, {rate:0.#}/min" +
                           (state.Definition.Id == MachineDefinitionIds.MobileMiner
                               ? $", Akku {state.InternalEnergyKilowattSeconds:0} kWs"
                               : string.Empty),
        };
    }

    private MachinePanelViewModel CreateGeneratorPanelViewModel(MachineState state)
    {
        var recipes = state.Definition.IsFuelledGenerator
            ? new[]
            {
                new MachineRecipeViewModel(
                    "generator_fuel",
                    "Treibstoff in Strom umwandeln",
                    [CreateMaterial(state.Definition.GeneratorFuelItemId!.Value, 1, state.InputInventory)],
                    [CreateOutput(state.Definition.GeneratorReturnedContainerItemId!.Value, 1, state.OutputInventory)],
                    (float)state.Definition.GeneratorFuelSecondsPerItem),
            }
            : Array.Empty<MachineRecipeViewModel>();
        var model = CreateBasePanel(
            state,
            recipes,
            recipes.FirstOrDefault()?.RecipeId,
            0,
            0);
        if (state.Definition.Id != MachineDefinitionIds.FuelGenerator)
        {
            return model;
        }

        var transferSlot = state.InputInventory.GetSlot(
            ProductionConfiguration.FuelGeneratorTankTransferSlotIndex);
        var filledFuelId = state.Definition.GeneratorFuelItemId!.Value;
        var emptyFuelId = state.Definition.GeneratorReturnedContainerItemId!.Value;
        return model with
        {
            HasGeneratorFuelTankControls = true,
            GeneratorFuelSeconds = (float)state.GeneratorFuelSecondsRemaining,
            GeneratorFuelCapacitySeconds = (float)state.GeneratorFuelTankCapacitySeconds,
            LoadedFilledFuelContainers = transferSlot.ItemId == filledFuelId ? transferSlot.Amount : 0,
            LoadedEmptyFuelContainers = transferSlot.ItemId == emptyFuelId ? transferSlot.Amount : 0,
            GeneratorTankTransferSlotOccupied = !transferSlot.IsEmpty,
            GeneratorTankTransferSlotHasWrongContent =
                !transferSlot.IsEmpty &&
                transferSlot.ItemId != filledFuelId &&
                transferSlot.ItemId != emptyFuelId,
        };
    }

    private MachinePanelViewModel CreateStoragePanelViewModel(MachineState state)
    {
        var stored = state.InputInventory.Slots
            .Where(slot => !slot.IsEmpty)
            .GroupBy(slot => slot.ItemId!.Value)
            .Select(group => CreateMaterial(group.Key, 0, state.InputInventory))
            .ToArray();
        var recipe = new MachineRecipeViewModel(
            "storage_contents",
            $"Lagerinhalt ({state.InputInventory.UsedSlotCount}/{state.InputInventory.SlotCount} Slots)",
            stored,
            [],
            0);
        return CreateBasePanel(state, [recipe], recipe.RecipeId, 0, 0);
    }

    private MachinePanelViewModel CreateBatteryPanelViewModel(MachineState state)
    {
        var stored = state.StoredGridEnergyKilowattSeconds;
        var capacity = MachineEnergyConfiguration.BatteryBankCapacityKilowattSeconds;
        var recipe = new MachineRecipeViewModel(
            "battery_storage",
            "Netzpuffer",
            [],
            [
                new MachineOutputViewModel(
                    "Gespeicherte Energie (kWs)",
                    0,
                    (int)Math.Round(stored),
                    (int)Math.Round(capacity),
                    new Color(0.24f, 0.85f, 1)),
            ],
            0);
        var model = CreateBasePanel(
            state,
            [recipe],
            recipe.RecipeId,
            (float)(stored / capacity),
            0);
        return model with
        {
            StatusDetail = $"Netzspeicher {stored / 3600:0.00}/{capacity / 3600:0.00} kWh · " +
                           $"max. {MachineEnergyConfiguration.BatteryBankMaximumChargeKilowatts:0} kW Laden/Entladen",
        };
    }

    private MachinePanelViewModel CreateResearchPanelViewModel(MachineState state)
    {
        var ownsActiveResearch = _research.ActiveResearchId is not null &&
                                 _activeResearchStation == state.InstanceId;
        var selectedResearchId = ownsActiveResearch
            ? _research.ActiveResearchId
            : (_pendingResearch.TryGetValue(state.InstanceId, out var pending) ? pending : null);
        var recipes = _researchCatalog.TopologicalOrder
            .Select(research =>
            {
                var completed = _research.IsCompleted(research.Id);
                var prerequisiteReady = research.Prerequisites.All(_research.IsCompleted);
                var discoveriesReady = research.RequiredDiscoveries.All(
                    _research.DiscoveredResources.Contains);
                var unlockMessage = completed
                    ? "Bereits abgeschlossen"
                    : !prerequisiteReady
                        ? "Vorherige Forschung erforderlich"
                        : !discoveriesReady
                            ? $"Entdeckung erforderlich: {string.Join(", ", research.RequiredDiscoveries
                                .Where(discovery => !_research.DiscoveredResources.Contains(discovery))
                                .Select(discovery => _itemPresentation.GetOrCreateFallback(
                                    discovery,
                                    InventoryConfiguration.MaximumStackSize).DisplayName))}"
                            : string.Empty;
                return new MachineRecipeViewModel(
                    $"research:{research.Id.Value}",
                    completed ? $"{research.DisplayName} · abgeschlossen" : research.DisplayName,
                    research.MaterialCosts.Select(cost => CreateMaterial(
                        cost.ItemId,
                        cost.Amount,
                        state.InputInventory)).ToArray(),
                    [new MachineOutputViewModel("Technologie", 1, completed ? 1 : 0, 1, new Color(0.72f, 0.48f, 1))],
                    (float)research.DurationSeconds,
                    !completed && prerequisiteReady && discoveriesReady,
                    unlockMessage);
            })
            .ToArray();
        var active = ownsActiveResearch && _research.ActiveResearchId is { } activeId
            ? _researchCatalog.Get(activeId)
            : null;
        var needsStationBinding = _research.ActiveResearchId is not null && _activeResearchStation is null;
        var activeElsewhere = _research.ActiveResearchId is not null &&
                              _activeResearchStation is not null &&
                              !ownsActiveResearch;
        var progress = active is null ? 0 : (float)(_research.ProgressSeconds / active.DurationSeconds);
        var requiredPower = active is null ? 0 : (float)active.RequiredPowerKilowatts;
        var baseModel = CreateBasePanel(
            state,
            recipes,
            selectedResearchId is null ? null : $"research:{selectedResearchId.Value.Value}",
            progress,
            requiredPower);
        var status = !state.IsConstructionComplete
            ? MachineUiStatus.UnderConstruction
            : activeElsewhere
                ? MachineUiStatus.Blocked
                : active is null
                    ? MachineUiStatus.Ready
                    : _research.Status switch
                    {
                        ResearchStatus.Researching => MachineUiStatus.Producing,
                        ResearchStatus.WaitingForEnergy => MachineUiStatus.WaitingForEnergy,
                        ResearchStatus.Disabled => MachineUiStatus.SwitchedOff,
                        _ => MachineUiStatus.Ready,
                    };
        return baseModel with
        {
            Status = status,
            StatusDetail = !state.IsConstructionComplete
                ? "Die Forschungsstation wird noch aufgebaut."
                : needsStationBinding
                    ? "Laufendes Forschungsprojekt mit dem Startschalter an diese Station binden."
                : activeElsewhere
                    ? "Ein Forschungsprojekt ist bereits an eine andere Station gebunden."
                    : active is null
                        ? "Forschung auswählen, Materialien laden und starten."
                        : "Forschung läuft im lokalen Stromnetz.",
            IsActive = state.IsConstructionComplete && ownsActiveResearch && _research.IsEnabled,
        };
    }

    private MachinePanelViewModel CreateBasePanel(
        MachineState state,
        IReadOnlyList<MachineRecipeViewModel> recipes,
        string? selectedRecipeId,
        float progress,
        float requiredPower)
    {
        var availablePower = _lastAvailablePowerByMachine.TryGetValue(state.InstanceId, out var componentPower)
            ? (float)componentPower
            : 0;
        var selectedRecipe = selectedRecipeId is not null &&
                             _recipeCatalog.TryGet(new RecipeId(selectedRecipeId), out var resolvedRecipe)
            ? resolvedRecipe
            : null;
        var reservedWasteSlots = selectedRecipe is null
            ? 0
            : MachineOutputInventoryRules.GetReservedWasteSlotCount(
                state.OutputInventory.SlotCount,
                selectedRecipe.CombinedOutputs);
        var firstWasteSlot = state.OutputInventory.SlotCount - reservedWasteSlots;

        return new MachinePanelViewModel(
            state.InstanceId.Value,
            state.Definition.DisplayName,
            MachinePresentationCatalog.Instance.Get(state.Definition.Id).Glyph,
            recipes,
            selectedRecipeId,
            MapStatus(state.Status),
            GetStatusDetail(state.Status),
            state.IsEnabled,
            Mathf.Clamp(progress, 0, 1),
            requiredPower,
            availablePower,
            state.OutputInventory.Slots.Select(slot =>
            {
                if (slot.IsEmpty)
                {
                    return new MachineInventorySlotViewModel(
                        slot.Index,
                        null,
                        string.Empty,
                        0,
                        slot.MaximumAmount,
                        BuildingUiTheme.TextMuted,
                        slot.Index >= firstWasteSlot);
                }

                var itemId = slot.ItemId!.Value;
                var item = _itemPresentation.GetOrCreateFallback(
                    itemId,
                    state.OutputInventory.GetMaximumStackSize(itemId));
                return new MachineInventorySlotViewModel(
                    slot.Index,
                    item.Id,
                    item.DisplayName,
                    slot.Amount,
                    state.OutputInventory.GetMaximumStackSize(item.Id),
                    item.Color,
                    item.Product?.Category == ProductionItemCategory.Waste,
                    item.Product?.HazardKind == ItemHazardKind.Radioactive);
            }).ToArray(),
            CreatePhysicalInventorySlotModels(state.InputInventory));
    }

    private IReadOnlyList<MachineInventorySlotViewModel> CreatePhysicalInventorySlotModels(
        SlotInventory inventory) => inventory.Slots.Select(slot =>
    {
        if (slot.IsEmpty)
        {
            return new MachineInventorySlotViewModel(
                slot.Index,
                null,
                string.Empty,
                0,
                slot.MaximumAmount,
                BuildingUiTheme.TextMuted);
        }

        var itemId = slot.ItemId!.Value;
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.GetMaximumStackSize(itemId));
        return new MachineInventorySlotViewModel(
            slot.Index,
            itemId,
            item.DisplayName,
            slot.Amount,
            inventory.GetMaximumStackSize(itemId),
            item.Color,
            IsWaste: item.Product?.Category == ProductionItemCategory.Waste,
            IsRadioactive: item.Product?.HazardKind == ItemHazardKind.Radioactive);
    }).ToArray();

    private MachineRecipeViewModel CreateRecipeViewModel(MachineState state, RecipeDefinition recipe) =>
        new(
            recipe.Id.Value,
            recipe.DisplayName,
            recipe.Inputs.Select(input => CreateMaterial(
                input.ItemId,
                input.Amount,
                state.InputInventory)).ToArray(),
            recipe.CombinedOutputs.Select(output => CreateOutput(
                output.ItemId,
                output.Amount,
                state.OutputInventory)).ToArray(),
            (float)recipe.DurationSeconds,
            IsRecipeUnlocked(recipe),
            GetRecipeUnlockMessage(recipe));

    private MachineMaterialViewModel CreateMaterial(
        SpaceFactory.Core.Items.ItemId itemId,
        int required,
        SlotInventory inventory)
    {
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.MaximumStackSize);
        return new MachineMaterialViewModel(
            item.DisplayName,
            required,
            inventory.GetAmount(itemId),
            item.Color,
            item.Id);
    }

    private MachineOutputViewModel CreateOutput(
        SpaceFactory.Core.Items.ItemId itemId,
        int produced,
        SlotInventory inventory)
    {
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.MaximumStackSize);
        var stored = inventory.GetAmount(itemId);
        var capacity = stored + ProductionInventoryRules.GetAvailableCapacity(inventory, itemId);
        return new MachineOutputViewModel(
            item.DisplayName,
            produced,
            stored,
            capacity,
            item.Color,
            item.Id,
            item.Product?.Category == ProductionItemCategory.Waste,
            item.Product?.HazardKind == ItemHazardKind.Radioactive);
    }

    private void SetResearchActive(MachineState station, bool active)
    {
        if (!station.IsConstructionComplete)
        {
            _showMessage("Forschungsstation noch im Bau");
            return;
        }

        if (_research.ActiveResearchId is not null)
        {
            if (_activeResearchStation is null)
            {
                if (!active)
                {
                    return;
                }

                _activeResearchStation = station.InstanceId;
                _research.SetEnabled(true);
                MarkDirty();
                FactoryStateChanged?.Invoke();
                _showMessage("Forschung an diese Station gebunden");
                return;
            }

            if (_activeResearchStation != station.InstanceId)
            {
                _showMessage("Forschung läuft bereits in einer anderen Station");
                return;
            }

            _research.SetEnabled(active);
            MarkDirty();
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!active)
        {
            _research.SetEnabled(false);
            MarkDirty();
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!TryGetSelectedResearch(station, out var selected))
        {
            _showMessage("Zuerst eine Forschung auswählen");
            return;
        }

        var result = _research.TryStart(selected, station.InputInventory);
        if (!result.Succeeded)
        {
            _showMessage(result.Failure switch
            {
                ResearchStartFailure.MissingMaterials => "Forschungsmaterialien fehlen",
                ResearchStartFailure.MissingPrerequisite => "Vorherige Forschung fehlt",
                ResearchStartFailure.MissingDiscovery => "Benötigte Ressource wurde noch nicht entdeckt",
                ResearchStartFailure.AlreadyCompleted => "Forschung bereits abgeschlossen",
                _ => "Forschung kann nicht gestartet werden",
            });
            return;
        }

        _activeResearchStation = station.InstanceId;
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    private bool TryGetSelectedResearch(MachineState station, out ResearchDefinition definition)
    {
        var selected = (_activeResearchStation == station.InstanceId
                            ? _research.ActiveResearchId
                            : null) ??
                       (_pendingResearch.TryGetValue(station.InstanceId, out var pending) ? pending : null);
        if (selected is null)
        {
            definition = null!;
            return false;
        }

        definition = _researchCatalog.Get(selected.Value);
        return true;
    }

    private bool HasBuildMaterials(MachineDefinitionId definitionId)
    {
        var definition = _machineCatalog.Get(definitionId);
        return IsMachineUnlocked(definition) &&
               ProductionInventoryRules.ContainsAll(
                   _astronautInventory,
                   _firstBasicGenerator.GetEffectiveBuildCosts(definition));
    }

    private bool HasHotbarPlacementItem(MachineDefinitionId definitionId)
    {
        if (_hotbarPlacementSource is not { } source ||
            !_machineCatalog.TryGet(definitionId, out var definition) || definition is null ||
            definition.PlacementItemId != source.ItemId)
        {
            return false;
        }

        var slot = _hotbarInventory.GetSlot(source.SlotIndex);
        return slot.ItemId == source.ItemId && slot.Amount > 0;
    }

    private IReadOnlyList<ItemAmount> GetActivePlacementCosts(MachineDefinition definition) =>
        _hotbarPlacementSource is { } source && definition.PlacementItemId == source.ItemId
            ? [new ItemAmount(source.ItemId, 1)]
            : _firstBasicGenerator.GetEffectiveBuildCosts(definition);

    private bool HasActivePlacementCosts(IReadOnlyList<ItemAmount> costs)
    {
        if (_hotbarPlacementSource is not { } source)
        {
            return ProductionInventoryRules.ContainsAll(_astronautInventory, costs);
        }

        return costs.Count == 1 && costs[0].ItemId == source.ItemId && costs[0].Amount == 1 &&
               HasActivePlacementItem(source.ItemId);
    }

    private bool TryConsumeActivePlacementCosts(IReadOnlyList<ItemAmount> costs) =>
        _hotbarPlacementSource is { } source
            ? costs.Count == 1 && costs[0].ItemId == source.ItemId && costs[0].Amount == 1 &&
              TryConsumeActivePlacementItem(source.ItemId)
            : ProductionInventoryRules.TryRemoveAll(_astronautInventory, costs);

    private bool TryRestoreActivePlacementCosts(IReadOnlyList<ItemAmount> costs) =>
        _hotbarPlacementSource is { } source
            ? costs.Count == 1 && costs[0].ItemId == source.ItemId && costs[0].Amount == 1 &&
              TryRestoreActivePlacementItem(source.ItemId)
            : ProductionInventoryRules.TryAddAll(_astronautInventory, costs);

    private bool HasActivePlacementItem(ItemId itemId)
    {
        if (_hotbarPlacementSource is not { } source)
        {
            return _astronautInventory.GetAmount(itemId) > 0;
        }

        var slot = _hotbarInventory.GetSlot(source.SlotIndex);
        return source.ItemId == itemId && slot.ItemId == itemId && slot.Amount > 0;
    }

    private bool TryConsumeActivePlacementItem(ItemId itemId) =>
        _hotbarPlacementSource is { } source
            ? source.ItemId == itemId &&
              _hotbarInventory.RemoveFromSlot(source.SlotIndex, itemId, 1).Succeeded
            : _astronautInventory.Remove(itemId, 1).Succeeded;

    private bool TryRestoreActivePlacementItem(ItemId itemId) =>
        _hotbarPlacementSource is { } source
            ? source.ItemId == itemId &&
              _hotbarInventory.AddToSlot(source.SlotIndex, itemId, 1).Succeeded
            : _astronautInventory.Add(itemId, 1).Succeeded;

    private bool IsConnectionUnlocked(ConnectionTypeDefinition definition)
    {
        var craftingRecipes = _recipeCatalog.All
            .Where(recipe => recipe.Outputs.Any(output => output.ItemId == definition.RequiredBuildItemId))
            .ToArray();
        return craftingRecipes.Length == 0 || craftingRecipes.Any(IsRecipeUnlocked);
    }

    private bool IsUnlocked(ResearchId? requirement) =>
        requirement is null || _research.IsCompleted(requirement.Value);

    private bool IsMachineUnlocked(MachineDefinition definition)
    {
        if (!IsUnlocked(definition.UnlockRequirement))
        {
            return false;
        }

        var unlockingResearch = _researchCatalog.All
            .Where(research => research.UnlockedMachines.Contains(definition.Id))
            .ToArray();
        return unlockingResearch.Length == 0 ||
               unlockingResearch.Any(research => _research.IsCompleted(research.Id));
    }

    private string GetMachineUnlockMessage(MachineDefinition definition)
    {
        if (definition.UnlockRequirement is { } direct && !_research.IsCompleted(direct))
        {
            return $"Forschung erforderlich: {_researchCatalog.Get(direct).DisplayName}";
        }

        var unlockingResearch = _researchCatalog.All
            .Where(research => research.UnlockedMachines.Contains(definition.Id))
            .ToArray();
        return unlockingResearch.Length > 0 &&
               unlockingResearch.All(research => !_research.IsCompleted(research.Id))
            ? $"Forschung erforderlich: {string.Join(" / ", unlockingResearch.Select(research => research.DisplayName))}"
            : string.Empty;
    }

    private bool IsRecipeUnlocked(RecipeDefinition recipe)
    {
        if (!IsUnlocked(recipe.UnlockRequirement))
        {
            return false;
        }

        var recipeResearch = _researchCatalog.All
            .Where(research => research.UnlockedRecipes.Contains(recipe.Id))
            .ToArray();
        if (recipeResearch.Length > 0 &&
            recipeResearch.All(research => !_research.IsCompleted(research.Id)))
        {
            return false;
        }

        if (recipe.Tags.Contains("special-resource", StringComparer.Ordinal) &&
            recipe.Inputs
                .Select(input => input.ItemId)
                .Where(itemId => DefaultProductionItemCatalog.Instance.TryGet(itemId, out var item) &&
                                 item?.Category == ProductionItemCategory.RawMaterial)
                .Any(itemId => !_research.DiscoveredResources.Contains(itemId)))
        {
            return false;
        }

        if (!recipe.Tags.Contains("alternative", StringComparer.Ordinal) &&
            !recipe.Tags.Contains("endgame", StringComparer.Ordinal))
        {
            return recipe.TechnologyTier <= GetMaximumAccessibleNormalRecipeTier();
        }

        var directGroupUnlock = recipe.AlternativeGroup is { } group &&
                                _research.CompletedResearch
                                    .Select(_researchCatalog.Get)
                                    .Any(research => research.UnlockedAlternativeRecipeGroups
                                        .Any(unlocked => string.Equals(
                                            unlocked.Value,
                                            group,
                                            StringComparison.Ordinal)));
        return directGroupUnlock || _research.CompletedResearch
            .Select(_researchCatalog.Get)
            .Any(research => research.Tier >= recipe.TechnologyTier);
    }

    private string GetRecipeUnlockMessage(RecipeDefinition recipe)
    {
        if (recipe.UnlockRequirement is { } researchId && !_research.IsCompleted(researchId))
        {
            return $"Forschung erforderlich: {_researchCatalog.Get(researchId).DisplayName}";
        }

        var recipeResearch = _researchCatalog.All
            .Where(research => research.UnlockedRecipes.Contains(recipe.Id))
            .ToArray();
        if (recipeResearch.Length > 0 &&
            recipeResearch.All(research => !_research.IsCompleted(research.Id)))
        {
            return $"Forschung erforderlich: {string.Join(" / ", recipeResearch.Select(research => research.DisplayName))}";
        }

        if (recipe.Tags.Contains("special-resource", StringComparer.Ordinal))
        {
            var missing = recipe.Inputs
                .Select(input => input.ItemId)
                .Where(itemId => DefaultProductionItemCatalog.Instance.TryGet(itemId, out var item) &&
                                 item?.Category == ProductionItemCategory.RawMaterial &&
                                 !_research.DiscoveredResources.Contains(itemId))
                .Select(itemId => _itemPresentation.GetOrCreateFallback(
                    itemId,
                    InventoryConfiguration.MaximumStackSize).DisplayName)
                .Distinct(StringComparer.CurrentCulture)
                .ToArray();
            if (missing.Length > 0)
            {
                return $"Ressource entdecken: {string.Join(", ", missing)}";
            }
        }

        if (!recipe.Tags.Contains("alternative", StringComparer.Ordinal) &&
            !recipe.Tags.Contains("endgame", StringComparer.Ordinal) &&
            recipe.TechnologyTier > GetMaximumAccessibleNormalRecipeTier())
        {
            return $"Forschung bis Stufe {(int)recipe.TechnologyTier - 1} erforderlich";
        }

        return recipe.Tags.Contains("alternative", StringComparer.Ordinal) ||
               recipe.Tags.Contains("endgame", StringComparer.Ordinal)
            ? $"Fortgeschrittene Forschung (Stufe {(int)recipe.TechnologyTier}) erforderlich"
            : string.Empty;
    }

    private TechnologyTier GetMaximumAccessibleNormalRecipeTier()
    {
        var highestCompletedTier = _research.CompletedResearch.Count == 0
            ? 0
            : _research.CompletedResearch
                .Select(_researchCatalog.Get)
                .Max(research => (int)research.Tier);
        return (TechnologyTier)Math.Min(
            (int)TechnologyTier.Tier10,
            highestCompletedTier + 1);
    }

    private void ShowTransferResult(MachineInventoryTransferResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            MarkDirty();
            BuildCatalogChanged?.Invoke();
            FactoryStateChanged?.Invoke();
            _showMessage(successMessage);
            return;
        }

        _showMessage(result.Failure switch
        {
            MachineInventoryTransferFailure.MissingSourceItems => "Benötigte Materialien fehlen",
            MachineInventoryTransferFailure.TargetFull => "Zielinventar voll",
            MachineInventoryTransferFailure.NothingToTransfer => "Nichts zu übertragen",
            _ => "Übertragung nicht möglich",
        });
    }

    private void ShowGeneratorTankTransferResult(
        GeneratorFuelTankTransferResult result,
        string successMessage)
    {
        if (result.Succeeded)
        {
            MarkDirty();
            FactoryStateChanged?.Invoke();
            _showMessage(successMessage);
            return;
        }

        _showMessage(result.Failure switch
        {
            GeneratorFuelTankTransferFailure.TankFull => "Tank voll",
            GeneratorFuelTankTransferFailure.TankEmpty => "Tank leer",
            GeneratorFuelTankTransferFailure.TankCannotFillContainer => "Zu wenig Treibstoff",
            GeneratorFuelTankTransferFailure.MissingFilledContainer => "Gefüllter Behälter fehlt",
            GeneratorFuelTankTransferFailure.MissingEmptyContainer => "Leerer Behälter fehlt",
            GeneratorFuelTankTransferFailure.WrongContent => "Falscher Inhalt",
            GeneratorFuelTankTransferFailure.TransferSlotBlocked => "Tank-Slot blockiert",
            _ => "Tank nicht verfügbar",
        });
    }

    private void MarkDirty()
    {
        _dirty = true;
        _autosaveElapsed = Math.Min(_autosaveElapsed, AutosaveIntervalSeconds);
    }

    private static BuildMenuCategory MapCategory(MachineCategory category) => category switch
    {
        MachineCategory.Processing => BuildMenuCategory.Processing,
        MachineCategory.Manufacturing => BuildMenuCategory.Manufacturing,
        MachineCategory.Energy => BuildMenuCategory.Energy,
        MachineCategory.Storage => BuildMenuCategory.Storage,
        MachineCategory.Research => BuildMenuCategory.Research,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    private bool TryResolveDropSource(InventorySlotAddress address, out SlotInventory inventory)
    {
        if (address.InventoryId.StartsWith("machine_output:", StringComparison.Ordinal) &&
            _machines.TryGetValue(
                new MachineInstanceId(address.InventoryId["machine_output:".Length..]),
                out var outputMachine))
        {
            inventory = outputMachine.OutputInventory;
            return address.SlotIndex >= 0 && address.SlotIndex < inventory.SlotCount;
        }

        if (address.InventoryId.StartsWith("machine_input:", StringComparison.Ordinal) &&
            _machines.TryGetValue(
                new MachineInstanceId(address.InventoryId["machine_input:".Length..]),
                out var inputMachine))
        {
            inventory = inputMachine.InputInventory;
            return address.SlotIndex >= 0 && address.SlotIndex < inventory.SlotCount;
        }

        inventory = address.InventoryId switch
        {
            InventoryMenuController.AstronautInventoryId => _astronautInventory,
            MachinePanelController.PersonalInventoryId => _astronautInventory,
            InventoryMenuController.HotbarInventoryId => _hotbarInventory,
            InventoryMenuController.ToolInventoryId => _toolInventoryState.Inventory,
            InventoryMenuController.ShipInventoryId when _shipInventory is not null => _shipInventory,
            InventoryMenuController.StorageInventoryId when _openMachine is { Definition.Kind: MachineKind.Storage } =>
                _openMachine.InputInventory,
            _ => null!,
        };
        return inventory is not null && address.SlotIndex >= 0 && address.SlotIndex < inventory.SlotCount;
    }

    private bool TryFindSafeDropPosition(
        Vector2 ownerPosition,
        Vector2 requestedWorldPosition,
        out Vector2 safePosition)
    {
        safePosition = default;
        var direction = (requestedWorldPosition - ownerPosition).Normalized();
        if (direction == Vector2.Zero)
        {
            direction = Vector2.Right.Rotated((float)(ownerPosition.X * 0.013 + ownerPosition.Y * 0.007));
        }

        // Honour the visible backdrop position selected by the player first. On a large comet,
        // every short offset around the astronaut can still overlap the asteroid even though the
        // cursor itself is already over clear space. Intermediate candidates retain the safe
        // fallback behaviour when the exact cursor position is obstructed.
        var requestedDistance = ownerPosition.DistanceTo(requestedWorldPosition);
        var distances = new[]
            {
                requestedDistance,
                requestedDistance * 0.75f,
                requestedDistance * 0.5f,
                330,
                235,
                145,
                (float)WorldItemDropConfiguration.SpawnOffset,
            }
            .Where(distance => distance >= WorldItemDropConfiguration.SpawnOffset)
            .Distinct()
            .ToArray();
        ReadOnlySpan<float> angles = [0, -0.32f, 0.32f, -0.65f, 0.65f];
        foreach (var distance in distances)
        {
            foreach (var angle in angles)
            {
                var candidate = ownerPosition + direction.Rotated(angle) * distance;
                if (IsDropPositionClear(candidate))
                {
                    safePosition = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsDropPositionClear(Vector2 position)
    {
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D
            {
                Radius = (float)WorldItemDropConfiguration.SpawnClearanceRadius,
            },
            Transform = new Transform2D(0, position),
            CollisionMask = 1u | 2u | ResourceDepositView.ResourceCollisionLayer |
                            MachineView.MachineCollisionLayer | DroppedItemView.DroppedItemCollisionLayer,
            CollideWithAreas = true,
            CollideWithBodies = true,
        };
        return GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private static Vector2 LimitDroppedItemVelocity(Vector2 velocity)
    {
        var maximum = (float)WorldItemDropConfiguration.MaximumInheritedSpeed;
        return velocity.LengthSquared() > maximum * maximum
            ? velocity.Normalized() * maximum
            : velocity;
    }

    private bool TryCreateDroppedItemView(DroppedItemStateData state)
    {
        if (_droppedItemViews.ContainsKey(state.Id))
        {
            return true;
        }

        try
        {
            var itemId = new ItemId(state.ItemId);
            var presentation = _itemPresentation.GetOrCreateFallback(
                itemId,
                DefaultProductionItemCatalog.Instance.TryGet(itemId, out var definition) && definition is not null
                    ? definition.MaximumStackSize
                    : InventoryConfiguration.MaximumStackSize);
            var view = new DroppedItemView { Name = $"DroppedItem_{state.Id}" };
            view.Configure(state, presentation);
            AddChild(view);
            _droppedItemViews.Add(state.Id, view);
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"Dropped item '{state.Id}' could not be presented: {exception.Message}");
            return false;
        }
    }

    private void RefreshDroppedItems()
    {
        CaptureDroppedItemViews();
        if (_dropPickupActiveProvider?.Invoke() == true && _dropOwnerPositionProvider is not null)
        {
            var playerPosition = _dropOwnerPositionProvider();
            var radiusSquared = (float)(WorldItemDropConfiguration.PickupRadius *
                                        WorldItemDropConfiguration.PickupRadius);
            foreach (var pair in _droppedItemViews.ToArray())
            {
                if (pair.Value.GlobalPosition.DistanceSquaredTo(playerPosition) > radiusSquared ||
                    !_droppedItems.TryGetValue(pair.Key, out var state))
                {
                    continue;
                }

                var itemId = new ItemId(state.ItemId);
                if (!_astronautInventory.Add(itemId, state.Amount).Succeeded)
                {
                    continue;
                }

                RemoveDroppedItem(pair.Key);
                _showMessage($"{_itemPresentation.GetOrCreateFallback(itemId, state.Amount).DisplayName} aufgenommen");
                MarkInventoryChanged();
            }
        }

        RefreshDroppedItemViews(force: false);
    }

    private void CaptureDroppedItemViews()
    {
        foreach (var pair in _droppedItemViews)
        {
            if (_droppedItems.TryGetValue(pair.Key, out var state) &&
                GodotObject.IsInstanceValid(pair.Value))
            {
                var captured = pair.Value.Capture(state);
                if (captured != state)
                {
                    _droppedItems[pair.Key] = captured;
                    _dirty = true;
                }
            }
        }
    }

    private void RefreshDroppedItemViews(bool force)
    {
        foreach (var state in _droppedItems.Values)
        {
            if (ShouldPresentDroppedItem(state))
            {
                TryCreateDroppedItemView(state);
            }
            else if (_droppedItemViews.TryGetValue(state.Id, out var view))
            {
                _droppedItemViews.Remove(state.Id);
                view.QueueFree();
            }
        }

        if (force)
        {
            CaptureDroppedItemViews();
        }
    }

    private bool ShouldPresentDroppedItem(DroppedItemStateData state)
    {
        if (_dropOwnerPositionProvider is null || _sectorSize <= 0)
        {
            return true;
        }

        var owner = _dropOwnerPositionProvider();
        var maximumDistance = _sectorSize * 2.5f;
        return new Vector2((float)state.PositionX, (float)state.PositionY)
                   .DistanceSquaredTo(owner) <= maximumDistance * maximumDistance;
    }

    private void RemoveDroppedItem(string id)
    {
        _droppedItems.Remove(id);
        if (_droppedItemViews.Remove(id, out var view) && GodotObject.IsInstanceValid(view))
        {
            view.QueueFree();
        }
    }

    private static MachineUiStatus MapStatus(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => MachineUiStatus.UnderConstruction,
        MachineOperationStatus.Disabled => MachineUiStatus.SwitchedOff,
        MachineOperationStatus.Ready or MachineOperationStatus.NoRecipeSelected => MachineUiStatus.Ready,
        MachineOperationStatus.Producing => MachineUiStatus.Producing,
        MachineOperationStatus.WaitingForMaterial => MachineUiStatus.WaitingForMaterials,
        MachineOperationStatus.WaitingForEnergy => MachineUiStatus.WaitingForEnergy,
        MachineOperationStatus.OutputFull => MachineUiStatus.OutputFull,
        _ => MachineUiStatus.Blocked,
    };

    private static string GetStatusDetail(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => "Die Maschine wird aufgebaut.",
        MachineOperationStatus.Disabled => "Startschalter ist ausgeschaltet.",
        MachineOperationStatus.NoRecipeSelected => "Wähle zuerst ein Rezept.",
        MachineOperationStatus.Ready => "Bereit für den nächsten Zyklus.",
        MachineOperationStatus.Producing => "Produktionszyklus läuft.",
        MachineOperationStatus.WaitingForMaterial => "Eingabematerialien fehlen.",
        MachineOperationStatus.WaitingForEnergy => "Das lokale Stromnetz liefert zu wenig Leistung.",
        MachineOperationStatus.OutputFull => "Der Ausgabespeicher ist voll.",
        MachineOperationStatus.MissingResourceSource => "Keine passende Erzquelle gebunden.",
        MachineOperationStatus.WaitingForLogistics => "Kein Förderband am Miner-Ausgang angeschlossen.",
        _ => "Maschine ist blockiert.",
    };

    private static string GetFunctionSummary(MachineDefinition definition) => definition.Kind switch
    {
        MachineKind.Generator => $"Erzeugt {definition.GeneratedPowerKilowatts:0} kW im lokalen Netz",
        MachineKind.Storage => $"Lagert bis zu {definition.InputSlotCount} Stapel",
        MachineKind.Research => "Schaltet Maschinen und Rezepte frei",
        _ => "Verarbeitet Materialien nach auswählbaren Rezepten",
    };

    private readonly record struct ConnectionPlacementEvaluation(
        bool Succeeded,
        string FailureMessage,
        ConnectionTypeDefinition? Type,
        MachineView? SourceView,
        MachineView? TargetView,
        MachinePortDefinition? SourcePort,
        MachinePortDefinition? TargetPort)
    {
        public static ConnectionPlacementEvaluation Failed(string message) =>
            new(false, message, null, null, null, null, null);

        public static ConnectionPlacementEvaluation ForSource(
            ConnectionTypeDefinition type,
            MachineView source,
            MachinePortDefinition sourcePort) =>
            new(true, string.Empty, type, source, null, sourcePort, null);

        public static ConnectionPlacementEvaluation ForTarget(
            ConnectionTypeDefinition type,
            MachineView source,
            MachineView target,
            MachinePortDefinition sourcePort,
            MachinePortDefinition targetPort) =>
            new(true, string.Empty, type, source, target, sourcePort, targetPort);
    }

    private readonly record struct PowerEndpointCandidate(
        PowerInteractionTarget Target,
        string CometId,
        Vector2 WorldPosition,
        PowerCableVisualEndpoint Visual,
        string DisplayName);

    private sealed record DismantlingTarget(
        string StableId,
        MachineInstanceId? MachineId,
        MachineConnectionId? ConnectionId,
        Vector2 WorldPosition,
        double DurationSeconds);

    private readonly record struct HotbarPlacementSource(int SlotIndex, ItemId ItemId);

    private static MachineSimulationFingerprint CreateSimulationFingerprint(MachineState state) => new(
        state.Status,
        state.ConstructionProgressSeconds,
        state.ProductionProgressSeconds,
        state.GeneratorFuelSecondsRemaining,
        state.InternalEnergyKilowattSeconds,
        state.StoredGridEnergyKilowattSeconds,
        state.InputInventory.TotalItemCount,
        state.OutputInventory.TotalItemCount);

    private ResearchSimulationFingerprint CreateResearchFingerprint() => new(
        _research.ActiveResearchId,
        _research.ProgressSeconds,
        _research.IsEnabled,
        _research.Status,
        _research.CompletedResearch.Count,
        _research.DiscoveredResources.Count);

    private readonly record struct MachineSimulationFingerprint(
        MachineOperationStatus Status,
        double ConstructionProgress,
        double ProductionProgress,
        double GeneratorFuel,
        double InternalEnergy,
        double StoredGridEnergy,
        int InputItems,
        int OutputItems);

    private readonly record struct ResearchSimulationFingerprint(
        ResearchId? ActiveResearch,
        double Progress,
        bool Enabled,
        ResearchStatus Status,
        int CompletedCount,
        int DiscoveredResourceCount);
}

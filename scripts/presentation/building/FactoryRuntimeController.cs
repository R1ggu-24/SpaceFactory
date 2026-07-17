using Godot;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;
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
    private readonly Dictionary<string, DateTimeOffset> _lastSimulatedUtc = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PowerNetworkTickResult> _lastPowerResults = new(StringComparer.Ordinal);
    private readonly Dictionary<PowerNetworkId, ConnectedPowerGridTickResult> _lastPowerGridResults = [];
    private readonly Dictionary<MachineInstanceId, double> _lastAvailablePowerByMachine = [];
    private readonly Dictionary<MachineInstanceId, ResearchId> _pendingResearch = [];

    private SlotInventory _astronautInventory = null!;
    private SlotInventory? _shipInventory;
    private IReadOnlyList<InventorySlotState> _pendingShipInventory = [];
    private IReadOnlyList<MachineConnectionSnapshot> _pendingShipConnections = [];
    private ShipPowerState _persistedShipPower = ShipPowerState.Default;
    private ShipDockingStateData _persistedShipDocking = ShipDockingStateData.Detached;
    private IReadOnlyList<InventorySlotState> _lastAstronautInventory = [];
    private IReadOnlyList<InventorySlotState> _lastShipInventory = [];
    private ItemPresentationCatalog _itemPresentation = null!;
    private IFactoryStateStore _stateStore = null!;
    private Func<double> _shipFuelProvider = null!;
    private Action<double> _restoreShipFuel = null!;
    private Action<string> _showMessage = null!;
    private ResearchState _research = new();
    private FirstBasicGeneratorState _firstBasicGenerator = new();
    private MachinePlacementPreview _placementPreview = null!;
    private MachineConnectionPlacementPreview _connectionPlacementPreview = null!;
    private PowerCablePlacementPreview _powerCablePlacementPreview = null!;
    private PowerInteractionTarget? _powerCableSourceTarget;
    private PowerInteractionTarget? _powerCableCandidateTarget;
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

    public FactoryRuntimeController()
    {
        _powerSimulation = new ConnectedPowerGridSimulation(_connectionNetwork, _recipeCatalog);
    }

    public bool IsPlacementActive =>
        _placementPreview?.IsActive == true ||
        _connectionPlacementPreview?.IsActive == true ||
        _powerCablePlacementPreview?.IsActive == true;

    public MachinePlacementFailureReason PlacementFailure =>
        _placementPreview?.CurrentFailure ?? MachinePlacementFailureReason.PlacementNotActive;

    public IReadOnlyCollection<MachineState> Machines => _machines.Values.ToArray();

    public IReadOnlyCollection<MachineConnection> Connections => _connectionNetwork.Connections;

    public ResearchState Research => _research;

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
            FactoryStateJsonCodec.RunSchemaV4SmokeTest();
            _persistenceSmokeCompleted = true;
            GD.Print(
                "FACTORY_PERSISTENCE_V4_SMOKE_OK: connections, power switches, ship ports, docking, migrations");
        }

        _placementPreview = new MachinePlacementPreview { Name = "MachinePlacementPreview" };
        AddChild(_placementPreview);
        _placementPreview.ConfigureWorldSources(
            () => _loadedComets.Values.Where(GodotObject.IsInstanceValid).ToArray(),
            () => _machineViews.Values.Where(GodotObject.IsInstanceValid).ToArray());
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
        var originalLastInventory = _lastAstronautInventory;
        var originalDirty = _dirty;
        var originalAutosaveElapsed = _autosaveElapsed;
        var hadLoadedComet = _loadedComets.TryGetValue(comet.CometId, out var previousComet);
        var debugInventory = new SlotInventory(InventoryConfiguration.AstronautSlotCount);
        if (!debugInventory.Add(ProductionItemIds.PowerCable, 3).Succeeded)
        {
            throw new InvalidOperationException("The cable runtime smoke could not create its isolated inventory.");
        }

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
            _lastAstronautInventory = originalLastInventory;
            _dirty = originalDirty;
            _autosaveElapsed = originalAutosaveElapsed;
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
        if (!StartPlacement(ConnectionTypeIds.PowerCable.Value) ||
            !TryResolvePowerEndpoint(source, out var sourceEndpoint) ||
            !TryResolvePowerEndpoint(target, out var targetEndpoint))
        {
            throw new InvalidOperationException("The real power cable build item could not be selected.");
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
        ItemPresentationCatalog itemPresentation,
        IFactoryStateStore stateStore,
        Func<double> shipFuelProvider,
        Action<double> restoreShipFuel,
        Action<string> showMessage)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(itemPresentation);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(shipFuelProvider);
        ArgumentNullException.ThrowIfNull(restoreShipFuel);
        ArgumentNullException.ThrowIfNull(showMessage);
        if (_initialized)
        {
            throw new InvalidOperationException("Factory runtime is already initialized.");
        }

        _astronautInventory = astronautInventory;
        _itemPresentation = itemPresentation;
        _stateStore = stateStore;
        _shipFuelProvider = shipFuelProvider;
        _restoreShipFuel = restoreShipFuel;
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
        foreach (var comet in sector.Comets)
        {
            RegisterComet(comet);
        }
    }

    public void UnregisterSector(SectorView sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
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
        var machines = _machineCatalog.All
            .OrderBy(machine => machine.Category)
            .ThenBy(machine => machine.DisplayName, StringComparer.CurrentCulture)
            .Select(CreateBuildMachineViewModel)
            .ToArray();
        var connections = _connectionTypes.All
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
            _placementPreview.Cancel();
            _connectionPlacementPreview.Cancel();
            _powerCablePlacementPreview.Cancel();
            _powerCableSourceTarget = null;
            _powerCableCandidateTarget = null;
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

        if (!IsUnlocked(definition.UnlockRequirement))
        {
            _showMessage("Maschine noch nicht erforscht");
            return false;
        }

        _connectionPlacementPreview.Cancel();
        _powerCablePlacementPreview.Cancel();
        _powerCableSourceTarget = null;
        _powerCableCandidateTarget = null;
        _placementPreview.Start(id, HasBuildMaterials);
        return true;
    }

    public void CancelPlacement()
    {
        _placementPreview.Cancel();
        _connectionPlacementPreview.Cancel();
        _powerCablePlacementPreview.Cancel();
        _powerCableSourceTarget = null;
        _powerCableCandidateTarget = null;
    }

    public void RotatePlacement(int direction)
    {
        if (_placementPreview.IsActive)
        {
            _placementPreview.Rotate(direction);
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
        var costs = _firstBasicGenerator.GetEffectiveBuildCosts(definition);
        if (!ProductionInventoryRules.ContainsAll(_astronautInventory, costs))
        {
            _showMessage("Materialien fehlen");
            return false;
        }

        var state = new MachineState(
            new MachineInstanceId($"machine:{Guid.NewGuid():N}"),
            definition,
            placement);
        if (definition.Kind is MachineKind.Generator or MachineKind.Research or
            MachineKind.Storage or MachineKind.Infrastructure)
        {
            state.SetEnabled(true);
        }

        if (!ProductionInventoryRules.TryRemoveAll(_astronautInventory, costs))
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

            if (!ProductionInventoryRules.TryAddAll(_astronautInventory, costs))
            {
                throw new InvalidOperationException("A failed construction could not restore its build costs.");
            }

            throw;
        }

        _placementPreview.Cancel();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage($"{definition.DisplayName} wird gebaut");
        return true;
    }

    private bool TryAdvanceConnectionPlacement()
    {
        var type = _connectionPlacementPreview.SelectedType;
        if (type is null)
        {
            return false;
        }

        if (_astronautInventory.GetAmount(type.RequiredBuildItemId) < 1)
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
        var selectedView = FindNearestConnectionMachine(worldPosition);
        if (selectedView is null)
        {
            const string message = "Keine Maschine am Verbindungspunkt";
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        var selectedState = _machines[selectedView.InstanceId];
        if (_connectionPlacementPreview.Source is not { } sourceView)
        {
            var sourcePort = FindConnectionPort(selectedState, type, source: true);
            if (sourcePort is null)
            {
                var message = type.IsDirectional
                    ? "Diese Maschine besitzt keinen passenden Ausgang"
                    : "Diese Maschine besitzt keinen passenden Anschluss";
                _connectionPlacementPreview.SetFailure(message);
                _showMessage(message);
                return false;
            }

            _connectionPlacementPreview.SetSource(selectedView);
            _showMessage("Quelle gewählt – jetzt Zielmaschine anklicken");
            return true;
        }

        var sourceState = _machines[sourceView.InstanceId];
        var sourceConnectionPort = FindConnectionPort(sourceState, type, source: true);
        var targetConnectionPort = FindConnectionPort(selectedState, type, source: false);
        if (sourceConnectionPort is null || targetConnectionPort is null)
        {
            var message = type.IsDirectional
                ? "Ausgang und Eingang sind nicht kompatibel"
                : "Die Anschlüsse sind nicht kompatibel";
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        var sourceAnchor = sourceView.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetSourceAnchor(type.Kind));
        var targetAnchor = selectedView.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetTargetAnchor(type.Kind));
        if (sourceAnchor.DistanceTo(targetAnchor) > ConnectionPresentationCatalog.MaximumConnectionLength)
        {
            const string message = "Verbindung ist zu lang";
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        var result = _connectionNetwork.TryConnect(
            new MachineConnectionId($"connection:{Guid.NewGuid():N}"),
            type.Id,
            new MachineConnectionEndpoint(sourceState.InstanceId, sourceConnectionPort.Id),
            new MachineConnectionEndpoint(selectedState.InstanceId, targetConnectionPort.Id));
        if (!result.Succeeded || result.Connection is null)
        {
            var message = GetConnectionFailureText(result.Failure);
            _connectionPlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (!_astronautInventory.Remove(type.RequiredBuildItemId, 1).Succeeded)
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
            if (!_astronautInventory.Add(type.RequiredBuildItemId, 1).Succeeded)
            {
                throw new InvalidOperationException("A failed connection could not restore its build item.");
            }

            throw;
        }

        _connectionPlacementPreview.Cancel();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage($"{type.DisplayName} verbunden");
        return true;
    }

    private bool TryAdvancePowerCablePlacement() =>
        TryAdvancePowerCablePlacement(GetGlobalMousePosition());

    private bool TryAdvancePowerCablePlacement(Vector2 worldPosition)
    {
        RefreshPowerCablePlacementCandidate(worldPosition);
        if (_powerCableCandidateTarget is not { } selected)
        {
            const string message = "Kein Stromanschluss am Verbindungspunkt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        return TryAdvancePowerCablePlacement(selected);
    }

    private bool TryAdvancePowerCablePlacement(PowerInteractionTarget selected)
    {
        var type = _connectionTypes.Get(ConnectionKind.PowerCable);
        if (_astronautInventory.GetAmount(type.RequiredBuildItemId) < 1)
        {
            const string message = "Stromkabel fehlt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (!TryResolvePowerEndpoint(selected, out var selectedEndpoint))
        {
            const string message = "Kein Stromanschluss am Verbindungspunkt";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (IsPowerEndpointOccupied(selected))
        {
            const string message = "Anschluss ist bereits belegt";
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
            const string message = "Zweiter Anschluss muss verschieden sein";
            _powerCablePlacementPreview.SetFailure(message);
            _showMessage(message);
            return false;
        }

        if (sourceEndpoint.WorldPosition.DistanceTo(selectedEndpoint.WorldPosition) >
            PowerGridConfiguration.MaximumCableLengthWorldUnits)
        {
            const string message = "Stromkabel ist zu lang";
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

        if (!_astronautInventory.Remove(type.RequiredBuildItemId, 1).Succeeded)
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
            if (!_astronautInventory.Add(type.RequiredBuildItemId, 1).Succeeded)
            {
                throw new InvalidOperationException("A failed power cable could not restore its build item.");
            }

            throw;
        }

        _powerCablePlacementPreview.Cancel();
        _powerCableSourceTarget = null;
        _powerCableCandidateTarget = null;
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

        var compatible = candidate is not null &&
                         candidate.Value.Target != _powerCableSourceTarget.Value &&
                         !IsPowerEndpointOccupied(candidate.Value.Target) &&
                         TryResolvePowerEndpoint(_powerCableSourceTarget.Value, out var source) &&
                         string.Equals(source.CometId, candidate.Value.CometId, StringComparison.Ordinal) &&
                         source.WorldPosition.DistanceTo(candidate.Value.WorldPosition) <=
                         PowerGridConfiguration.MaximumCableLengthWorldUnits;
        _powerCablePlacementPreview.SetCandidate(
            candidate?.Visual,
            compatible,
            candidate is null
                ? string.Empty
                : IsPowerEndpointOccupied(candidate.Value.Target)
                    ? "Anschluss ist bereits belegt"
                    : "Anschluss nicht kompatibel");
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
            (float)PowerGridConfiguration.PortInteractionRadiusWorldUnits)?.Target;

    public double GetPowerInteractionDistanceSquared(
        PowerInteractionTarget target,
        Vector2 worldPosition) =>
        TryResolvePowerEndpoint(target, out var endpoint)
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

    private static string GetConnectionFailureText(MachineConnectionFailure failure) => failure switch
    {
        MachineConnectionFailure.SameMachine => "Eine Maschine kann nicht mit sich selbst verbunden werden",
        MachineConnectionFailure.DifferentComets => "Verbindungen sind nur auf demselben Kometen möglich",
        MachineConnectionFailure.PortMediumMismatch => "Falscher Anschlusstyp",
        MachineConnectionFailure.DirectionMismatch => "Erst Ausgang, dann Eingang auswählen",
        MachineConnectionFailure.PortCapacityReached => "Anschluss ist bereits belegt",
        MachineConnectionFailure.DuplicateEndpoints => "Diese Maschinen sind bereits so verbunden",
        MachineConnectionFailure.UnknownPort => "Maschine besitzt keinen passenden Anschluss",
        _ => "Verbindung kann hier nicht erstellt werden",
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
                interactionState.Definition.Id == MachineDefinitionIds.PowerPole)
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
        if (!TryResolvePowerEndpoint(target, out _))
        {
            throw new InvalidOperationException("The selected power endpoint is no longer available.");
        }

        _openPowerTarget = target;
    }

    public PowerMenuViewModel? RefreshOpenPowerViewModel()
    {
        if (_openPowerTarget is not { } target ||
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
            recipe.MachineId != _openMachine.Definition.Id || !IsUnlocked(recipe.UnlockRequirement))
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
            case MachineKind.Production when _openMachine.SelectedRecipeId is { } recipeId:
                result = MachineInventoryTransfer.LoadRecipeInputs(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    _recipeCatalog.Get(recipeId));
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
                    _openMachine.InputInventory);
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

        var saved = _stateStore.Save(new FactoryStateData(
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
            new Dictionary<string, DateTimeOffset>(_lastSimulatedUtc, StringComparer.Ordinal),
            InventoryStatePersistence.Capture(_astronautInventory),
            CaptureShipInventory(),
            _activeResearchStation?.Value,
            CapturePowerNetworkControls(),
            CaptureShipPowerState(),
            CaptureShipDockingState()));
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
        _firstBasicGenerator = new FirstBasicGeneratorState(saved.FirstBasicGeneratorBuilt);
        _restoreShipFuel(saved.ShipFuel);
        _persistedShipPower = saved.ShipPower;
        _persistedShipDocking = saved.ShipDocking;
        _awaitingShipDockingRestore = saved.ShipDocking.IsAttached;
        RestorePowerNetworkControls(saved.PowerNetworkControls);
        InventoryStatePersistence.Restore(_astronautInventory, saved.AstronautInventory);
        _pendingShipInventory = saved.ShipInventory.ToArray();
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

        _ship.RestoreAttachedPose(
            comet,
            comet.CometId,
            new Vector2(
                (float)_persistedShipDocking.RelativePositionX,
                (float)_persistedShipDocking.RelativePositionY),
            (float)_persistedShipDocking.RelativeRotationRadians,
            _persistedShipDocking.LandingLegProgress);
        _awaitingShipDockingRestore = false;
        SynchronizeShipPowerDocking(markDirty: false);
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
        var astronaut = InventoryStatePersistence.Capture(_astronautInventory);
        var ship = CaptureShipInventory();
        var changed = !_lastAstronautInventory.SequenceEqual(astronaut) ||
                      !_lastShipInventory.SequenceEqual(ship);
        _lastAstronautInventory = astronaut;
        _lastShipInventory = ship;
        return changed;
    }

    private void RememberPlayerInventories()
    {
        _lastAstronautInventory = InventoryStatePersistence.Capture(_astronautInventory);
        _lastShipInventory = CaptureShipInventory();
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
            IsUnlocked(definition.UnlockRequirement),
            definition.UnlockRequirement is { } researchId
                ? $"Forschung erforderlich: {_researchCatalog.Get(researchId).DisplayName}"
                : string.Empty);
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
            .Select(recipe => CreateRecipeViewModel(state, recipe))
            .ToArray();
        var selected = state.SelectedRecipeId is { } selectedId
            ? _recipeCatalog.Get(selectedId)
            : null;
        return CreateBasePanel(
            state,
            recipes,
            selected?.Id.Value,
            selected is null ? 0 : (float)(state.ProductionProgressSeconds / selected.DurationSeconds),
            selected is null ? 0 : (float)selected.RequiredPowerKilowatts);
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
        return CreateBasePanel(
            state,
            recipes,
            recipes.FirstOrDefault()?.RecipeId,
            0,
            0);
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

    private MachinePanelViewModel CreateResearchPanelViewModel(MachineState state)
    {
        var ownsActiveResearch = _research.ActiveResearchId is not null &&
                                 _activeResearchStation == state.InstanceId;
        var selectedResearchId = ownsActiveResearch
            ? _research.ActiveResearchId
            : (_pendingResearch.TryGetValue(state.InstanceId, out var pending) ? pending : null);
        var recipes = _researchCatalog.All
            .OrderBy(research => research.DisplayName, StringComparer.CurrentCulture)
            .Select(research =>
            {
                var completed = _research.IsCompleted(research.Id);
                var prerequisiteReady = research.Prerequisites.All(_research.IsCompleted);
                return new MachineRecipeViewModel(
                    $"research:{research.Id.Value}",
                    completed ? $"{research.DisplayName} · abgeschlossen" : research.DisplayName,
                    research.MaterialCosts.Select(cost => CreateMaterial(
                        cost.ItemId,
                        cost.Amount,
                        state.InputInventory)).ToArray(),
                    [new MachineOutputViewModel("Technologie", 1, completed ? 1 : 0, 1, new Color(0.72f, 0.48f, 1))],
                    (float)research.DurationSeconds,
                    !completed && prerequisiteReady,
                    completed ? "Bereits abgeschlossen" : "Vorherige Forschung erforderlich");
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
            availablePower);
    }

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
            IsUnlocked(recipe.UnlockRequirement),
            recipe.UnlockRequirement is { } researchId
                ? $"Forschung erforderlich: {_researchCatalog.Get(researchId).DisplayName}"
                : string.Empty);

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
            item.Color);
    }

    private MachineOutputViewModel CreateOutput(
        SpaceFactory.Core.Items.ItemId itemId,
        int produced,
        SlotInventory inventory)
    {
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.MaximumStackSize);
        var stored = inventory.GetAmount(itemId);
        var capacity = stored + ProductionInventoryRules.GetAvailableCapacity(inventory, itemId);
        return new MachineOutputViewModel(item.DisplayName, produced, stored, capacity, item.Color);
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
        return IsUnlocked(definition.UnlockRequirement) &&
               ProductionInventoryRules.ContainsAll(
                   _astronautInventory,
                   _firstBasicGenerator.GetEffectiveBuildCosts(definition));
    }

    private bool IsUnlocked(ResearchId? requirement) =>
        requirement is null || _research.IsCompleted(requirement.Value);

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
        _ => "Maschine ist blockiert.",
    };

    private static string GetFunctionSummary(MachineDefinition definition) => definition.Kind switch
    {
        MachineKind.Generator => $"Erzeugt {definition.GeneratedPowerKilowatts:0} kW im lokalen Netz",
        MachineKind.Storage => $"Lagert bis zu {ProductionConfiguration.StorageContainerSlotCount} Stapel",
        MachineKind.Research => "Schaltet Maschinen und Rezepte frei",
        _ => "Verarbeitet Materialien nach auswählbaren Rezepten",
    };

    private readonly record struct PowerEndpointCandidate(
        PowerInteractionTarget Target,
        string CometId,
        Vector2 WorldPosition,
        PowerCableVisualEndpoint Visual,
        string DisplayName);

    private static MachineSimulationFingerprint CreateSimulationFingerprint(MachineState state) => new(
        state.Status,
        state.ConstructionProgressSeconds,
        state.ProductionProgressSeconds,
        state.GeneratorFuelSecondsRemaining,
        state.InputInventory.TotalItemCount,
        state.OutputInventory.TotalItemCount);

    private ResearchSimulationFingerprint CreateResearchFingerprint() => new(
        _research.ActiveResearchId,
        _research.ProgressSeconds,
        _research.IsEnabled,
        _research.Status,
        _research.CompletedResearch.Count);

    private readonly record struct MachineSimulationFingerprint(
        MachineOperationStatus Status,
        double ConstructionProgress,
        double ProductionProgress,
        double GeneratorFuel,
        int InputItems,
        int OutputItems);

    private readonly record struct ResearchSimulationFingerprint(
        ResearchId? ActiveResearch,
        double Progress,
        bool Enabled,
        ResearchStatus Status,
        int CompletedCount);
}

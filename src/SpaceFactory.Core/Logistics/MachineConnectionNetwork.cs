using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Logistics;

public enum MachineConnectionFailure
{
    None,
    DuplicateConnectionId,
    UnknownConnectionType,
    UnknownMachine,
    MachineNotPlaced,
    SameMachine,
    DifferentComets,
    UnknownPort,
    PortMediumMismatch,
    ItemCompatibilityMismatch,
    DirectionMismatch,
    PortCapacityReached,
    DuplicateEndpoints,
    PowerNodeUnavailable,
}

public sealed record MachineConnection(
    MachineConnectionId Id,
    ConnectionTypeId TypeId,
    ConnectionKind Kind,
    MachineConnectionEndpoint Source,
    MachineConnectionEndpoint Target)
{
    public MachineConnectionSnapshot CreateSnapshot() => new(
        Id,
        Source.MachineId,
        Source.PortId,
        Target.MachineId,
        Target.PortId,
        Kind);
}

public sealed record MachineConnectionSnapshot(
    MachineConnectionId ConnectionId,
    MachineInstanceId SourceMachineId,
    MachinePortId SourcePortId,
    MachineInstanceId TargetMachineId,
    MachinePortId TargetPortId,
    ConnectionKind Kind);

public readonly record struct MachineConnectionResult(
    MachineConnectionFailure Failure,
    MachineConnection? Connection)
{
    public bool Succeeded => Failure == MachineConnectionFailure.None;

    public static MachineConnectionResult Success(MachineConnection connection) =>
        new(MachineConnectionFailure.None, connection);

    public static MachineConnectionResult Failed(MachineConnectionFailure failure) =>
        new(failure, null);
}

public sealed record PowerConnectionComponent(
    string CometId,
    IReadOnlyList<MachineInstanceId> MachineIds,
    IReadOnlyList<MachineConnectionId> ConnectionIds)
{
    public IReadOnlyList<MachineInstanceId> NodeIds { get; init; } = [];

    public IReadOnlyList<MachineConnectionEndpoint> Endpoints { get; init; } = [];
}

public sealed record PowerConnectionComponentTickResult(
    PowerConnectionComponent Component,
    PowerNetworkTickResult PowerResult);

public sealed record DirectedConnectionTickResult(
    MachineConnectionId ConnectionId,
    MachineInstanceId SourceMachineId,
    MachineInstanceId TargetMachineId,
    TransportMedium Medium,
    TransportTransferResult TransferResult);

public sealed class MachineConnectionNetwork
{
    private const double Epsilon = 0.000_001;
    private readonly Dictionary<MachineInstanceId, MachineState> _machines = [];
    private readonly Dictionary<MachineInstanceId, IPowerGridExternalNode> _powerNodes = [];
    private readonly Dictionary<MachineConnectionId, MachineConnection> _connections = [];
    private readonly Dictionary<MachineConnectionId, double> _transferCredits = [];
    private readonly MachinePortCatalog _portCatalog;
    private readonly ConnectionTypeCatalog _connectionTypes;
    private readonly ProductionItemCatalog _itemCatalog;

    public MachineConnectionNetwork(
        MachinePortCatalog? portCatalog = null,
        ConnectionTypeCatalog? connectionTypes = null,
        ProductionItemCatalog? itemCatalog = null)
    {
        _portCatalog = portCatalog ?? DefaultMachinePortCatalog.Instance;
        _connectionTypes = connectionTypes ?? DefaultConnectionTypeCatalog.Instance;
        _itemCatalog = itemCatalog ?? DefaultProductionItemCatalog.Instance;
    }

    public IReadOnlyCollection<MachineState> Machines => _machines.Values.ToArray();

    public IReadOnlyCollection<IPowerGridExternalNode> PowerNodes => _powerNodes.Values.ToArray();

    public IReadOnlyCollection<MachineConnection> Connections => _connections.Values.ToArray();

    public long TopologyVersion { get; private set; }

    public IReadOnlyList<MachineConnectionSnapshot> CreateSnapshots() => _connections.Values
        .OrderBy(connection => connection.Id.Value, StringComparer.Ordinal)
        .Select(connection => connection.CreateSnapshot())
        .ToArray();

    public bool RegisterMachine(MachineState machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (_powerNodes.ContainsKey(machine.InstanceId) || !_machines.TryAdd(machine.InstanceId, machine))
        {
            return false;
        }

        TopologyVersion++;
        return true;
    }

    public bool RegisterPowerNode(IPowerGridExternalNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.CometId) || node.Ports.Count == 0 ||
            node.Ports.Select(port => port.Id).Distinct().Count() != node.Ports.Count ||
            node.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() != node.Sources.Count ||
            node.Ports.Any(port => !IsValid(port)) ||
            node.Sources.Any(source => source.Endpoint.MachineId != node.NodeId ||
                                       node.Ports.All(port => port.Id != source.Endpoint.PortId)))
        {
            throw new ArgumentException("The external power node is invalid.", nameof(node));
        }

        if (_machines.ContainsKey(node.NodeId) || !_powerNodes.TryAdd(node.NodeId, node))
        {
            return false;
        }

        TopologyVersion++;
        return true;
    }

    public bool UnregisterMachine(MachineInstanceId machineId)
    {
        if (!_machines.Remove(machineId))
        {
            return false;
        }

        foreach (var connectionId in _connections.Values
                     .Where(connection => connection.Source.MachineId == machineId ||
                                          connection.Target.MachineId == machineId)
                     .Select(connection => connection.Id)
                     .ToArray())
        {
            RemoveConnection(connectionId);
        }

        TopologyVersion++;
        return true;
    }

    public bool UnregisterPowerNode(MachineInstanceId nodeId)
    {
        if (!_powerNodes.Remove(nodeId))
        {
            return false;
        }

        RemoveConnectionsForNode(nodeId);
        TopologyVersion++;
        return true;
    }

    public bool TryGetMachine(MachineInstanceId machineId, out MachineState? machine) =>
        _machines.TryGetValue(machineId, out machine);

    public bool TryGetPowerNode(MachineInstanceId nodeId, out IPowerGridExternalNode? node) =>
        _powerNodes.TryGetValue(nodeId, out node);

    public MachineConnectionResult TryConnect(
        MachineConnectionId connectionId,
        ConnectionTypeId connectionTypeId,
        MachineConnectionEndpoint source,
        MachineConnectionEndpoint target)
    {
        if (_connections.ContainsKey(connectionId))
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.DuplicateConnectionId);
        }

        if (!_connectionTypes.TryGet(connectionTypeId, out var connectionType) || connectionType is null)
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.UnknownConnectionType);
        }

        if (source.MachineId == target.MachineId)
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.SameMachine);
        }

        var sourceResolution = ResolveEndpoint(source, connectionType.Medium);
        if (sourceResolution.Failure != MachineConnectionFailure.None)
        {
            return MachineConnectionResult.Failed(sourceResolution.Failure);
        }

        var targetResolution = ResolveEndpoint(target, connectionType.Medium);
        if (targetResolution.Failure != MachineConnectionFailure.None)
        {
            return MachineConnectionResult.Failed(targetResolution.Failure);
        }

        if (!string.Equals(
                sourceResolution.CometId,
                targetResolution.CometId,
                StringComparison.Ordinal))
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.DifferentComets);
        }

        if (sourceResolution.Medium != connectionType.Medium || targetResolution.Medium != connectionType.Medium)
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.PortMediumMismatch);
        }

        if ((connectionType.IsDirectional && (!sourceResolution.CanSend || !targetResolution.CanReceive)) ||
            (!connectionType.IsDirectional &&
             !((sourceResolution.CanSend && targetResolution.CanReceive) ||
               (targetResolution.CanSend && sourceResolution.CanReceive))))
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.DirectionMismatch);
        }

        if (connectionType.Medium != TransportMedium.Power &&
            !HaveCompatibleItems(sourceResolution.MachinePort!, targetResolution.MachinePort!))
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.ItemCompatibilityMismatch);
        }

        if (HasEquivalentEndpoints(connectionType, source, target))
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.DuplicateEndpoints);
        }

        if (CountConnections(source) >= sourceResolution.MaximumConnections ||
            CountConnections(target) >= targetResolution.MaximumConnections)
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.PortCapacityReached);
        }

        var connection = new MachineConnection(
            connectionId,
            connectionType.Id,
            connectionType.Kind,
            source,
            target);
        _connections.Add(connection.Id, connection);
        if (connectionType.IsDirectional)
        {
            _transferCredits.Add(connection.Id, 0);
        }

        TopologyVersion++;
        return MachineConnectionResult.Success(connection);
    }

    public MachineConnectionResult TryRestore(MachineConnectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!_connectionTypes.TryGet(snapshot.Kind, out var definition) || definition is null)
        {
            return MachineConnectionResult.Failed(MachineConnectionFailure.UnknownConnectionType);
        }

        return TryConnect(
            snapshot.ConnectionId,
            definition.Id,
            new MachineConnectionEndpoint(snapshot.SourceMachineId, snapshot.SourcePortId),
            new MachineConnectionEndpoint(snapshot.TargetMachineId, snapshot.TargetPortId));
    }

    public bool RemoveConnection(MachineConnectionId connectionId)
    {
        _transferCredits.Remove(connectionId);
        if (!_connections.Remove(connectionId))
        {
            return false;
        }

        TopologyVersion++;
        return true;
    }

    public bool TryRemoveConnection(
        MachineConnectionId connectionId,
        out MachineConnection? removedConnection)
    {
        if (!_connections.TryGetValue(connectionId, out removedConnection))
        {
            return false;
        }

        if (!RemoveConnection(connectionId))
        {
            throw new InvalidOperationException("A prevalidated machine connection could not be removed.");
        }

        return true;
    }

    public IReadOnlyList<MachineConnection> GetConnectionsForMachine(MachineInstanceId machineId) =>
        _connections.Values
            .Where(connection => connection.Source.MachineId == machineId ||
                                 connection.Target.MachineId == machineId)
            .OrderBy(connection => connection.Id.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<IPowerGridExternalSource> GetExternalPowerSources(
        PowerConnectionComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var endpoints = component.Endpoints.ToHashSet();
        return _powerNodes.Values
            .SelectMany(node => node.Sources)
            .Where(source => endpoints.Contains(source.Endpoint))
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MachineConnection> GetDirectedTransferConnections() => _connections.Values
        .Where(connection => _connectionTypes.Get(connection.TypeId).IsDirectional)
        .OrderBy(connection => connection.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<PowerConnectionComponent> GetPowerComponents(string cometId)
    {
        if (string.IsNullOrWhiteSpace(cometId))
        {
            throw new ArgumentException("A power component query needs a comet ID.", nameof(cometId));
        }

        var endpointGroups = GetPowerEndpointGroups(cometId);
        var endpoints = endpointGroups
            .SelectMany(group => group)
            .Distinct()
            .OrderBy(EndpointSortKey, StringComparer.Ordinal)
            .ToArray();
        var endpointSet = endpoints.ToHashSet();
        var powerConnections = _connections.Values
            .Where(connection => connection.Kind == ConnectionKind.PowerCable &&
                                 endpointSet.Contains(connection.Source) &&
                                 endpointSet.Contains(connection.Target))
            .ToArray();
        var adjacency = endpoints.ToDictionary(
            endpoint => endpoint,
            _ => new HashSet<MachineConnectionEndpoint>());
        foreach (var group in endpointGroups)
        {
            var materialized = group.ToArray();
            for (var index = 1; index < materialized.Length; index++)
            {
                adjacency[materialized[0]].Add(materialized[index]);
                adjacency[materialized[index]].Add(materialized[0]);
            }
        }

        foreach (var connection in powerConnections)
        {
            adjacency[connection.Source].Add(connection.Target);
            adjacency[connection.Target].Add(connection.Source);
        }

        var visited = new HashSet<MachineConnectionEndpoint>();
        var components = new List<PowerConnectionComponent>();
        foreach (var root in endpoints)
        {
            if (!visited.Add(root))
            {
                continue;
            }

            var componentEndpoints = new List<MachineConnectionEndpoint>();
            var pending = new Queue<MachineConnectionEndpoint>();
            pending.Enqueue(root);
            while (pending.TryDequeue(out var current))
            {
                componentEndpoints.Add(current);
                foreach (var neighbour in adjacency[current])
                {
                    if (visited.Add(neighbour))
                    {
                        pending.Enqueue(neighbour);
                    }
                }
            }

            componentEndpoints.Sort((left, right) =>
                StringComparer.Ordinal.Compare(EndpointSortKey(left), EndpointSortKey(right)));
            var componentSet = componentEndpoints.ToHashSet();
            var componentMachines = componentEndpoints
                .Select(endpoint => endpoint.MachineId)
                .Where(_machines.ContainsKey)
                .Distinct()
                .OrderBy(machineId => machineId.Value, StringComparer.Ordinal)
                .ToArray();
            var componentNodes = componentEndpoints
                .Select(endpoint => endpoint.MachineId)
                .Distinct()
                .OrderBy(nodeId => nodeId.Value, StringComparer.Ordinal)
                .ToArray();
            var connectionIds = powerConnections
                .Where(connection => componentSet.Contains(connection.Source) &&
                                     componentSet.Contains(connection.Target))
                .Select(connection => connection.Id)
                .OrderBy(connectionId => connectionId.Value, StringComparer.Ordinal)
                .ToArray();
            components.Add(new PowerConnectionComponent(cometId, componentMachines, connectionIds)
            {
                NodeIds = componentNodes,
                Endpoints = componentEndpoints,
            });
        }

        return components;
    }

    public IReadOnlyList<PowerConnectionComponentTickResult> TickPower(
        string cometId,
        double deltaSeconds,
        RecipeCatalog recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var results = new List<PowerConnectionComponentTickResult>();
        foreach (var component in GetPowerComponents(cometId))
        {
            var network = new LocalCometPowerNetwork(cometId);
            foreach (var machineId in component.MachineIds)
            {
                network.AddMachine(_machines[machineId]);
            }

            results.Add(new PowerConnectionComponentTickResult(
                component,
                network.Tick(
                    deltaSeconds,
                    recipes,
                    externalRequestedPowerKilowatts: 0,
                    externalSources: GetExternalPowerSources(component),
                    allowPowerDelivery: true)));
        }

        return results;
    }

    public IReadOnlyList<DirectedConnectionTickResult> TickDirectedTransfers(double deltaSeconds) =>
        TickDirectedTransfers(deltaSeconds, _ => true);

    public IReadOnlyList<DirectedConnectionTickResult> TickDirectedTransfers(
        string cometId,
        double deltaSeconds)
    {
        if (string.IsNullOrWhiteSpace(cometId))
        {
            throw new ArgumentException("A directed transfer tick needs a comet ID.", nameof(cometId));
        }

        return TickDirectedTransfers(
            deltaSeconds,
            connection => string.Equals(
                _machines[connection.Source.MachineId].Placement?.CometId,
                cometId,
                StringComparison.Ordinal));
    }

    private IReadOnlyList<DirectedConnectionTickResult> TickDirectedTransfers(
        double deltaSeconds,
        Func<MachineConnection, bool> predicate)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var results = new List<DirectedConnectionTickResult>();
        foreach (var connection in GetDirectedTransferConnections().Where(predicate))
        {
            var definition = _connectionTypes.Get(connection.TypeId);
            var maximumCredit = definition.TransferUnitsPerSecond *
                                LogisticsConfiguration.MaximumStoredTransferCreditSeconds;
            var credit = Math.Min(
                maximumCredit,
                _transferCredits[connection.Id] + definition.TransferUnitsPerSecond * deltaSeconds);
            _transferCredits[connection.Id] = credit;
            var transferableAmount = (int)Math.Floor(credit + Epsilon);
            var transferResult = transferableAmount > 0
                ? Transfer(connection, definition, transferableAmount)
                : TransportTransferResult.Failed(TransportTransferFailure.InsufficientTransferCredit);
            if (transferResult.Succeeded)
            {
                _transferCredits[connection.Id] = Math.Max(0, credit - transferResult.TransferredAmount);
            }

            results.Add(new DirectedConnectionTickResult(
                connection.Id,
                connection.Source.MachineId,
                connection.Target.MachineId,
                definition.Medium,
                transferResult));
        }

        return results;
    }

    private TransportTransferResult Transfer(
        MachineConnection connection,
        ConnectionTypeDefinition definition,
        int maximumAmount)
    {
        var sourceMachine = _machines[connection.Source.MachineId];
        var targetMachine = _machines[connection.Target.MachineId];
        var sourcePort = _portCatalog.Get(sourceMachine.Definition.Id, connection.Source.PortId);
        var targetPort = _portCatalog.Get(targetMachine.Definition.Id, connection.Target.PortId);
        return MachineTransportTransfer.TransferFirstCompatible(
            ResolveInventory(sourceMachine, sourcePort),
            ResolveInventory(targetMachine, targetPort),
            definition.Medium,
            maximumAmount,
            _itemCatalog,
            GetCompatibleItems(sourcePort, targetPort));
    }

    private ResolvedEndpoint ResolveEndpoint(
        MachineConnectionEndpoint endpoint,
        TransportMedium requestedMedium)
    {
        if (_machines.TryGetValue(endpoint.MachineId, out var machine))
        {
            if (machine.Placement is null)
            {
                return ResolvedEndpoint.Failed(MachineConnectionFailure.MachineNotPlaced);
            }

            if (!_portCatalog.TryGet(machine.Definition.Id, endpoint.PortId, out var port) || port is null)
            {
                return ResolvedEndpoint.Failed(MachineConnectionFailure.UnknownPort);
            }

            return ResolvedEndpoint.Success(
                machine.Placement.CometId,
                port.Medium,
                port.CanSend,
                port.CanReceive,
                port.MaximumConnections,
                port);
        }

        if (!_powerNodes.TryGetValue(endpoint.MachineId, out var node))
        {
            return ResolvedEndpoint.Failed(MachineConnectionFailure.UnknownMachine);
        }

        if (!node.IsAvailable)
        {
            return ResolvedEndpoint.Failed(MachineConnectionFailure.PowerNodeUnavailable);
        }

        var externalPort = node.Ports.SingleOrDefault(port => port.Id == endpoint.PortId);
        if (externalPort is null)
        {
            return ResolvedEndpoint.Failed(MachineConnectionFailure.UnknownPort);
        }

        if (requestedMedium != TransportMedium.Power)
        {
            return ResolvedEndpoint.Failed(MachineConnectionFailure.PortMediumMismatch);
        }

        return ResolvedEndpoint.Success(
            node.CometId,
            TransportMedium.Power,
            canSend: true,
            canReceive: true,
            maximumConnections: externalPort.MaximumConnections,
            machinePort: null);
    }

    private IReadOnlyList<IReadOnlyList<MachineConnectionEndpoint>> GetPowerEndpointGroups(string cometId)
    {
        var groups = new List<IReadOnlyList<MachineConnectionEndpoint>>();
        foreach (var machine in _machines.Values
                     .Where(machine => string.Equals(machine.Placement?.CometId, cometId, StringComparison.Ordinal))
                     .OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal))
        {
            var endpoints = _portCatalog.ForMachine(machine.Definition.Id)
                .Where(port => port.Medium == TransportMedium.Power)
                .Select(port => new MachineConnectionEndpoint(machine.InstanceId, port.Id))
                .OrderBy(EndpointSortKey, StringComparer.Ordinal)
                .ToArray();
            if (endpoints.Length > 0)
            {
                groups.Add(endpoints);
            }
        }

        foreach (var node in _powerNodes.Values
                     .Where(node => node.IsAvailable &&
                                    string.Equals(node.CometId, cometId, StringComparison.Ordinal))
                     .OrderBy(node => node.NodeId.Value, StringComparer.Ordinal))
        {
            groups.AddRange(node.Ports
                .GroupBy(port => port.ElectricalBus)
                .OrderBy(group => group.Key)
                .Select(group => (IReadOnlyList<MachineConnectionEndpoint>)group
                    .Select(port => new MachineConnectionEndpoint(node.NodeId, port.Id))
                    .OrderBy(EndpointSortKey, StringComparer.Ordinal)
                    .ToArray()));
        }

        return groups;
    }

    private void RemoveConnectionsForNode(MachineInstanceId nodeId)
    {
        foreach (var connectionId in _connections.Values
                     .Where(connection => connection.Source.MachineId == nodeId ||
                                          connection.Target.MachineId == nodeId)
                     .Select(connection => connection.Id)
                     .ToArray())
        {
            RemoveConnection(connectionId);
        }
    }

    private static bool IsValid(PowerGridExternalPort port)
    {
        try
        {
            port.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string EndpointSortKey(MachineConnectionEndpoint endpoint) =>
        $"{endpoint.MachineId.Value}/{endpoint.PortId.Value}";

    private bool HasEquivalentEndpoints(
        ConnectionTypeDefinition type,
        MachineConnectionEndpoint source,
        MachineConnectionEndpoint target) =>
        _connections.Values.Any(existing =>
        {
            if (existing.TypeId != type.Id)
            {
                return false;
            }

            var sameDirection = existing.Source == source && existing.Target == target;
            var reverseDirection = !type.IsDirectional && existing.Source == target && existing.Target == source;
            return sameDirection || reverseDirection;
        });

    private int CountConnections(MachineConnectionEndpoint endpoint) => _connections.Values.Count(connection =>
        connection.Source == endpoint || connection.Target == endpoint);

    private static bool HaveCompatibleItems(MachinePortDefinition source, MachinePortDefinition target) =>
        source.AllowedItemIds is null ||
        target.AllowedItemIds is null ||
        source.AllowedItemIds.Intersect(target.AllowedItemIds).Any();

    private static IReadOnlyCollection<ItemId>? GetCompatibleItems(
        MachinePortDefinition source,
        MachinePortDefinition target)
    {
        if (source.AllowedItemIds is null)
        {
            return target.AllowedItemIds;
        }

        if (target.AllowedItemIds is null)
        {
            return source.AllowedItemIds;
        }

        return source.AllowedItemIds.Intersect(target.AllowedItemIds).ToArray();
    }

    private static SlotInventory ResolveInventory(MachineState machine, MachinePortDefinition port) =>
        port.InventorySide switch
        {
            MachinePortInventorySide.Input => machine.InputInventory,
            MachinePortInventorySide.Output => machine.OutputInventory,
            _ => throw new InvalidOperationException($"Power port '{port.Id}' has no transport inventory."),
        };

    private sealed record ResolvedEndpoint(
        MachineConnectionFailure Failure,
        string CometId,
        TransportMedium Medium,
        bool CanSend,
        bool CanReceive,
        int MaximumConnections,
        MachinePortDefinition? MachinePort)
    {
        public static ResolvedEndpoint Failed(MachineConnectionFailure failure) =>
            new(failure, string.Empty, default, false, false, 0, null);

        public static ResolvedEndpoint Success(
            string cometId,
            TransportMedium medium,
            bool canSend,
            bool canReceive,
            int maximumConnections,
            MachinePortDefinition? machinePort) =>
            new(
                MachineConnectionFailure.None,
                cometId,
                medium,
                canSend,
                canReceive,
                maximumConnections,
                machinePort);
    }
}

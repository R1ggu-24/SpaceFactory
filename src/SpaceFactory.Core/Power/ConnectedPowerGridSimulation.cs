using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Power;

public sealed record ConnectedPowerGridTickResult(
    PowerNetworkId NetworkId,
    PowerConnectionComponent Topology,
    PowerNetworkControlState Control,
    PowerNetworkTickResult Dispatch,
    PowerGridMetrics Metrics);

public sealed class ConnectedPowerGridSimulation
{
    private const double Epsilon = 0.000_001;
    private readonly MachineConnectionNetwork _connections;
    private readonly RecipeCatalog _recipes;
    private readonly Dictionary<string, RuntimeComponent[]> _componentsByComet = new(StringComparer.Ordinal);
    private readonly Dictionary<PowerNetworkId, PowerNetworkControlState> _states = [];
    private long _cachedTopologyVersion = -1;

    public ConnectedPowerGridSimulation(
        MachineConnectionNetwork connections,
        RecipeCatalog? recipes = null)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _recipes = recipes ?? DefaultRecipeCatalog.Instance;
    }

    public IReadOnlyCollection<PowerNetworkControlState> NetworkStates => _states.Values.ToArray();

    public IReadOnlyList<ConnectedPowerGridTickResult> Tick(
        string cometId,
        double deltaSeconds,
        Func<PowerConnectionComponent, double>? externalDemandResolver = null)
    {
        if (string.IsNullOrWhiteSpace(cometId))
        {
            throw new ArgumentException("A connected power grid tick needs a comet ID.", nameof(cometId));
        }

        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var results = new List<ConnectedPowerGridTickResult>();
        foreach (var runtime in GetRuntimeComponents(cometId))
        {
            var externalDemand = externalDemandResolver?.Invoke(runtime.Topology) ?? 0;
            if (!double.IsFinite(externalDemand) || externalDemand < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(externalDemandResolver));
            }

            var state = GetOrCreateState(runtime.NetworkId);
            var forecast = runtime.Network.Forecast(
                deltaSeconds,
                _recipes,
                externalDemand,
                runtime.ExternalSources);
            var overloaded = forecast.MaximumCapacityKilowatts > Epsilon &&
                             forecast.RequestedPowerKilowatts >
                             forecast.MaximumCapacityKilowatts + Epsilon;
            state.AdvanceProtection(deltaSeconds, overloaded);
            var dispatch = runtime.Network.Tick(
                deltaSeconds,
                _recipes,
                externalDemand,
                runtime.ExternalSources,
                state.CanDeliverPower);
            var actualConsumption = dispatch.ConsumedEnergyKilowattSeconds / deltaSeconds;
            var fuelPerMinute = dispatch.ConsumedFuel / deltaSeconds * 60;
            var remainingFuel = runtime.ExternalSources
                .GroupBy(source => source.ResourcePoolId, StringComparer.Ordinal)
                .Sum(group => group.First().RemainingResourceAmount);
            var status = ResolveStatus(state, overloaded, forecast, actualConsumption);
            var metrics = new PowerGridMetrics(
                forecast.MaximumCapacityKilowatts,
                actualConsumption,
                forecast.RequestedPowerKilowatts,
                actualConsumption,
                forecast.MaximumCapacityKilowatts - forecast.RequestedPowerKilowatts,
                forecast.ActiveSourceCount,
                status,
                fuelPerMinute,
                fuelPerMinute > Epsilon ? remainingFuel / fuelPerMinute : null);
            state.History.Advance(deltaSeconds, metrics);
            results.Add(new ConnectedPowerGridTickResult(
                runtime.NetworkId,
                runtime.Topology,
                state,
                dispatch,
                metrics));
        }

        return results;
    }

    public bool SetNetworkEnabled(PowerNetworkId networkId, bool enabled)
    {
        if (!_states.TryGetValue(networkId, out var state))
        {
            return false;
        }

        state.SetEnabled(enabled);
        return true;
    }

    public bool ResetBreaker(PowerNetworkId networkId) =>
        _states.TryGetValue(networkId, out var state) && state.ResetBreaker();

    public PowerNetworkControlState? GetNetworkState(PowerNetworkId networkId) =>
        _states.GetValueOrDefault(networkId);

    public IReadOnlyList<PowerNetworkControlSnapshot> CreateControlSnapshots()
    {
        var currentNetworkIds = GetCurrentNetworkIds();
        foreach (var staleId in _states.Keys.Where(id => !currentNetworkIds.Contains(id)).ToArray())
        {
            _states.Remove(staleId);
        }

        return _states.Values
        .OrderBy(state => state.NetworkId.Value, StringComparer.Ordinal)
        .Select(state => state.CreateSnapshot())
        .ToArray();
    }

    public void RestoreControlSnapshots(IEnumerable<PowerNetworkControlSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _states.Clear();
        foreach (var snapshot in snapshots)
        {
            var state = PowerNetworkControlState.Restore(snapshot);
            if (!_states.TryAdd(state.NetworkId, state))
            {
                throw new ArgumentException(
                    $"Duplicate persisted power network '{state.NetworkId}'.",
                    nameof(snapshots));
            }
        }
    }

    public void InvalidateTopology()
    {
        _componentsByComet.Clear();
        _cachedTopologyVersion = _connections.TopologyVersion;
    }

    public PowerNetworkId GetNetworkId(PowerConnectionComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return CreateNetworkId(component);
    }

    public PowerNetworkControlState GetOrCreateNetworkState(PowerConnectionComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return GetOrCreateState(CreateNetworkId(component));
    }

    private IReadOnlyList<RuntimeComponent> GetRuntimeComponents(string cometId)
    {
        if (_cachedTopologyVersion != _connections.TopologyVersion)
        {
            _componentsByComet.Clear();
            _cachedTopologyVersion = _connections.TopologyVersion;
        }

        if (_componentsByComet.TryGetValue(cometId, out var cached))
        {
            return cached;
        }

        var components = _connections.GetPowerComponents(cometId)
            .Select(topology =>
            {
                var network = new LocalCometPowerNetwork(cometId);
                foreach (var machineId in topology.MachineIds)
                {
                    if (_connections.TryGetMachine(machineId, out var machine) && machine is not null)
                    {
                        network.AddMachine(machine);
                    }
                }

                return new RuntimeComponent(
                    CreateNetworkId(topology),
                    topology,
                    network,
                    _connections.GetExternalPowerSources(topology));
            })
            .ToArray();
        _componentsByComet.Add(cometId, components);
        return components;
    }

    private PowerNetworkControlState GetOrCreateState(PowerNetworkId networkId)
    {
        if (!_states.TryGetValue(networkId, out var state))
        {
            state = new PowerNetworkControlState(networkId);
            _states.Add(networkId, state);
        }

        return state;
    }

    private HashSet<PowerNetworkId> GetCurrentNetworkIds()
    {
        var cometIds = _connections.Machines
            .Select(machine => machine.Placement?.CometId)
            .Concat(_connections.PowerNodes.Select(node => node.CometId))
            .Where(cometId => !string.IsNullOrWhiteSpace(cometId))
            .Select(cometId => cometId!)
            .Distinct(StringComparer.Ordinal);
        return cometIds
            .SelectMany(_connections.GetPowerComponents)
            .Select(CreateNetworkId)
            .ToHashSet();
    }

    private static PowerNetworkId CreateNetworkId(PowerConnectionComponent component)
    {
        const ulong offsetBasis = 14_695_981_039_346_656_037;
        const ulong prime = 1_099_511_628_211;
        var hash = offsetBasis;
        var signature = component.CometId + "|" + string.Join(
            '|',
            component.Endpoints.Select(endpoint =>
                $"{endpoint.MachineId.Value}/{endpoint.PortId.Value}"));
        foreach (var character in signature)
        {
            hash ^= character;
            hash *= prime;
        }

        return new PowerNetworkId($"power-{hash:x16}");
    }

    private static PowerGridStatus ResolveStatus(
        PowerNetworkControlState state,
        bool overloaded,
        PowerNetworkForecast forecast,
        double actualConsumptionKilowatts)
    {
        if (state.IsBreakerTripped)
        {
            return PowerGridStatus.BreakerTripped;
        }

        if (!state.IsEnabled)
        {
            return PowerGridStatus.Disabled;
        }

        if (overloaded)
        {
            return PowerGridStatus.Overloaded;
        }

        if (forecast.RequestedPowerKilowatts > Epsilon &&
            forecast.MaximumCapacityKilowatts <= Epsilon)
        {
            return PowerGridStatus.NoCapacity;
        }

        return actualConsumptionKilowatts > Epsilon
            ? PowerGridStatus.Online
            : PowerGridStatus.Idle;
    }

    private sealed record RuntimeComponent(
        PowerNetworkId NetworkId,
        PowerConnectionComponent Topology,
        LocalCometPowerNetwork Network,
        IReadOnlyList<IPowerGridExternalSource> ExternalSources);
}

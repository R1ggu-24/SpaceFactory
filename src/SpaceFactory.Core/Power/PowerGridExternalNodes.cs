using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Core.Power;

public sealed record PowerGridExternalPort(
    MachinePortId Id,
    int ElectricalBus,
    int MaximumConnections = PowerGridConfiguration.MaximumCablesPerPort)
{
    public void Validate()
    {
        if (ElectricalBus < 0 || MaximumConnections <= 0)
        {
            throw new ArgumentException($"Power port '{Id}' is invalid.");
        }
    }
}

public readonly record struct ExternalPowerDelivery(
    double RequestedEnergyKilowattSeconds,
    double DeliveredEnergyKilowattSeconds,
    double ConsumedFuel)
{
    public bool FullyDelivered =>
        Math.Abs(RequestedEnergyKilowattSeconds - DeliveredEnergyKilowattSeconds) <= 0.000_001;
}

public interface IPowerGridExternalSource
{
    string SourceId { get; }

    MachineConnectionEndpoint Endpoint { get; }

    bool IsEnabled { get; }

    double RatedCapacityKilowatts { get; }

    string ResourcePoolId { get; }

    double RemainingResourceAmount { get; }

    double GetAvailableResourceEnergyKilowattSeconds();

    ExternalPowerDelivery DeliverEnergy(double requestedEnergyKilowattSeconds);
}

public interface IPowerGridExternalNode
{
    MachineInstanceId NodeId { get; }

    string CometId { get; }

    bool IsAvailable { get; }

    IReadOnlyList<PowerGridExternalPort> Ports { get; }

    IReadOnlyList<IPowerGridExternalSource> Sources { get; }
}

public sealed record ShipPowerNodeSnapshot(
    MachineInstanceId NodeId,
    string CometId,
    bool ConnectorAEnabled,
    bool ConnectorBEnabled);

public sealed class ShipPowerNode : IPowerGridExternalNode
{
    private readonly IReadOnlyList<PowerGridExternalPort> _ports;
    private readonly IReadOnlyList<IPowerGridExternalSource> _sources;

    public ShipPowerNode(
        MachineInstanceId nodeId,
        string cometId,
        ShipFuelTank fuelTank,
        bool connectorAEnabled = true,
        bool connectorBEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(fuelTank);
        if (string.IsNullOrWhiteSpace(cometId))
        {
            throw new ArgumentException("A docked ship power node needs a comet ID.", nameof(cometId));
        }

        NodeId = nodeId;
        CometId = cometId;
        FuelTank = fuelTank;
        _ports =
        [
            new PowerGridExternalPort(MachinePortIds.ShipPowerA, ElectricalBus: 0),
            new PowerGridExternalPort(MachinePortIds.ShipPowerB, ElectricalBus: 1),
        ];
        ConnectorA = new ShipPowerConnector(this, MachinePortIds.ShipPowerA, "A", connectorAEnabled);
        ConnectorB = new ShipPowerConnector(this, MachinePortIds.ShipPowerB, "B", connectorBEnabled);
        _sources = [ConnectorA, ConnectorB];
    }

    public MachineInstanceId NodeId { get; }

    public string CometId { get; }

    public bool IsAvailable => true;

    public ShipFuelTank FuelTank { get; }

    public ShipPowerConnector ConnectorA { get; }

    public ShipPowerConnector ConnectorB { get; }

    public IReadOnlyList<PowerGridExternalPort> Ports => _ports;

    public IReadOnlyList<IPowerGridExternalSource> Sources => _sources;

    public bool SetConnectorEnabled(MachinePortId portId, bool enabled)
    {
        var connector = GetConnector(portId);
        if (connector is null)
        {
            return false;
        }

        connector.SetEnabled(enabled);
        return true;
    }

    public ShipPowerConnector? GetConnector(MachinePortId portId) => portId switch
    {
        var id when id == MachinePortIds.ShipPowerA => ConnectorA,
        var id when id == MachinePortIds.ShipPowerB => ConnectorB,
        _ => null,
    };

    public ShipPowerNodeSnapshot CreateSnapshot() => new(
        NodeId,
        CometId,
        ConnectorA.IsEnabled,
        ConnectorB.IsEnabled);

    public static ShipPowerNode Restore(ShipPowerNodeSnapshot snapshot, ShipFuelTank fuelTank)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new ShipPowerNode(
            snapshot.NodeId,
            snapshot.CometId,
            fuelTank,
            snapshot.ConnectorAEnabled,
            snapshot.ConnectorBEnabled);
    }

    public sealed class ShipPowerConnector : IPowerGridExternalSource
    {
        private readonly ShipPowerNode _owner;

        internal ShipPowerConnector(
            ShipPowerNode owner,
            MachinePortId portId,
            string displaySuffix,
            bool enabled)
        {
            _owner = owner;
            Endpoint = new MachineConnectionEndpoint(owner.NodeId, portId);
            SourceId = $"{owner.NodeId.Value}:ship-power-{displaySuffix.ToLowerInvariant()}";
            IsEnabled = enabled;
        }

        public string SourceId { get; }

        public MachineConnectionEndpoint Endpoint { get; }

        public bool IsEnabled { get; private set; }

        public double RatedCapacityKilowatts => PowerGridConfiguration.ShipConnectorPowerKilowatts;

        public string ResourcePoolId => $"{_owner.NodeId.Value}:ship-fuel";

        public double RemainingResourceAmount => _owner.FuelTank.CurrentFuel;

        public double GetAvailableResourceEnergyKilowattSeconds() =>
            _owner.FuelTank.CurrentFuel / PowerGridConfiguration.ShipFuelPerKilowattSecond;

        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        public ExternalPowerDelivery DeliverEnergy(double requestedEnergyKilowattSeconds)
        {
            if (!double.IsFinite(requestedEnergyKilowattSeconds) || requestedEnergyKilowattSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedEnergyKilowattSeconds));
            }

            if (!IsEnabled || requestedEnergyKilowattSeconds <= 0)
            {
                return new ExternalPowerDelivery(requestedEnergyKilowattSeconds, 0, 0);
            }

            var result = _owner.FuelTank.ConsumePowerGenerationFuel(requestedEnergyKilowattSeconds);
            return new ExternalPowerDelivery(
                result.RequestedEnergyKilowattSeconds,
                result.DeliveredEnergyKilowattSeconds,
                result.ConsumedFuel);
        }
    }
}

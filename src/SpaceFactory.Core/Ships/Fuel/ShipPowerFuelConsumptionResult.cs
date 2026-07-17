namespace SpaceFactory.Core.Ships.Fuel;

public readonly record struct ShipPowerFuelConsumptionResult(
    double RequestedEnergyKilowattSeconds,
    double DeliveredEnergyKilowattSeconds,
    double ConsumedFuel)
{
    public bool FullyDelivered =>
        Math.Abs(RequestedEnergyKilowattSeconds - DeliveredEnergyKilowattSeconds) <= 0.000_001;
}

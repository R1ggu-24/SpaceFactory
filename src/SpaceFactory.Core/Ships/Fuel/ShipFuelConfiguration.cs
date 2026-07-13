using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Ships.Fuel;

public static class ShipFuelConfiguration
{
    public const double TankCapacity = 1_800;
    public const double ConsumptionPerBoostSecond = 1;
    public const double MaximumBoostDurationSeconds = TankCapacity / ConsumptionPerBoostSecond;
    public const double MinimumFuelToActivateBoost = 0.000_001;
    public const double FuelPerContainer = DefaultProductionItemCatalog.ContainerCapacity;
}

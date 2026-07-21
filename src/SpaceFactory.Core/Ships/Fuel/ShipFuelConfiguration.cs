using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Ships.Fuel;

public static class ShipFuelConfiguration
{
    public const double TankCapacity = 1_800;
    public const double ConsumptionPerBoostSecond = 1;
    public const double MaximumBoostDurationSeconds = TankCapacity / ConsumptionPerBoostSecond;
    public const double MinimumFuelToActivateBoost = 0.000_001;
    public const double FuelPerContainer = DefaultProductionItemCatalog.ContainerCapacity;

    public const ShipFuelType NewGameFuelType = ShipFuelType.Standard;
    public const double StandardBoostSpeedFactor = 1.0;
    public const double HighPerformanceBoostSpeedFactor = 1.7;

    // Temporary, centrally removable acceptance-test cargo. FactoryStateData
    // consumes this definition when composing a new ship inventory.
    public const bool IncludeHighPerformanceTestTankInNewGame = true;
    public const int HighPerformanceTestContainerCount =
        (int)(TankCapacity / FuelPerContainer);

    public static ItemAmount HighPerformanceTestCargo { get; } = new(
        ProductionItemIds.HighPerformanceFuelContainer,
        HighPerformanceTestContainerCount);

    public static string GetDisplayName(ShipFuelType fuelType) => fuelType switch
    {
        ShipFuelType.Standard => "Standardtreibstoff",
        ShipFuelType.HighPerformance => "Hochleistungstreibstoff",
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    public static double GetBoostSpeedFactor(ShipFuelType fuelType) => fuelType switch
    {
        ShipFuelType.Standard => StandardBoostSpeedFactor,
        ShipFuelType.HighPerformance => HighPerformanceBoostSpeedFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    public static ItemId GetLooseFuelItemId(ShipFuelType fuelType) => fuelType switch
    {
        ShipFuelType.Standard => ProductionItemIds.StandardFuel,
        ShipFuelType.HighPerformance => ProductionItemIds.HighPerformanceFuel,
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    public static ItemId GetFilledContainerItemId(ShipFuelType fuelType) => fuelType switch
    {
        ShipFuelType.Standard => ProductionItemIds.StandardFuelContainer,
        ShipFuelType.HighPerformance => ProductionItemIds.HighPerformanceFuelContainer,
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    public static bool TryGetFuelTypeForContainer(ItemId itemId, out ShipFuelType fuelType)
    {
        if (itemId == ProductionItemIds.StandardFuelContainer)
        {
            fuelType = ShipFuelType.Standard;
            return true;
        }

        if (itemId == ProductionItemIds.HighPerformanceFuelContainer)
        {
            fuelType = ShipFuelType.HighPerformance;
            return true;
        }

        fuelType = default;
        return false;
    }
}

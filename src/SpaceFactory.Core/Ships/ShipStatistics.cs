namespace SpaceFactory.Core.Ships;

public sealed record ShipStatistics(
    int StorageCapacity,
    double MovementSpeed,
    double ScannerRange,
    double EnergyCapacity,
    int UpgradeSlots);

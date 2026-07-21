using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Core.Ships;

public static class ShipFlightConfiguration
{
    public const double NormalFlightSpeed = 975.0;
    public const double StandardBoostFlightSpeed = 2_600.0;
    public const double HighPerformanceBoostFlightSpeed =
        StandardBoostFlightSpeed * ShipFuelConfiguration.HighPerformanceBoostSpeedFactor;

    // Compatibility aliases: existing callers continue to mean standard boost.
    public const double BoostFlightSpeed = StandardBoostFlightSpeed;
    public const double BoostSpeedMultiplier = StandardBoostFlightSpeed / NormalFlightSpeed;
    public const double HighPerformanceBoostSpeedMultiplier =
        HighPerformanceBoostFlightSpeed / NormalFlightSpeed;
    public const double NormalAcceleration = 1_350.0 * 1.5;
    public const double NormalDeceleration = 1_750.0 * 1.5;
    public const double BoostAcceleration = 3_250.0 * 2.0;
    public const double BoostReleaseDeceleration = 4_200.0 * 2.0;

    public static double GetBoostFlightSpeed(ShipFuelType fuelType) => fuelType switch
    {
        ShipFuelType.Standard => StandardBoostFlightSpeed,
        ShipFuelType.HighPerformance => HighPerformanceBoostFlightSpeed,
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    public static double GetBoostSpeedMultiplier(ShipFuelType fuelType) =>
        GetBoostFlightSpeed(fuelType) / NormalFlightSpeed;
}

namespace SpaceFactory.Core.Ships;

public static class ShipFlightConfiguration
{
    public const double NormalFlightSpeed = 975.0;
    public const double BoostFlightSpeed = 2_600.0;
    public const double BoostSpeedMultiplier = BoostFlightSpeed / NormalFlightSpeed;
    public const double NormalAcceleration = 1_350.0 * 1.5;
    public const double NormalDeceleration = 1_750.0 * 1.5;
    public const double BoostAcceleration = 3_250.0 * 2.0;
    public const double BoostReleaseDeceleration = 4_200.0 * 2.0;
}

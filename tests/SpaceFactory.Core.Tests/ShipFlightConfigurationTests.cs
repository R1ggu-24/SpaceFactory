using SpaceFactory.Core.Ships;

namespace SpaceFactory.Core.Tests;

public sealed class ShipFlightConfigurationTests
{
    [Fact]
    public void Defaults_ApplyRequestedSpeedIncreasesExactly()
    {
        Assert.Equal(650 * 1.5, ShipFlightConfiguration.NormalFlightSpeed);
        Assert.Equal(1_300 * 2.0, ShipFlightConfiguration.BoostFlightSpeed);
        Assert.Equal(
            ShipFlightConfiguration.StandardBoostFlightSpeed * 1.7,
            ShipFlightConfiguration.HighPerformanceBoostFlightSpeed,
            precision: 12);
        Assert.Equal(
            ShipFlightConfiguration.BoostFlightSpeed / ShipFlightConfiguration.NormalFlightSpeed,
            ShipFlightConfiguration.BoostSpeedMultiplier,
            precision: 12);
        Assert.Equal(
            ShipFlightConfiguration.HighPerformanceBoostFlightSpeed /
            ShipFlightConfiguration.NormalFlightSpeed,
            ShipFlightConfiguration.HighPerformanceBoostSpeedMultiplier,
            precision: 12);
        Assert.Equal(1_350 * 1.5, ShipFlightConfiguration.NormalAcceleration);
        Assert.Equal(1_750 * 1.5, ShipFlightConfiguration.NormalDeceleration);
        Assert.Equal(3_250 * 2.0, ShipFlightConfiguration.BoostAcceleration);
        Assert.Equal(4_200 * 2.0, ShipFlightConfiguration.BoostReleaseDeceleration);
    }
}

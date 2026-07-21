using SpaceFactory.Core.Hazards;

namespace SpaceFactory.Core.Tests;

public sealed class RadiationExposureTests
{
    [Fact]
    public void NearbyUnshieldedSource_AccumulatesDoseAndCrossesWarningLevel()
    {
        var exposure = new RadiationExposureState(suitProtection: 0);
        var source = new RadiationSource("waste", 24, 0, StrengthPerSecond: 1);

        var rate = exposure.Advance(2, 0, 0, [source]);

        Assert.True(rate > 0);
        Assert.True(exposure.AccumulatedDose > RadiationConfiguration.DoseWarningThreshold);
        Assert.Equal(RadiationExposureLevel.Elevated, exposure.Level);
    }

    [Fact]
    public void ShieldingAndDistance_ReduceDoseRate()
    {
        var near = new RadiationExposureState(suitProtection: 0);
        var farShielded = new RadiationExposureState(suitProtection: 0);

        var nearRate = near.Advance(1, 0, 0, [new RadiationSource("near", 24, 0, 1)]);
        var farRate = farShielded.Advance(
            1,
            0,
            0,
            [new RadiationSource("far", 240, 0, 1, Shielding: 0.75)]);

        Assert.True(nearRate > farRate);
        Assert.True(farRate > 0);
    }

    [Fact]
    public void EmptyEnvironment_RecoversDoseAndSnapshotRoundTrips()
    {
        var exposure = new RadiationExposureState(suitProtection: 0.6);
        exposure.Advance(1, 0, 0, [new RadiationSource("source", 24, 0, 1)]);
        var beforeRecovery = exposure.AccumulatedDose;

        exposure.Advance(20, 0, 0, []);
        var restored = RadiationExposureState.Restore(exposure.CreateSnapshot());

        Assert.True(exposure.AccumulatedDose < beforeRecovery);
        Assert.Equal(exposure.AccumulatedDose, restored.AccumulatedDose);
        Assert.Equal(0.6, restored.SuitProtection);
    }

    [Fact]
    public void AccumulatedDose_DamagesSuitAndReducesMovementAndMiningEfficiency()
    {
        var exposure = new RadiationExposureState(suitProtection: 0);
        exposure.Advance(10, 0, 0, [new RadiationSource("critical", 24, 0, 2)]);

        Assert.Equal(0, exposure.Integrity);
        Assert.Equal(RadiationConfiguration.MinimumMovementMultiplier, exposure.MovementMultiplier);
        Assert.Equal(RadiationConfiguration.MinimumMiningEfficiencyMultiplier, exposure.MiningEfficiencyMultiplier);
    }
}

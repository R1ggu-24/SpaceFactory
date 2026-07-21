using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class ResourceExtractionTests
{
    [Fact]
    public void PurityMultipliers_AffectRateCycleTimeYieldAndEnergyEfficiency()
    {
        Assert.True(MiningConfiguration.GetExtractionRateMultiplier(ResourcePurity.Impure) < 1);
        Assert.Equal(1, MiningConfiguration.GetExtractionRateMultiplier(ResourcePurity.Normal));
        Assert.True(MiningConfiguration.GetExtractionRateMultiplier(ResourcePurity.Pure) > 1);

        Assert.True(MiningConfiguration.GetManualCycleTimeMultiplier(ResourcePurity.Impure) > 1);
        Assert.Equal(1, MiningConfiguration.GetManualCycleTimeMultiplier(ResourcePurity.Normal));
        Assert.True(MiningConfiguration.GetManualCycleTimeMultiplier(ResourcePurity.Pure) < 1);

        Assert.True(MiningConfiguration.GetEnergyEfficiencyMultiplier(ResourcePurity.Impure) < 1);
        Assert.Equal(1, MiningConfiguration.GetEnergyEfficiencyMultiplier(ResourcePurity.Normal));
        Assert.True(MiningConfiguration.GetEnergyEfficiencyMultiplier(ResourcePurity.Pure) > 1);

        Assert.Equal(1, MiningConfiguration.GetManualYield(2, ResourcePurity.Impure));
        Assert.Equal(2, MiningConfiguration.GetManualYield(2, ResourcePurity.Normal));
        Assert.Equal(3, MiningConfiguration.GetManualYield(2, ResourcePurity.Pure));
    }

    [Fact]
    public void ManualMining_IsGloballySlowerThanLegacyBaseTime()
    {
        var duration = MiningConfiguration.GetManualCycleDurationSeconds(2, ResourcePurity.Normal);

        Assert.Equal(2 * MiningConfiguration.ManualSpeedMultiplier, duration, 6);
        Assert.True(duration > 2);
    }

    [Fact]
    public void InfiniteSource_RepeatedHarvestsNeverReduceRemainingMarker()
    {
        var source = CreateDeposit(isInfinite: true, ResourcePurity.Pure);
        var remaining = source.OriginalAmount;

        for (var cycle = 0; cycle < 100; cycle++)
        {
            Assert.Equal(3, ResourceExtractionRules.GetManualYield(source, remaining));
            remaining = ResourceExtractionRules.GetRemainingAmountAfterManualHarvest(source, remaining);
        }

        Assert.Equal(source.OriginalAmount, remaining);
    }

    [Fact]
    public void LegacyFiniteDeposit_StillYieldsRemainingAmountAndBecomesExhausted()
    {
        var legacy = CreateDeposit(isInfinite: false, ResourcePurity.Normal) with
        {
            OriginalAmount = 17,
        };

        Assert.Equal(17, ResourceExtractionRules.GetManualYield(legacy, 17));
        Assert.Equal(0, ResourceExtractionRules.GetRemainingAmountAfterManualHarvest(legacy, 17));
        Assert.Equal(legacy.MiningTimeSeconds, legacy.EffectiveManualMiningTimeSeconds);
    }

    [Fact]
    public void FiniteOreStone_YieldsFiveToTenUnitsAndLosesExactlyOneHitPerHarvest()
    {
        var stone = CreateDeposit(isInfinite: false, ResourcePurity.Normal) with
        {
            Id = "comet:4:ore-stone:v1:0",
            OriginalAmount = 7,
            VisualSeed = 741_029_384,
            ManualYieldPerCycle = 0,
            Kind = ResourceDepositKind.FiniteOreStone,
        };
        var firstRun = ExtractStone(stone);
        var secondRun = ExtractStone(stone);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(stone.OriginalAmount, firstRun.Count);
        Assert.All(firstRun, amount => Assert.InRange(
            amount,
            MiningConfiguration.MinimumFiniteOreStoneYieldPerHit,
            MiningConfiguration.MaximumFiniteOreStoneYieldPerHit));
        Assert.Equal(0, ResourceExtractionRules.GetManualYield(stone, 0));
    }

    private static IReadOnlyList<int> ExtractStone(ResourceDepositDefinition stone)
    {
        var remainingHits = stone.OriginalAmount;
        var yields = new List<int>();
        while (remainingHits > 0)
        {
            yields.Add(ResourceExtractionRules.GetManualYield(stone, remainingHits));
            var updated = ResourceExtractionRules.GetRemainingAmountAfterManualHarvest(stone, remainingHits);
            Assert.Equal(remainingHits - 1, updated);
            remainingHits = updated;
        }

        return yields;
    }

    private static ResourceDepositDefinition CreateDeposit(bool isInfinite, ResourcePurity purity) => new(
        "source:test",
        "comet:test",
        new ItemId("iron_ore"),
        new WorldPosition(0.4, 0),
        0.1,
        2,
        2,
        42,
        purity,
        42,
        12,
        2,
        isInfinite);
}

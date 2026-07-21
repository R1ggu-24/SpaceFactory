using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class MiningToolRulesTests
{
    [Fact]
    public void ToolItems_MapToThreeIncreasingTiers()
    {
        Assert.True(MiningToolRules.TryGetTier(ProductionItemIds.MiningTool, out var standard));
        Assert.True(MiningToolRules.TryGetTier(ProductionItemIds.UpgradedMiningTool, out var improved));
        Assert.True(MiningToolRules.TryGetTier(ProductionItemIds.HighPerformanceMiningTool, out var high));

        Assert.Equal(MiningToolTier.Standard, standard);
        Assert.Equal(MiningToolTier.Improved, improved);
        Assert.Equal(MiningToolTier.HighPerformance, high);
        Assert.True(MiningToolRules.GetSpeedMultiplier(high) > MiningToolRules.GetSpeedMultiplier(improved));
        Assert.True(MiningToolRules.GetSpeedMultiplier(improved) > MiningToolRules.GetSpeedMultiplier(standard));
    }
}

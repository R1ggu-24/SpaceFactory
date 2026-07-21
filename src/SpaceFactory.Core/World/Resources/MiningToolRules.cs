using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.World.Resources;

public enum MiningToolTier
{
    Standard,
    Improved,
    HighPerformance,
}

public static class MiningToolConfiguration
{
    public const double StandardMaximumHardness = 4.0;
    public const double ImprovedMaximumHardness = 8.5;
    public const double StandardSpeedMultiplier = 1.0;
    public const double ImprovedSpeedMultiplier = 2.0;
    public const double HighPerformanceSpeedMultiplier = 4.0;
}

public static class MiningToolRules
{
    public static bool TryGetTier(ItemId itemId, out MiningToolTier tier)
    {
        if (itemId == ProductionItemIds.MiningTool)
        {
            tier = MiningToolTier.Standard;
            return true;
        }

        if (itemId == ProductionItemIds.UpgradedMiningTool)
        {
            tier = MiningToolTier.Improved;
            return true;
        }

        if (itemId == ProductionItemIds.HighPerformanceMiningTool)
        {
            tier = MiningToolTier.HighPerformance;
            return true;
        }

        tier = default;
        return false;
    }

    public static bool CanMine(MiningToolTier tier, ResourceDefinition resource) => tier switch
    {
        MiningToolTier.Standard => resource.Hardness <= MiningToolConfiguration.StandardMaximumHardness,
        MiningToolTier.Improved => resource.Hardness <= MiningToolConfiguration.ImprovedMaximumHardness,
        MiningToolTier.HighPerformance => true,
        _ => false,
    };

    public static double GetSpeedMultiplier(MiningToolTier tier) => tier switch
    {
        MiningToolTier.Standard => MiningToolConfiguration.StandardSpeedMultiplier,
        MiningToolTier.Improved => MiningToolConfiguration.ImprovedSpeedMultiplier,
        MiningToolTier.HighPerformance => MiningToolConfiguration.HighPerformanceSpeedMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };
}

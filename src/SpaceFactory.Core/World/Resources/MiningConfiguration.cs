namespace SpaceFactory.Core.World.Resources;

/// <summary>
/// Central balancing values shared by manual extraction and future mining machines.
/// Sources remain deterministic; changing these values changes throughput, not placement.
/// </summary>
public static class MiningConfiguration
{
    public const double MinimumSourceCometRadiusWorldUnits = 190;
    public const int MinimumSourcesPerComet = 1;
    public const int MaximumSourcesPerComet = 3;
    public const double DefaultSourceRadiusWorldUnits = 42;
    public const double MaximumSourceRadiusWorldUnits = 42;
    public const double SourceClearanceWorldUnits = 6;
    public const double DefaultExtractionUnitsPerMinute = 12;
    public const int DefaultManualYieldPerCycle = 2;

    // Small finite ore stones are generated alongside normal infinite sources at half their
    // expected frequency. Their hit count and each hit's yield are deterministic per ID/seed.
    public const double FiniteOreStoneSpawnChancePerSource = 0.5;
    public const double FiniteOreStoneRadiusWorldUnits = 18;
    public const double FiniteOreStoneMiningTimeMultiplier = 0.65;
    public const int MinimumFiniteOreStoneHits = 5;
    public const int MaximumFiniteOreStoneHits = 7;
    public const int MinimumFiniteOreStoneYieldPerHit = 5;
    public const int MaximumFiniteOreStoneYieldPerHit = 10;

    // Manual extraction is intentionally an early-game or emergency option.
    public const double ManualSpeedMultiplier = 1.75;

    public const double ImpureChance = 0.35;
    public const double NormalChance = 0.50;
    public const double PureChance = 0.15;

    public static double GetExtractionRateMultiplier(ResourcePurity purity) => purity switch
    {
        ResourcePurity.Impure => 0.65,
        ResourcePurity.Normal => 1.0,
        ResourcePurity.Pure => 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(purity)),
    };

    public static double GetManualCycleTimeMultiplier(ResourcePurity purity) => purity switch
    {
        ResourcePurity.Impure => 1.30,
        ResourcePurity.Normal => 1.0,
        ResourcePurity.Pure => 0.80,
        _ => throw new ArgumentOutOfRangeException(nameof(purity)),
    };

    public static double GetEnergyEfficiencyMultiplier(ResourcePurity purity) => purity switch
    {
        ResourcePurity.Impure => 0.75,
        ResourcePurity.Normal => 1.0,
        ResourcePurity.Pure => 1.25,
        _ => throw new ArgumentOutOfRangeException(nameof(purity)),
    };

    public static double GetManualCycleDurationSeconds(double baseDurationSeconds, ResourcePurity purity)
    {
        if (!double.IsFinite(baseDurationSeconds) || baseDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDurationSeconds));
        }

        return baseDurationSeconds * ManualSpeedMultiplier * GetManualCycleTimeMultiplier(purity);
    }

    public static double GetExtractionUnitsPerMinute(double baseUnitsPerMinute, ResourcePurity purity)
    {
        if (!double.IsFinite(baseUnitsPerMinute) || baseUnitsPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseUnitsPerMinute));
        }

        return baseUnitsPerMinute * GetExtractionRateMultiplier(purity);
    }

    public static int GetManualYield(int baseYield, ResourcePurity purity)
    {
        if (baseYield <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseYield));
        }

        return Math.Max(
            1,
            (int)Math.Round(
                baseYield * GetExtractionRateMultiplier(purity),
                MidpointRounding.AwayFromZero));
    }

    public static string GetPurityDisplayName(ResourcePurity purity) => purity switch
    {
        ResourcePurity.Impure => "Unrein",
        ResourcePurity.Normal => "Normal",
        ResourcePurity.Pure => "Rein",
        _ => throw new ArgumentOutOfRangeException(nameof(purity)),
    };
}

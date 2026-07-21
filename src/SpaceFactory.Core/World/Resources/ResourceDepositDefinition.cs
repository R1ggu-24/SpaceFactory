using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.World.Resources;

public sealed record ResourceDepositDefinition(
    string Id,
    string CometId,
    ItemId ResourceId,
    WorldPosition NormalizedPosition,
    double RadiusFactor,
    int OriginalAmount,
    double MiningTimeSeconds,
    ulong VisualSeed,
    ResourcePurity Purity = ResourcePurity.Normal,
    double RadiusWorldUnits = 0,
    double BaseExtractionUnitsPerMinute = 0,
    int ManualYieldPerCycle = 0,
    bool IsInfinite = false,
    ResourceDepositKind Kind = ResourceDepositKind.LegacyDeposit)
{
    public bool IsFiniteOreStone => Kind == ResourceDepositKind.FiniteOreStone;

    public double GetRadiusWorldUnits(double cometRadius)
    {
        if (!double.IsFinite(cometRadius) || cometRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cometRadius));
        }

        return RadiusWorldUnits > 0
            ? RadiusWorldUnits
            : RadiusFactor * cometRadius;
    }

    public double EffectiveExtractionUnitsPerMinute => IsInfinite
        ? MiningConfiguration.GetExtractionUnitsPerMinute(BaseExtractionUnitsPerMinute, Purity)
        : 0;

    public double EffectiveManualMiningTimeSeconds => IsInfinite
        ? MiningConfiguration.GetManualCycleDurationSeconds(MiningTimeSeconds, Purity)
        : MiningTimeSeconds;
}

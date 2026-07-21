namespace SpaceFactory.Core.World.Resources;

/// <summary>
/// Pure extraction transaction rules. Inventory capacity is checked by the caller before
/// applying a harvest, so an unsuccessful transfer never mutates a source.
/// </summary>
public static class ResourceExtractionRules
{
    public static int GetManualYield(ResourceDepositDefinition source, int currentRemainingAmount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsFiniteOreStone)
        {
            return currentRemainingAmount <= 0
                ? 0
                : GetFiniteOreStoneYield(source, currentRemainingAmount);
        }

        if (source.IsInfinite)
        {
            return MiningConfiguration.GetManualYield(source.ManualYieldPerCycle, source.Purity);
        }

        return Math.Max(0, currentRemainingAmount);
    }

    public static int GetRemainingAmountAfterManualHarvest(
        ResourceDepositDefinition source,
        int currentRemainingAmount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (currentRemainingAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentRemainingAmount));
        }

        if (source.IsInfinite)
        {
            return currentRemainingAmount;
        }

        return source.IsFiniteOreStone
            ? Math.Max(0, currentRemainingAmount - 1)
            : 0;
    }

    private static int GetFiniteOreStoneYield(
        ResourceDepositDefinition source,
        int currentRemainingHits)
    {
        if (currentRemainingHits > source.OriginalAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(currentRemainingHits));
        }

        var completedHitCount = source.OriginalAmount - currentRemainingHits;
        var mixed = Mix(source.VisualSeed ^ ((ulong)completedHitCount * 0x9E3779B97F4A7C15UL));
        var range = MiningConfiguration.MaximumFiniteOreStoneYieldPerHit -
                    MiningConfiguration.MinimumFiniteOreStoneYieldPerHit + 1;
        return MiningConfiguration.MinimumFiniteOreStoneYieldPerHit + (int)(mixed % (uint)range);
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

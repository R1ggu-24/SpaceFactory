namespace SpaceFactory.Core.Production;

/// <summary>
/// Shared timing rules for raw and crushed ore recipes. Keeping the multiplier
/// here prevents individual smelter recipes from drifting apart over time.
/// </summary>
public static class SmeltingConfiguration
{
    public const double RawOreDurationMultiplier = 1.5;

    public static double GetRawOreDuration(double crushedOreDurationSeconds)
    {
        if (!double.IsFinite(crushedOreDurationSeconds) || crushedOreDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(crushedOreDurationSeconds),
                "Crushed-ore smelting duration must be positive and finite.");
        }

        return crushedOreDurationSeconds * RawOreDurationMultiplier;
    }
}

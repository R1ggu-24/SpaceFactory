namespace SpaceFactory.Core.Hazards;

/// <summary>
/// Central balancing values for environmental radiation. Dose is deliberately expressed in
/// gameplay units instead of a real-world medical unit so later suit upgrades can change the
/// protection curve without migrating saves.
/// </summary>
public static class RadiationConfiguration
{
    public const double MinimumSourceDistanceWorldUnits = 24;
    public const double ReferenceDistanceWorldUnits = 120;
    public const double DoseWarningThreshold = 25;
    public const double DoseCriticalThreshold = 70;
    public const double MaximumDose = 100;
    public const double PassiveDoseRecoveryPerSecond = 0.015;
    public const double StandardSuitProtection = 0.15;
    public const double ImprovedSuitProtection = 0.60;
    public const double NuclearSuitProtection = 0.90;
    public const double StoredItemStrengthScale = 0.08;
    public const double ResourceSourceStrengthScale = 0.12;
    public const double MinimumMovementMultiplier = 0.55;
    public const double MinimumMiningEfficiencyMultiplier = 0.65;
}

public enum RadiationExposureLevel
{
    Safe,
    Elevated,
    Critical,
}

public sealed record RadiationSource(
    string Id,
    double PositionX,
    double PositionY,
    double StrengthPerSecond,
    double Shielding = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || !double.IsFinite(PositionX) ||
            !double.IsFinite(PositionY) || !double.IsFinite(StrengthPerSecond) ||
            StrengthPerSecond < 0 || !double.IsFinite(Shielding) ||
            Shielding is < 0 or > 1)
        {
            throw new ArgumentException("The radiation source is invalid.");
        }
    }
}

public sealed record RadiationExposureSnapshot(double AccumulatedDose, double SuitProtection);

/// <summary>
/// Persistent player exposure plus a deterministic distance-falloff simulation. Sources can be
/// supplied from loaded machines, inventories or resource deposits without coupling the hazard
/// model to a presentation node.
/// </summary>
public sealed class RadiationExposureState
{
    private const double Epsilon = 0.000_001;

    public RadiationExposureState(double suitProtection = RadiationConfiguration.StandardSuitProtection)
    {
        SetSuitProtection(suitProtection);
    }

    public double AccumulatedDose { get; private set; }

    public double SuitProtection { get; private set; }

    public RadiationExposureLevel Level => AccumulatedDose switch
    {
        >= RadiationConfiguration.DoseCriticalThreshold => RadiationExposureLevel.Critical,
        >= RadiationConfiguration.DoseWarningThreshold => RadiationExposureLevel.Elevated,
        _ => RadiationExposureLevel.Safe,
    };

    public double Integrity => Math.Clamp(
        1 - (AccumulatedDose / RadiationConfiguration.MaximumDose),
        0,
        1);

    public double MovementMultiplier => Lerp(
        RadiationConfiguration.MinimumMovementMultiplier,
        1,
        Integrity);

    public double MiningEfficiencyMultiplier => Lerp(
        RadiationConfiguration.MinimumMiningEfficiencyMultiplier,
        1,
        Integrity);

    public double Advance(
        double deltaSeconds,
        double playerPositionX,
        double playerPositionY,
        IEnumerable<RadiationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 ||
            !double.IsFinite(playerPositionX) || !double.IsFinite(playerPositionY))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var materialized = sources.ToArray();
        foreach (var source in materialized)
        {
            source.Validate();
        }

        var unprotectedRate = materialized.Sum(source => GetDoseRate(
            playerPositionX,
            playerPositionY,
            source));
        var protectedRate = unprotectedRate * (1 - SuitProtection);
        var doseDelta = protectedRate > Epsilon
            ? protectedRate * deltaSeconds
            : -RadiationConfiguration.PassiveDoseRecoveryPerSecond * deltaSeconds;
        AccumulatedDose = Math.Clamp(
            AccumulatedDose + doseDelta,
            0,
            RadiationConfiguration.MaximumDose);
        return Math.Max(0, protectedRate);
    }

    public void SetSuitProtection(double protection)
    {
        if (!double.IsFinite(protection) || protection is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(protection));
        }

        SuitProtection = protection;
    }

    public RadiationExposureSnapshot CreateSnapshot() => new(AccumulatedDose, SuitProtection);

    public static RadiationExposureState Restore(RadiationExposureSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!double.IsFinite(snapshot.AccumulatedDose) ||
            snapshot.AccumulatedDose is < 0 or > RadiationConfiguration.MaximumDose)
        {
            throw new ArgumentException("The radiation exposure snapshot is invalid.", nameof(snapshot));
        }

        return new RadiationExposureState(snapshot.SuitProtection)
        {
            AccumulatedDose = snapshot.AccumulatedDose,
        };
    }

    private static double GetDoseRate(double playerX, double playerY, RadiationSource source)
    {
        var deltaX = source.PositionX - playerX;
        var deltaY = source.PositionY - playerY;
        var distance = Math.Max(
            RadiationConfiguration.MinimumSourceDistanceWorldUnits,
            Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
        var distanceFactor = Math.Pow(
            RadiationConfiguration.ReferenceDistanceWorldUnits / distance,
            2);
        return source.StrengthPerSecond * (1 - source.Shielding) * distanceFactor;
    }

    private static double Lerp(double minimum, double maximum, double amount) =>
        minimum + ((maximum - minimum) * amount);
}

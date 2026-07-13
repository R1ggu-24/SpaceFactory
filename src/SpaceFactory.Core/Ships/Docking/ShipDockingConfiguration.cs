namespace SpaceFactory.Core.Ships.Docking;

public sealed class ShipDockingConfiguration
{
    public const double DefaultMaximumAttachmentDistance = 96.0;
    public const double DefaultMaximumAttachmentSpeed = 90.0;
    public const double DefaultSafeExitDriftSpeed = 110.0;
    public const double DefaultAstronautVelocityInheritance = 0.85;
    public const double DefaultLandingLegAnimationSpeed = 4.0;

    public static ShipDockingConfiguration Default { get; } = new();

    public ShipDockingConfiguration(
        double maximumAttachmentDistance = DefaultMaximumAttachmentDistance,
        double maximumAttachmentSpeed = DefaultMaximumAttachmentSpeed,
        double safeExitDriftSpeed = DefaultSafeExitDriftSpeed,
        double astronautVelocityInheritance = DefaultAstronautVelocityInheritance,
        double landingLegAnimationSpeed = DefaultLandingLegAnimationSpeed)
    {
        MaximumAttachmentDistance = RequirePositiveFinite(
            maximumAttachmentDistance,
            nameof(maximumAttachmentDistance));
        MaximumAttachmentSpeed = RequireNonNegativeFinite(
            maximumAttachmentSpeed,
            nameof(maximumAttachmentSpeed));
        SafeExitDriftSpeed = RequireNonNegativeFinite(
            safeExitDriftSpeed,
            nameof(safeExitDriftSpeed));

        if (!double.IsFinite(astronautVelocityInheritance) || astronautVelocityInheritance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(astronautVelocityInheritance),
                astronautVelocityInheritance,
                "The inherited velocity fraction must be between zero and one.");
        }

        AstronautVelocityInheritance = astronautVelocityInheritance;
        LandingLegAnimationSpeed = RequirePositiveFinite(
            landingLegAnimationSpeed,
            nameof(landingLegAnimationSpeed));
    }

    public double MaximumAttachmentDistance { get; }

    public double MaximumAttachmentSpeed { get; }

    public double SafeExitDriftSpeed { get; }

    public double AstronautVelocityInheritance { get; }

    /// <summary>
    /// Normalized landing-leg travel per second. A value of four completes the animation in 0.25 seconds.
    /// </summary>
    public double LandingLegAnimationSpeed { get; }

    private static double RequirePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite and positive.");
        }

        return value;
    }

    private static double RequireNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite and non-negative.");
        }

        return value;
    }
}

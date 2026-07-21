namespace SpaceFactory.Core.Construction;

/// <summary>
/// Keeps the last player-selected construction rotation independently from a
/// particular preview or placement source. Presentation code can therefore
/// recreate a preview without resetting the next rotatable object to zero.
/// </summary>
public sealed class PlacementRotationState
{
    public const double DefaultRotationRadians = 0;

    public double LastRotationRadians { get; private set; } = DefaultRotationRadians;

    public double ResolveInitialRotation(bool supportsRotation) =>
        supportsRotation ? LastRotationRadians : DefaultRotationRadians;

    public bool Remember(double rotationRadians, bool supportsRotation)
    {
        if (!double.IsFinite(rotationRadians))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rotationRadians),
                "A construction rotation must be finite.");
        }

        if (!supportsRotation)
        {
            return false;
        }

        var normalized = Normalize(rotationRadians);
        if (Math.Abs(normalized - LastRotationRadians) <= 1e-9)
        {
            return false;
        }

        LastRotationRadians = normalized;
        return true;
    }

    private static double Normalize(double radians)
    {
        var wrapped = (radians + Math.PI) % Math.Tau;
        if (wrapped < 0)
        {
            wrapped += Math.Tau;
        }

        return wrapped - Math.PI;
    }
}

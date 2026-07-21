using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Ships.Docking;

/// <summary>
/// Pure migration helpers for docking poses whose host-comet geometry may have changed.
/// Persisted attachment positions define a stable radial direction; the current surface remains
/// authoritative for the restored distance and orientation.
/// </summary>
public static class ShipDockingRestoreRules
{
    private const double MinimumDirectionLengthSquared = 0.000001;

    public static WorldPosition ResolveRadialDirection(
        WorldPosition persistedRelativePosition,
        double persistedRelativeRotationRadians)
    {
        if (!double.IsFinite(persistedRelativePosition.X) ||
            !double.IsFinite(persistedRelativePosition.Y) ||
            !double.IsFinite(persistedRelativeRotationRadians))
        {
            throw new ArgumentException("The persisted docking pose must be finite.");
        }

        var lengthSquared =
            (persistedRelativePosition.X * persistedRelativePosition.X) +
            (persistedRelativePosition.Y * persistedRelativePosition.Y);
        if (lengthSquared > MinimumDirectionLengthSquared)
        {
            var inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new WorldPosition(
                persistedRelativePosition.X * inverseLength,
                persistedRelativePosition.Y * inverseLength);
        }

        // Attached ships point away from the surface. Their local +Y axis faces the comet,
        // therefore the outward surface direction is rotation minus ninety degrees.
        var outwardAngle = persistedRelativeRotationRadians - (Math.PI * 0.5);
        return new WorldPosition(Math.Cos(outwardAngle), Math.Sin(outwardAngle));
    }

    public static ShipDockingRestoreProjection ProjectOntoCurrentSurface(
        WorldPosition relativeSurfacePoint,
        WorldPosition relativeOutwardNormal,
        double centerClearance)
    {
        if (!double.IsFinite(relativeSurfacePoint.X) ||
            !double.IsFinite(relativeSurfacePoint.Y) ||
            !double.IsFinite(relativeOutwardNormal.X) ||
            !double.IsFinite(relativeOutwardNormal.Y) ||
            !double.IsFinite(centerClearance) ||
            centerClearance <= 0)
        {
            throw new ArgumentException("The current docking surface projection must be finite and positive.");
        }

        var normalLengthSquared =
            (relativeOutwardNormal.X * relativeOutwardNormal.X) +
            (relativeOutwardNormal.Y * relativeOutwardNormal.Y);
        if (normalLengthSquared <= MinimumDirectionLengthSquared)
        {
            throw new ArgumentException("The current docking surface normal must not be zero.");
        }

        var inverseNormalLength = 1.0 / Math.Sqrt(normalLengthSquared);
        var normalX = relativeOutwardNormal.X * inverseNormalLength;
        var normalY = relativeOutwardNormal.Y * inverseNormalLength;
        return new ShipDockingRestoreProjection(
            new WorldPosition(
                relativeSurfacePoint.X + (normalX * centerClearance),
                relativeSurfacePoint.Y + (normalY * centerClearance)),
            Math.Atan2(normalY, normalX) + (Math.PI * 0.5));
    }
}

public readonly record struct ShipDockingRestoreProjection(
    WorldPosition RelativeAttachmentPosition,
    double RelativeAttachmentRotationRadians);

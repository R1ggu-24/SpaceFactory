using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Ships.Docking;

public readonly record struct ShipDockingCandidate(
    string CometId,
    double SurfaceDistance,
    bool HasFreeSurface,
    bool HasSafeExitPosition,
    WorldPosition RelativeAttachmentPosition,
    double AttachmentRotationRadians)
{
    public bool IsStructurallyValid =>
        !string.IsNullOrWhiteSpace(CometId) &&
        double.IsFinite(SurfaceDistance) &&
        SurfaceDistance >= 0 &&
        double.IsFinite(RelativeAttachmentPosition.X) &&
        double.IsFinite(RelativeAttachmentPosition.Y) &&
        double.IsFinite(AttachmentRotationRadians);
}

using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Ships.Docking;

public sealed class ShipDockingState
{
    private const double FullyDeployedProgress = 1.0;

    public bool IsAttached { get; private set; }

    public string? AttachedCometId { get; private set; }

    public WorldPosition RelativeAttachmentPosition { get; private set; }

    public double AttachmentRotationRadians { get; private set; }

    public double LandingLegProgress { get; private set; }

    public bool AreLandingLegsDeployed => LandingLegProgress >= FullyDeployedProgress;

    public bool AreLandingLegsRetracted => LandingLegProgress <= 0;

    public void RestoreAttached(
        string cometId,
        WorldPosition relativeAttachmentPosition,
        double attachmentRotationRadians,
        double landingLegProgress)
    {
        if (string.IsNullOrWhiteSpace(cometId) ||
            !double.IsFinite(relativeAttachmentPosition.X) ||
            !double.IsFinite(relativeAttachmentPosition.Y) ||
            !double.IsFinite(attachmentRotationRadians) ||
            !double.IsFinite(landingLegProgress) ||
            landingLegProgress is < 0 or > FullyDeployedProgress)
        {
            throw new ArgumentException("The persisted ship docking state is invalid.");
        }

        IsAttached = true;
        AttachedCometId = cometId;
        RelativeAttachmentPosition = relativeAttachmentPosition;
        AttachmentRotationRadians = attachmentRotationRadians;
        LandingLegProgress = landingLegProgress;
    }

    public void RestoreDetached(double landingLegProgress = 0)
    {
        if (!double.IsFinite(landingLegProgress) ||
            landingLegProgress is < 0 or > FullyDeployedProgress)
        {
            throw new ArgumentOutOfRangeException(nameof(landingLegProgress));
        }

        IsAttached = false;
        AttachedCometId = null;
        RelativeAttachmentPosition = default;
        AttachmentRotationRadians = 0;
        LandingLegProgress = landingLegProgress;
    }

    public ShipDockingDecision TryExecute(
        ShipDockingContext context,
        ShipDockingConfiguration? configuration = null)
    {
        var decision = ShipDockingRules.Evaluate(this, context, configuration);
        if (!decision.IsAllowed)
        {
            return decision;
        }

        if (decision.Action == ShipDockingAction.Attach)
        {
            var candidate = context.Candidate!.Value;
            IsAttached = true;
            AttachedCometId = candidate.CometId;
            RelativeAttachmentPosition = candidate.RelativeAttachmentPosition;
            AttachmentRotationRadians = candidate.AttachmentRotationRadians;
        }
        else
        {
            IsAttached = false;
            AttachedCometId = null;
            RelativeAttachmentPosition = default;
            AttachmentRotationRadians = 0;
        }

        return decision;
    }

    public void AdvanceLandingLegs(
        double elapsedSeconds,
        ShipDockingConfiguration? configuration = null)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "Elapsed time must be finite and non-negative.");
        }

        configuration ??= ShipDockingConfiguration.Default;
        var target = IsAttached ? FullyDeployedProgress : 0;
        var travel = configuration.LandingLegAnimationSpeed * elapsedSeconds;
        LandingLegProgress = MoveTowards(LandingLegProgress, target, travel);
    }

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        if (Math.Abs(target - current) <= maximumDelta)
        {
            return target;
        }

        return current + (Math.Sign(target - current) * maximumDelta);
    }
}

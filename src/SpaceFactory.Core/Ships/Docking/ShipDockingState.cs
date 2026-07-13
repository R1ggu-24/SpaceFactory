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

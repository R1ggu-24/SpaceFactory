namespace SpaceFactory.Core.Ships.Docking;

public static class ShipDockingRules
{
    public static ShipDockingDecision Evaluate(
        ShipDockingState state,
        ShipDockingContext context,
        ShipDockingConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        configuration ??= ShipDockingConfiguration.Default;

        if (!context.IsShipControlled)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.PlayerNotControllingShip);
        }

        if (context.IsPauseMenuOpen)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.PauseMenuOpen);
        }

        if (state.IsAttached)
        {
            return ShipDockingDecision.Allowed(ShipDockingAction.Detach);
        }

        if (!double.IsFinite(context.ShipSpeed) || context.ShipSpeed < 0)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.MovingTooFast);
        }

        if (context.Candidate is not { } candidate)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.NoCandidate);
        }

        if (!candidate.IsStructurallyValid)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.InvalidCandidate);
        }

        if (!candidate.HasFreeSurface)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.SurfaceUnavailable);
        }

        if (!candidate.HasSafeExitPosition)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.UnsafeExitPosition);
        }

        if (candidate.SurfaceDistance > configuration.MaximumAttachmentDistance)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.TooFarFromSurface);
        }

        if (context.ShipSpeed > configuration.MaximumAttachmentSpeed)
        {
            return ShipDockingDecision.Blocked(ShipDockingBlockReason.MovingTooFast);
        }

        return ShipDockingDecision.Allowed(ShipDockingAction.Attach);
    }
}

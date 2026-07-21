namespace SpaceFactory.Core.Ships.Docking;

public enum ShipDockingAction
{
    None,
    Attach,
    Detach,
}

public enum ShipDockingBlockReason
{
    None,
    PlayerNotControllingShip,
    PauseMenuOpen,
    NoCandidate,
    InvalidCandidate,
    SurfaceUnavailable,
    UnsafeExitPosition,
    TooFarFromSurface,
    MovingTooFast,
    ActivePowerConnections,
}

public readonly record struct ShipDockingDecision(
    ShipDockingAction Action,
    ShipDockingBlockReason BlockReason)
{
    public bool IsAllowed => Action != ShipDockingAction.None && BlockReason == ShipDockingBlockReason.None;

    public static ShipDockingDecision Allowed(ShipDockingAction action)
    {
        if (action == ShipDockingAction.None)
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "An allowed decision needs an action.");
        }

        return new ShipDockingDecision(action, ShipDockingBlockReason.None);
    }

    public static ShipDockingDecision Blocked(ShipDockingBlockReason reason)
    {
        if (reason == ShipDockingBlockReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "A blocked decision needs a reason.");
        }

        return new ShipDockingDecision(ShipDockingAction.None, reason);
    }
}

using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Player;

public enum PlayerControlMode
{
    Ship,
    OnFoot,
}

public enum ShipInteractionAction
{
    None,
    EnterShip,
    ExitShip,
}

public static class ShipInteractionRules
{
    public static ShipInteractionAction GetAvailableAction(
        PlayerControlMode controlMode,
        bool isMining,
        bool isInteractionBlocked,
        WorldPosition astronautPosition,
        WorldPosition entryPosition,
        double interactionDistance)
    {
        if (isInteractionBlocked)
        {
            return ShipInteractionAction.None;
        }

        return controlMode switch
        {
            PlayerControlMode.Ship => ShipInteractionAction.ExitShip,
            PlayerControlMode.OnFoot when CanEnterShip(
                true,
                isMining,
                astronautPosition,
                entryPosition,
                interactionDistance) => ShipInteractionAction.EnterShip,
            PlayerControlMode.OnFoot => ShipInteractionAction.None,
            _ => throw new ArgumentOutOfRangeException(nameof(controlMode), controlMode, "Unknown control mode."),
        };
    }

    public static bool CanEnterShip(
        bool isOnFoot,
        bool isMining,
        WorldPosition astronautPosition,
        WorldPosition entryPosition,
        double interactionDistance)
    {
        if (!isOnFoot || isMining || interactionDistance <= 0)
        {
            return false;
        }

        var deltaX = astronautPosition.X - entryPosition.X;
        var deltaY = astronautPosition.Y - entryPosition.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= interactionDistance * interactionDistance;
    }
}

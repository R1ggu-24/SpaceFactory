using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Player;

public static class ShipInteractionRules
{
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

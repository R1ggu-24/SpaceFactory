using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.Player;

public static class MiningInteractionRules
{
    public static bool CanMine(
        bool isOnFoot,
        bool isUiBlocked,
        bool miningInputHeld,
        bool targetHasMaterial,
        WorldPosition astronautPosition,
        WorldPosition targetPosition,
        double miningRange)
    {
        if (!isOnFoot || isUiBlocked || !miningInputHeld || !targetHasMaterial || miningRange <= 0)
        {
            return false;
        }

        var deltaX = astronautPosition.X - targetPosition.X;
        var deltaY = astronautPosition.Y - targetPosition.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= miningRange * miningRange;
    }
}

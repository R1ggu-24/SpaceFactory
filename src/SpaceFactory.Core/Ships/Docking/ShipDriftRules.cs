namespace SpaceFactory.Core.Ships.Docking;

public readonly record struct ShipVelocity(double X, double Y)
{
    public double Speed => Math.Sqrt((X * X) + (Y * Y));

    public static ShipVelocity operator *(ShipVelocity velocity, double factor) =>
        new(velocity.X * factor, velocity.Y * factor);
}

public static class ShipDriftRules
{
    public static ShipVelocity CalculateUnpilotedDrift(
        ShipVelocity currentVelocity,
        ShipDockingConfiguration? configuration = null)
    {
        configuration ??= ShipDockingConfiguration.Default;
        ValidateVelocity(currentVelocity);
        return ClampSpeed(currentVelocity, configuration.SafeExitDriftSpeed);
    }

    public static ShipVelocity CalculateAstronautExitVelocity(
        ShipVelocity shipDriftVelocity,
        ShipDockingConfiguration? configuration = null)
    {
        configuration ??= ShipDockingConfiguration.Default;
        ValidateVelocity(shipDriftVelocity);
        var safeShipDrift = ClampSpeed(shipDriftVelocity, configuration.SafeExitDriftSpeed);
        return safeShipDrift * configuration.AstronautVelocityInheritance;
    }

    private static ShipVelocity ClampSpeed(ShipVelocity velocity, double maximumSpeed)
    {
        var speed = velocity.Speed;
        if (speed <= maximumSpeed || speed <= double.Epsilon)
        {
            return velocity;
        }

        return velocity * (maximumSpeed / speed);
    }

    private static void ValidateVelocity(ShipVelocity velocity)
    {
        if (!double.IsFinite(velocity.X) || !double.IsFinite(velocity.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "Velocity must be finite.");
        }
    }
}

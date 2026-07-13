namespace SpaceFactory.Core.Ships.Fuel;

public enum BoostFuelConsumptionFailure
{
    None,
    BoostNotActive,
    TankBelowMinimum,
    InsufficientFuelForInterval,
}

public readonly record struct BoostFuelConsumptionResult(
    bool BoostAllowed,
    BoostFuelConsumptionFailure Failure,
    double RequestedFuel,
    double ConsumedFuel)
{
    public bool Succeeded => BoostAllowed;

    public static BoostFuelConsumptionResult Success(double consumedFuel) =>
        new(true, BoostFuelConsumptionFailure.None, consumedFuel, consumedFuel);

    public static BoostFuelConsumptionResult Success(double requestedFuel, double consumedFuel) =>
        new(true, BoostFuelConsumptionFailure.None, requestedFuel, consumedFuel);

    public static BoostFuelConsumptionResult Failed(
        BoostFuelConsumptionFailure failure,
        double requestedFuel = 0) =>
        new(false, failure, requestedFuel, 0);
}

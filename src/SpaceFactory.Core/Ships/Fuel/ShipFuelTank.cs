using SpaceFactory.Core.Power;

namespace SpaceFactory.Core.Ships.Fuel;

public sealed class ShipFuelTank
{
    private const double ComparisonTolerance = 0.000_000_001;

    public ShipFuelTank(
        double initialFuel = ShipFuelConfiguration.TankCapacity,
        ShipFuelType initialFuelType = ShipFuelConfiguration.NewGameFuelType)
    {
        if (!double.IsFinite(initialFuel) || initialFuel < 0 ||
            initialFuel > ShipFuelConfiguration.TankCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(initialFuel));
        }

        if (!Enum.IsDefined(initialFuelType))
        {
            throw new ArgumentOutOfRangeException(nameof(initialFuelType));
        }

        CurrentFuel = initialFuel;
        CurrentFuelType = initialFuelType;
    }

    public double CurrentFuel { get; private set; }

    public ShipFuelType CurrentFuelType { get; private set; }

    public double Capacity => ShipFuelConfiguration.TankCapacity;

    public double RemainingCapacity => Capacity - CurrentFuel;

    public double FillRatio => CurrentFuel / Capacity;

    public double RemainingBoostSeconds => CurrentFuel / ShipFuelConfiguration.ConsumptionPerBoostSecond;

    public double BoostFlightSpeed =>
        SpaceFactory.Core.Ships.ShipFlightConfiguration.GetBoostFlightSpeed(CurrentFuelType);

    public double BoostSpeedMultiplier =>
        SpaceFactory.Core.Ships.ShipFlightConfiguration.GetBoostSpeedMultiplier(CurrentFuelType);

    public bool CanActivateBoost =>
        CurrentFuel + ComparisonTolerance >= ShipFuelConfiguration.MinimumFuelToActivateBoost;

    public BoostFuelConsumptionResult ConsumeBoostFuel(
        double elapsedSeconds,
        bool isBoostActuallyActive)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!isBoostActuallyActive)
        {
            return BoostFuelConsumptionResult.Failed(BoostFuelConsumptionFailure.BoostNotActive);
        }

        if (!CanActivateBoost)
        {
            return BoostFuelConsumptionResult.Failed(BoostFuelConsumptionFailure.TankBelowMinimum);
        }

        var requestedFuel = elapsedSeconds * ShipFuelConfiguration.ConsumptionPerBoostSecond;
        if (requestedFuel > CurrentFuel + ComparisonTolerance)
        {
            var remainingFuel = CurrentFuel;
            CurrentFuel = 0;
            return BoostFuelConsumptionResult.Success(requestedFuel, remainingFuel);
        }

        CurrentFuel = Math.Max(0, CurrentFuel - requestedFuel);
        if (CurrentFuel < ShipFuelConfiguration.MinimumFuelToActivateBoost)
        {
            CurrentFuel = 0;
        }

        return BoostFuelConsumptionResult.Success(requestedFuel);
    }

    public ShipPowerFuelConsumptionResult ConsumePowerGenerationFuel(
        double requestedEnergyKilowattSeconds)
    {
        if (!double.IsFinite(requestedEnergyKilowattSeconds) || requestedEnergyKilowattSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedEnergyKilowattSeconds));
        }

        if (requestedEnergyKilowattSeconds <= ComparisonTolerance || CurrentFuel <= ComparisonTolerance)
        {
            return new ShipPowerFuelConsumptionResult(requestedEnergyKilowattSeconds, 0, 0);
        }

        var requestedFuel = requestedEnergyKilowattSeconds *
                            PowerGridConfiguration.ShipFuelPerKilowattSecond;
        var consumedFuel = Math.Min(CurrentFuel, requestedFuel);
        CurrentFuel = Math.Max(0, CurrentFuel - consumedFuel);
        var deliveredEnergy = consumedFuel / PowerGridConfiguration.ShipFuelPerKilowattSecond;
        return new ShipPowerFuelConsumptionResult(
            requestedEnergyKilowattSeconds,
            Math.Min(requestedEnergyKilowattSeconds, deliveredEnergy),
            consumedFuel);
    }

    public bool TryAddFuel(double amount) => TryAddFuel(amount, CurrentFuelType);

    public bool TryAddFuel(double amount, ShipFuelType fuelType)
    {
        if (!double.IsFinite(amount) || amount <= 0 || !Enum.IsDefined(fuelType))
        {
            return false;
        }

        if (amount > RemainingCapacity + ComparisonTolerance ||
            (CurrentFuel > ComparisonTolerance && CurrentFuelType != fuelType))
        {
            return false;
        }

        CurrentFuelType = fuelType;
        CurrentFuel = Math.Min(Capacity, CurrentFuel + amount);
        return true;
    }

    public bool TryRemoveFuel(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0 || amount > CurrentFuel + ComparisonTolerance)
        {
            return false;
        }

        CurrentFuel = Math.Max(0, CurrentFuel - amount);
        return true;
    }

    public bool TrySelectFuelType(ShipFuelType fuelType)
    {
        if (!Enum.IsDefined(fuelType) || CurrentFuel > ComparisonTolerance)
        {
            return false;
        }

        CurrentFuelType = fuelType;
        return true;
    }

    public void RestoreFuel(double amount)
    {
        RestoreFuel(amount, CurrentFuelType);
    }

    public void RestoreFuel(double amount, ShipFuelType fuelType)
    {
        if (!double.IsFinite(amount) || amount < 0 || amount > Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Enum.IsDefined(fuelType))
        {
            throw new ArgumentOutOfRangeException(nameof(fuelType));
        }

        CurrentFuel = amount;
        CurrentFuelType = fuelType;
    }
}

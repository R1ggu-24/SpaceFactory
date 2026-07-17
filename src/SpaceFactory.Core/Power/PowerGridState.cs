namespace SpaceFactory.Core.Power;

public readonly record struct PowerNetworkId
{
    public PowerNetworkId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A power network ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum PowerGridStatus
{
    Disabled,
    Idle,
    Online,
    NoCapacity,
    Overloaded,
    BreakerTripped,
}

public sealed record PowerGridMetrics(
    double MaximumCapacityKilowatts,
    double ActualProductionKilowatts,
    double RequestedPowerKilowatts,
    double ActualConsumptionKilowatts,
    double ReserveKilowatts,
    int ActiveSourceCount,
    PowerGridStatus Status,
    double FuelConsumptionPerMinute,
    double? EstimatedFuelRuntimeMinutes);

public sealed record PowerGridHistorySample(
    double ElapsedSeconds,
    double MaximumCapacityKilowatts,
    double ActualProductionKilowatts,
    double ActualConsumptionKilowatts,
    double RequestedPowerKilowatts);

public sealed class PowerGridHistory
{
    private readonly Queue<PowerGridHistorySample> _samples = [];
    private double _elapsedSeconds;
    private double _secondsSinceMeasurement;

    public IReadOnlyList<PowerGridHistorySample> Samples => _samples.ToArray();

    public void Advance(double deltaSeconds, PowerGridMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        _elapsedSeconds += deltaSeconds;
        _secondsSinceMeasurement += deltaSeconds;
        while (_secondsSinceMeasurement + 0.000_001 >= PowerGridConfiguration.MeasurementIntervalSeconds)
        {
            _secondsSinceMeasurement -= PowerGridConfiguration.MeasurementIntervalSeconds;
            var measurementTime = _elapsedSeconds - _secondsSinceMeasurement;
            _samples.Enqueue(new PowerGridHistorySample(
                measurementTime,
                metrics.MaximumCapacityKilowatts,
                metrics.ActualProductionKilowatts,
                metrics.ActualConsumptionKilowatts,
                metrics.RequestedPowerKilowatts));
            while (_samples.Count > PowerGridConfiguration.HistorySampleCapacity)
            {
                _samples.Dequeue();
            }
        }
    }
}

public sealed record PowerNetworkControlSnapshot(
    PowerNetworkId NetworkId,
    bool IsEnabled,
    bool IsBreakerTripped,
    double OverloadElapsedSeconds);

public sealed class PowerNetworkControlState
{
    private const double Epsilon = 0.000_001;

    public PowerNetworkControlState(PowerNetworkId networkId)
    {
        NetworkId = networkId;
    }

    public PowerNetworkId NetworkId { get; }

    public bool IsEnabled { get; private set; } = true;

    public bool IsBreakerTripped { get; private set; }

    public double OverloadElapsedSeconds { get; private set; }

    public bool CanDeliverPower => IsEnabled && !IsBreakerTripped;

    public PowerGridHistory History { get; } = new();

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
        {
            OverloadElapsedSeconds = 0;
        }
    }

    public bool ResetBreaker()
    {
        if (!IsBreakerTripped)
        {
            return false;
        }

        IsBreakerTripped = false;
        OverloadElapsedSeconds = 0;
        IsEnabled = true;
        return true;
    }

    internal void AdvanceProtection(double deltaSeconds, bool overloaded)
    {
        if (!IsEnabled || IsBreakerTripped || !overloaded)
        {
            OverloadElapsedSeconds = 0;
            return;
        }

        OverloadElapsedSeconds += deltaSeconds;
        if (OverloadElapsedSeconds + Epsilon >= PowerGridConfiguration.OverloadToleranceSeconds)
        {
            IsBreakerTripped = true;
            OverloadElapsedSeconds = 0;
        }
    }

    public PowerNetworkControlSnapshot CreateSnapshot() => new(
        NetworkId,
        IsEnabled,
        IsBreakerTripped,
        OverloadElapsedSeconds);

    public static PowerNetworkControlState Restore(PowerNetworkControlSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!double.IsFinite(snapshot.OverloadElapsedSeconds) || snapshot.OverloadElapsedSeconds < 0 ||
            snapshot.OverloadElapsedSeconds >= PowerGridConfiguration.OverloadToleranceSeconds ||
            (snapshot.IsBreakerTripped && snapshot.OverloadElapsedSeconds > Epsilon))
        {
            throw new ArgumentException("The persisted power network state is invalid.", nameof(snapshot));
        }

        return new PowerNetworkControlState(snapshot.NetworkId)
        {
            IsEnabled = snapshot.IsEnabled,
            IsBreakerTripped = snapshot.IsBreakerTripped,
            OverloadElapsedSeconds = snapshot.OverloadElapsedSeconds,
        };
    }
}

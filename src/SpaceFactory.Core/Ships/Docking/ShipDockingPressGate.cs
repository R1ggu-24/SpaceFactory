namespace SpaceFactory.Core.Ships.Docking;

public sealed class ShipDockingPressGate
{
    public const double DefaultCooldownSeconds = 0.20;

    private readonly double _cooldownSeconds;

    public ShipDockingPressGate(double cooldownSeconds = DefaultCooldownSeconds)
    {
        if (!double.IsFinite(cooldownSeconds) || cooldownSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cooldownSeconds),
                cooldownSeconds,
                "The cooldown must be finite and non-negative.");
        }

        _cooldownSeconds = cooldownSeconds;
    }

    public bool IsHeld { get; private set; }

    public double RemainingCooldownSeconds { get; private set; }

    /// <summary>
    /// Consumes only a rising input edge. Rejected presses are also held until release, so a held key
    /// cannot become accepted merely because the cooldown expires.
    /// </summary>
    public bool TryConsume(bool isPressed)
    {
        if (!isPressed)
        {
            IsHeld = false;
            return false;
        }

        if (IsHeld)
        {
            return false;
        }

        IsHeld = true;
        if (RemainingCooldownSeconds > 0)
        {
            return false;
        }

        RemainingCooldownSeconds = _cooldownSeconds;
        return true;
    }

    public void Advance(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "Elapsed time must be finite and non-negative.");
        }

        RemainingCooldownSeconds = Math.Max(0, RemainingCooldownSeconds - elapsedSeconds);
    }

    public void SuppressUntilReleased()
    {
        IsHeld = true;
    }

    public void Reset()
    {
        IsHeld = false;
        RemainingCooldownSeconds = 0;
    }
}

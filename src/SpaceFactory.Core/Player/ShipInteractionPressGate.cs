namespace SpaceFactory.Core.Player;

public sealed class ShipInteractionPressGate
{
    public const double DefaultCooldownSeconds = 0.25;

    private readonly double _cooldownSeconds;

    public ShipInteractionPressGate(double cooldownSeconds = DefaultCooldownSeconds)
    {
        if (cooldownSeconds < 0 || !double.IsFinite(cooldownSeconds))
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

    public bool TryPress()
    {
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

    public void Release() => IsHeld = false;

    public void Reset()
    {
        IsHeld = false;
        RemainingCooldownSeconds = 0;
    }

    public void Advance(double elapsedSeconds)
    {
        if (elapsedSeconds < 0 || !double.IsFinite(elapsedSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "Elapsed time must be finite and non-negative.");
        }

        RemainingCooldownSeconds = Math.Max(0, RemainingCooldownSeconds - elapsedSeconds);
    }
}

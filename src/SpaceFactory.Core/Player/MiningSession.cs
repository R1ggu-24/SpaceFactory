namespace SpaceFactory.Core.Player;

public sealed class MiningSession
{
    private double _elapsedSeconds;
    private double _durationSeconds;

    public string? TargetId { get; private set; }

    public bool IsActive => TargetId is not null;

    public double Progress => !IsActive ? 0 : Math.Clamp(_elapsedSeconds / _durationSeconds, 0, 1);

    public void Begin(string targetId, double durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("A mining target ID is required.", nameof(targetId));
        }

        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (TargetId == targetId)
        {
            return;
        }

        TargetId = targetId;
        _durationSeconds = durationSeconds;
        _elapsedSeconds = 0;
    }

    public bool Advance(double deltaSeconds)
    {
        if (!IsActive || deltaSeconds < 0)
        {
            return false;
        }

        _elapsedSeconds += deltaSeconds;
        return _elapsedSeconds >= _durationSeconds;
    }

    public void Cancel()
    {
        TargetId = null;
        _durationSeconds = 0;
        _elapsedSeconds = 0;
    }
}

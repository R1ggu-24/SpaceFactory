namespace SpaceFactory.Presentation.Ship;

/// <summary>
/// Stable presentation identifiers for the two physical ship sockets. Gameplay
/// maps these values to its own endpoint IDs; the renderer intentionally does
/// not own connection or fuel state.
/// </summary>
public enum ShipPowerPortId
{
    A = 0,
    B = 1,
}

/// <summary>
/// Small immutable snapshot used to update a ship socket without coupling the
/// ship renderer to the power-network implementation.
/// </summary>
public readonly record struct ShipPowerPortVisualState(
    bool IsConnected,
    bool IsEnabled,
    float OutputRatio)
{
    public static ShipPowerPortVisualState Free { get; } = new(false, true, 0);

    public ShipPowerPortVisualState Normalized() => this with
    {
        OutputRatio = float.IsFinite(OutputRatio) ? Math.Clamp(OutputRatio, 0, 1) : 0,
    };
}

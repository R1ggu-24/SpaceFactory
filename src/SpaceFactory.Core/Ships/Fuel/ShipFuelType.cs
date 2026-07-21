namespace SpaceFactory.Core.Ships.Fuel;

/// <summary>
/// Identifies the single propellant currently contained in the ship tank.
/// The persisted legacy fuel maps to <see cref="HighPerformance"/>, while new
/// games explicitly start with <see cref="Standard"/> fuel.
/// </summary>
public enum ShipFuelType
{
    Standard,
    HighPerformance,
}

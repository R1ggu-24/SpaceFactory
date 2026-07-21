namespace SpaceFactory.Core.World.Generation;

public static class WorldGenerationDefaults
{
    /// <summary>
    /// Linear radius multiplier for buildable large and huge comets. Because radius is the
    /// shared world-space source for rendering, collision, maps, construction and docking,
    /// consumers must not apply an additional presentation-only scale.
    /// </summary>
    public const double LargeCometScaleMultiplier = 1.5;

    public const double FieldSpawnChance = 0.76;

    public const double LoneCometChancePerSector = 0.020;
}

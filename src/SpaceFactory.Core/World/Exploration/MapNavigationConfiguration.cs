namespace SpaceFactory.Core.World.Exploration;

/// <summary>
/// Central gameplay values for active map navigation. Keeping the arrival
/// radius in Core ensures the world map, minimap and HUD all use one rule.
/// </summary>
public static class MapNavigationConfiguration
{
    public const double TargetReachedDistanceWorldUnits = 150;
}

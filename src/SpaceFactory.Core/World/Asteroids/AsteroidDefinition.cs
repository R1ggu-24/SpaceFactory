using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.World.Asteroids;

public enum AsteroidGeology
{
    Carbonaceous,
    Silicate,
    Metallic,
    VolatileRich,
    Radiogenic,
}

public sealed record AsteroidDefinition(
    string Id,
    WorldPosition Position,
    double Radius,
    AsteroidSize Size,
    string Type,
    ItemId ResourceType,
    ulong VisualSeed,
    double RotationRadians,
    double SurfaceRoughness,
    double ElevationVariation,
    IReadOnlyList<AsteroidCrater> Craters,
    AsteroidSurfaceProfile? SurfaceProfile,
    AsteroidGeology Geology = AsteroidGeology.Carbonaceous)
{
    public bool SupportsLanding => SurfaceProfile is not null;
}

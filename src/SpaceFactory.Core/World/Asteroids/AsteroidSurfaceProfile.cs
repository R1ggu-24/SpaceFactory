namespace SpaceFactory.Core.World.Asteroids;

public sealed record AsteroidSurfaceProfile(
    string SurfaceId,
    double TraversableRadius,
    double BuildableRadius,
    int SuggestedLandingZoneCount,
    ulong TerrainSeed,
    ulong ResourceDistributionSeed,
    string PersistenceKey);

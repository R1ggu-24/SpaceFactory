using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Exploration;

public sealed record DiscoveredCometData(
    string Id,
    SectorCoordinate Sector,
    WorldPosition WorldPosition,
    double Radius,
    AsteroidSize Size,
    string Type,
    ulong VisualSeed,
    double RotationRadians,
    bool IsLandable,
    IReadOnlyList<ItemId> DetectedResourceIds,
    long DiscoveryOrder,
    DateTimeOffset DiscoveredAtUtc,
    bool IsVisited,
    bool Exists);

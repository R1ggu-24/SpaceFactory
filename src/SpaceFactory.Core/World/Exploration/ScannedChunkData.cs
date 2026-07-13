using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Exploration;

public sealed record ScannedChunkData(
    SectorCoordinate Coordinate,
    WorldPosition WorldOrigin,
    long WorldSeed,
    ChunkDiscoveryStatus Status,
    long DiscoveryOrder,
    DateTimeOffset DiscoveredAtUtc,
    IReadOnlyList<DiscoveredCometData> Comets,
    IReadOnlyList<DiscoveredResourceData> Resources);

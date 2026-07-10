using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Generation;

public sealed record SectorGenerationRequest(
    WorldSeed Seed,
    SectorCoordinate Coordinate,
    WorldGenerationSettings Settings,
    ulong Salt = 0x5350414345464143UL);

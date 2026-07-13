using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Exploration;

public sealed record MapMarkerData(
    string Id,
    string Name,
    SectorCoordinate Sector,
    WorldPosition WorldPosition,
    string SymbolId,
    string ColorHex,
    long CreationOrder,
    DateTimeOffset CreatedAtUtc,
    bool Exists);

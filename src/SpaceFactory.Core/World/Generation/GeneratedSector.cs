using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Generation;

public sealed record GeneratedSector(
    SectorCoordinate Coordinate,
    IReadOnlyList<AsteroidDefinition> Asteroids,
    string? CometFieldId,
    double CometFieldDensity);

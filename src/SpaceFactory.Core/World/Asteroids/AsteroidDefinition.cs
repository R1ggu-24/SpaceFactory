using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.World.Asteroids;

public sealed record AsteroidDefinition(
    string Id,
    WorldPosition Position,
    double Radius,
    AsteroidSize Size,
    string Type,
    ItemId ResourceType);

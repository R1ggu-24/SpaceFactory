using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Exploration;

public sealed record DiscoveredResourceData(
    string Id,
    string CometId,
    SectorCoordinate Sector,
    ItemId ResourceId,
    WorldPosition WorldPosition,
    double Radius,
    int OriginalAmount,
    ulong VisualSeed,
    bool Exists,
    ResourcePurity Purity = ResourcePurity.Normal,
    bool IsInfinite = false,
    double ExtractionUnitsPerMinute = 0);

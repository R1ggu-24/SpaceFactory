using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Exploration;

namespace SpaceFactory.Presentation.WorldMap;

public readonly record struct MapShipState(
    WorldPosition Position,
    double RotationRadians,
    bool IsInShip);

/// <summary>
/// Immutable UI projection over ExplorationMapService data. It contains no
/// generated content of its own and therefore cannot reveal unknown sectors.
/// </summary>
public sealed record MapViewState(
    IReadOnlyList<ScannedChunkData> Chunks,
    IReadOnlyList<DiscoveredCometData> Comets,
    IReadOnlyList<DiscoveredResourceData> Resources,
    IReadOnlyList<MapMarkerData> Markers,
    int SectorSize,
    IReadOnlyList<WorldPosition> FlightRoute,
    MapShipState Ship,
    string? ActiveTargetCometId,
    string? ActiveTargetMarkerId,
    string? LastDiscoveredCometId)
{
    public static MapViewState Empty { get; } = new(
        [],
        [],
        [],
        [],
        5_000,
        [],
        new MapShipState(new WorldPosition(0, 0), 0, true),
        null,
        null,
        null);

    public DiscoveredCometData? FindComet(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Comets.FirstOrDefault(comet => comet.Id == id && comet.Exists);
    }

    public MapMarkerData? FindMarker(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Markers.FirstOrDefault(marker => marker.Id == id && marker.Exists);
    }

    public WorldPosition? FindActiveTargetPosition() =>
        FindComet(ActiveTargetCometId)?.WorldPosition ??
        FindMarker(ActiveTargetMarkerId)?.WorldPosition;

    public bool HasActiveTarget => FindActiveTargetPosition() is not null;
}

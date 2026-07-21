using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Exploration;

/// <summary>
/// Lightweight fog-of-war state shared by minimap and world map. Unknown
/// sectors are deliberately absent and cannot be queried through this service.
/// </summary>
public sealed class ExplorationMapService
{
    private readonly Dictionary<SectorCoordinate, ScannedChunkData> _chunks = [];
    private readonly Dictionary<string, DiscoveredCometData> _comets = [];
    private readonly Dictionary<string, DiscoveredResourceData> _resources = [];
    private readonly Dictionary<string, MapMarkerData> _markers = [];
    private HashSet<SectorCoordinate> _activeCoordinates = [];
    private HashSet<SectorCoordinate> _currentlyScannedCoordinates = [];
    private readonly Func<DateTimeOffset> _utcNow;
    private long _nextDiscoveryOrder = 1;
    private bool _chunkStatesInitialized;

    public ExplorationMapService(int sectorSize, long worldSeed, Func<DateTimeOffset>? utcNow = null)
    {
        if (sectorSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorSize));
        }

        SectorSize = sectorSize;
        WorldSeed = worldSeed;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public event Action? Changed;

    public event Action<string?>? TargetChanged;

    public int SectorSize { get; }

    public long WorldSeed { get; }

    public string? SelectedTargetCometId { get; private set; }

    public string? SelectedTargetMarkerId { get; private set; }

    public IReadOnlyList<ScannedChunkData> ScannedChunks =>
        _chunks.Values.OrderBy(chunk => chunk.DiscoveryOrder).ToArray();

    public IReadOnlyList<DiscoveredCometData> DiscoveredComets =>
        _comets.Values.Where(comet => comet.Exists).OrderBy(comet => comet.DiscoveryOrder).ToArray();

    public IReadOnlyList<DiscoveredResourceData> DiscoveredResources =>
        _resources.Values.Where(resource => resource.Exists).ToArray();

    public IReadOnlyList<MapMarkerData> Markers =>
        _markers.Values.Where(marker => marker.Exists).OrderBy(marker => marker.CreationOrder).ToArray();

    public DiscoveredCometData? LastDiscoveredComet =>
        _comets.Values.Where(comet => comet.Exists).MaxBy(comet => comet.DiscoveryOrder);

    public DiscoveredCometData? SelectedTarget =>
        SelectedTargetCometId is not null && _comets.TryGetValue(SelectedTargetCometId, out var comet) && comet.Exists
            ? comet
            : null;

    public MapMarkerData? SelectedMarkerTarget =>
        SelectedTargetMarkerId is not null &&
        _markers.TryGetValue(SelectedTargetMarkerId, out var marker) && marker.Exists
            ? marker
            : null;

    public ScannedChunkData Scan(GeneratedSectorContent content)
    {
        var coordinate = content.Sector.Coordinate;
        if (_chunks.TryGetValue(coordinate, out var existing))
        {
            return existing;
        }

        var discoveredAt = _utcNow();
        var worldOrigin = ToWorldOrigin(coordinate);
        var discoveredComets = new List<DiscoveredCometData>(content.Sector.Asteroids.Count);
        var discoveredResources = new List<DiscoveredResourceData>();

        foreach (var comet in content.Sector.Asteroids)
        {
            var deposits = content.GetResourceDeposits(comet.Id);
            var worldPosition = new WorldPosition(
                worldOrigin.X + comet.Position.X,
                worldOrigin.Y + comet.Position.Y);
            var mapComet = new DiscoveredCometData(
                comet.Id,
                coordinate,
                worldPosition,
                comet.Radius,
                comet.Size,
                comet.Type,
                comet.VisualSeed,
                comet.RotationRadians,
                comet.SupportsLanding,
                deposits.Select(deposit => deposit.ResourceId).Distinct().ToArray(),
                NextDiscoveryOrder(),
                discoveredAt,
                IsVisited: false,
                Exists: true);
            discoveredComets.Add(mapComet);
            _comets.Add(mapComet.Id, mapComet);

            foreach (var deposit in deposits)
            {
                var localX = deposit.NormalizedPosition.X * comet.Radius;
                var localY = deposit.NormalizedPosition.Y * comet.Radius;
                var cosine = Math.Cos(comet.RotationRadians);
                var sine = Math.Sin(comet.RotationRadians);
                var rotatedX = (localX * cosine) - (localY * sine);
                var rotatedY = (localX * sine) + (localY * cosine);
                var mapResource = new DiscoveredResourceData(
                    deposit.Id,
                    deposit.CometId,
                    coordinate,
                    deposit.ResourceId,
                    new WorldPosition(
                        worldPosition.X + rotatedX,
                        worldPosition.Y + rotatedY),
                    deposit.GetRadiusWorldUnits(comet.Radius),
                    deposit.OriginalAmount,
                    deposit.VisualSeed,
                    Exists: true,
                    deposit.Purity,
                    deposit.IsInfinite,
                    deposit.EffectiveExtractionUnitsPerMinute);
                discoveredResources.Add(mapResource);
                _resources.Add(mapResource.Id, mapResource);
            }
        }

        var chunk = new ScannedChunkData(
            coordinate,
            worldOrigin,
            WorldSeed,
            ChunkDiscoveryStatus.Scanned,
            NextDiscoveryOrder(),
            discoveredAt,
            discoveredComets,
            discoveredResources);
        _chunks.Add(coordinate, chunk);
        Changed?.Invoke();
        return chunk;
    }

    public ChunkDiscoveryStatus GetChunkStatus(SectorCoordinate coordinate) =>
        _chunks.TryGetValue(coordinate, out var chunk) ? chunk.Status : ChunkDiscoveryStatus.Unknown;

    public bool TryGetChunk(SectorCoordinate coordinate, out ScannedChunkData chunk) =>
        _chunks.TryGetValue(coordinate, out chunk!);

    public IReadOnlyList<ScannedChunkData> GetScannedChunksAround(
        WorldPosition center,
        double radius)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var minimumX = (int)Math.Floor((center.X - radius) / SectorSize);
        var maximumX = (int)Math.Floor((center.X + radius) / SectorSize);
        var minimumY = (int)Math.Floor((center.Y - radius) / SectorSize);
        var maximumY = (int)Math.Floor((center.Y + radius) / SectorSize);
        var result = new List<ScannedChunkData>();
        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var coordinate = new SectorCoordinate(x, y);
                if (_chunks.TryGetValue(coordinate, out var chunk) &&
                    ChunkIntersectsRadius(chunk, center, radius))
                {
                    result.Add(chunk);
                }
            }
        }

        return result.OrderBy(chunk => chunk.DiscoveryOrder).ToArray();
    }

    public void SetActiveChunks(IEnumerable<SectorCoordinate> activeCoordinates)
    {
        var active = activeCoordinates.ToHashSet();
        var affected = _activeCoordinates.Concat(active).ToHashSet();
        var changed = false;
        foreach (var coordinate in affected)
        {
            if (!_chunks.TryGetValue(coordinate, out var chunk))
            {
                continue;
            }

            var status = active.Contains(coordinate)
                ? ChunkDiscoveryStatus.ActiveLoaded
                : chunk.Status == ChunkDiscoveryStatus.ActiveLoaded
                    ? _currentlyScannedCoordinates.Contains(coordinate)
                        ? ChunkDiscoveryStatus.Scanned
                        : ChunkDiscoveryStatus.UnloadedDiscovered
                    : chunk.Status;
            if (status == chunk.Status)
            {
                continue;
            }

            _chunks[coordinate] = chunk with { Status = status };
            changed = true;
        }

        _activeCoordinates = active;

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void SetChunkStates(
        IEnumerable<SectorCoordinate> activeCoordinates,
        IEnumerable<SectorCoordinate> currentlyScannedCoordinates)
    {
        var active = activeCoordinates.ToHashSet();
        var scanned = currentlyScannedCoordinates.ToHashSet();
        var affected = _chunkStatesInitialized
            ? _activeCoordinates
                .Concat(_currentlyScannedCoordinates)
                .Concat(active)
                .Concat(scanned)
                .ToHashSet()
            : _chunks.Keys.ToHashSet();
        var changed = false;
        foreach (var coordinate in affected)
        {
            if (!_chunks.TryGetValue(coordinate, out var chunk))
            {
                continue;
            }

            var status = active.Contains(coordinate)
                ? ChunkDiscoveryStatus.ActiveLoaded
                : scanned.Contains(coordinate)
                    ? ChunkDiscoveryStatus.Scanned
                    : ChunkDiscoveryStatus.UnloadedDiscovered;
            if (status == chunk.Status)
            {
                continue;
            }

            _chunks[coordinate] = chunk with { Status = status };
            changed = true;
        }

        _activeCoordinates = active;
        _currentlyScannedCoordinates = scanned;
        _chunkStatesInitialized = true;

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public bool MarkCometVisited(string cometId)
    {
        if (!_comets.TryGetValue(cometId, out var comet) || !comet.Exists || comet.IsVisited)
        {
            return false;
        }

        var updated = comet with { IsVisited = true };
        _comets[cometId] = updated;
        ReplaceChunkComet(updated);
        Changed?.Invoke();
        return true;
    }

    public bool MarkCometMissing(string cometId)
    {
        if (!_comets.TryGetValue(cometId, out var comet) || !comet.Exists)
        {
            return false;
        }

        var updated = comet with { Exists = false };
        _comets[cometId] = updated;
        ReplaceChunkComet(updated);
        if (SelectedTargetCometId == cometId)
        {
            SelectedTargetCometId = null;
            TargetChanged?.Invoke(null);
        }

        Changed?.Invoke();
        return true;
    }

    public bool MarkResourceMissing(string resourceId)
    {
        if (!_resources.TryGetValue(resourceId, out var resource) || !resource.Exists)
        {
            return false;
        }

        var updated = resource with { Exists = false };
        _resources[resourceId] = updated;
        if (_chunks.TryGetValue(resource.Sector, out var chunk))
        {
            _chunks[resource.Sector] = chunk with
            {
                Resources = chunk.Resources.Select(item => item.Id == resourceId ? updated : item).ToArray(),
            };
        }

        var resourceStillDetected = _resources.Values.Any(item =>
            item.Exists &&
            item.CometId == resource.CometId &&
            item.ResourceId == resource.ResourceId);
        if (!resourceStillDetected && _comets.TryGetValue(resource.CometId, out var comet))
        {
            var updatedComet = comet with
            {
                DetectedResourceIds = comet.DetectedResourceIds
                    .Where(itemId => itemId != resource.ResourceId)
                    .ToArray(),
            };
            _comets[resource.CometId] = updatedComet;
            ReplaceChunkComet(updatedComet);
        }

        Changed?.Invoke();
        return true;
    }

    public bool SelectTarget(string? cometId)
    {
        if (cometId is not null &&
            (!_comets.TryGetValue(cometId, out var comet) || !comet.Exists))
        {
            return false;
        }

        if (SelectedTargetCometId == cometId && SelectedTargetMarkerId is null)
        {
            return true;
        }

        SelectedTargetCometId = cometId;
        SelectedTargetMarkerId = null;
        TargetChanged?.Invoke(cometId);
        Changed?.Invoke();
        return true;
    }

    public bool ToggleTarget(string cometId) =>
        SelectTarget(SelectedTargetCometId == cometId ? null : cometId);

    public bool SelectMarkerTarget(string? markerId)
    {
        if (markerId is not null &&
            (!_markers.TryGetValue(markerId, out var marker) || !marker.Exists))
        {
            return false;
        }

        if (SelectedTargetMarkerId == markerId && SelectedTargetCometId is null)
        {
            return true;
        }

        SelectedTargetCometId = null;
        SelectedTargetMarkerId = markerId;
        TargetChanged?.Invoke(markerId);
        Changed?.Invoke();
        return true;
    }

    public bool ToggleMarkerTarget(string markerId) =>
        SelectMarkerTarget(SelectedTargetMarkerId == markerId ? null : markerId);

    /// <summary>
    /// Ends only the active navigation when the ship enters the configured
    /// arrival radius. Discovery and marker data remain untouched.
    /// </summary>
    public bool TryCompleteNavigation(WorldPosition shipPosition)
    {
        var cometTarget = SelectedTarget;
        var markerTarget = SelectedMarkerTarget;
        if (cometTarget is null && markerTarget is null)
        {
            return false;
        }

        var targetPosition = cometTarget?.WorldPosition ?? markerTarget!.WorldPosition;
        var deltaX = shipPosition.X - targetPosition.X;
        var deltaY = shipPosition.Y - targetPosition.Y;
        // A comet's navigable destination is its surface, not its unreachable
        // centre. A custom marker is a point target and therefore has no radius.
        var reachedDistance = (cometTarget?.Radius ?? 0) +
                              MapNavigationConfiguration.TargetReachedDistanceWorldUnits;
        if ((deltaX * deltaX) + (deltaY * deltaY) >= reachedDistance * reachedDistance)
        {
            return false;
        }

        return SelectTarget(null);
    }

    public bool IsWorldPositionDiscovered(WorldPosition position) =>
        _chunks.ContainsKey(ToSectorCoordinate(position));

    public bool TryAddMarker(
        string id,
        string name,
        WorldPosition position,
        string symbolId,
        string colorHex,
        out MapMarkerData marker)
    {
        marker = null!;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(symbolId) || !IsValidColor(colorHex) ||
            _markers.ContainsKey(id))
        {
            return false;
        }

        var sector = ToSectorCoordinate(position);
        if (!_chunks.ContainsKey(sector))
        {
            return false;
        }

        marker = new MapMarkerData(
            id,
            name.Trim(),
            sector,
            position,
            symbolId,
            colorHex.ToUpperInvariant(),
            NextDiscoveryOrder(),
            _utcNow(),
            Exists: true);
        _markers.Add(id, marker);
        Changed?.Invoke();
        return true;
    }

    public bool RemoveMarker(string markerId)
    {
        if (!_markers.TryGetValue(markerId, out var marker) || !marker.Exists)
        {
            return false;
        }

        _markers[markerId] = marker with { Exists = false };
        if (SelectedTargetMarkerId == markerId)
        {
            SelectedTargetMarkerId = null;
            TargetChanged?.Invoke(null);
        }

        Changed?.Invoke();
        return true;
    }

    private void ReplaceChunkComet(DiscoveredCometData comet)
    {
        if (_chunks.TryGetValue(comet.Sector, out var chunk))
        {
            _chunks[comet.Sector] = chunk with
            {
                Comets = chunk.Comets.Select(item => item.Id == comet.Id ? comet : item).ToArray(),
            };
        }
    }

    private WorldPosition ToWorldOrigin(SectorCoordinate coordinate) =>
        new((double)coordinate.X * SectorSize, (double)coordinate.Y * SectorSize);

    private SectorCoordinate ToSectorCoordinate(WorldPosition position) =>
        new(
            (int)Math.Floor(position.X / SectorSize),
            (int)Math.Floor(position.Y / SectorSize));

    private long NextDiscoveryOrder() => _nextDiscoveryOrder++;

    private bool ChunkIntersectsRadius(
        ScannedChunkData chunk,
        WorldPosition center,
        double radius)
    {
        var closestX = Math.Clamp(center.X, chunk.WorldOrigin.X, chunk.WorldOrigin.X + SectorSize);
        var closestY = Math.Clamp(center.Y, chunk.WorldOrigin.Y, chunk.WorldOrigin.Y + SectorSize);
        var deltaX = center.X - closestX;
        var deltaY = center.Y - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }

    private static bool IsValidColor(string colorHex) =>
        colorHex.Length == 7 && colorHex[0] == '#' &&
        colorHex.Skip(1).All(character => Uri.IsHexDigit(character));
}

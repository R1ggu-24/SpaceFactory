using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Exploration;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.Tests;

public sealed class ExplorationMapServiceTests
{
    private static readonly DateTimeOffset DiscoveryTime =
        new(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Scan_UsesPreparedWorldAndResourceData()
    {
        var service = CreateService();
        var content = CreateContent(new SectorCoordinate(2, -1), "comet:a", "deposit:a");

        var chunk = service.Scan(content);

        Assert.Equal(ChunkDiscoveryStatus.Scanned, chunk.Status);
        Assert.Equal(new WorldPosition(10_000, -5_000), chunk.WorldOrigin);
        var comet = Assert.Single(chunk.Comets);
        Assert.Equal(new WorldPosition(10_120, -4_660), comet.WorldPosition);
        Assert.Equal(AsteroidSize.Large, comet.Size);
        Assert.True(comet.IsLandable);
        Assert.Equal([new ItemId("titanium")], comet.DetectedResourceIds);

        var resource = Assert.Single(chunk.Resources);
        Assert.Equal(10_191.66, resource.WorldPosition.X, 2);
        Assert.Equal(-4_651.94, resource.WorldPosition.Y, 2);
        Assert.Equal(36, resource.Radius);
        Assert.Same(chunk, service.Scan(content));
        Assert.Single(service.ScannedChunks);
        Assert.Single(service.DiscoveredComets);
    }

    [Fact]
    public void SetActiveChunks_TracksActiveAndUnloadedDiscoveredStates()
    {
        var service = CreateService();
        var first = new SectorCoordinate(0, 0);
        var second = new SectorCoordinate(1, 0);
        service.Scan(CreateContent(first, "comet:first", "deposit:first"));
        service.Scan(CreateContent(second, "comet:second", "deposit:second"));

        service.SetActiveChunks([first]);

        Assert.Equal(ChunkDiscoveryStatus.ActiveLoaded, service.GetChunkStatus(first));
        Assert.Equal(ChunkDiscoveryStatus.Scanned, service.GetChunkStatus(second));

        service.SetActiveChunks([second]);

        Assert.Equal(ChunkDiscoveryStatus.UnloadedDiscovered, service.GetChunkStatus(first));
        Assert.Equal(ChunkDiscoveryStatus.ActiveLoaded, service.GetChunkStatus(second));
        Assert.Equal(ChunkDiscoveryStatus.Unknown, service.GetChunkStatus(new SectorCoordinate(9, 9)));
    }

    [Fact]
    public void SetChunkStates_DistinguishesCurrentScanFromUnloadedDiscovery()
    {
        var service = CreateService();
        var active = new SectorCoordinate(0, 0);
        var scanned = new SectorCoordinate(1, 0);
        var old = new SectorCoordinate(2, 0);
        service.Scan(CreateContent(active, "comet:active", "deposit:active"));
        service.Scan(CreateContent(scanned, "comet:scanned", "deposit:scanned"));
        service.Scan(CreateContent(old, "comet:old", "deposit:old"));

        service.SetChunkStates([active], [active, scanned]);

        Assert.Equal(ChunkDiscoveryStatus.ActiveLoaded, service.GetChunkStatus(active));
        Assert.Equal(ChunkDiscoveryStatus.Scanned, service.GetChunkStatus(scanned));
        Assert.Equal(ChunkDiscoveryStatus.UnloadedDiscovered, service.GetChunkStatus(old));
    }

    [Fact]
    public void TargetSelection_AllowsOnlyExistingDiscoveredComets()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:a", "deposit:a"));
        var targetEvents = new List<string?>();
        service.TargetChanged += targetEvents.Add;

        Assert.False(service.SelectTarget("comet:unknown"));
        Assert.True(service.SelectTarget("comet:a"));
        Assert.Equal("comet:a", service.SelectedTargetCometId);
        Assert.Equal("comet:a", service.SelectedTarget?.Id);

        Assert.True(service.MarkCometMissing("comet:a"));

        Assert.Null(service.SelectedTargetCometId);
        Assert.Null(service.SelectedTarget);
        Assert.Equal(["comet:a", null], targetEvents);
    }

    [Fact]
    public void Navigation_AutomaticallyEndsOnlyInsideCentralArrivalRadius()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:a", "deposit:a"));
        Assert.True(service.TryAddMarker(
            "base",
            "Basis",
            new WorldPosition(20, 20),
            "base",
            "#33CCFF",
            out _));
        Assert.True(service.SelectTarget("comet:a"));
        var target = Assert.IsType<DiscoveredCometData>(service.SelectedTarget);
        var targetEvents = new List<string?>();
        service.TargetChanged += targetEvents.Add;

        var exactlyAtBoundary = new WorldPosition(
            target.WorldPosition.X + target.Radius +
            MapNavigationConfiguration.TargetReachedDistanceWorldUnits,
            target.WorldPosition.Y);
        Assert.False(service.TryCompleteNavigation(exactlyAtBoundary));
        Assert.Equal("comet:a", service.SelectedTargetCometId);

        var insideBoundary = new WorldPosition(
            target.WorldPosition.X + target.Radius +
            MapNavigationConfiguration.TargetReachedDistanceWorldUnits - 0.01,
            target.WorldPosition.Y);
        Assert.True(service.TryCompleteNavigation(insideBoundary));

        Assert.Null(service.SelectedTargetCometId);
        Assert.Equal([null], targetEvents);
        Assert.Contains(service.DiscoveredComets, comet => comet.Id == "comet:a");
        Assert.Contains(service.Markers, marker => marker.Id == "base");
        Assert.False(service.TryCompleteNavigation(target.WorldPosition));
    }

    [Fact]
    public void MarkerNavigation_AutomaticallyEndsInsideCentralArrivalRadius()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:a", "deposit:a"));
        var markerPosition = new WorldPosition(800, 900);
        Assert.True(service.TryAddMarker(
            "marker:destination",
            "Zielpunkt",
            markerPosition,
            "pin",
            "#33CCFF",
            out _));
        Assert.False(service.SelectMarkerTarget("marker:unknown"));

        var targetEvents = new List<string?>();
        service.TargetChanged += targetEvents.Add;
        Assert.True(service.SelectMarkerTarget("marker:destination"));
        Assert.Equal("marker:destination", service.SelectedTargetMarkerId);
        Assert.Equal("marker:destination", service.SelectedMarkerTarget?.Id);
        Assert.Null(service.SelectedTargetCometId);

        var exactlyAtBoundary = markerPosition with
        {
            X = markerPosition.X + MapNavigationConfiguration.TargetReachedDistanceWorldUnits,
        };
        Assert.False(service.TryCompleteNavigation(exactlyAtBoundary));
        Assert.Equal("marker:destination", service.SelectedTargetMarkerId);

        var insideBoundary = exactlyAtBoundary with { X = exactlyAtBoundary.X - 0.01 };
        Assert.True(service.TryCompleteNavigation(insideBoundary));

        Assert.Null(service.SelectedTargetMarkerId);
        Assert.Null(service.SelectedMarkerTarget);
        Assert.Equal(["marker:destination", null], targetEvents);
        Assert.Contains(service.Markers, marker => marker.Id == "marker:destination");
    }

    [Fact]
    public void NavigationTarget_ExclusivelySwitchesBetweenCometAndMarker()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:a", "deposit:a"));
        Assert.True(service.TryAddMarker(
            "marker:destination",
            "Zielpunkt",
            new WorldPosition(800, 900),
            "pin",
            "#33CCFF",
            out _));

        Assert.True(service.SelectTarget("comet:a"));
        Assert.True(service.SelectMarkerTarget("marker:destination"));
        Assert.Null(service.SelectedTargetCometId);
        Assert.Equal("marker:destination", service.SelectedTargetMarkerId);

        Assert.True(service.SelectTarget("comet:a"));
        Assert.Equal("comet:a", service.SelectedTargetCometId);
        Assert.Null(service.SelectedTargetMarkerId);

        Assert.True(service.SelectMarkerTarget("marker:destination"));
        Assert.True(service.RemoveMarker("marker:destination"));
        Assert.Null(service.SelectedTargetMarkerId);
        Assert.Empty(service.Markers);
    }

    [Fact]
    public void MissingResource_RemovesDepletedTypeFromCometSummary()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:a", "deposit:a"));

        Assert.True(service.MarkResourceMissing("deposit:a"));

        Assert.Empty(service.DiscoveredResources);
        Assert.Empty(Assert.Single(service.DiscoveredComets).DetectedResourceIds);
    }

    [Fact]
    public void VisitAndDiscoveryOrder_RemainAvailableAfterChunkUnloads()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:first", "deposit:first"));
        service.Scan(CreateContent(new SectorCoordinate(1, 0), "comet:last", "deposit:last"));

        Assert.True(service.MarkCometVisited("comet:first"));
        service.SetActiveChunks([]);

        var first = service.DiscoveredComets.Single(comet => comet.Id == "comet:first");
        Assert.True(first.IsVisited);
        Assert.Equal("comet:last", service.LastDiscoveredComet?.Id);
        Assert.Equal(DiscoveryTime, first.DiscoveredAtUtc);
    }

    [Fact]
    public void CustomMarkers_CanOnlyBeCreatedInsideScannedChunks()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(-1, -1), "comet:a", "deposit:a"));

        Assert.False(service.TryAddMarker(
            "unknown",
            "Nicht entdeckt",
            new WorldPosition(25, 25),
            "pin",
            "#33CCFF",
            out _));
        Assert.True(service.TryAddMarker(
            "known",
            "Basis",
            new WorldPosition(-25, -25),
            "base",
            "#33ccff",
            out var marker));
        Assert.Equal(new SectorCoordinate(-1, -1), marker.Sector);
        Assert.Equal("#33CCFF", marker.ColorHex);
        Assert.True(service.RemoveMarker("known"));
        Assert.Empty(service.Markers);
    }

    [Fact]
    public void GetScannedChunksAround_NeverReturnsUnknownWorldData()
    {
        var service = CreateService();
        service.Scan(CreateContent(new SectorCoordinate(0, 0), "comet:near", "deposit:near"));
        service.Scan(CreateContent(new SectorCoordinate(5, 5), "comet:far", "deposit:far"));

        var result = service.GetScannedChunksAround(new WorldPosition(2_500, 2_500), 4_000);

        Assert.Single(result);
        Assert.Equal(new SectorCoordinate(0, 0), result[0].Coordinate);
    }

    private static ExplorationMapService CreateService() =>
        new(5_000, 741029384, () => DiscoveryTime);

    private static GeneratedSectorContent CreateContent(
        SectorCoordinate coordinate,
        string cometId,
        string depositId)
    {
        var comet = new AsteroidDefinition(
            cometId,
            new WorldPosition(120, 340),
            300,
            AsteroidSize.Large,
            "rocky",
            new ItemId("iron_ore"),
            42,
            0.7,
            0.5,
            0.4,
            [],
            new AsteroidSurfaceProfile("surface", 220, 180, 2, 91, 92, "test"));
        var deposit = new ResourceDepositDefinition(
            depositId,
            cometId,
            new ItemId("titanium"),
            new WorldPosition(0.2, -0.13333333333333333),
            0.12,
            180,
            2.5,
            77);
        return new GeneratedSectorContent(
            new GeneratedSector(coordinate, [comet], "field:test", 0.5),
            new Dictionary<string, IReadOnlyList<ResourceDepositDefinition>>
            {
                [cometId] = [deposit],
            });
    }
}

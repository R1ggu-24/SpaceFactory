using System.Text.Json;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.Tests;

public sealed class WorldGenerationTests
{
    private const int SectorSize = 5000;
    private const double MinimumSpacing = 180;
    private readonly DeterministicWorldGenerator _generator = new();

    [Fact]
    public void Generate_SameInput_ReturnsIdenticalSector()
    {
        var request = Request(741029384, 2, -3);

        var first = _generator.Generate(request);
        var second = _generator.Generate(request);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Generate_DifferentSeed_ChangesRegion()
    {
        var first = GenerateRegion(1, -15, 15);
        var second = GenerateRegion(2, -15, 15);

        Assert.NotEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Generate_DifferentCoordinate_ChangesSector()
    {
        var sectors = Enumerable.Range(-5, 11)
            .Select(x => _generator.Generate(Request(1, x, 0)))
            .Select(sector => JsonSerializer.Serialize(sector))
            .Distinct()
            .ToArray();

        Assert.True(sectors.Length > 1);
    }

    [Fact]
    public void Generate_ProducesValidConfiguredComets()
    {
        var sectors = GenerateRegion(31, -5, 5);

        Assert.All(sectors, sector => Assert.InRange(sector.Asteroids.Count, 0, 9));
        Assert.All(sectors.SelectMany(sector => sector.Asteroids), comet =>
        {
            Assert.True(Enum.IsDefined(comet.Size));
            Assert.InRange(comet.Position.X, 0, SectorSize);
            Assert.InRange(comet.Position.Y, 0, SectorSize);
            Assert.InRange(comet.Radius, 40, WorldGenerationSettings.MaximumSupportedRadius);
            Assert.InRange(comet.SurfaceRoughness, 0.18, 0.7);
            Assert.InRange(comet.ElevationVariation, 0.12, 0.7);
            Assert.NotEmpty(comet.Craters);
            Assert.All(comet.Craters, crater => Assert.True(crater.RadiusFactor > 0));
        });
    }

    [Fact]
    public void Generate_CometsDoNotOverlapAcrossSectorBoundaries()
    {
        var comets = GenerateRegion(741029384, -4, 4)
            .SelectMany(ToGlobalComets)
            .ToArray();

        for (var firstIndex = 0; firstIndex < comets.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < comets.Length; secondIndex++)
            {
                var first = comets[firstIndex];
                var second = comets[secondIndex];
                var distance = Math.Sqrt(
                    Math.Pow(first.X - second.X, 2) +
                    Math.Pow(first.Y - second.Y, 2));
                Assert.True(distance >= first.Radius + second.Radius + MinimumSpacing - 0.001,
                    $"{first.Id} overlaps {second.Id}.");
            }
        }
    }

    [Fact]
    public void Generate_LeavesStartingAreaClear()
    {
        var sector = _generator.Generate(Request(741029384, 0, 0));

        Assert.All(sector.Asteroids, comet =>
        {
            var distance = Math.Sqrt(
                Math.Pow(comet.Position.X - 2500, 2) +
                Math.Pow(comet.Position.Y - 2500, 2));
            Assert.True(distance >= 900 + comet.Radius);
        });
    }

    [Fact]
    public void Generate_ProvidesDiscoverableFieldNearStartingSector()
    {
        var nearbySectors = new List<GeneratedSector>();
        for (var y = -1; y <= 1; y++)
        {
            for (var x = 1; x <= 4; x++)
            {
                nearbySectors.Add(_generator.Generate(Request(741029384, x, y)));
            }
        }

        Assert.Contains(nearbySectors, sector => sector.CometFieldId == "field:starting-discovery");
        Assert.True(nearbySectors.Sum(sector => sector.Asteroids.Count) >= 3);
    }

    [Fact]
    public void Generate_CreatesSparseAndDenseRegions()
    {
        var counts = GenerateRegion(741029384, -12, 12)
            .Select(sector => sector.Asteroids.Count)
            .ToArray();

        Assert.Contains(0, counts);
        Assert.True(counts.Max() >= 4);
        Assert.True(counts.Distinct().Count() >= 4);
        Assert.InRange(counts.Average(), 0.08, 1.2);
        Assert.True(counts.Count(count => count == 0) > counts.Length * 0.7);
    }

    [Fact]
    public void Generate_CreatesLongEmptyFlightSectionsBetweenFields()
    {
        var longestEmptyRun = 0;
        var discoveredFields = new HashSet<string>();
        for (var y = -20; y <= 20; y++)
        {
            var currentEmptyRun = 0;
            foreach (var sector in Enumerable.Range(-70, 141)
                .Select(x => _generator.Generate(Request(741029384, x, y))))
            {
                currentEmptyRun = sector.Asteroids.Count == 0 ? currentEmptyRun + 1 : 0;
                longestEmptyRun = Math.Max(longestEmptyRun, currentEmptyRun);
                if (sector.CometFieldId is not null)
                {
                    discoveredFields.Add(sector.CometFieldId);
                }
            }
        }

        Assert.True(longestEmptyRun >= 10);
        Assert.True(discoveredFields.Count >= 2);
    }

    [Fact]
    public void Generate_NormalFieldsUseExpectedSizeDistribution()
    {
        var fieldComets = GenerateRegion(741029384, -20, 20)
            .Where(sector => sector.CometFieldId is not null)
            .SelectMany(sector => sector.Asteroids)
            .ToArray();
        var normalComets = fieldComets.Where(comet => comet.Size != AsteroidSize.Huge).ToArray();

        Assert.True(normalComets.Length >= 100);
        var smallShare = normalComets.Count(comet => comet.Size is AsteroidSize.Tiny or AsteroidSize.Small) /
            (double)normalComets.Length;
        var mediumShare = normalComets.Count(comet => comet.Size == AsteroidSize.Medium) /
            (double)normalComets.Length;
        var largeShare = normalComets.Count(comet => comet.Size == AsteroidSize.Large) /
            (double)normalComets.Length;
        var hugeShareInFields = fieldComets.Count(comet => comet.Size == AsteroidSize.Huge) /
            (double)fieldComets.Length;

        Assert.InRange(smallShare, 0.55, 0.75);
        Assert.InRange(mediumShare, 0.15, 0.35);
        Assert.InRange(largeShare, 0.04, 0.16);
        Assert.InRange(hugeShareInFields, 0, 0.01);
    }

    [Fact]
    public void Generate_DifferentFieldsKeepFreeSectorsBetweenThem()
    {
        var fieldSectors = GenerateRegion(741029384, -35, 35)
            .Where(sector => sector.CometFieldId is not null)
            .ToArray();

        Assert.True(fieldSectors.Select(sector => sector.CometFieldId).Distinct().Count() >= 3);
        for (var firstIndex = 0; firstIndex < fieldSectors.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < fieldSectors.Length; secondIndex++)
            {
                var first = fieldSectors[firstIndex];
                var second = fieldSectors[secondIndex];
                if (first.CometFieldId == second.CometFieldId)
                {
                    continue;
                }

                var distance = Math.Sqrt(
                    Math.Pow(first.Coordinate.X - second.Coordinate.X, 2) +
                    Math.Pow(first.Coordinate.Y - second.Coordinate.Y, 2));
                Assert.True(distance >= 2);
            }
        }
    }

    [Fact]
    public void Generate_ExtremeCometsRespectSectorSeparation()
    {
        var extremeSectors = GenerateRegion(741029384, -20, 20)
            .Where(sector => sector.Asteroids.Any(comet => comet.Size == AsteroidSize.Huge))
            .Select(sector => sector.Coordinate)
            .ToArray();

        Assert.NotEmpty(extremeSectors);
        for (var firstIndex = 0; firstIndex < extremeSectors.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < extremeSectors.Length; secondIndex++)
            {
                var first = extremeSectors[firstIndex];
                var second = extremeSectors[secondIndex];
                var sectorDistance = Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
                Assert.True(sectorDistance > 6);
            }
        }
    }

    [Fact]
    public void Generate_LandableCometsHaveStableSurfaceMetadata()
    {
        var comets = GenerateRegion(741029384, -15, 15)
            .SelectMany(sector => sector.Asteroids)
            .ToArray();
        var landable = comets.Where(comet => comet.SupportsLanding).ToArray();

        Assert.NotEmpty(landable);
        Assert.All(landable, comet =>
        {
            Assert.True(comet.Size is AsteroidSize.Large or AsteroidSize.Huge);
            var profile = Assert.IsType<AsteroidSurfaceProfile>(comet.SurfaceProfile);
            Assert.True(profile.BuildableRadius > 0);
            Assert.True(profile.TraversableRadius > profile.BuildableRadius);
            Assert.StartsWith("sectors/", profile.PersistenceKey);
        });

        var hugeCount = comets.Count(comet => comet.Size == AsteroidSize.Huge);
        var normalCount = comets.Count(comet => comet.Size is AsteroidSize.Small or AsteroidSize.Medium);
        Assert.True(hugeCount > 0);
        Assert.True(hugeCount < normalCount);
        Assert.All(comets.Where(comet => comet.Size == AsteroidSize.Huge),
            comet =>
            {
                Assert.True(comet.SupportsLanding);
                Assert.InRange(comet.Radius, 1300, 1800);
                Assert.True(comet.SurfaceProfile!.BuildableRadius >= 845);
            });
    }

    private IReadOnlyList<GeneratedSector> GenerateRegion(long seed, int minimum, int maximum)
    {
        var sectors = new List<GeneratedSector>();
        for (var y = minimum; y <= maximum; y++)
        {
            for (var x = minimum; x <= maximum; x++)
            {
                sectors.Add(_generator.Generate(Request(seed, x, y)));
            }
        }

        return sectors;
    }

    private static IEnumerable<GlobalComet> ToGlobalComets(GeneratedSector sector) =>
        sector.Asteroids.Select(comet => new GlobalComet(
            comet.Id,
            (sector.Coordinate.X * SectorSize) + comet.Position.X,
            (sector.Coordinate.Y * SectorSize) + comet.Position.Y,
            comet.Radius));

    internal static SectorGenerationRequest Request(long seed, int x, int y) => new(
        new WorldSeed(seed),
        new SectorCoordinate(x, y),
        new WorldGenerationSettings(
            SectorSize,
            0,
            8,
            new Dictionary<AsteroidSize, double>
            {
                [AsteroidSize.Tiny] = 0.20,
                [AsteroidSize.Small] = 0.45,
                [AsteroidSize.Medium] = 0.25,
                [AsteroidSize.Large] = 0.09,
                [AsteroidSize.Huge] = 0.01,
            },
            MinimumCometSpacing: MinimumSpacing,
            FieldCellSizeInSectors: 16,
            FieldSpawnChance: 0.68,
            MinimumFieldRadiusInSectors: 1.5,
            MaximumFieldRadiusInSectors: 3.8,
            MinimumFieldGapInSectors: 3,
            MaximumFieldGapInSectors: 7,
            LoneCometChancePerSector: 0.018,
            ExtremeCometSeparationInSectors: 6,
            EnableStartingDiscoveryField: true,
            StartingFieldCenterSectorX: 2.75,
            StartingFieldCenterSectorY: 0.5,
            StartingFieldMajorRadiusInSectors: 2.2,
            StartingFieldMinorRadiusInSectors: 1.45,
            StartingFieldIntensity: 0.85,
            StartingSafeRadius: 900,
            StartingSafeCenterX: 2500,
            StartingSafeCenterY: 2500));

    private sealed record GlobalComet(string Id, double X, double Y, double Radius);
}

using System.Text.Json;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class ResourceGenerationTests
{
    private readonly ResourceDepositGenerator _generator = new();
    private readonly IReadOnlyList<ResourceDefinition> _resources = CreateResources();

    [Fact]
    public void Generate_SameComet_ReturnsSameSources()
    {
        var comet = CreateComet(AsteroidSize.Medium, 42);

        var first = _generator.Generate(comet, _resources);
        var second = _generator.Generate(comet, _resources);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Theory]
    [InlineData(AsteroidSize.Tiny)]
    [InlineData(AsteroidSize.Small)]
    public void Generate_TooSmallComet_HasNoSources(AsteroidSize size)
    {
        var sources = _generator.Generate(CreateComet(size, 12), _resources);

        Assert.Empty(sources);
    }

    [Theory]
    [InlineData(AsteroidSize.Medium)]
    [InlineData(AsteroidSize.Large)]
    [InlineData(AsteroidSize.Huge)]
    public void Generate_SourceBearingComet_HasOneToThreeInfiniteSources(AsteroidSize size)
    {
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var sources = _generator.Generate(CreateComet(size, seed), _resources)
                .Where(deposit => deposit.Kind == ResourceDepositKind.InfiniteSource)
                .ToArray();

            Assert.InRange(sources.Length, 1, 3);
            Assert.All(sources, source =>
            {
                Assert.True(source.IsInfinite);
                Assert.Contains(":source:v2:", source.Id, StringComparison.Ordinal);
                Assert.InRange(source.ManualYieldPerCycle, 1, 10);
                Assert.True(source.BaseExtractionUnitsPerMinute > 0);
            });
        }
    }

    [Fact]
    public void Generate_HugeLandableComet_KeepsCentralAreaFree()
    {
        var sources = _generator.Generate(CreateComet(AsteroidSize.Huge, 91, landable: true), _resources)
            .Where(deposit => deposit.Kind == ResourceDepositKind.InfiniteSource)
            .ToArray();

        Assert.InRange(sources.Length, 1, 3);
        Assert.All(sources, source =>
        {
            var centerDistance = Math.Sqrt(
                Math.Pow(source.NormalizedPosition.X, 2) +
                Math.Pow(source.NormalizedPosition.Y, 2));
            Assert.True(centerDistance - source.RadiusFactor >= 0.23);
            Assert.True(centerDistance + source.RadiusFactor <= 0.72);
        });
    }

    [Fact]
    public void Generate_SourceWorldRadiusIsFixedAcrossCometSizes()
    {
        var singleResource = new[] { CreateResource("common", ResourceRarity.VeryCommon, 100) };
        var medium = _generator.Generate(CreateComet(AsteroidSize.Medium, 44), singleResource)
            .Where(deposit => deposit.IsInfinite);
        var huge = _generator.Generate(CreateComet(AsteroidSize.Huge, 44), singleResource)
            .Where(deposit => deposit.IsInfinite);

        Assert.All(medium.Concat(huge), source =>
        {
            Assert.Equal(MiningConfiguration.DefaultSourceRadiusWorldUnits, source.RadiusWorldUnits);
            Assert.Equal(source.RadiusWorldUnits, source.GetRadiusWorldUnits(987), 6);
        });
    }

    [Fact]
    public void Generate_SourcesDoNotOverlapAtMinimumSupportedRadius()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var comet = CreateComet(AsteroidSize.Medium, seed);
            var sources = _generator.Generate(comet, _resources);
            for (var firstIndex = 0; firstIndex < sources.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < sources.Count; secondIndex++)
                {
                    var first = sources[firstIndex];
                    var second = sources[secondIndex];
                    var normalizedDistance = Math.Sqrt(
                        Math.Pow(first.NormalizedPosition.X - second.NormalizedPosition.X, 2) +
                        Math.Pow(first.NormalizedPosition.Y - second.NormalizedPosition.Y, 2));
                    var worldDistance = normalizedDistance * comet.Radius;
                    var required = first.GetRadiusWorldUnits(comet.Radius) +
                                   second.GetRadiusWorldUnits(comet.Radius) +
                                   MiningConfiguration.SourceClearanceWorldUnits;
                    Assert.True(worldDistance + 0.001 >= required);
                }
            }
        }
    }

    [Fact]
    public void Generate_VeryCommonResourceAppearsMoreOftenThanRareResource()
    {
        var commonCount = 0;
        var rareCount = 0;
        for (ulong seed = 1; seed <= 500; seed++)
        {
            var sources = _generator.Generate(CreateComet(AsteroidSize.Medium, seed), _resources)
                .Where(deposit => deposit.IsInfinite);
            commonCount += sources.Count(source => source.ResourceId == new ItemId("common"));
            rareCount += sources.Count(source => source.ResourceId == new ItemId("rare"));
        }

        Assert.True(commonCount > rareCount * 5);
        Assert.True(rareCount > 0);
    }

    [Fact]
    public void Generate_PurityDistributionApproximatesConfiguredWeights()
    {
        var purities = Enumerable.Range(1, 1_200)
            .SelectMany(seed => _generator.Generate(CreateComet(AsteroidSize.Medium, (ulong)seed), _resources))
            .Where(deposit => deposit.IsInfinite)
            .Select(source => source.Purity)
            .ToArray();

        var impureShare = purities.Count(purity => purity == ResourcePurity.Impure) / (double)purities.Length;
        var normalShare = purities.Count(purity => purity == ResourcePurity.Normal) / (double)purities.Length;
        var pureShare = purities.Count(purity => purity == ResourcePurity.Pure) / (double)purities.Length;
        Assert.InRange(impureShare, 0.30, 0.40);
        Assert.InRange(normalShare, 0.45, 0.55);
        Assert.InRange(pureShare, 0.11, 0.19);
    }

    [Fact]
    public void Generate_UraniumIsRestrictedToLargeAndHugeComets()
    {
        var uranium = CreateResource(
            "uranium_ore",
            ResourceRarity.VeryRare,
            1,
            [AsteroidSize.Large, AsteroidSize.Huge]);

        Assert.Empty(_generator.Generate(CreateComet(
            AsteroidSize.Medium,
            71,
            geology: AsteroidGeology.Radiogenic), [uranium]));
        Assert.InRange(CountInfinite(_generator.Generate(CreateComet(
            AsteroidSize.Large,
            71,
            geology: AsteroidGeology.Radiogenic), [uranium])), 1, 3);
        Assert.InRange(CountInfinite(_generator.Generate(CreateComet(
            AsteroidSize.Huge,
            71,
            geology: AsteroidGeology.Radiogenic), [uranium])), 1, 3);
    }

    [Fact]
    public void Generate_VeryRareResourcesRequireMetallicOrRadiogenicGeology()
    {
        var veryRare = CreateResource("very_rare", ResourceRarity.VeryRare, 1);

        Assert.Empty(_generator.Generate(CreateComet(
            AsteroidSize.Large,
            81,
            geology: AsteroidGeology.Carbonaceous), [veryRare]));
        Assert.InRange(CountInfinite(_generator.Generate(CreateComet(
            AsteroidSize.Large,
            81,
            geology: AsteroidGeology.Metallic), [veryRare])), 1, 3);
        Assert.InRange(CountInfinite(_generator.Generate(CreateComet(
            AsteroidSize.Large,
            81,
            geology: AsteroidGeology.Radiogenic), [veryRare])), 1, 3);
    }

    [Fact]
    public void Generate_FiniteOreStonesAreHalfAsFrequentAsNormalSources()
    {
        var normalSourceCount = 0;
        var finiteStoneCount = 0;
        for (ulong seed = 1; seed <= 2_000; seed++)
        {
            var deposits = _generator.Generate(CreateComet(AsteroidSize.Large, seed), _resources);
            normalSourceCount += deposits.Count(deposit => deposit.IsInfinite);
            finiteStoneCount += deposits.Count(deposit => deposit.IsFiniteOreStone);
            Assert.All(deposits.Where(deposit => deposit.IsFiniteOreStone), stone =>
            {
                Assert.Contains(":ore-stone:v1:", stone.Id, StringComparison.Ordinal);
                Assert.InRange(
                    stone.OriginalAmount,
                    MiningConfiguration.MinimumFiniteOreStoneHits,
                    MiningConfiguration.MaximumFiniteOreStoneHits);
                Assert.Equal(MiningConfiguration.FiniteOreStoneRadiusWorldUnits, stone.RadiusWorldUnits);
            });
        }

        var frequency = finiteStoneCount / (double)normalSourceCount;
        Assert.InRange(frequency, 0.46, 0.54);
    }

    [Theory]
    [InlineData(AsteroidSize.Large)]
    [InlineData(AsteroidSize.Huge)]
    public void Generate_LargeCometDepositsStayInsideAndNeverOverlap(AsteroidSize size)
    {
        for (ulong seed = 1; seed <= 250; seed++)
        {
            var comet = CreateComet(size, seed, landable: true);
            var deposits = _generator.Generate(comet, _resources);
            var outline = AsteroidOutlineGeometry.CreateNormalizedOutline(comet);
            Assert.InRange(deposits.Count(deposit => deposit.IsInfinite), 1, 3);
            foreach (var deposit in deposits)
            {
                var centerDistance = Math.Sqrt(
                    Math.Pow(deposit.NormalizedPosition.X, 2) +
                    Math.Pow(deposit.NormalizedPosition.Y, 2));
                Assert.True(centerDistance + deposit.RadiusFactor <= 0.72 + 0.000001);
                Assert.True(AsteroidOutlineGeometry.ContainsNormalizedCircle(
                    outline,
                    deposit.NormalizedPosition,
                    deposit.RadiusFactor));
            }

            for (var firstIndex = 0; firstIndex < deposits.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < deposits.Count; secondIndex++)
                {
                    var first = deposits[firstIndex];
                    var second = deposits[secondIndex];
                    var worldDistance = Math.Sqrt(
                        Math.Pow(first.NormalizedPosition.X - second.NormalizedPosition.X, 2) +
                        Math.Pow(first.NormalizedPosition.Y - second.NormalizedPosition.Y, 2)) * comet.Radius;
                    var minimumDistance = first.GetRadiusWorldUnits(comet.Radius) +
                                          second.GetRadiusWorldUnits(comet.Radius) +
                                          MiningConfiguration.SourceClearanceWorldUnits;
                    Assert.True(worldDistance + 0.001 >= minimumDistance);
                }
            }
        }
    }

    private static int CountInfinite(IEnumerable<ResourceDepositDefinition> deposits) =>
        deposits.Count(deposit => deposit.IsInfinite);

    private static AsteroidDefinition CreateComet(
        AsteroidSize size,
        ulong seed,
        bool landable = false,
        AsteroidGeology geology = AsteroidGeology.Carbonaceous)
    {
        var radius = size switch
        {
            AsteroidSize.Tiny => 60,
            AsteroidSize.Small => 140,
            AsteroidSize.Medium => MiningConfiguration.MinimumSourceCometRadiusWorldUnits,
            AsteroidSize.Large => 600,
            AsteroidSize.Huge => 1_500,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
        return new AsteroidDefinition(
            $"comet:{seed}",
            new WorldPosition(100, 100),
            radius,
            size,
            "comet",
            new ItemId("iron_ore"),
            seed,
            0,
            0.4,
            0.4,
            [],
            landable
                ? new AsteroidSurfaceProfile("surface", 1200, 900, 4, seed, seed + 1, "test")
                : null,
            geology);
    }

    private static IReadOnlyList<ResourceDefinition> CreateResources() =>
    [
        CreateResource("common", ResourceRarity.VeryCommon, 100),
        CreateResource("uncommon", ResourceRarity.Uncommon, 20),
        CreateResource("rare", ResourceRarity.Rare, 3),
        CreateResource("very_rare", ResourceRarity.VeryRare, 0.8),
        CreateResource("special_a", ResourceRarity.Special, 12),
        CreateResource("special_b", ResourceRarity.Special, 5),
        CreateResource("common_b", ResourceRarity.Common, 50),
        CreateResource("uncommon_b", ResourceRarity.Uncommon, 18),
    ];

    private static ResourceDefinition CreateResource(
        string id,
        ResourceRarity rarity,
        double weight,
        IReadOnlyList<AsteroidSize>? sizes = null) => new(
        new ItemId(id),
        id,
        rarity,
        weight,
        "#808080",
        ResourceVisualStyle.Vein,
        2,
        4,
        12,
        2,
        "dust",
        string.Empty,
        100,
        ["test"],
        sizes ?? Enum.GetValues<AsteroidSize>(),
        0.025,
        0.055);
}

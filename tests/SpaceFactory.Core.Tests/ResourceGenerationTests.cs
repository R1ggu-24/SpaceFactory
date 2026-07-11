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
    public void Generate_SameComet_ReturnsSameDeposits()
    {
        var comet = CreateComet(AsteroidSize.Medium, 42);

        var first = _generator.Generate(comet, _resources);
        var second = _generator.Generate(comet, _resources);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Generate_TinyComet_HasFewResources()
    {
        var deposits = _generator.Generate(CreateComet(AsteroidSize.Tiny, 12), _resources);

        Assert.InRange(deposits.Count, 1, 2);
        Assert.InRange(deposits.Select(deposit => deposit.ResourceId).Distinct().Count(), 1, 2);
    }

    [Fact]
    public void Generate_HugeComet_HasManyResourcesAndFreeCenter()
    {
        var deposits = _generator.Generate(CreateComet(AsteroidSize.Huge, 91, landable: true), _resources);

        Assert.InRange(deposits.Count, 15, 24);
        Assert.InRange(deposits.Select(deposit => deposit.ResourceId).Distinct().Count(), 5, 8);
        Assert.All(deposits, deposit =>
        {
            var centerDistance = Math.Sqrt(
                Math.Pow(deposit.NormalizedPosition.X, 2) +
                Math.Pow(deposit.NormalizedPosition.Y, 2));
            Assert.True(centerDistance - deposit.RadiusFactor >= 0.23);
            Assert.True(centerDistance + deposit.RadiusFactor <= 0.72);
        });
    }

    [Fact]
    public void Generate_DepositsDoNotOverlap()
    {
        var deposits = _generator.Generate(CreateComet(AsteroidSize.Huge, 123, landable: true), _resources);

        for (var firstIndex = 0; firstIndex < deposits.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < deposits.Count; secondIndex++)
            {
                var first = deposits[firstIndex];
                var second = deposits[secondIndex];
                var distance = Math.Sqrt(
                    Math.Pow(first.NormalizedPosition.X - second.NormalizedPosition.X, 2) +
                    Math.Pow(first.NormalizedPosition.Y - second.NormalizedPosition.Y, 2));
                Assert.True(distance >= first.RadiusFactor + second.RadiusFactor + 0.025 - 0.001);
            }
        }
    }

    [Fact]
    public void Generate_VeryCommonResourceAppearsMoreOftenThanRareResource()
    {
        var commonCount = 0;
        var rareCount = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var deposits = _generator.Generate(CreateComet(AsteroidSize.Medium, seed), _resources);
            commonCount += deposits.Count(deposit => deposit.ResourceId == new ItemId("common"));
            rareCount += deposits.Count(deposit => deposit.ResourceId == new ItemId("rare"));
        }

        Assert.True(commonCount > rareCount * 5);
        Assert.True(rareCount > 0);
    }

    private static AsteroidDefinition CreateComet(AsteroidSize size, ulong seed, bool landable = false) => new(
        $"comet:{seed}",
        new WorldPosition(100, 100),
        size == AsteroidSize.Huge ? 1500 : 260,
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
            : null);

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

    private static ResourceDefinition CreateResource(string id, ResourceRarity rarity, double weight) => new(
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
        Enum.GetValues<AsteroidSize>(),
        0.025,
        0.055);
}

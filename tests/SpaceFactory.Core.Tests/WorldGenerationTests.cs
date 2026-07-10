using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.Tests;

public sealed class WorldGenerationTests
{
    private readonly DeterministicWorldGenerator _generator = new();

    [Fact]
    public void Generate_SameInput_ReturnsIdenticalSector()
    {
        var request = Request(741029384, 2, -3);

        var first = _generator.Generate(request);
        var second = _generator.Generate(request);

        Assert.Equal(first.Coordinate, second.Coordinate);
        Assert.True(first.Asteroids.SequenceEqual(second.Asteroids));
    }

    [Fact]
    public void Generate_DifferentSeed_ChangesSector()
    {
        var first = _generator.Generate(Request(1, 0, 0));
        var second = _generator.Generate(Request(2, 0, 0));

        Assert.False(first.Asteroids.SequenceEqual(second.Asteroids));
    }

    [Fact]
    public void Generate_DifferentCoordinate_ChangesSector()
    {
        var first = _generator.Generate(Request(1, 0, 0));
        var second = _generator.Generate(Request(1, 1, 0));

        Assert.False(first.Asteroids.SequenceEqual(second.Asteroids));
    }

    [Fact]
    public void Generate_ProducesValidConfiguredAsteroids()
    {
        var sector = _generator.Generate(Request(1, 0, 0));

        Assert.InRange(sector.Asteroids.Count, 3, 12);
        Assert.All(sector.Asteroids, asteroid =>
        {
            Assert.True(Enum.IsDefined(asteroid.Size));
            Assert.True(double.IsFinite(asteroid.Position.X));
            Assert.True(double.IsFinite(asteroid.Position.Y));
            Assert.True(asteroid.Radius > 0);
        });
    }

    internal static SectorGenerationRequest Request(long seed, int x, int y) => new(
        new WorldSeed(seed),
        new SectorCoordinate(x, y),
        new WorldGenerationSettings(
            5000,
            3,
            12,
            new Dictionary<AsteroidSize, double>
            {
                [AsteroidSize.Tiny] = 0.25,
                [AsteroidSize.Small] = 0.35,
                [AsteroidSize.Medium] = 0.25,
                [AsteroidSize.Large] = 0.12,
                [AsteroidSize.Huge] = 0.03,
            }));
}

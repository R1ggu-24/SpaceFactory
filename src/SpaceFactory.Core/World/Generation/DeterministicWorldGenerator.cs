using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Generation;

public sealed class DeterministicWorldGenerator : IWorldGenerator
{
    public GeneratedSector Generate(SectorGenerationRequest request)
    {
        request.Settings.Validate();
        var random = new SplitMix64(Mix(request));
        var count = random.NextInt(
            request.Settings.MinimumAsteroidsPerSector,
            request.Settings.MaximumAsteroidsPerSector + 1);
        var asteroids = new List<AsteroidDefinition>(count);

        for (var index = 0; index < count; index++)
        {
            var size = ChooseSize(random, request.Settings.AsteroidSizeWeights);
            var radius = GetRadius(size, random.NextDouble());
            var resource = random.NextDouble() < 0.65 ? "iron_ore" : "copper_ore";
            asteroids.Add(new AsteroidDefinition(
                $"{request.Coordinate.X}:{request.Coordinate.Y}:{index}",
                new WorldPosition(
                    random.NextDouble() * request.Settings.SectorSize,
                    random.NextDouble() * request.Settings.SectorSize),
                radius,
                size,
                $"{size.ToString().ToLowerInvariant()}_{resource}",
                new ItemId(resource)));
        }

        return new GeneratedSector(request.Coordinate, asteroids);
    }

    private static ulong Mix(SectorGenerationRequest request)
    {
        unchecked
        {
            var value = (ulong)request.Seed.Value ^ request.Salt;
            value ^= (ulong)(long)request.Coordinate.X * 0x9E3779B97F4A7C15UL;
            value ^= (ulong)(long)request.Coordinate.Y * 0xBF58476D1CE4E5B9UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static AsteroidSize ChooseSize(SplitMix64 random, IReadOnlyDictionary<AsteroidSize, double> weights)
    {
        var roll = random.NextDouble() * weights.Values.Sum();
        foreach (var pair in weights.OrderBy(pair => pair.Key))
        {
            roll -= pair.Value;
            if (roll <= 0)
            {
                return pair.Key;
            }
        }

        return weights.Keys.Last();
    }

    private static double GetRadius(AsteroidSize size, double factor) => size switch
    {
        AsteroidSize.Tiny => 25 + (factor * 20),
        AsteroidSize.Small => 50 + (factor * 35),
        AsteroidSize.Medium => 90 + (factor * 50),
        AsteroidSize.Large => 150 + (factor * 80),
        AsteroidSize.Huge => 250 + (factor * 120),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    private sealed class SplitMix64(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

        public int NextInt(int minimum, int maximum) => minimum + (int)(Next() % (uint)(maximum - minimum));

        private ulong Next()
        {
            unchecked
            {
                var value = (_state += 0x9E3779B97F4A7C15UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }
}

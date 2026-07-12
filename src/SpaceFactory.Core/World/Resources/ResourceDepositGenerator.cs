using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Resources;

public sealed class ResourceDepositGenerator
{
    private const ulong ResourceSalt = 0x5245534F55524345UL;

    public IReadOnlyList<ResourceDepositDefinition> Generate(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDefinition> resources)
    {
        if (resources.Count == 0)
        {
            return [];
        }

        foreach (var resource in resources)
        {
            resource.Validate();
        }

        var available = resources
            .Where(resource => resource.PossibleCometSizes.Contains(comet.Size))
            .ToArray();
        if (available.Length == 0)
        {
            return [];
        }

        var random = new ResourceRandom(comet.VisualSeed ^ ResourceSalt);
        var typeCount = GetResourceTypeCount(comet.Size, random);
        var selectedResources = SelectResourceTypes(available, comet.Size, typeCount, random);
        var depositTarget = GetDepositCount(comet.Size, random);
        return CreateDeposits(comet, selectedResources, depositTarget, random);
    }

    private static IReadOnlyList<ResourceDefinition> SelectResourceTypes(
        IReadOnlyList<ResourceDefinition> available,
        AsteroidSize cometSize,
        int count,
        ResourceRandom random)
    {
        var selected = new List<ResourceDefinition>(count);
        var veryCommon = available.Where(resource => resource.Rarity == ResourceRarity.VeryCommon).ToArray();
        if (veryCommon.Length > 0)
        {
            selected.Add(ChooseWeighted(veryCommon, cometSize, random));
        }

        while (selected.Count < count)
        {
            var candidates = available.Where(resource => !selected.Contains(resource)).ToArray();
            if (candidates.Length == 0)
            {
                break;
            }

            selected.Add(ChooseWeighted(candidates, cometSize, random));
        }

        return selected;
    }

    private static ResourceDefinition ChooseWeighted(
        IReadOnlyList<ResourceDefinition> resources,
        AsteroidSize cometSize,
        ResourceRandom random)
    {
        var totalWeight = resources.Sum(resource => GetEffectiveWeight(resource, cometSize));
        var roll = random.NextDouble() * totalWeight;
        foreach (var resource in resources)
        {
            roll -= GetEffectiveWeight(resource, cometSize);
            if (roll <= 0)
            {
                return resource;
            }
        }

        return resources[^1];
    }

    private static double GetEffectiveWeight(ResourceDefinition resource, AsteroidSize cometSize)
    {
        var rarityFactor = (resource.Rarity, cometSize) switch
        {
            (ResourceRarity.Rare, AsteroidSize.Tiny) => 0.18,
            (ResourceRarity.VeryRare, AsteroidSize.Tiny) => 0.025,
            (ResourceRarity.Rare, AsteroidSize.Small) => 0.38,
            (ResourceRarity.VeryRare, AsteroidSize.Small) => 0.08,
            (ResourceRarity.Uncommon, AsteroidSize.Large or AsteroidSize.Huge) => 1.35,
            (ResourceRarity.Rare, AsteroidSize.Large or AsteroidSize.Huge) => 1.7,
            (ResourceRarity.VeryRare, AsteroidSize.Large or AsteroidSize.Huge) => 1.45,
            _ => 1.0,
        };
        return resource.SpawnWeight * rarityFactor;
    }

    private static IReadOnlyList<ResourceDepositDefinition> CreateDeposits(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDefinition> resources,
        int targetCount,
        ResourceRandom random)
    {
        if (resources.Count == 0)
        {
            return [];
        }

        var deposits = new List<ResourceDepositDefinition>(targetCount);
        var clusterCenters = resources.ToDictionary(
            resource => resource.Id,
            _ => CreateValidPosition(comet, [], 0.08, random));

        for (var index = 0; index < targetCount; index++)
        {
            var resource = resources[index % resources.Count];
            var radiusFactor = Lerp(
                resource.MinimumDepositRadiusFactor,
                resource.MaximumDepositRadiusFactor,
                random.NextDouble());
            var position = CreateClusteredPosition(
                comet,
                clusterCenters[resource.Id],
                radiusFactor,
                deposits,
                random);
            var sizeAmountFactor = comet.Size switch
            {
                AsteroidSize.Tiny => 0.65,
                AsteroidSize.Small => 0.85,
                AsteroidSize.Medium => 1.0,
                AsteroidSize.Large => 1.4,
                AsteroidSize.Huge => 1.8,
                _ => 1.0,
            };
            var amount = (int)Math.Round(Lerp(resource.MinimumAmount, resource.MaximumAmount, random.NextDouble()) *
                sizeAmountFactor);

            deposits.Add(new ResourceDepositDefinition(
                $"{comet.Id}:deposit:{index}",
                comet.Id,
                resource.Id,
                position,
                radiusFactor,
                Math.Max(1, amount),
                resource.MiningTimeSeconds,
                random.Next()));
        }

        return deposits;
    }

    private static WorldPosition CreateClusteredPosition(
        AsteroidDefinition comet,
        WorldPosition clusterCenter,
        double radiusFactor,
        IReadOnlyList<ResourceDepositDefinition> existing,
        ResourceRandom random)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var useCluster = random.NextDouble() < 0.72;
            var candidate = useCluster
                ? new WorldPosition(
                    clusterCenter.X + (random.NextSignedDouble() * 0.16),
                    clusterCenter.Y + (random.NextSignedDouble() * 0.16))
                : CreateValidPosition(comet, existing, radiusFactor, random);
            if (IsValidPosition(comet, candidate, radiusFactor, existing))
            {
                return candidate;
            }
        }

        return CreateValidPosition(comet, existing, radiusFactor, random);
    }

    private static WorldPosition CreateValidPosition(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDepositDefinition> existing,
        double radiusFactor,
        ResourceRandom random)
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var minimumDistance = comet.SupportsLanding ? 0.27 : 0.06;
            var distance = Lerp(minimumDistance, 0.64, Math.Sqrt(random.NextDouble()));
            var candidate = new WorldPosition(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
            if (IsValidPosition(comet, candidate, radiusFactor, existing))
            {
                return candidate;
            }
        }

        var fallbackAngle = random.NextDouble() * Math.PI * 2;
        return new WorldPosition(Math.Cos(fallbackAngle) * 0.58, Math.Sin(fallbackAngle) * 0.58);
    }

    private static bool IsValidPosition(
        AsteroidDefinition comet,
        WorldPosition position,
        double radiusFactor,
        IReadOnlyList<ResourceDepositDefinition> existing)
    {
        var distanceFromCenter = Math.Sqrt((position.X * position.X) + (position.Y * position.Y));
        if (distanceFromCenter + radiusFactor > 0.72)
        {
            return false;
        }

        if (comet.SupportsLanding && distanceFromCenter - radiusFactor < 0.23)
        {
            return false;
        }

        return existing.All(deposit =>
        {
            var deltaX = position.X - deposit.NormalizedPosition.X;
            var deltaY = position.Y - deposit.NormalizedPosition.Y;
            var minimumDistance = radiusFactor + deposit.RadiusFactor + 0.025;
            return (deltaX * deltaX) + (deltaY * deltaY) >= minimumDistance * minimumDistance;
        });
    }

    private static int GetResourceTypeCount(AsteroidSize size, ResourceRandom random) => size switch
    {
        AsteroidSize.Tiny => random.NextDouble() < 0.15 ? 2 : 1,
        AsteroidSize.Small => random.NextInt(1, 3),
        AsteroidSize.Medium => random.NextInt(2, 5),
        AsteroidSize.Large => random.NextInt(3, 7),
        AsteroidSize.Huge => random.NextInt(5, 9),
        _ => 1,
    };

    private static int GetDepositCount(AsteroidSize size, ResourceRandom random) => size switch
    {
        AsteroidSize.Tiny => random.NextInt(1, 3),
        AsteroidSize.Small => random.NextInt(2, 5),
        AsteroidSize.Medium => random.NextInt(5, 9),
        AsteroidSize.Large => random.NextInt(9, 16),
        AsteroidSize.Huge => random.NextInt(15, 25),
        _ => 1,
    };

    private static double Lerp(double first, double second, double amount) => first + ((second - first) * amount);

    private sealed class ResourceRandom(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

        public double NextSignedDouble() => (NextDouble() * 2) - 1;

        public int NextInt(int minimum, int maximum) => minimum + (int)(Next() % (uint)(maximum - minimum));

        public ulong Next()
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

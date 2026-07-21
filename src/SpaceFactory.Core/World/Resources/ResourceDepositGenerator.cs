using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Resources;

public sealed class ResourceDepositGenerator
{
    private const ulong ResourceSaltV2 = 0x5245535F56325F21UL;

    public IReadOnlyList<ResourceDepositDefinition> Generate(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDefinition> resources)
    {
        ArgumentNullException.ThrowIfNull(comet);
        ArgumentNullException.ThrowIfNull(resources);
        foreach (var resource in resources)
        {
            resource.Validate();
        }

        if (comet.Radius < MiningConfiguration.MinimumSourceCometRadiusWorldUnits ||
            comet.Size is AsteroidSize.Tiny or AsteroidSize.Small ||
            resources.Count == 0)
        {
            return [];
        }

        var available = resources
            .Where(resource => resource.PossibleCometSizes.Contains(comet.Size) &&
                               IsCompatibleWithGeology(resource, comet.Geology))
            .ToArray();
        if (available.Length == 0)
        {
            return [];
        }

        var random = new ResourceRandom(
            (comet.SurfaceProfile?.ResourceDistributionSeed ?? comet.VisualSeed) ^ ResourceSaltV2);
        var targetCount = random.NextInt(
            MiningConfiguration.MinimumSourcesPerComet,
            MiningConfiguration.MaximumSourcesPerComet + 1);
        var selectedResources = SelectResourceTypes(
            available,
            comet.Size,
            comet.Geology,
            targetCount,
            random);
        var outline = AsteroidOutlineGeometry.CreateNormalizedOutline(comet);
        var deposits = CreateSources(comet, selectedResources, outline, random).ToList();
        CreateFiniteOreStones(comet, available, deposits.Count, deposits, outline, random);
        return deposits;
    }

    private static IReadOnlyList<ResourceDefinition> SelectResourceTypes(
        IReadOnlyList<ResourceDefinition> available,
        AsteroidSize cometSize,
        AsteroidGeology geology,
        int count,
        ResourceRandom random)
    {
        var remaining = available.ToList();
        var selected = new List<ResourceDefinition>(count);
        while (selected.Count < count && remaining.Count > 0)
        {
            var resource = ChooseWeighted(remaining, cometSize, geology, random);
            selected.Add(resource);
            remaining.Remove(resource);
        }

        // A catalog may contain fewer compatible resource types than the configured source count.
        // Reusing a type is valid: these are separate physical sources with independent seeds.
        while (selected.Count < count)
        {
            selected.Add(ChooseWeighted(available, cometSize, geology, random));
        }

        return selected;
    }

    private static ResourceDefinition ChooseWeighted(
        IReadOnlyList<ResourceDefinition> resources,
        AsteroidSize cometSize,
        AsteroidGeology geology,
        ResourceRandom random)
    {
        var totalWeight = resources.Sum(resource => GetEffectiveWeight(resource, cometSize, geology));
        var roll = random.NextDouble() * totalWeight;
        foreach (var resource in resources)
        {
            roll -= GetEffectiveWeight(resource, cometSize, geology);
            if (roll <= 0)
            {
                return resource;
            }
        }

        return resources[^1];
    }

    private static double GetEffectiveWeight(
        ResourceDefinition resource,
        AsteroidSize cometSize,
        AsteroidGeology geology)
    {
        var rarityFactor = (resource.Rarity, cometSize) switch
        {
            (ResourceRarity.Uncommon, AsteroidSize.Large or AsteroidSize.Huge) => 1.35,
            (ResourceRarity.Rare, AsteroidSize.Large or AsteroidSize.Huge) => 1.7,
            (ResourceRarity.VeryRare, AsteroidSize.Large or AsteroidSize.Huge) => 1.45,
            _ => 1.0,
        };
        var geologyFactor = geology switch
        {
            AsteroidGeology.Carbonaceous when resource.Id.Value is "carbon" or "troilite" or "phosphorus" => 2.8,
            AsteroidGeology.Silicate when resource.Id.Value is "silicate_rock" or "olivine" or "calcite" or "schreibersite" => 3.0,
            AsteroidGeology.Metallic when resource.Uses.Any(use =>
                use is "metals" or "alloys" or "precision_alloys" or "hightech" or "catalysts") => 2.4,
            AsteroidGeology.VolatileRich when resource.Id.Value is "water_ice" or "sulfur" or "halite" or "sylvite" => 3.2,
            AsteroidGeology.Radiogenic when resource.Id.Value == "uranium_ore" => 55.0,
            AsteroidGeology.Radiogenic when resource.Rarity == ResourceRarity.VeryRare => 3.5,
            _ => 1.0,
        };
        return resource.SpawnWeight * rarityFactor * geologyFactor;
    }

    private static bool IsCompatibleWithGeology(
        ResourceDefinition resource,
        AsteroidGeology geology)
    {
        if (resource.Id.Value == "uranium_ore")
        {
            return geology == AsteroidGeology.Radiogenic;
        }

        return resource.Rarity != ResourceRarity.VeryRare ||
               geology is AsteroidGeology.Metallic or AsteroidGeology.Radiogenic;
    }

    private static IReadOnlyList<ResourceDepositDefinition> CreateSources(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDefinition> resources,
        IReadOnlyList<WorldPosition> outline,
        ResourceRandom random)
    {
        var sources = new List<ResourceDepositDefinition>(resources.Count);
        for (var index = 0; index < resources.Count; index++)
        {
            var resource = resources[index];
            var radiusWorldUnits = resource.SourceRadiusWorldUnits;
            var radiusFactor = radiusWorldUnits / comet.Radius;
            if (!TryCreateValidPosition(
                    comet,
                    radiusFactor,
                    sources,
                    outline,
                    random,
                    out var position))
            {
                // Fixed-size sources can make the requested third source physically impossible
                // on a particularly narrow medium outline. Keeping the already valid one or two
                // sources preserves the configured 1..3 contract without shrinking or overlap.
                break;
            }
            var purity = ChoosePurity(random);
            sources.Add(new ResourceDepositDefinition(
                $"{comet.Id}:source:v2:{index}",
                comet.Id,
                resource.Id,
                position,
                radiusFactor,
                resource.ManualYieldPerCycle,
                resource.MiningTimeSeconds,
                random.Next(),
                purity,
                radiusWorldUnits,
                resource.BaseExtractionUnitsPerMinute,
                resource.ManualYieldPerCycle,
                IsInfinite: true,
                Kind: ResourceDepositKind.InfiniteSource));
        }

        return sources;
    }

    private static void CreateFiniteOreStones(
        AsteroidDefinition comet,
        IReadOnlyList<ResourceDefinition> availableResources,
        int normalSourceCount,
        List<ResourceDepositDefinition> deposits,
        IReadOnlyList<WorldPosition> outline,
        ResourceRandom random)
    {
        var stoneIndex = 0;
        for (var sourceIndex = 0; sourceIndex < normalSourceCount; sourceIndex++)
        {
            if (random.NextDouble() >= MiningConfiguration.FiniteOreStoneSpawnChancePerSource)
            {
                continue;
            }

            var resource = ChooseWeighted(availableResources, comet.Size, comet.Geology, random);
            var radiusWorldUnits = MiningConfiguration.FiniteOreStoneRadiusWorldUnits;
            var radiusFactor = radiusWorldUnits / comet.Radius;
            if (!TryCreateValidPosition(
                    comet,
                    radiusFactor,
                    deposits,
                    outline,
                    random,
                    out var position))
            {
                // Finite stones are optional discoveries. Never compromise the guaranteed
                // infinite sources or overlap another object merely to satisfy a chance roll.
                continue;
            }
            var hitCount = random.NextInt(
                MiningConfiguration.MinimumFiniteOreStoneHits,
                MiningConfiguration.MaximumFiniteOreStoneHits + 1);
            deposits.Add(new ResourceDepositDefinition(
                $"{comet.Id}:ore-stone:v1:{stoneIndex}",
                comet.Id,
                resource.Id,
                position,
                radiusFactor,
                hitCount,
                resource.MiningTimeSeconds * MiningConfiguration.FiniteOreStoneMiningTimeMultiplier,
                random.Next(),
                ResourcePurity.Normal,
                radiusWorldUnits,
                BaseExtractionUnitsPerMinute: 0,
                ManualYieldPerCycle: 0,
                IsInfinite: false,
                Kind: ResourceDepositKind.FiniteOreStone));
            stoneIndex++;
        }
    }

    private static bool TryCreateValidPosition(
        AsteroidDefinition comet,
        double radiusFactor,
        IReadOnlyList<ResourceDepositDefinition> existing,
        IReadOnlyList<WorldPosition> outline,
        ResourceRandom random,
        out WorldPosition position)
    {
        // Landable comets keep their construction clearing. Medium source-bearing rocks do not
        // need that reserved centre; allowing central candidates prevents an unlucky organic,
        // narrow outline from making the guaranteed third fixed-size source impossible.
        var minimumDistance = comet.SupportsLanding ? 0.27 : 0.0;
        var maximumDistance = 0.72 - radiusFactor;
        if (maximumDistance <= minimumDistance)
        {
            position = default;
            return false;
        }

        for (var attempt = 0; attempt < 256; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var distance = Lerp(minimumDistance, maximumDistance, Math.Sqrt(random.NextDouble()));
            var candidate = new WorldPosition(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
            if (IsValidPosition(comet, candidate, radiusFactor, existing, outline))
            {
                position = candidate;
                return true;
            }
        }

        // Deterministic lattice fallback makes the configured 1-3 guarantee independent of
        // unlucky rejection-sampling sequences.
        for (var ringIndex = 0; ringIndex < 6; ringIndex++)
        {
            var ringFactor = (ringIndex + 0.5) / 6.0;
            var distance = Lerp(minimumDistance, maximumDistance, ringFactor);
            for (var angleIndex = 0; angleIndex < 96; angleIndex++)
            {
                var angle = (Math.PI * 2 * angleIndex / 96.0) + (random.NextDouble() * 0.01);
                var candidate = new WorldPosition(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
                if (IsValidPosition(comet, candidate, radiusFactor, existing, outline))
                {
                    position = candidate;
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    private static bool IsValidPosition(
        AsteroidDefinition comet,
        WorldPosition position,
        double radiusFactor,
        IReadOnlyList<ResourceDepositDefinition> existing,
        IReadOnlyList<WorldPosition> outline)
    {
        var distanceFromCenter = Math.Sqrt((position.X * position.X) + (position.Y * position.Y));
        if (distanceFromCenter + radiusFactor > 0.72 ||
            !AsteroidOutlineGeometry.ContainsNormalizedCircle(outline, position, radiusFactor))
        {
            return false;
        }

        if (comet.SupportsLanding && distanceFromCenter - radiusFactor < 0.23)
        {
            return false;
        }

        var normalizedClearance = MiningConfiguration.SourceClearanceWorldUnits / comet.Radius;
        return existing.All(source =>
        {
            var deltaX = position.X - source.NormalizedPosition.X;
            var deltaY = position.Y - source.NormalizedPosition.Y;
            var minimumSeparation = radiusFactor + source.RadiusFactor + normalizedClearance;
            return (deltaX * deltaX) + (deltaY * deltaY) >= minimumSeparation * minimumSeparation;
        });
    }

    private static ResourcePurity ChoosePurity(ResourceRandom random)
    {
        var roll = random.NextDouble();
        if (roll < MiningConfiguration.ImpureChance)
        {
            return ResourcePurity.Impure;
        }

        return roll < MiningConfiguration.ImpureChance + MiningConfiguration.NormalChance
            ? ResourcePurity.Normal
            : ResourcePurity.Pure;
    }

    private static double Lerp(double first, double second, double amount) =>
        first + ((second - first) * amount);

    private sealed class ResourceRandom(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

        public int NextInt(int minimum, int maximum) =>
            minimum + (int)(Next() % (uint)(maximum - minimum));

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

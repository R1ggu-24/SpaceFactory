using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Generation;

public sealed class DeterministicWorldGenerator : IWorldGenerator
{
    private const ulong CandidateSalt = 0x434F4D45545F5631UL;
    private const ulong LoneCometSalt = 0x4C4F4E455F5631UL;
    private const ulong ExtremeCometSalt = 0x45585452454D455FUL;
    private const ulong ExtremeCandidateSalt = 0x485547455F5631UL;
    private static readonly CometFieldPlanner FieldPlanner = new();

    public GeneratedSector Generate(SectorGenerationRequest request)
    {
        request.Settings.Validate();
        var candidatesBySector = new Dictionary<SectorCoordinate, IReadOnlyList<CometCandidate>>();

        for (var y = request.Coordinate.Y - 1; y <= request.Coordinate.Y + 1; y++)
        {
            for (var x = request.Coordinate.X - 1; x <= request.Coordinate.X + 1; x++)
            {
                var coordinate = new SectorCoordinate(x, y);
                candidatesBySector[coordinate] = CreateCandidates(request, coordinate);
            }
        }

        var allNearbyCandidates = candidatesBySector.Values.SelectMany(candidates => candidates).ToArray();
        var accepted = candidatesBySector[request.Coordinate]
            .Where(candidate => IsOutsideStartingSafeZone(candidate, request.Settings))
            .Where(candidate => HasHighestPriorityAtPosition(candidate, allNearbyCandidates, request.Settings.MinimumCometSpacing))
            .OrderBy(candidate => candidate.Index)
            .Select(ToDefinition)
            .ToArray();

        var fieldSample = FieldPlanner.Sample(request.Seed, request.Coordinate, request.Settings, request.Salt);
        return new GeneratedSector(
            request.Coordinate,
            accepted,
            fieldSample.FieldId,
            fieldSample.Density);
    }

    private static IReadOnlyList<CometCandidate> CreateCandidates(
        SectorGenerationRequest request,
        SectorCoordinate coordinate)
    {
        var fieldSample = FieldPlanner.Sample(request.Seed, coordinate, request.Settings, request.Salt);
        var loneComet = !fieldSample.IsInsideField && IsLoneCometSector(request, coordinate);
        var normalCount = fieldSample.IsInsideField
            ? request.Settings.MinimumAsteroidsPerSector + (int)Math.Round(
                (request.Settings.MaximumAsteroidsPerSector - request.Settings.MinimumAsteroidsPerSector) *
                Math.Pow(fieldSample.Density, 1.12))
            : loneComet ? 1 : 0;
        var hasExtremeComet = IsExtremeCometSector(request, coordinate);
        if (normalCount == 0 && !hasExtremeComet)
        {
            return [];
        }

        var random = new SplitMix64(HashSector(request.Seed.Value, coordinate, request.Salt ^ CandidateSalt));
        var candidates = new List<CometCandidate>(normalCount + (hasExtremeComet ? 1 : 0));
        for (var index = 0; index < normalCount; index++)
        {
            var size = loneComet
                ? random.NextDouble() < 0.3 ? AsteroidSize.Tiny : AsteroidSize.Small
                : ChooseNormalSize(random, request.Settings.AsteroidSizeWeights);
            candidates.Add(CreateCandidate(request, coordinate, index, size, random));
        }

        if (hasExtremeComet)
        {
            var extremeRandom = new SplitMix64(HashSector(
                request.Seed.Value,
                coordinate,
                request.Salt ^ ExtremeCandidateSalt));
            candidates.Add(CreateCandidate(
                request,
                coordinate,
                request.Settings.MaximumAsteroidsPerSector,
                AsteroidSize.Huge,
                extremeRandom));
        }

        return candidates;
    }

    private static CometCandidate CreateCandidate(
        SectorGenerationRequest request,
        SectorCoordinate coordinate,
        int index,
        AsteroidSize size,
        SplitMix64 random)
    {
        var radius = GetRadius(size, random.NextDouble());
        var localPosition = new WorldPosition(
            random.NextDouble() * request.Settings.SectorSize,
            random.NextDouble() * request.Settings.SectorSize);
        var globalPosition = new WorldPosition(
            (coordinate.X * (double)request.Settings.SectorSize) + localPosition.X,
            (coordinate.Y * (double)request.Settings.SectorSize) + localPosition.Y);
        var visualSeed = random.Next();
        var priority = CreatePlacementPriority(size, random.Next());
        var resource = random.NextDouble() < 0.68 ? "iron_ore" : "copper_ore";
        var roughness = 0.18 + (random.NextDouble() * 0.52);
        var elevation = 0.12 + (random.NextDouble() * 0.58);
        var craterCount = GetCraterCount(size, roughness, random);
        var id = $"comet:{coordinate.X}:{coordinate.Y}:{index}";

        return new CometCandidate(
            coordinate,
            index,
            localPosition,
            globalPosition,
            radius,
            size,
            new ItemId(resource),
            visualSeed,
            priority,
            random.NextDouble() * Math.PI * 2,
            roughness,
            elevation,
            CreateCraters(craterCount, random),
            CreateSurfaceProfile(id, coordinate, index, size, radius, roughness, visualSeed));
    }

    private static bool HasHighestPriorityAtPosition(
        CometCandidate candidate,
        IReadOnlyList<CometCandidate> allCandidates,
        double spacing)
    {
        foreach (var other in allCandidates)
        {
            if (ReferenceEquals(candidate, other))
            {
                continue;
            }

            var minimumDistance = candidate.Radius + other.Radius + spacing;
            var deltaX = candidate.GlobalPosition.X - other.GlobalPosition.X;
            var deltaY = candidate.GlobalPosition.Y - other.GlobalPosition.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) >= minimumDistance * minimumDistance)
            {
                continue;
            }

            if (other.Priority > candidate.Priority ||
                (other.Priority == candidate.Priority && string.CompareOrdinal(other.Id, candidate.Id) > 0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOutsideStartingSafeZone(CometCandidate candidate, WorldGenerationSettings settings)
    {
        var deltaX = candidate.GlobalPosition.X - settings.StartingSafeCenterX;
        var deltaY = candidate.GlobalPosition.Y - settings.StartingSafeCenterY;
        var requiredDistance = settings.StartingSafeRadius + candidate.Radius;
        return (deltaX * deltaX) + (deltaY * deltaY) >= requiredDistance * requiredDistance;
    }

    private static AsteroidDefinition ToDefinition(CometCandidate candidate) => new(
        candidate.Id,
        candidate.LocalPosition,
        candidate.Radius,
        candidate.Size,
        $"comet_{candidate.Size.ToString().ToLowerInvariant()}",
        candidate.ResourceType,
        candidate.VisualSeed,
        candidate.RotationRadians,
        candidate.SurfaceRoughness,
        candidate.ElevationVariation,
        candidate.Craters,
        candidate.SurfaceProfile);

    private static AsteroidSurfaceProfile? CreateSurfaceProfile(
        string cometId,
        SectorCoordinate coordinate,
        int index,
        AsteroidSize size,
        double radius,
        double roughness,
        ulong visualSeed)
    {
        var supportsLanding = size == AsteroidSize.Huge ||
            (size == AsteroidSize.Large && radius >= 560 && roughness <= 0.55);
        if (!supportsLanding)
        {
            return null;
        }

        return new AsteroidSurfaceProfile(
            $"{cometId}:surface",
            radius * (size == AsteroidSize.Huge ? 0.82 : 0.78),
            radius * (size == AsteroidSize.Huge ? 0.65 : 0.52),
            size == AsteroidSize.Huge ? 4 : 1,
            visualSeed ^ 0x5445525241494EUL,
            visualSeed ^ 0x5245534F55524345UL,
            $"sectors/{coordinate.X}/{coordinate.Y}/comets/{index}");
    }

    private static IReadOnlyList<AsteroidCrater> CreateCraters(int count, SplitMix64 random)
    {
        var craters = new AsteroidCrater[count];
        for (var index = 0; index < count; index++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var distance = Math.Sqrt(random.NextDouble()) * 0.62;
            var radiusFactor = random.NextDouble() < 0.12
                ? 0.16 + (random.NextDouble() * 0.12)
                : 0.03 + (Math.Pow(random.NextDouble(), 1.7) * 0.16);
            craters[index] = new AsteroidCrater(
                Math.Cos(angle) * distance,
                Math.Sin(angle) * distance,
                radiusFactor,
                0.25 + (random.NextDouble() * 0.65),
                random.NextDouble() * Math.PI * 2);
        }

        return craters;
    }

    private static int GetCraterCount(AsteroidSize size, double roughness, SplitMix64 random)
    {
        var baseCount = size switch
        {
            AsteroidSize.Tiny => 1,
            AsteroidSize.Small => 2,
            AsteroidSize.Medium => 4,
            AsteroidSize.Large => 8,
            AsteroidSize.Huge => 13,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
        return baseCount + (int)Math.Round(roughness * 4) + random.NextInt(0, 3);
    }

    private static bool IsLoneCometSector(SectorGenerationRequest request, SectorCoordinate coordinate)
    {
        var random = new SplitMix64(HashSector(
            request.Seed.Value,
            coordinate,
            request.Salt ^ LoneCometSalt));
        return random.NextDouble() < request.Settings.LoneCometChancePerSector;
    }

    private static bool IsExtremeCometSector(SectorGenerationRequest request, SectorCoordinate coordinate)
    {
        var separation = request.Settings.ExtremeCometSeparationInSectors;
        var candidatePriority = HashSector(
            request.Seed.Value,
            coordinate,
            request.Salt ^ ExtremeCometSalt);

        for (var y = coordinate.Y - separation; y <= coordinate.Y + separation; y++)
        {
            for (var x = coordinate.X - separation; x <= coordinate.X + separation; x++)
            {
                if (x == coordinate.X && y == coordinate.Y)
                {
                    continue;
                }

                var otherCoordinate = new SectorCoordinate(x, y);
                var otherPriority = HashSector(
                    request.Seed.Value,
                    otherCoordinate,
                    request.Salt ^ ExtremeCometSalt);
                if (otherPriority > candidatePriority ||
                    (otherPriority == candidatePriority && CompareCoordinates(otherCoordinate, coordinate) > 0))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ulong HashSector(long seed, SectorCoordinate coordinate, ulong salt)
    {
        unchecked
        {
            var value = (ulong)seed ^ salt;
            value ^= (ulong)(long)coordinate.X * 0x9E3779B97F4A7C15UL;
            value ^= (ulong)(long)coordinate.Y * 0xBF58476D1CE4E5B9UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static AsteroidSize ChooseNormalSize(
        SplitMix64 random,
        IReadOnlyDictionary<AsteroidSize, double> weights)
    {
        var normalWeights = weights
            .Where(pair => pair.Key != AsteroidSize.Huge)
            .OrderBy(pair => pair.Key)
            .ToArray();
        var roll = random.NextDouble() * normalWeights.Sum(pair => pair.Value);
        foreach (var pair in normalWeights)
        {
            roll -= pair.Value;
            if (roll <= 0)
            {
                return pair.Key;
            }
        }

        return normalWeights[^1].Key;
    }

    private static ulong CreatePlacementPriority(AsteroidSize size, ulong randomPriority)
    {
        const ulong highestBit = 1UL << 63;
        return size == AsteroidSize.Huge
            ? randomPriority | highestBit
            : randomPriority & ~highestBit;
    }

    private static double GetRadius(AsteroidSize size, double factor) => size switch
    {
        AsteroidSize.Tiny => 40 + (factor * 35),
        AsteroidSize.Small => 90 + (factor * 70),
        AsteroidSize.Medium => 190 + (factor * 130),
        AsteroidSize.Large => 480 + (factor * 200),
        AsteroidSize.Huge => 1300 + (factor * 500),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    private static int CompareCoordinates(SectorCoordinate first, SectorCoordinate second)
    {
        var xComparison = first.X.CompareTo(second.X);
        return xComparison != 0 ? xComparison : first.Y.CompareTo(second.Y);
    }

    private sealed record CometCandidate(
        SectorCoordinate Owner,
        int Index,
        WorldPosition LocalPosition,
        WorldPosition GlobalPosition,
        double Radius,
        AsteroidSize Size,
        ItemId ResourceType,
        ulong VisualSeed,
        ulong Priority,
        double RotationRadians,
        double SurfaceRoughness,
        double ElevationVariation,
        IReadOnlyList<AsteroidCrater> Craters,
        AsteroidSurfaceProfile? SurfaceProfile)
    {
        public string Id => $"comet:{Owner.X}:{Owner.Y}:{Index}";
    }

    private sealed class SplitMix64(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

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

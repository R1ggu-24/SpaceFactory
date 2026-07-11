using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Core.World.Generation;

public sealed class CometFieldPlanner
{
    private const ulong FieldSalt = 0x4649454C445F5631UL;

    public CometFieldSample Sample(
        WorldSeed seed,
        SectorCoordinate sector,
        WorldGenerationSettings settings,
        ulong generationSalt)
    {
        var cellSize = settings.FieldCellSizeInSectors;
        var cellX = FloorDivide(sector.X, cellSize);
        var cellY = FloorDivide(sector.Y, cellSize);
        var candidates = new List<FieldCandidate>();

        if (settings.EnableStartingDiscoveryField)
        {
            candidates.Add(CreateStartingField(settings));
        }

        for (var y = cellY - 1; y <= cellY + 1; y++)
        {
            for (var x = cellX - 1; x <= cellX + 1; x++)
            {
                var candidate = CreateCandidate(seed, x, y, settings, generationSalt);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        var accepted = candidates
            .Where(candidate => HasHighestPriority(candidate, candidates))
            .ToArray();
        var sectorCenterX = sector.X + 0.5;
        var sectorCenterY = sector.Y + 0.5;

        foreach (var field in accepted)
        {
            var normalizedDistance = GetNormalizedDistance(field, sectorCenterX, sectorCenterY);
            if (normalizedDistance > 1)
            {
                continue;
            }

            var edgeFactor = Math.Pow(1 - normalizedDistance, 0.7);
            var density = field.Intensity * (0.18 + (0.82 * edgeFactor));
            return new CometFieldSample(field.Id, Math.Clamp(density, 0, 1));
        }

        return CometFieldSample.Empty;
    }

    private static FieldCandidate CreateStartingField(WorldGenerationSettings settings) => new(
        "field:starting-discovery",
        settings.StartingFieldCenterSectorX,
        settings.StartingFieldCenterSectorY,
        settings.StartingFieldMajorRadiusInSectors,
        settings.StartingFieldMinorRadiusInSectors,
        0,
        settings.MaximumFieldGapInSectors,
        settings.StartingFieldIntensity,
        ulong.MaxValue);

    private static FieldCandidate? CreateCandidate(
        WorldSeed seed,
        int cellX,
        int cellY,
        WorldGenerationSettings settings,
        ulong generationSalt)
    {
        var random = new FieldRandom(Hash(seed.Value, cellX, cellY, generationSalt ^ FieldSalt));
        if (random.NextDouble() > settings.FieldSpawnChance)
        {
            return null;
        }

        var cellSize = settings.FieldCellSizeInSectors;
        var padding = settings.MaximumFieldRadiusInSectors;
        var usableWidth = cellSize - (padding * 2);
        var centerX = (cellX * cellSize) + padding + (random.NextDouble() * usableWidth);
        var centerY = (cellY * cellSize) + padding + (random.NextDouble() * usableWidth);
        var majorRadius = Lerp(
            settings.MinimumFieldRadiusInSectors,
            settings.MaximumFieldRadiusInSectors,
            random.NextDouble());
        var minorRadius = majorRadius * Lerp(0.58, 0.92, random.NextDouble());

        return new FieldCandidate(
            $"field:{cellX}:{cellY}",
            centerX,
            centerY,
            majorRadius,
            minorRadius,
            random.NextDouble() * Math.PI * 2,
            Lerp(settings.MinimumFieldGapInSectors, settings.MaximumFieldGapInSectors, random.NextDouble()),
            Lerp(0.32, 1.0, random.NextDouble()),
            random.Next());
    }

    private static bool HasHighestPriority(FieldCandidate candidate, IReadOnlyList<FieldCandidate> candidates)
    {
        foreach (var other in candidates)
        {
            if (ReferenceEquals(candidate, other))
            {
                continue;
            }

            var deltaX = candidate.CenterX - other.CenterX;
            var deltaY = candidate.CenterY - other.CenterY;
            var minimumDistance = candidate.MajorRadius + other.MajorRadius +
                Math.Max(candidate.RequiredGap, other.RequiredGap);
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

    private static double GetNormalizedDistance(FieldCandidate field, double x, double y)
    {
        var deltaX = x - field.CenterX;
        var deltaY = y - field.CenterY;
        var cosine = Math.Cos(-field.RotationRadians);
        var sine = Math.Sin(-field.RotationRadians);
        var rotatedX = (deltaX * cosine) - (deltaY * sine);
        var rotatedY = (deltaX * sine) + (deltaY * cosine);
        return Math.Sqrt(
            Math.Pow(rotatedX / field.MajorRadius, 2) +
            Math.Pow(rotatedY / field.MinorRadius, 2));
    }

    private static ulong Hash(long seed, int x, int y, ulong salt)
    {
        unchecked
        {
            var value = (ulong)seed ^ salt;
            value ^= (ulong)(long)x * 0x9E3779B97F4A7C15UL;
            value ^= (ulong)(long)y * 0xBF58476D1CE4E5B9UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    private static double Lerp(double first, double second, double amount) => first + ((second - first) * amount);

    private sealed record FieldCandidate(
        string Id,
        double CenterX,
        double CenterY,
        double MajorRadius,
        double MinorRadius,
        double RotationRadians,
        double RequiredGap,
        double Intensity,
        ulong Priority);

    private sealed class FieldRandom(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

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

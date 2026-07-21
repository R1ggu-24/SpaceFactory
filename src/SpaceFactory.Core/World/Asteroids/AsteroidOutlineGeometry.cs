using SpaceFactory.Core.Common;

namespace SpaceFactory.Core.World.Asteroids;

/// <summary>
/// Single deterministic source for the visible/collision outline and geometry validation.
/// Keeping normalized points in Core prevents resources from being placed against an idealized
/// circle while the presentation renders a narrower organic comet silhouette.
/// </summary>
public static class AsteroidOutlineGeometry
{
    private const double Epsilon = 0.000_001;

    public static IReadOnlyList<WorldPosition> CreateNormalizedOutline(AsteroidDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var random = new OutlineRandom(definition.VisualSeed);
        var pointCount = random.NextInt(26, 43);
        var outline = new WorldPosition[pointCount];
        var firstWave = random.NextDouble(2.0, 4.5);
        var secondWave = random.NextDouble(5.0, 9.0);
        var thirdWave = random.NextDouble(10.0, 15.0);
        var firstPhase = random.NextDouble(0, Math.Tau);
        var secondPhase = random.NextDouble(0, Math.Tau);
        var thirdPhase = random.NextDouble(0, Math.Tau);
        var minimumAspect = definition.SupportsLanding ? 0.72 : 0.56;
        var minorAspect = random.NextDouble(minimumAspect, 0.96);
        var stretchAlongX = random.NextDouble() > 0.5;

        for (var index = 0; index < pointCount; index++)
        {
            var angle = Math.Tau * index / pointCount;
            var wave = Math.Sin((angle * firstWave) + firstPhase) * 0.07 +
                       Math.Sin((angle * secondWave) + secondPhase) * 0.038 +
                       Math.Sin((angle * thirdWave) + thirdPhase) * 0.018;
            var noise = random.NextDouble(-0.028, 0.028) *
                        (0.72 + definition.SurfaceRoughness);
            var radiusFactor = 0.87 + wave + noise;
            var xScale = stretchAlongX ? 1.0 : minorAspect;
            var yScale = stretchAlongX ? minorAspect : 1.0;
            outline[index] = new WorldPosition(
                Math.Cos(angle) * radiusFactor * xScale,
                Math.Sin(angle) * radiusFactor * yScale);
        }

        return outline;
    }

    public static bool ContainsNormalizedCircle(
        IReadOnlyList<WorldPosition> outline,
        WorldPosition center,
        double radius)
    {
        ArgumentNullException.ThrowIfNull(outline);
        if (outline.Count < 3 || !double.IsFinite(radius) || radius < 0 ||
            !double.IsFinite(center.X) || !double.IsFinite(center.Y) ||
            !ContainsPoint(outline, center))
        {
            return false;
        }

        for (var index = 0; index < outline.Count; index++)
        {
            var first = outline[index];
            var second = outline[(index + 1) % outline.Count];
            if (DistanceToSegment(center, first, second) + Epsilon < radius)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsPoint(IReadOnlyList<WorldPosition> outline, WorldPosition point)
    {
        var inside = false;
        for (int current = 0, previous = outline.Count - 1;
             current < outline.Count;
             previous = current++)
        {
            var first = outline[current];
            var second = outline[previous];
            var crosses = (first.Y > point.Y) != (second.Y > point.Y) &&
                          point.X < ((second.X - first.X) * (point.Y - first.Y) /
                                     (second.Y - first.Y)) + first.X;
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double DistanceToSegment(
        WorldPosition point,
        WorldPosition first,
        WorldPosition second)
    {
        var deltaX = second.X - first.X;
        var deltaY = second.Y - first.Y;
        var lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (lengthSquared <= Epsilon)
        {
            return Math.Sqrt(
                Math.Pow(point.X - first.X, 2) +
                Math.Pow(point.Y - first.Y, 2));
        }

        var projection = Math.Clamp(
            (((point.X - first.X) * deltaX) + ((point.Y - first.Y) * deltaY)) /
            lengthSquared,
            0,
            1);
        var closestX = first.X + (projection * deltaX);
        var closestY = first.Y + (projection * deltaY);
        return Math.Sqrt(
            Math.Pow(point.X - closestX, 2) +
            Math.Pow(point.Y - closestY, 2));
    }

    private sealed class OutlineRandom(ulong state)
    {
        private ulong _state = state;

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

        public double NextDouble(double minimum, double maximum) =>
            minimum + (NextDouble() * (maximum - minimum));

        public int NextInt(int minimum, int maximum) =>
            minimum + (int)(Next() % (uint)(maximum - minimum));

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

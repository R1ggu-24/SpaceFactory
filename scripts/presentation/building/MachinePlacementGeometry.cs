using Godot;

namespace SpaceFactory.Presentation.Building;

public static class MachinePlacementGeometry
{
    private const float SeparationTolerance = 0.001f;

    public static Vector2 SnapToGrid(Vector2 point, float gridSize)
    {
        if (!float.IsFinite(gridSize) || gridSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gridSize));
        }

        return new Vector2(
            Mathf.Round(point.X / gridSize) * gridSize,
            Mathf.Round(point.Y / gridSize) * gridSize);
    }

    public static Vector2[] CreateRectangleCorners(
        Vector2 center,
        Vector2 size,
        float rotationRadians)
    {
        if (size.X <= 0 || size.Y <= 0 || !float.IsFinite(rotationRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var half = size * 0.5f;
        return
        [
            center + new Vector2(-half.X, -half.Y).Rotated(rotationRadians),
            center + new Vector2(half.X, -half.Y).Rotated(rotationRadians),
            center + new Vector2(half.X, half.Y).Rotated(rotationRadians),
            center + new Vector2(-half.X, half.Y).Rotated(rotationRadians),
        ];
    }

    public static bool ConvexPolygonsOverlap(
        IReadOnlyList<Vector2> first,
        IReadOnlyList<Vector2> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Count < 3 || second.Count < 3)
        {
            return false;
        }

        return HasNoSeparatingAxis(first, second) && HasNoSeparatingAxis(second, first);
    }

    private static bool HasNoSeparatingAxis(
        IReadOnlyList<Vector2> axisSource,
        IReadOnlyList<Vector2> other)
    {
        for (var index = 0; index < axisSource.Count; index++)
        {
            var edge = axisSource[(index + 1) % axisSource.Count] - axisSource[index];
            if (edge.LengthSquared() <= SeparationTolerance)
            {
                continue;
            }

            var axis = edge.Orthogonal().Normalized();
            Project(axisSource, axis, out var firstMinimum, out var firstMaximum);
            Project(other, axis, out var secondMinimum, out var secondMaximum);
            if (firstMaximum <= secondMinimum + SeparationTolerance ||
                secondMaximum <= firstMinimum + SeparationTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static void Project(
        IReadOnlyList<Vector2> polygon,
        Vector2 axis,
        out float minimum,
        out float maximum)
    {
        minimum = polygon[0].Dot(axis);
        maximum = minimum;
        for (var index = 1; index < polygon.Count; index++)
        {
            var projection = polygon[index].Dot(axis);
            minimum = Math.Min(minimum, projection);
            maximum = Math.Max(maximum, projection);
        }
    }
}

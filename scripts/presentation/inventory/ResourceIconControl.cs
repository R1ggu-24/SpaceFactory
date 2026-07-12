using Godot;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Draws a compact resource icon from the central resource definition. This is
/// used as a fallback while dedicated bitmap icons are not part of the project.
/// </summary>
public partial class ResourceIconControl : Control
{
    private ResourceDefinition? _resource;
    private Color _resourceColor = new(0.35f, 0.65f, 0.75f);
    private uint _seed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Configure(ResourceDefinition? resource)
    {
        _resource = resource;
        _resourceColor = resource is null
            ? new Color(0.35f, 0.65f, 0.75f)
            : Color.FromHtml(resource.BaseColorHex);
        _seed = StableHash(resource?.Id.Value ?? string.Empty);
        Visible = resource is not null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_resource is null || Size.X < 4 || Size.Y < 4)
        {
            return;
        }

        var center = Size * 0.5f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.38f;
        var silhouette = CreateSilhouette(center, radius);
        DrawColoredPolygon(silhouette, _resourceColor.Darkened(0.48f));
        DrawPolyline(Close(silhouette), _resourceColor.Lightened(0.2f), 1.3f, true);

        switch (_resource.VisualStyle)
        {
            case ResourceVisualStyle.Crystal:
                DrawCrystals(center, radius);
                break;
            case ResourceVisualStyle.MetallicInclusion:
                DrawMetallicInclusions(center, radius);
                break;
            case ResourceVisualStyle.FrozenDeposit:
                DrawFrozenShards(center, radius);
                break;
            case ResourceVisualStyle.MineralLayer:
                DrawMineralLayers(center, radius);
                break;
            default:
                DrawVeins(center, radius);
                break;
        }

        DrawCircle(
            center - new Vector2(radius * 0.22f, radius * 0.22f),
            radius * 0.09f,
            Colors.White with { A = 0.22f });
    }

    private Vector2[] CreateSilhouette(Vector2 center, float radius)
    {
        const int pointCount = 12;
        var points = new Vector2[pointCount];
        for (var index = 0; index < pointCount; index++)
        {
            var angle = Mathf.Tau * index / pointCount;
            var variation = 0.82f + (Noise01(index) * 0.2f);
            var stretch = new Vector2(1.04f, 0.9f + (Noise01(20) * 0.12f));
            points[index] = center + (Vector2.FromAngle(angle) * radius * variation * stretch);
        }

        return points;
    }

    private void DrawVeins(Vector2 center, float radius)
    {
        for (var vein = 0; vein < 3; vein++)
        {
            var y = center.Y + ((vein - 1) * radius * 0.25f);
            var points = new Vector2[5];
            for (var point = 0; point < points.Length; point++)
            {
                points[point] = new Vector2(
                    center.X - (radius * 0.68f) + (point * radius * 0.34f),
                    y + ((Noise01(50 + (vein * 7) + point) - 0.5f) * radius * 0.34f));
            }

            DrawPolyline(points, _resourceColor.Lightened(0.28f), 1.8f, true);
            DrawPolyline(points, Colors.White with { A = 0.16f }, 0.7f, true);
        }
    }

    private void DrawCrystals(Vector2 center, float radius)
    {
        for (var index = 0; index < 4; index++)
        {
            var x = center.X + ((index - 1.5f) * radius * 0.3f);
            var height = radius * (0.58f + (Noise01(70 + index) * 0.3f));
            var width = radius * (0.19f + (Noise01(80 + index) * 0.09f));
            var bottom = center.Y + (radius * 0.42f);
            Vector2[] shard =
            [
                new(x - width, bottom),
                new(x - (width * 0.72f), bottom - (height * 0.58f)),
                new(x, bottom - height),
                new(x + (width * 0.72f), bottom - (height * 0.58f)),
                new(x + width, bottom),
            ];
            DrawColoredPolygon(shard, _resourceColor.Lightened(0.04f + (index * 0.05f)));
            DrawPolyline(Close(shard), Colors.White with { A = 0.48f }, 1.1f, true);
            DrawLine(shard[2], new Vector2(x, bottom), Colors.White with { A = 0.28f }, 0.8f, true);
        }
    }

    private void DrawMetallicInclusions(Vector2 center, float radius)
    {
        for (var index = 0; index < 7; index++)
        {
            var angle = Noise01(100 + index) * Mathf.Tau;
            var distance = radius * Noise01(120 + index) * 0.55f;
            var position = center + (Vector2.FromAngle(angle) * distance);
            var inclusionRadius = radius * (0.07f + (Noise01(140 + index) * 0.1f));
            DrawCircle(position, inclusionRadius, _resourceColor.Lightened(0.38f));
            DrawArc(position, inclusionRadius, 3.4f, 5.7f, 8, Colors.White with { A = 0.72f }, 1.1f, true);
        }
    }

    private void DrawFrozenShards(Vector2 center, float radius)
    {
        for (var index = 0; index < 5; index++)
        {
            var angle = (-0.95f + (index * 0.44f)) + ((Noise01(160 + index) - 0.5f) * 0.2f);
            var direction = Vector2.FromAngle(angle);
            var start = center - (direction * radius * 0.2f);
            var end = center + (direction * radius * (0.48f + (Noise01(180 + index) * 0.25f)));
            DrawLine(start, end, _resourceColor.Lightened(0.42f), 2.4f, true);
            DrawLine(start, end, Colors.White with { A = 0.4f }, 0.8f, true);
        }
    }

    private void DrawMineralLayers(Vector2 center, float radius)
    {
        for (var layer = 0; layer < 4; layer++)
        {
            var y = center.Y - (radius * 0.43f) + (layer * radius * 0.28f);
            var halfWidth = radius * (0.72f - (Mathf.Abs(layer - 1.5f) * 0.1f));
            var points = new[]
            {
                new Vector2(center.X - halfWidth, y),
                new Vector2(center.X - (halfWidth * 0.32f), y + ((Noise01(200 + layer) - 0.5f) * radius * 0.2f)),
                new Vector2(center.X + (halfWidth * 0.3f), y + ((Noise01(210 + layer) - 0.5f) * radius * 0.2f)),
                new Vector2(center.X + halfWidth, y),
            };
            DrawPolyline(points, _resourceColor.Lightened(0.12f + (layer * 0.07f)), 2.2f, true);
        }
    }

    private float Noise01(int salt)
    {
        var value = _seed + ((uint)salt * 0x9E3779B9u);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777215.0f;
    }

    private static Vector2[] Close(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        Array.Copy(points, closed, points.Length);
        closed[^1] = points[0];
        return closed;
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}

using Godot;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Draws compact procedural icons for both raw resources and manufactured
/// products. The scene-facing class name is retained for compatibility.
/// </summary>
public partial class ResourceIconControl : Control
{
    private ItemPresentationViewModel? _item;
    private Color _itemColor = new(0.35f, 0.65f, 0.75f);
    private uint _seed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Configure(ItemPresentationViewModel? item)
    {
        _item = item;
        _itemColor = item?.Color ?? new Color(0.35f, 0.65f, 0.75f);
        _seed = StableHash(item?.Id.Value ?? string.Empty);
        Visible = item is not null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_item is null || Size.X < 4 || Size.Y < 4)
        {
            return;
        }

        var center = Size * 0.5f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.38f;
        if (!_item.UsesResourceIcon)
        {
            DrawProductionIcon(center, radius);
            return;
        }

        var silhouette = CreateSilhouette(center, radius);
        DrawColoredPolygon(silhouette, _itemColor.Darkened(0.48f));
        DrawPolyline(Close(silhouette), _itemColor.Lightened(0.2f), 1.3f, true);

        switch (_item.Resource!.VisualStyle)
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

            DrawPolyline(points, _itemColor.Lightened(0.28f), 1.8f, true);
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
            DrawColoredPolygon(shard, _itemColor.Lightened(0.04f + (index * 0.05f)));
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
            DrawCircle(position, inclusionRadius, _itemColor.Lightened(0.38f));
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
            DrawLine(start, end, _itemColor.Lightened(0.42f), 2.4f, true);
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
            DrawPolyline(points, _itemColor.Lightened(0.12f + (layer * 0.07f)), 2.2f, true);
        }
    }

    private void DrawProductionIcon(Vector2 center, float radius)
    {
        var dark = _itemColor.Darkened(0.62f);
        var body = _itemColor.Darkened(0.12f);
        var light = _itemColor.Lightened(0.32f);
        var frame = new[]
        {
            center + new Vector2(-radius * 0.72f, -radius * 0.48f),
            center + new Vector2(-radius * 0.48f, -radius * 0.72f),
            center + new Vector2(radius * 0.55f, -radius * 0.72f),
            center + new Vector2(radius * 0.72f, -radius * 0.55f),
            center + new Vector2(radius * 0.72f, radius * 0.48f),
            center + new Vector2(radius * 0.48f, radius * 0.72f),
            center + new Vector2(-radius * 0.55f, radius * 0.72f),
            center + new Vector2(-radius * 0.72f, radius * 0.55f),
        };
        DrawColoredPolygon(frame, dark with { A = 0.82f });
        DrawPolyline(Close(frame), _itemColor.Lightened(0.22f) with { A = 0.78f }, 1.1f, true);

        switch (_item!.Glyph)
        {
            case ItemIconGlyph.Powder:
                DrawPowder(center, radius, body, light);
                break;
            case ItemIconGlyph.Ingot:
                DrawIngot(center, radius, body, light, alloy: false);
                break;
            case ItemIconGlyph.Alloy:
                DrawIngot(center, radius, body, light, alloy: true);
                break;
            case ItemIconGlyph.Plate:
                DrawPlate(center, radius, body, light);
                break;
            case ItemIconGlyph.Rod:
                DrawRods(center, radius, light);
                break;
            case ItemIconGlyph.Fastener:
                DrawFasteners(center, radius, body, light);
                break;
            case ItemIconGlyph.Wire:
            case ItemIconGlyph.Cable:
                DrawWire(center, radius, light, _item.Glyph == ItemIconGlyph.Cable);
                break;
            case ItemIconGlyph.Pipe:
                DrawPipe(center, radius, body, light);
                break;
            case ItemIconGlyph.Circuit:
                DrawCircuit(center, radius, body, light);
                break;
            case ItemIconGlyph.Battery:
                DrawBattery(center, radius, body, light);
                break;
            case ItemIconGlyph.Motor:
                DrawMotor(center, radius, body, light);
                break;
            case ItemIconGlyph.Chip:
                DrawChip(center, radius, body, light);
                break;
            case ItemIconGlyph.MachinePart:
                DrawMachinePart(center, radius, body, light);
                break;
            case ItemIconGlyph.Conveyor:
                DrawConveyor(center, radius, body, light);
                break;
            case ItemIconGlyph.ShipPart:
                DrawShipPart(center, radius, body, light);
                break;
            case ItemIconGlyph.Liquid:
                DrawDroplet(center, radius, body, light);
                break;
            case ItemIconGlyph.Gas:
                DrawGas(center, radius, body, light);
                break;
            case ItemIconGlyph.Fuel:
                DrawFuel(center, radius, body, light);
                break;
            case ItemIconGlyph.Container:
                DrawContainer(center, radius, body, light, _item.IsEmptyContainer);
                break;
            default:
                DrawUnknown(center, radius, body, light);
                break;
        }

        DrawLine(
            center + new Vector2(-radius * 0.54f, radius * 0.57f),
            center + new Vector2(-radius * 0.18f, radius * 0.57f),
            light with { A = 0.45f },
            1,
            true);
    }

    private void DrawPowder(Vector2 center, float radius, Color body, Color light)
    {
        for (var index = 0; index < 13; index++)
        {
            var angle = Noise01(240 + index) * Mathf.Tau;
            var distance = radius * 0.48f * Mathf.Sqrt(Noise01(280 + index));
            var grainRadius = radius * (0.055f + (Noise01(320 + index) * 0.065f));
            DrawCircle(center + (Vector2.FromAngle(angle) * distance), grainRadius, body.Lightened(index * 0.012f));
        }

        DrawArc(center, radius * 0.5f, 0.12f, 2.85f, 16, light with { A = 0.48f }, 1.2f, true);
    }

    private void DrawIngot(Vector2 center, float radius, Color body, Color light, bool alloy)
    {
        var ingot = new[]
        {
            center + new Vector2(-radius * 0.58f, radius * 0.38f),
            center + new Vector2(-radius * 0.42f, -radius * 0.32f),
            center + new Vector2(-radius * 0.2f, -radius * 0.48f),
            center + new Vector2(radius * 0.42f, -radius * 0.32f),
            center + new Vector2(radius * 0.58f, radius * 0.38f),
        };
        DrawColoredPolygon(ingot, body);
        DrawPolyline(Close(ingot), light, 1.5f, true);
        DrawLine(ingot[1], ingot[3], Colors.White with { A = 0.34f }, 1.1f, true);
        if (alloy)
        {
            DrawLine(
                center + new Vector2(-radius * 0.35f, radius * 0.05f),
                center + new Vector2(radius * 0.36f, radius * 0.05f),
                _itemColor.Darkened(0.38f),
                radius * 0.15f,
                true);
        }
    }

    private void DrawPlate(Vector2 center, float radius, Color body, Color light)
    {
        for (var layer = 2; layer >= 0; layer--)
        {
            var offset = new Vector2(layer * radius * 0.09f, -layer * radius * 0.1f);
            var rect = new Rect2(
                center + new Vector2(-radius * 0.52f, -radius * 0.27f) + offset,
                new Vector2(radius * 0.95f, radius * 0.58f));
            DrawRect(rect, body.Darkened(layer * 0.06f), true);
            DrawRect(rect, light with { A = 0.7f }, false, 1.1f, true);
        }
    }

    private void DrawRods(Vector2 center, float radius, Color light)
    {
        for (var index = -1; index <= 1; index++)
        {
            var offset = new Vector2(index * radius * 0.22f, -index * radius * 0.08f);
            var start = center + new Vector2(-radius * 0.38f, radius * 0.48f) + offset;
            var end = center + new Vector2(radius * 0.38f, -radius * 0.48f) + offset;
            DrawLine(start, end, _itemColor.Darkened(0.2f), radius * 0.16f, true);
            DrawLine(start, end - new Vector2(radius * 0.025f, radius * 0.025f), light with { A = 0.72f }, 1.1f, true);
        }
    }

    private void DrawFasteners(Vector2 center, float radius, Color body, Color light)
    {
        for (var index = 0; index < 3; index++)
        {
            var position = center + new Vector2(
                (index - 1) * radius * 0.34f,
                (index % 2) * radius * 0.25f - radius * 0.12f);
            DrawCircle(position, radius * 0.2f, body);
            DrawArc(position, radius * 0.2f, 0, Mathf.Tau, 10, light, 1.1f, true);
            DrawLine(position - new Vector2(radius * 0.1f, 0), position + new Vector2(radius * 0.1f, 0), light, 1.2f, true);
        }
    }

    private void DrawWire(Vector2 center, float radius, Color light, bool cable)
    {
        var width = cable ? radius * 0.2f : radius * 0.09f;
        DrawArc(center, radius * 0.48f, -0.35f, 5.05f, 28, _itemColor.Darkened(0.18f), width, true);
        DrawArc(center, radius * 0.48f, -0.35f, 5.05f, 28, light with { A = 0.72f }, 1.2f, true);
        var direction = Vector2.FromAngle(-0.35f);
        DrawLine(center + direction * radius * 0.47f, center + direction * radius * 0.68f, light, width, true);
    }

    private void DrawPipe(Vector2 center, float radius, Color body, Color light)
    {
        var start = center + new Vector2(-radius * 0.46f, -radius * 0.3f);
        var corner = center + new Vector2(-radius * 0.46f, radius * 0.32f);
        var end = center + new Vector2(radius * 0.46f, radius * 0.32f);
        DrawPolyline([start, corner, end], body, radius * 0.27f, true);
        DrawPolyline([start, corner, end], light with { A = 0.65f }, 1.2f, true);
        DrawCircle(start, radius * 0.19f, body.Darkened(0.25f));
        DrawArc(start, radius * 0.19f, 0, Mathf.Tau, 16, light, 1.2f, true);
    }

    private void DrawCircuit(Vector2 center, float radius, Color body, Color light)
    {
        var rect = new Rect2(center - Vector2.One * radius * 0.38f, Vector2.One * radius * 0.76f);
        DrawRect(rect, body, true);
        DrawRect(rect, light, false, 1.3f, true);
        for (var index = -1; index <= 1; index++)
        {
            var y = center.Y + index * radius * 0.22f;
            DrawLine(new Vector2(center.X - radius * 0.62f, y), new Vector2(center.X - radius * 0.38f, y), light, 1.2f, true);
            DrawLine(new Vector2(center.X + radius * 0.38f, y), new Vector2(center.X + radius * 0.62f, y), light, 1.2f, true);
        }

        DrawCircle(center, radius * 0.13f, light);
    }

    private void DrawBattery(Vector2 center, float radius, Color body, Color light)
    {
        var rect = new Rect2(center + new Vector2(-radius * 0.42f, -radius * 0.48f), new Vector2(radius * 0.84f, radius * 1.02f));
        DrawRect(rect, body, true);
        DrawRect(rect, light, false, 1.4f, true);
        DrawRect(new Rect2(center + new Vector2(-radius * 0.17f, -radius * 0.61f), new Vector2(radius * 0.34f, radius * 0.13f)), light, true);
        DrawLine(center - new Vector2(radius * 0.2f, 0), center + new Vector2(radius * 0.2f, 0), light, 1.8f, true);
        DrawLine(center - new Vector2(0, radius * 0.2f), center + new Vector2(0, radius * 0.2f), light, 1.8f, true);
    }

    private void DrawMotor(Vector2 center, float radius, Color body, Color light)
    {
        DrawCircle(center, radius * 0.5f, body);
        DrawArc(center, radius * 0.5f, 0, Mathf.Tau, 24, light, 1.4f, true);
        DrawCircle(center, radius * 0.23f, _itemColor.Darkened(0.46f));
        for (var index = 0; index < 6; index++)
        {
            var direction = Vector2.FromAngle(index * Mathf.Tau / 6);
            DrawLine(center + direction * radius * 0.27f, center + direction * radius * 0.46f, light, radius * 0.08f, true);
        }
    }

    private void DrawChip(Vector2 center, float radius, Color body, Color light)
    {
        var rect = new Rect2(center - Vector2.One * radius * 0.36f, Vector2.One * radius * 0.72f);
        DrawRect(rect, body.Darkened(0.28f), true);
        DrawRect(rect, light, false, 1.3f, true);
        for (var index = -2; index <= 2; index++)
        {
            var offset = index * radius * 0.17f;
            DrawLine(new Vector2(rect.Position.X - radius * 0.18f, center.Y + offset), new Vector2(rect.Position.X, center.Y + offset), light, 1.2f, true);
            DrawLine(new Vector2(rect.End.X, center.Y + offset), new Vector2(rect.End.X + radius * 0.18f, center.Y + offset), light, 1.2f, true);
        }

        DrawCircle(center, radius * 0.08f, light);
    }

    private void DrawMachinePart(Vector2 center, float radius, Color body, Color light)
    {
        const int pointCount = 16;
        var gear = new Vector2[pointCount];
        for (var index = 0; index < pointCount; index++)
        {
            var gearRadius = radius * (index % 2 == 0 ? 0.57f : 0.43f);
            gear[index] = center + Vector2.FromAngle(index * Mathf.Tau / pointCount) * gearRadius;
        }

        DrawColoredPolygon(gear, body);
        DrawPolyline(Close(gear), light, 1.2f, true);
        DrawCircle(center, radius * 0.2f, _itemColor.Darkened(0.5f));
    }

    private void DrawConveyor(Vector2 center, float radius, Color body, Color light)
    {
        var rect = new Rect2(center + new Vector2(-radius * 0.58f, -radius * 0.32f), new Vector2(radius * 1.16f, radius * 0.64f));
        DrawRect(rect, body.Darkened(0.25f), true);
        DrawRect(rect, light, false, 1.2f, true);
        for (var index = -1; index <= 1; index++)
        {
            var x = center.X + index * radius * 0.35f;
            DrawPolyline(
                [new Vector2(x - radius * 0.12f, center.Y - radius * 0.16f), new Vector2(x + radius * 0.1f, center.Y), new Vector2(x - radius * 0.12f, center.Y + radius * 0.16f)],
                light,
                radius * 0.09f,
                true);
        }
    }

    private void DrawShipPart(Vector2 center, float radius, Color body, Color light)
    {
        var ship = new[]
        {
            center + new Vector2(0, -radius * 0.6f),
            center + new Vector2(radius * 0.22f, -radius * 0.12f),
            center + new Vector2(radius * 0.56f, radius * 0.38f),
            center + new Vector2(radius * 0.16f, radius * 0.24f),
            center + new Vector2(0, radius * 0.52f),
            center + new Vector2(-radius * 0.16f, radius * 0.24f),
            center + new Vector2(-radius * 0.56f, radius * 0.38f),
            center + new Vector2(-radius * 0.22f, -radius * 0.12f),
        };
        DrawColoredPolygon(ship, body);
        DrawPolyline(Close(ship), light, 1.3f, true);
        DrawLine(center - new Vector2(0, radius * 0.3f), center + new Vector2(0, radius * 0.27f), light with { A = 0.65f }, 1.1f, true);
    }

    private void DrawDroplet(Vector2 center, float radius, Color body, Color light)
    {
        var drop = new[]
        {
            center + new Vector2(0, -radius * 0.62f),
            center + new Vector2(radius * 0.42f, radius * 0.12f),
            center + new Vector2(radius * 0.3f, radius * 0.48f),
            center + new Vector2(0, radius * 0.61f),
            center + new Vector2(-radius * 0.3f, radius * 0.48f),
            center + new Vector2(-radius * 0.42f, radius * 0.12f),
        };
        DrawColoredPolygon(drop, body);
        DrawPolyline(Close(drop), light, 1.3f, true);
        DrawArc(center + new Vector2(-radius * 0.08f, radius * 0.1f), radius * 0.22f, 2.6f, 4.7f, 8, Colors.White with { A = 0.5f }, 1.2f, true);
    }

    private void DrawGas(Vector2 center, float radius, Color body, Color light)
    {
        Vector2[] offsets = [new(-0.25f, 0.16f), new(0.18f, 0.22f), new(-0.02f, -0.24f)];
        for (var index = 0; index < offsets.Length; index++)
        {
            var position = center + offsets[index] * radius;
            var bubbleRadius = radius * (0.22f + index * 0.055f);
            DrawCircle(position, bubbleRadius, body with { A = 0.66f });
            DrawArc(position, bubbleRadius, 0, Mathf.Tau, 16, light with { A = 0.82f }, 1.1f, true);
        }
    }

    private void DrawFuel(Vector2 center, float radius, Color body, Color light)
    {
        var flame = new[]
        {
            center + new Vector2(-radius * 0.12f, -radius * 0.62f),
            center + new Vector2(radius * 0.4f, -radius * 0.02f),
            center + new Vector2(radius * 0.26f, radius * 0.48f),
            center + new Vector2(0, radius * 0.62f),
            center + new Vector2(-radius * 0.36f, radius * 0.35f),
            center + new Vector2(-radius * 0.42f, -radius * 0.08f),
        };
        DrawColoredPolygon(flame, body);
        DrawPolyline(Close(flame), light, 1.3f, true);
        DrawCircle(center + new Vector2(0, radius * 0.24f), radius * 0.13f, light with { A = 0.82f });
    }

    private void DrawContainer(Vector2 center, float radius, Color body, Color light, bool empty)
    {
        var canister = new Rect2(center + new Vector2(-radius * 0.42f, -radius * 0.5f), new Vector2(radius * 0.84f, radius));
        DrawRect(canister, body.Darkened(0.34f), true);
        if (!empty)
        {
            DrawRect(new Rect2(center + new Vector2(-radius * 0.31f, -radius * 0.04f), new Vector2(radius * 0.62f, radius * 0.42f)), body, true);
        }

        DrawRect(canister, light, false, 1.35f, true);
        DrawRect(new Rect2(center + new Vector2(-radius * 0.17f, -radius * 0.64f), new Vector2(radius * 0.34f, radius * 0.14f)), light, true);
        DrawLine(center + new Vector2(-radius * 0.29f, -radius * 0.28f), center + new Vector2(radius * 0.29f, -radius * 0.28f), light with { A = 0.68f }, 1.1f, true);
        if (empty)
        {
            DrawLine(center + new Vector2(-radius * 0.28f, radius * 0.3f), center + new Vector2(radius * 0.28f, -radius * 0.12f), light with { A = 0.5f }, 1.1f, true);
        }
    }

    private void DrawUnknown(Vector2 center, float radius, Color body, Color light)
    {
        DrawCircle(center, radius * 0.48f, body);
        DrawArc(center, radius * 0.48f, 0, Mathf.Tau, 20, light, 1.3f, true);
        DrawLine(center - new Vector2(radius * 0.21f, 0), center + new Vector2(radius * 0.21f, 0), light, 1.4f, true);
        DrawLine(center - new Vector2(0, radius * 0.21f), center + new Vector2(0, radius * 0.21f), light, 1.4f, true);
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

using Godot;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Lightweight procedural machine preview. It avoids duplicated bitmap assets
/// and can later be replaced by authored art without changing card APIs.
/// </summary>
public partial class MachineGlyphControl : Control
{
    private MachineGlyph _glyph;
    private bool _available = true;
    private Color? _accentOverride;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _ExitTree()
    {
        Resized -= QueueRedraw;
    }

    public void Configure(MachineGlyph glyph, bool available = true, Color? accentOverride = null)
    {
        _glyph = glyph;
        _available = available;
        _accentOverride = accentOverride;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var scale = Mathf.Min(Size.X, Size.Y) / 72f;
        var accent = _accentOverride ??
                     (_available ? BuildingUiTheme.Accent : BuildingUiTheme.TextMuted.Darkened(0.25f));
        var dim = accent.Darkened(0.55f);
        var fill = new Color(dim.R, dim.G, dim.B, 0.44f);

        var chassis = new Rect2(center - (new Vector2(25, 20) * scale), new Vector2(50, 40) * scale);
        DrawRect(chassis, fill, filled: true);
        DrawRect(chassis, accent, filled: false, width: Mathf.Max(1, 1.5f * scale), antialiased: true);
        DrawLine(
            new Vector2(chassis.Position.X + (6 * scale), chassis.End.Y + (4 * scale)),
            new Vector2(chassis.End.X - (6 * scale), chassis.End.Y + (4 * scale)),
            dim,
            Mathf.Max(1, 2 * scale),
            true);

        DrawGlyph(center, scale, accent, dim);
    }

    private void DrawGlyph(Vector2 center, float scale, Color accent, Color dim)
    {
        switch (_glyph)
        {
            case MachineGlyph.Crusher:
                DrawTriangle(center + new Vector2(-14, 0) * scale, 9 * scale, accent, pointsRight: true);
                DrawTriangle(center + new Vector2(14, 0) * scale, 9 * scale, accent, pointsRight: false);
                DrawLine(center + new Vector2(0, -13) * scale, center + new Vector2(0, 13) * scale, dim, 2 * scale, true);
                break;
            case MachineGlyph.Smelter:
                DrawFlame(center + new Vector2(0, 4) * scale, scale, accent, dim);
                break;
            case MachineGlyph.Foundry:
                DrawCircle(center + new Vector2(-11, -7) * scale, 5 * scale, accent);
                DrawCircle(center + new Vector2(11, -7) * scale, 5 * scale, accent);
                DrawLine(center + new Vector2(-11, -2) * scale, center + new Vector2(0, 9) * scale, dim, 3 * scale, true);
                DrawLine(center + new Vector2(11, -2) * scale, center + new Vector2(0, 9) * scale, dim, 3 * scale, true);
                DrawRect(new Rect2(center + new Vector2(-10, 8) * scale, new Vector2(20, 7) * scale), accent, true);
                break;
            case MachineGlyph.Constructor:
                DrawGear(center, 14 * scale, accent, dim);
                break;
            case MachineGlyph.Fabricator:
                DrawChip(center, scale, accent, dim);
                break;
            case MachineGlyph.WaterProcessor:
                DrawDrop(center, 14 * scale, accent, dim);
                break;
            case MachineGlyph.Electrolyzer:
                DrawCircle(center + new Vector2(-10, 2) * scale, 7 * scale, dim);
                DrawCircle(center + new Vector2(10, 2) * scale, 7 * scale, accent);
                DrawLine(center + new Vector2(0, -14) * scale, center + new Vector2(0, 14) * scale, accent, 2 * scale, true);
                break;
            case MachineGlyph.Refinery:
                DrawRect(new Rect2(center + new Vector2(-15, -12) * scale, new Vector2(9, 27) * scale), dim, true);
                DrawRect(new Rect2(center + new Vector2(3, -18) * scale, new Vector2(12, 33) * scale), accent, false, 2 * scale, true);
                DrawLine(center + new Vector2(-6, -6) * scale, center + new Vector2(3, -6) * scale, accent, 2 * scale, true);
                break;
            case MachineGlyph.BasicGenerator:
                DrawBolt(center, scale, accent);
                break;
            case MachineGlyph.FuelGenerator:
                DrawBolt(center + new Vector2(-7, 0) * scale, scale * 0.85f, accent);
                DrawDrop(center + new Vector2(12, 4) * scale, 8 * scale, dim, accent);
                break;
            case MachineGlyph.Storage:
                for (var row = -1; row <= 1; row++)
                {
                    DrawRect(new Rect2(center + new Vector2(-16, (row * 9) - 4) * scale, new Vector2(32, 7) * scale), row == 0 ? accent : dim, row == 0);
                }
                break;
            case MachineGlyph.Research:
                DrawCircle(center, 4 * scale, accent);
                DrawArc(center, 16 * scale, 0, Mathf.Tau, 28, dim, 1.5f * scale, true);
                DrawArc(center, 16 * scale, -0.9f, 0.9f, 18, accent, 2 * scale, true);
                DrawLine(center + new Vector2(-17, -12) * scale, center + new Vector2(17, 12) * scale, dim, 1.5f * scale, true);
                break;
        }
    }

    private void DrawTriangle(Vector2 center, float radius, Color color, bool pointsRight)
    {
        var direction = pointsRight ? 1 : -1;
        Vector2[] points =
        [
            center + new Vector2(direction * radius, 0),
            center + new Vector2(-direction * radius, -radius),
            center + new Vector2(-direction * radius, radius),
        ];
        DrawColoredPolygon(points, color);
    }

    private void DrawFlame(Vector2 center, float scale, Color accent, Color dim)
    {
        Vector2[] flame =
        [
            center + new Vector2(0, -18) * scale,
            center + new Vector2(11, -2) * scale,
            center + new Vector2(7, 14) * scale,
            center + new Vector2(0, 8) * scale,
            center + new Vector2(-8, 15) * scale,
            center + new Vector2(-11, -2) * scale,
        ];
        DrawColoredPolygon(flame, dim);
        DrawCircle(center + new Vector2(0, 4) * scale, 6 * scale, accent);
    }

    private void DrawGear(Vector2 center, float radius, Color accent, Color dim)
    {
        DrawCircle(center, radius, dim);
        DrawCircle(center, radius * 0.56f, BuildingUiTheme.PanelBackground);
        DrawCircle(center, radius * 0.25f, accent);
        for (var index = 0; index < 8; index++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * index / 8f);
            DrawLine(center + (direction * radius * 0.8f), center + (direction * radius * 1.25f), accent, 3, true);
        }
    }

    private void DrawChip(Vector2 center, float scale, Color accent, Color dim)
    {
        var chip = new Rect2(center - (new Vector2(11, 11) * scale), new Vector2(22, 22) * scale);
        DrawRect(chip, dim, true);
        DrawRect(chip, accent, false, 2 * scale, true);
        DrawCircle(center, 4 * scale, accent);
        for (var index = -1; index <= 1; index++)
        {
            var offset = index * 8 * scale;
            DrawLine(new Vector2(chip.Position.X - (5 * scale), center.Y + offset), new Vector2(chip.Position.X, center.Y + offset), accent, 1.5f * scale, true);
            DrawLine(new Vector2(chip.End.X, center.Y + offset), new Vector2(chip.End.X + (5 * scale), center.Y + offset), accent, 1.5f * scale, true);
        }
    }

    private void DrawDrop(Vector2 center, float radius, Color fill, Color outline)
    {
        Vector2[] points =
        [
            center + new Vector2(0, -radius),
            center + new Vector2(radius * 0.72f, radius * 0.22f),
            center + new Vector2(radius * 0.55f, radius * 0.75f),
            center + new Vector2(0, radius),
            center + new Vector2(-radius * 0.55f, radius * 0.75f),
            center + new Vector2(-radius * 0.72f, radius * 0.22f),
        ];
        DrawColoredPolygon(points, fill);
        var outlinePoints = points.Append(points[0]).ToArray();
        DrawPolyline(outlinePoints, outline, 1.7f, true);
    }

    private void DrawBolt(Vector2 center, float scale, Color color)
    {
        Vector2[] bolt =
        [
            center + new Vector2(3, -18) * scale,
            center + new Vector2(-11, 2) * scale,
            center + new Vector2(-2, 2) * scale,
            center + new Vector2(-6, 18) * scale,
            center + new Vector2(12, -5) * scale,
            center + new Vector2(3, -5) * scale,
        ];
        DrawColoredPolygon(bolt, color);
    }
}

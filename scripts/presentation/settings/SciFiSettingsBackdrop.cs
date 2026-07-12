using Godot;

namespace SpaceFactory.Presentation.Settings;

public partial class SciFiSettingsBackdrop : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.005f, 0.012f, 0.022f, 0.97f));

        var random = new RandomNumberGenerator { Seed = 0x5350414345464143UL };
        var starCount = Mathf.Clamp(Mathf.RoundToInt(Size.X * Size.Y / 9000.0f), 70, 220);
        for (var index = 0; index < starCount; index++)
        {
            var point = new Vector2(random.RandfRange(0, Size.X), random.RandfRange(0, Size.Y));
            var alpha = random.RandfRange(0.08f, 0.34f);
            DrawCircle(point, random.RandfRange(0.45f, 1.25f), new Color(0.08f, 0.67f, 0.92f, alpha));
        }

        var center = Size / 2;
        for (var ring = 0; ring < 4; ring++)
        {
            var radius = Mathf.Min(Size.X, Size.Y) * (0.24f + (ring * 0.095f));
            DrawArc(center, radius, 0.12f, 1.38f, 48, new Color(0.04f, 0.45f, 0.65f, 0.11f), 1);
            DrawArc(center, radius, 3.26f, 4.52f, 48, new Color(0.04f, 0.45f, 0.65f, 0.11f), 1);
        }

        var cyan = new Color(0.02f, 0.65f, 0.9f, 0.34f);
        var faint = new Color(0.04f, 0.32f, 0.46f, 0.3f);
        const float margin = 22;
        const float corner = 62;
        DrawLine(new Vector2(margin, margin + corner), new Vector2(margin, margin), cyan, 2);
        DrawLine(new Vector2(margin, margin), new Vector2(margin + corner, margin), cyan, 2);
        DrawLine(new Vector2(Size.X - margin - corner, margin), new Vector2(Size.X - margin, margin), cyan, 2);
        DrawLine(new Vector2(Size.X - margin, margin), new Vector2(Size.X - margin, margin + corner), cyan, 2);
        DrawLine(new Vector2(margin, Size.Y - margin - corner), new Vector2(margin, Size.Y - margin), cyan, 2);
        DrawLine(new Vector2(margin, Size.Y - margin), new Vector2(margin + corner, Size.Y - margin), cyan, 2);
        DrawLine(new Vector2(Size.X - margin - corner, Size.Y - margin), new Vector2(Size.X - margin, Size.Y - margin), cyan, 2);
        DrawLine(new Vector2(Size.X - margin, Size.Y - margin), new Vector2(Size.X - margin, Size.Y - margin - corner), cyan, 2);

        DrawLine(new Vector2(Size.X * 0.15f, margin), new Vector2(Size.X * 0.38f, margin), faint, 1);
        DrawLine(new Vector2(Size.X * 0.62f, margin), new Vector2(Size.X * 0.85f, margin), faint, 1);
        DrawLine(new Vector2(Size.X * 0.15f, Size.Y - margin), new Vector2(Size.X * 0.38f, Size.Y - margin), faint, 1);
        DrawLine(new Vector2(Size.X * 0.62f, Size.Y - margin), new Vector2(Size.X * 0.85f, Size.Y - margin), faint, 1);
    }
}

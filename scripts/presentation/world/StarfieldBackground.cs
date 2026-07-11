using Godot;

namespace SpaceFactory.Presentation.World;

public partial class StarfieldBackground : Control
{
    private const ulong StarSeed = 741029384;
    private const float StarsPerMillionPixels = 95.0f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);

        var random = new RandomNumberGenerator { Seed = StarSeed };
        var starCount = Mathf.Max(45, Mathf.RoundToInt(Size.X * Size.Y / 1_000_000.0f * StarsPerMillionPixels));

        for (var index = 0; index < starCount; index++)
        {
            var position = new Vector2(
                random.RandfRange(0, Size.X),
                random.RandfRange(0, Size.Y));
            var brightness = random.RandfRange(0.28f, 0.7f);
            var radius = random.RandfRange(0.45f, 1.05f);

            if (random.Randf() < 0.06f)
            {
                brightness = random.RandfRange(0.72f, 0.9f);
                radius = random.RandfRange(1.1f, 1.55f);
            }

            var temperature = random.Randf();
            var tint = temperature < 0.18f
                ? new Color(0.82f, 0.88f, 1.0f, brightness)
                : temperature > 0.92f
                    ? new Color(1.0f, 0.9f, 0.78f, brightness)
                    : new Color(0.94f, 0.96f, 1.0f, brightness);

            DrawCircle(position, radius, tint);
        }
    }
}

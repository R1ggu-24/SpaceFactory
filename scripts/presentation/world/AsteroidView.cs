using Godot;

namespace SpaceFactory.Presentation.World;

public partial class AsteroidView : Node2D
{
    public float Radius { get; set; } = 40;

    public Color Color { get; set; } = new(0.38f, 0.42f, 0.5f);

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, Color);
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 32, Color.Lightened(0.2f), 3);
        DrawCircle(new Vector2(-Radius * 0.25f, -Radius * 0.15f), Radius * 0.18f, Color.Darkened(0.22f));
    }
}

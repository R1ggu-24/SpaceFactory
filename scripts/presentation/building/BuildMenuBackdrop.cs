using Godot;

namespace SpaceFactory.Presentation.Building;

public partial class BuildMenuBackdrop : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _ExitTree()
    {
        Resized -= QueueRedraw;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.001f, 0.008f, 0.014f, 0.82f));

        var gridColor = new Color(0.03f, 0.3f, 0.4f, 0.07f);
        const int spacing = 88;
        for (var x = 0; x < Size.X; x += spacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridColor);
        }

        for (var y = 0; y < Size.Y; y += spacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridColor);
        }

        var horizon = new Color(0.04f, 0.5f, 0.64f, 0.085f);
        DrawLine(new Vector2(0, Size.Y * 0.5f), new Vector2(Size.X, Size.Y * 0.5f), horizon, 1.5f);
        DrawCorner(new Vector2(20, 20), new Vector2(1, 1));
        DrawCorner(new Vector2(Size.X - 20, 20), new Vector2(-1, 1));
        DrawCorner(new Vector2(20, Size.Y - 20), new Vector2(1, -1));
        DrawCorner(new Vector2(Size.X - 20, Size.Y - 20), new Vector2(-1, -1));
    }

    private void DrawCorner(Vector2 origin, Vector2 direction)
    {
        var color = new Color(0.05f, 0.67f, 0.88f, 0.48f);
        DrawLine(origin, origin + new Vector2(direction.X * 58, 0), color, 1.5f, true);
        DrawLine(origin, origin + new Vector2(0, direction.Y * 58), color, 1.5f, true);
    }
}

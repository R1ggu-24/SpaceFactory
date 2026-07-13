using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryBackdrop : Control
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
        // A translucent technical overlay keeps the game world readable without
        // relying on an expensive realtime blur.
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.002f, 0.009f, 0.015f, 0.76f));
        DrawRect(new Rect2(0, 0, Size.X, 5), new Color(0.02f, 0.45f, 0.62f, 0.16f));

        var gridColor = new Color(0.04f, 0.34f, 0.44f, 0.075f);
        const int spacing = 96;
        for (var x = 0; x < Size.X; x += spacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridColor, 1);
        }

        for (var y = 0; y < Size.Y; y += spacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridColor, 1);
        }

        var edge = new Color(0.03f, 0.66f, 0.86f, 0.45f);
        DrawCorner(new Vector2(18, 18), new Vector2(1, 1), edge);
        DrawCorner(new Vector2(Size.X - 18, 18), new Vector2(-1, 1), edge);
        DrawCorner(new Vector2(18, Size.Y - 18), new Vector2(1, -1), edge);
        DrawCorner(new Vector2(Size.X - 18, Size.Y - 18), new Vector2(-1, -1), edge);

        var centerLine = new Color(0.06f, 0.48f, 0.62f, 0.12f);
        DrawLine(new Vector2((Size.X * 0.5f) - 42, 15), new Vector2((Size.X * 0.5f) + 42, 15), centerLine, 2);
        DrawLine(
            new Vector2((Size.X * 0.5f) - 24, Size.Y - 15),
            new Vector2((Size.X * 0.5f) + 24, Size.Y - 15),
            centerLine,
            2);
    }

    private void DrawCorner(Vector2 origin, Vector2 direction, Color color)
    {
        DrawLine(origin, origin + new Vector2(direction.X * 62, 0), color, 1.5f, true);
        DrawLine(origin, origin + new Vector2(0, direction.Y * 62), color, 1.5f, true);
    }
}

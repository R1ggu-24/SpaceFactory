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
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.002f, 0.012f, 0.022f, 0.94f));
        var gridColor = new Color(0.04f, 0.32f, 0.45f, 0.12f);
        const int spacing = 72;
        for (var x = 0; x < Size.X; x += spacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridColor, 1);
        }

        for (var y = 0; y < Size.Y; y += spacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridColor, 1);
        }

        var edge = new Color(0.03f, 0.76f, 1, 0.58f);
        DrawCorner(new Vector2(26, 26), new Vector2(1, 1), edge);
        DrawCorner(new Vector2(Size.X - 26, 26), new Vector2(-1, 1), edge);
        DrawCorner(new Vector2(26, Size.Y - 26), new Vector2(1, -1), edge);
        DrawCorner(new Vector2(Size.X - 26, Size.Y - 26), new Vector2(-1, -1), edge);
    }

    private void DrawCorner(Vector2 origin, Vector2 direction, Color color)
    {
        DrawLine(origin, origin + new Vector2(direction.X * 76, 0), color, 2, true);
        DrawLine(origin, origin + new Vector2(0, direction.Y * 76), color, 2, true);
    }
}

using Godot;

namespace SpaceFactory.Presentation.WorldMap;

public partial class MapFrameControl : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, new Color(0.012f, 0.025f, 0.036f, 0.94f));
        DrawRect(rect.Grow(-1), new Color(0.08f, 0.43f, 0.58f, 0.62f), false, 1.5f);
        DrawRect(rect.Grow(-5), new Color(0.04f, 0.14f, 0.19f, 0.78f), false, 1.0f);

        const float cornerLength = 42;
        const float inset = 10;
        var topLeft = new Vector2(inset, inset);
        var topRight = new Vector2(Size.X - inset, inset);
        var bottomLeft = new Vector2(inset, Size.Y - inset);
        var bottomRight = new Vector2(Size.X - inset, Size.Y - inset);
        DrawCorner(topLeft, Vector2.Right, Vector2.Down, cornerLength);
        DrawCorner(topRight, Vector2.Left, Vector2.Down, cornerLength);
        DrawCorner(bottomLeft, Vector2.Right, Vector2.Up, cornerLength);
        DrawCorner(bottomRight, Vector2.Left, Vector2.Up, cornerLength);

        for (var index = 0; index < 5; index++)
        {
            var x = (Size.X * 0.5f) + ((index - 2) * 13);
            DrawRect(new Rect2(new Vector2(x - 3, 13), new Vector2(7, 3)), MapVisualPalette.Cyan);
        }
    }

    private void DrawCorner(Vector2 origin, Vector2 horizontal, Vector2 vertical, float length)
    {
        DrawLine(origin, origin + (horizontal * length), MapVisualPalette.Cyan, 2, true);
        DrawLine(origin, origin + (vertical * length), MapVisualPalette.Cyan, 2, true);
    }
}

using Godot;
using SpaceFactory.Core.World.Sectors;

namespace SpaceFactory.Presentation.WorldMap;

public partial class WorldMapController : Control
{
    private SectorCoordinate _current;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("open_map"))
        {
            Visible = !Visible;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetCurrentSector(SectorCoordinate coordinate)
    {
        _current = coordinate;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2;
        const float cellSize = 72;
        for (var y = -3; y <= 3; y++)
        {
            for (var x = -5; x <= 5; x++)
            {
                var rect = new Rect2(center + new Vector2(x * cellSize, y * cellSize) - new Vector2(30, 30), new Vector2(60, 60));
                var selected = x == 0 && y == 0;
                DrawRect(rect, selected ? new Color(0.2f, 0.75f, 1, 0.8f) : new Color(0.15f, 0.2f, 0.32f, 0.8f));
                DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(7, 35), $"{_current.X + x},{_current.Y + y}", HorizontalAlignment.Left, -1, 14, Colors.White);
            }
        }
    }
}

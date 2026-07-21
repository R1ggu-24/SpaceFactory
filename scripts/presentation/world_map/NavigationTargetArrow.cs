using Godot;
using SpaceFactory.Core.Common;

namespace SpaceFactory.Presentation.WorldMap;

public partial class NavigationTargetArrow : Control
{
    private MapShipState _ship = MapViewState.Empty.Ship;
    private WorldPosition? _targetPosition;

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

    public void SetSnapshot(MapViewState snapshot)
    {
        _ship = snapshot.Ship;
        _targetPosition = snapshot.FindActiveTargetPosition();
        QueueRedraw();
    }

    public void SetShipState(MapShipState ship)
    {
        _ship = ship;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_targetPosition is null || !_ship.IsInShip || Size.X < 100 || Size.Y < 100)
        {
            return;
        }

        var delta = ToVector(_targetPosition.Value) - ToVector(_ship.Position);
        if (delta.LengthSquared() < 1)
        {
            return;
        }

        var direction = delta.Normalized();
        var center = Size * 0.5f;
        const float horizontalInset = 72;
        const float verticalInset = 64;
        var halfWidth = Mathf.Max(1, center.X - horizontalInset);
        var halfHeight = Mathf.Max(1, center.Y - verticalInset);
        var horizontalFactor = Mathf.Abs(direction.X) < 0.0001f
            ? float.PositiveInfinity
            : halfWidth / Mathf.Abs(direction.X);
        var verticalFactor = Mathf.Abs(direction.Y) < 0.0001f
            ? float.PositiveInfinity
            : halfHeight / Mathf.Abs(direction.Y);
        var arrowCenter = center + (direction * Mathf.Min(horizontalFactor, verticalFactor));
        var angle = direction.Angle() + (Mathf.Pi * 0.5f);
        var forward = Vector2.Up.Rotated(angle);
        var side = forward.Orthogonal();
        var polygon = new[]
        {
            arrowCenter + (forward * 15),
            arrowCenter - (forward * 11) + (side * 9),
            arrowCenter - (forward * 5),
            arrowCenter - (forward * 11) - (side * 9),
        };
        DrawCircle(arrowCenter, 21, new Color(0.02f, 0.12f, 0.17f, 0.80f));
        DrawCircle(arrowCenter, 21, new Color(0.12f, 0.67f, 0.86f, 0.75f), false, 1.3f);
        DrawColoredPolygon(polygon, new Color(0.18f, 0.82f, 1.0f));
        DrawPolyline([.. polygon, polygon[0]], new Color(0.82f, 0.97f, 1.0f), 1.2f, true);

        var distance = delta.Length();
        var label = distance >= 1_000 ? $"{distance / 1_000:0.0}k" : $"{distance:0}";
        var labelPosition = arrowCenter - (direction * 31) + new Vector2(-22, 5);
        DrawString(ThemeDB.FallbackFont, labelPosition, label,
            HorizontalAlignment.Center, 44, 12, MapVisualPalette.TextPrimary);
    }

    private static Vector2 ToVector(WorldPosition position) => new((float)position.X, (float)position.Y);
}

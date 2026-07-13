using Godot;

namespace SpaceFactory.Presentation.WorldMap;

public partial class MinimapShipIndicator : Control
{
    private float _shipRotation;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
#if DEBUG
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            RunRotationSmokeTest();
        }
#endif
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }

    public void SetShipRotation(float rotationRadians)
    {
        if (Mathf.IsEqualApprox(_shipRotation, rotationRadians))
        {
            return;
        }

        _shipRotation = rotationRadians;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var direction = GetShipDirection(_shipRotation);
        var side = direction.Orthogonal();
        var ship = new[]
        {
            center + (direction * 13),
            center - (direction * 10) + (side * 8),
            center,
            center - (direction * 10) - (side * 8),
        };
        DrawColoredPolygon(ship, new Color(0.20f, 0.82f, 1.0f));
        DrawPolyline([.. ship, ship[0]], new Color(0.80f, 0.97f, 1.0f), 1.4f, true);
        DrawLine(
            center + (direction * 18),
            center + (direction * 27),
            new Color(0.26f, 0.84f, 1.0f, 0.9f),
            2,
            true);
    }

    private static Vector2 GetShipDirection(float rotationRadians) =>
        Vector2.Up.Rotated(rotationRadians);

#if DEBUG
    private static void RunRotationSmokeTest()
    {
        var north = GetShipDirection(0);
        var east = GetShipDirection(Mathf.Pi * 0.5f);
        if (!north.IsEqualApprox(Vector2.Up) || !east.IsEqualApprox(Vector2.Right))
        {
            throw new InvalidOperationException(
                "Minimap smoke test failed: the center ship symbol must follow world rotation.");
        }

        GD.Print("MINIMAP_SHIP_ROTATION_SMOKE_OK: center symbol tracks ship heading");
    }
#endif
}

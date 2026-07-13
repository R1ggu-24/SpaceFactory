using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.WorldMap;

public partial class CircularMinimapControl : Control
{
    public const float DefaultScanRadius = 14_000.0f;

    [Export(PropertyHint.Range, "3000,16000,250")]
    public float ScanRadius { get; set; } = DefaultScanRadius;

    private MapViewState _snapshot = MapViewState.Empty;
    private MapShipState _ship = MapViewState.Empty.Ship;
    private MinimapShipIndicator _shipIndicator = null!;
    private bool _isHovered;
    private IReadOnlyDictionary<string, ResourceDefinition> _resourceCatalog =
        new Dictionary<string, ResourceDefinition>();

    /// <summary>
    /// Raised once for a primary-button click inside the circular map surface.
    /// The owner decides how the shared primary-UI state opens the world map.
    /// </summary>
    public event Action? OpenMapRequested;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _shipIndicator = GetNode<MinimapShipIndicator>("ShipIndicator");
        MouseExited += HandleMouseExited;
#if DEBUG
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            RunNorthUpSmokeTest();
        }
#endif
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        MouseExited -= HandleMouseExited;
    }

    public override bool _HasPoint(Vector2 point)
    {
        var radius = Mathf.Min(Size.X, Size.Y) * 0.5f;
        return point.DistanceSquaredTo(Size * 0.5f) <= radius * radius;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            if (!_isHovered)
            {
                _isHovered = true;
                QueueRedraw();
            }

            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: true,
            })
        {
            OpenMapRequested?.Invoke();
            AcceptEvent();
        }
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
        _snapshot = snapshot;
        _ship = snapshot.Ship;
        _shipIndicator.SetShipRotation((float)_ship.RotationRadians);
        Visible = _ship.IsInShip;
        QueueRedraw();
    }

    public void SetResourceCatalog(IReadOnlyList<ResourceDefinition> resources)
    {
        _resourceCatalog = resources.ToDictionary(resource => resource.Id.Value, StringComparer.Ordinal);
        QueueRedraw();
    }

    public void SetShipState(MapShipState ship)
    {
        var positionChanged = ship.Position != _ship.Position;
        var visibilityChanged = ship.IsInShip != _ship.IsInShip;
        _ship = ship;
        _shipIndicator.SetShipRotation((float)ship.RotationRadians);
        Visible = ship.IsInShip;
        if (!Visible)
        {
            _isHovered = false;
        }

        if (positionChanged || visibilityChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!_ship.IsInShip || Size.X <= 1 || Size.Y <= 1)
        {
            return;
        }

        var center = Size * 0.5f;
        var outerRadius = Mathf.Max(1, Mathf.Min(Size.X, Size.Y) * 0.5f - 2);
        var contentRadius = outerRadius - 15;
        DrawCircle(center, outerRadius, new Color(0.006f, 0.015f, 0.023f, 0.96f));
        DrawCircle(center, contentRadius, new Color(0.018f, 0.035f, 0.048f, 0.98f));
        DrawStars(center, contentRadius);
        DrawCircle(center, contentRadius * 0.66f, new Color(0.10f, 0.34f, 0.43f, 0.35f), false, 1);
        DrawCircle(center, contentRadius * 0.33f, new Color(0.10f, 0.34f, 0.43f, 0.28f), false, 1);
        DrawLine(center + new Vector2(-contentRadius, 0), center + new Vector2(contentRadius, 0),
            new Color(0.09f, 0.28f, 0.35f, 0.32f), 1);
        DrawLine(center + new Vector2(0, -contentRadius), center + new Vector2(0, contentRadius),
            new Color(0.09f, 0.28f, 0.35f, 0.32f), 1);

        foreach (var item in _snapshot.Comets)
        {
            if (!item.Exists)
            {
                continue;
            }

            var relative = ToVector(item.WorldPosition) - ToVector(_ship.Position);
            if (relative.LengthSquared() > ScanRadius * ScanRadius)
            {
                continue;
            }

            var markerCenter = center + ProjectNorthUp(relative, ScanRadius, contentRadius);
            var markerRadius = Mathf.Clamp(
                ((float)item.Radius / ScanRadius) * contentRadius,
                MapVisualPalette.GetMinimumMarkerRadius(item.Size),
                14);
            var polygon = MapVisualPalette.CreateCometPolygon(item, markerCenter, markerRadius, 9);
            DrawColoredPolygon(polygon, MapVisualPalette.GetCometColor(item.Size));
            DrawPolyline([.. polygon, polygon[0]], new Color(0.05f, 0.06f, 0.065f, 0.94f), 1, true);
            if (item.IsLandable)
            {
                DrawCircle(markerCenter, markerRadius + 3, new Color(0.85f, 0.65f, 0.28f, 0.82f), false, 1.5f);
            }

            DrawResourcePips(item, markerCenter, markerRadius);
        }

        var interactiveBorder = _isHovered
            ? new Color(0.22f, 0.82f, 1.0f, 1.0f)
            : new Color(0.08f, 0.55f, 0.72f, 0.88f);
        DrawCircle(center, contentRadius, interactiveBorder, false, _isHovered ? 2.8f : 2);
        DrawCircle(center, outerRadius - 2, new Color(0.04f, 0.20f, 0.28f, 0.94f), false, 8);
        DrawCircle(
            center,
            outerRadius - 1,
            _isHovered ? new Color(0.32f, 0.88f, 1.0f) : MapVisualPalette.Cyan,
            false,
            _isHovered ? 2.2f : 1.5f);
        DrawString(ThemeDB.FallbackFont, new Vector2(center.X - 5, 17), "N",
            HorizontalAlignment.Left, -1, 11, MapVisualPalette.TextMuted);
    }

    private void HandleMouseExited()
    {
        if (!_isHovered)
        {
            return;
        }

        _isHovered = false;
        QueueRedraw();
    }

    private void DrawResourcePips(DiscoveredCometData item, Vector2 center, float cometRadius)
    {
        var count = Math.Min(3, item.DetectedResourceIds.Count);
        for (var index = 0; index < count; index++)
        {
            var angle = -Mathf.Pi * 0.72f + (index * Mathf.Pi * 0.32f);
            var pipPosition = center + (Vector2.FromAngle(angle) * (cometRadius + 3));
            var resourceId = item.DetectedResourceIds[index].Value;
            var color = _resourceCatalog.TryGetValue(resourceId, out var resource)
                ? MapVisualPalette.ParseColor(resource.BaseColorHex, MapVisualPalette.Cyan)
                : MapVisualPalette.GetResourceColor(resourceId);
            DrawCircle(pipPosition, 1.7f, color);
        }
    }

    private void DrawStars(Vector2 center, float radius)
    {
        for (var index = 0; index < 28; index++)
        {
            var angle = index * 2.399963f;
            var distance = radius * (0.15f + (0.82f * ((index * 37 % 97) / 96.0f)));
            var point = center + (Vector2.FromAngle(angle) * distance);
            DrawCircle(point, index % 7 == 0 ? 1.1f : 0.65f,
                new Color(0.38f, 0.60f, 0.68f, index % 5 == 0 ? 0.50f : 0.24f));
        }
    }

    private static Vector2 ProjectNorthUp(Vector2 worldOffset, float scanRadius, float contentRadius) =>
        (worldOffset / scanRadius) * contentRadius;

#if DEBUG
    private static void RunNorthUpSmokeTest()
    {
        var north = ProjectNorthUp(new Vector2(0, -1_000), 10_000, 100);
        var east = ProjectNorthUp(new Vector2(1_000, 0), 10_000, 100);
        if (!north.IsEqualApprox(new Vector2(0, -10)) ||
            !east.IsEqualApprox(new Vector2(10, 0)))
        {
            throw new InvalidOperationException(
                "Minimap smoke test failed: world north/east projection must remain screen up/right.");
        }

        GD.Print("MINIMAP_NORTH_UP_SMOKE_OK: fixed world axes");
    }
#endif

    private static Vector2 ToVector(WorldPosition position) => new((float)position.X, (float)position.Y);
}

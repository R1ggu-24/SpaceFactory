using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.WorldMap;

public partial class WorldMapCanvas : Control
{
    private const float MinimumZoom = 0.006f;
    private const float MaximumZoom = 0.18f;
    private const float DefaultZoom = 0.025f;
    private const float DragThreshold = 4;

    private readonly List<VisibleComet> _visibleComets = [];
    private IReadOnlyDictionary<string, ResourceDefinition> _resourceCatalog =
        new Dictionary<string, ResourceDefinition>();
    private MapViewState _snapshot = MapViewState.Empty;
    private MapShipState _ship = MapViewState.Empty.Ship;
    private Vector2 _viewCenter;
    private float _zoom = DefaultZoom;
    private bool _viewInitialized;
    private bool _pointerDown;
    private bool _didDrag;
    private MouseButton _dragButton;
    private Vector2 _pressPosition;
    private string? _pressedObjectId;
    private string? _hoveredObjectId;
    private Vector2 _hoverPosition;
    private bool _landableOnly;
    private bool _isFollowingShip;

    public event Action<string?>? TargetSelectionRequested;
    public event Action<WorldPosition>? MarkerPlacementRequested;
    public event Action<bool>? ShipFollowingChanged;

    public float Zoom => _zoom;
    public bool IsFollowingShip => _isFollowingShip;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Cross;
        ClipContents = true;
#if DEBUG
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            RunNavigationSmokeTest();
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

    public void SetSnapshot(MapViewState snapshot)
    {
        _snapshot = snapshot;
        _ship = snapshot.Ship;
        if (!_viewInitialized)
        {
            FocusPosition(snapshot.Ship.Position);
            _viewInitialized = true;
        }
        else if (_isFollowingShip)
        {
            _viewCenter = ToVector(snapshot.Ship.Position);
        }

        QueueRedraw();
    }

    public void SetResourceCatalog(IReadOnlyList<ResourceDefinition> resources)
    {
        _resourceCatalog = resources.ToDictionary(resource => resource.Id.Value, StringComparer.Ordinal);
        QueueRedraw();
    }

    public void SetShipState(MapShipState ship)
    {
        if (_ship == ship)
        {
            return;
        }

        _ship = ship;
        if (_isFollowingShip)
        {
            _viewCenter = ToVector(ship.Position);
        }

        QueueRedraw();
    }

    public void SetLandableOnly(bool enabled)
    {
        if (_landableOnly == enabled)
        {
            return;
        }

        _landableOnly = enabled;
        _hoveredObjectId = null;
        QueueRedraw();
    }

    /// <summary>Centers once without changing the current follow mode.</summary>
    public void CenterOnShip() => FocusPosition(_ship.Position);

    /// <summary>Compatibility entry point: focusing the ship enables continuous following.</summary>
    public void FocusShip() => SetShipFollowing(true);

    public void SetShipFollowing(bool enabled)
    {
        if (_isFollowingShip == enabled)
        {
            if (enabled)
            {
                CenterOnShip();
            }

            return;
        }

        _isFollowingShip = enabled;
        if (enabled)
        {
            CenterOnShip();
        }
        else
        {
            QueueRedraw();
        }

        ShipFollowingChanged?.Invoke(enabled);
    }

    public bool FocusObject(string? objectId)
    {
        var item = _snapshot.FindComet(objectId);
        if (item is null)
        {
            return false;
        }

        SetShipFollowing(false);
        FocusPosition(item.WorldPosition);
        return true;
    }

    public void ResetView()
    {
        _zoom = DefaultZoom;
        CenterOnShip();
    }

    public void CancelPointerInteraction()
    {
        _pointerDown = false;
        _didDrag = false;
        _pressedObjectId = null;
        _hoveredObjectId = null;
        MouseDefaultCursorShape = CursorShape.Cross;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && button.Pressed)
            {
                var factor = button.ButtonIndex == MouseButton.WheelUp ? 1.18f : 1.0f / 1.18f;
                ZoomAt(button.Position, factor);
                AcceptEvent();
                return;
            }

            if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                var worldPosition = ScreenToWorld(button.Position);
                if (IsDiscovered(worldPosition))
                {
                    if (_isFollowingShip && IsDifferentMapLocation(button.Position))
                    {
                        SetShipFollowing(false);
                    }

                    MarkerPlacementRequested?.Invoke(worldPosition);
                }

                AcceptEvent();
                return;
            }

            if (button.ButtonIndex is MouseButton.Left or MouseButton.Middle)
            {
                HandleDragButton(button);
                AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion motion)
        {
            HandlePointerMotion(motion);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), MapVisualPalette.MapBackground);
        DrawBackgroundGrid();
        DrawScannedSectors();
        DrawFlightRoute();
        _visibleComets.Clear();
        DrawDiscoveredObjects();
        DrawCustomMarkers();
        DrawShip();
        DrawScaleIndicator();
        DrawTooltip();
    }

    private void HandleDragButton(InputEventMouseButton button)
    {
        if (button.Pressed)
        {
            _pointerDown = true;
            _didDrag = false;
            _dragButton = button.ButtonIndex;
            _pressPosition = button.Position;
            _pressedObjectId = button.ButtonIndex == MouseButton.Left
                ? FindCometAt(button.Position)?.Item.Id
                : null;
            return;
        }

        if (!_pointerDown || button.ButtonIndex != _dragButton)
        {
            return;
        }

        if (!_didDrag && button.ButtonIndex == MouseButton.Left)
        {
            var releasedOver = FindCometAt(button.Position)?.Item.Id;
            if (_pressedObjectId is not null && releasedOver == _pressedObjectId)
            {
                var newTarget = _snapshot.ActiveTargetCometId == _pressedObjectId
                    ? null
                    : _pressedObjectId;
                if (newTarget is not null)
                {
                    SetShipFollowing(false);
                }

                TargetSelectionRequested?.Invoke(newTarget);
            }
        }

        _pointerDown = false;
        _didDrag = false;
        _pressedObjectId = null;
        MouseDefaultCursorShape = CursorShape.Cross;
    }

    private void HandlePointerMotion(InputEventMouseMotion motion)
    {
        _hoverPosition = motion.Position;
        if (_pointerDown)
        {
            if (!_didDrag && motion.Position.DistanceTo(_pressPosition) >= DragThreshold)
            {
                _didDrag = true;
                SetShipFollowing(false);
                MouseDefaultCursorShape = CursorShape.Drag;
            }

            if (_didDrag)
            {
                _viewCenter -= motion.Relative / _zoom;
                _hoveredObjectId = null;
                QueueRedraw();
            }

            return;
        }

        var hover = FindCometAt(motion.Position);
        var hoverId = hover?.Item.Id;
        if (_hoveredObjectId != hoverId)
        {
            _hoveredObjectId = hoverId;
            QueueRedraw();
            return;
        }

        if (hoverId is not null)
        {
            // Keep the tooltip anchored to the pointer without redrawing an idle map.
            QueueRedraw();
        }
    }

    private void DrawBackgroundGrid()
    {
        var majorWorldSpacing = GetGridWorldSpacing();
        var majorScreenSpacing = majorWorldSpacing * _zoom;
        if (majorScreenSpacing < 8)
        {
            return;
        }

        var centerScreen = Size * 0.5f;
        var offsetX = PositiveModulo(centerScreen.X - (_viewCenter.X * _zoom), majorScreenSpacing);
        var offsetY = PositiveModulo(centerScreen.Y - (_viewCenter.Y * _zoom), majorScreenSpacing);
        var color = new Color(0.06f, 0.20f, 0.26f, 0.18f);
        for (var x = offsetX; x < Size.X; x += majorScreenSpacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), color, 1);
        }

        for (var y = offsetY; y < Size.Y; y += majorScreenSpacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), color, 1);
        }
    }

    private void DrawScannedSectors()
    {
        foreach (var sector in _snapshot.Chunks)
        {
            if (sector.Status == ChunkDiscoveryStatus.Unknown)
            {
                continue;
            }

            var topLeft = WorldToScreen(sector.WorldOrigin);
            var pixelSize = _snapshot.SectorSize * _zoom;
            var rect = new Rect2(topLeft, new Vector2(pixelSize, pixelSize));
            if (!rect.Intersects(new Rect2(Vector2.Zero, Size)))
            {
                continue;
            }

            var fill = sector.Status == ChunkDiscoveryStatus.ActiveLoaded
                ? new Color(0.025f, 0.075f, 0.095f, 0.72f)
                : new Color(0.018f, 0.050f, 0.064f, 0.58f);
            DrawRect(rect, fill);
            if (pixelSize >= 22)
            {
                DrawRect(rect, new Color(0.08f, 0.31f, 0.39f, 0.30f), false, 1);
            }
        }
    }

    private void DrawFlightRoute()
    {
        if (_snapshot.FlightRoute.Count < 2)
        {
            return;
        }

        var points = _snapshot.FlightRoute.Select(WorldToScreen).ToArray();
        DrawPolyline(points, new Color(0.16f, 0.66f, 0.82f, 0.24f), 1.4f, true);
    }

    private void DrawDiscoveredObjects()
    {
        foreach (var item in _snapshot.Comets)
        {
            if (!item.Exists || (_landableOnly && !item.IsLandable))
            {
                continue;
            }

            var center = WorldToScreen(item.WorldPosition);
            var radius = Mathf.Clamp(
                (float)item.Radius * _zoom,
                MapVisualPalette.GetMinimumMarkerRadius(item.Size),
                46);
            if (center.X < -radius || center.Y < -radius ||
                center.X > Size.X + radius || center.Y > Size.Y + radius)
            {
                continue;
            }

            var polygon = MapVisualPalette.CreateCometPolygon(item, center, radius, 12);
            var color = MapVisualPalette.GetCometColor(item.Size);
            DrawColoredPolygon(polygon, color);
            DrawPolyline([.. polygon, polygon[0]], color.Darkened(0.50f), 1.2f, true);
            if (item.IsLandable)
            {
                DrawCircle(center, radius + 4, new Color(0.80f, 0.62f, 0.30f, 0.72f), false, 1.5f);
            }

            if (item.IsVisited)
            {
                DrawCircle(center, 2, new Color(0.38f, 0.92f, 0.72f));
            }

            DrawResourcePips(item, center, radius);
            if (_snapshot.ActiveTargetCometId == item.Id)
            {
                DrawTargetBrackets(center, radius + 10);
            }

            _visibleComets.Add(new VisibleComet(item, center, Mathf.Max(radius + 7, 9)));
        }
    }

    private void DrawCustomMarkers()
    {
        foreach (var marker in _snapshot.Markers)
        {
            var point = WorldToScreen(marker.WorldPosition);
            if (!new Rect2(Vector2.Zero, Size).HasPoint(point))
            {
                continue;
            }

            var color = MapVisualPalette.ParseColor(marker.ColorHex, MapVisualPalette.Cyan);
            DrawLine(point + new Vector2(0, 5), point + new Vector2(0, -8), color, 2, true);
            DrawCircle(point + new Vector2(0, -10), 4, color);
        }
    }

    private void DrawShip()
    {
        var center = WorldToScreen(_ship.Position);
        var direction = Vector2.Up.Rotated((float)_ship.RotationRadians);
        var side = direction.Orthogonal();
        var polygon = new[]
        {
            center + (direction * 12),
            center - (direction * 8) + (side * 8),
            center - (direction * 3),
            center - (direction * 8) - (side * 8),
        };
        DrawColoredPolygon(polygon, new Color(0.16f, 0.78f, 1.0f));
        DrawPolyline([.. polygon, polygon[0]], new Color(0.78f, 0.96f, 1.0f), 1.3f, true);
        DrawCircle(center, 17, new Color(0.16f, 0.70f, 0.92f, 0.30f), false, 1);
    }

    private void DrawResourcePips(DiscoveredCometData item, Vector2 center, float radius)
    {
        var count = Math.Min(4, item.DetectedResourceIds.Count);
        for (var index = 0; index < count; index++)
        {
            var x = center.X + ((index - ((count - 1) * 0.5f)) * 5);
            var point = new Vector2(x, center.Y + radius + 5);
            DrawCircle(point, 1.8f, GetResourceColor(item.DetectedResourceIds[index].Value));
        }
    }

    private void DrawTargetBrackets(Vector2 center, float radius)
    {
        var color = new Color(0.16f, 0.80f, 1.0f, 0.94f);
        const float length = 6;
        DrawLine(center + new Vector2(-radius, -radius), center + new Vector2(-radius + length, -radius), color, 2);
        DrawLine(center + new Vector2(-radius, -radius), center + new Vector2(-radius, -radius + length), color, 2);
        DrawLine(center + new Vector2(radius, -radius), center + new Vector2(radius - length, -radius), color, 2);
        DrawLine(center + new Vector2(radius, -radius), center + new Vector2(radius, -radius + length), color, 2);
        DrawLine(center + new Vector2(-radius, radius), center + new Vector2(-radius + length, radius), color, 2);
        DrawLine(center + new Vector2(-radius, radius), center + new Vector2(-radius, radius - length), color, 2);
        DrawLine(center + new Vector2(radius, radius), center + new Vector2(radius - length, radius), color, 2);
        DrawLine(center + new Vector2(radius, radius), center + new Vector2(radius, radius - length), color, 2);
    }

    private void DrawScaleIndicator()
    {
        const float width = 110;
        var worldDistance = width / _zoom;
        var label = worldDistance >= 1_000
            ? $"{worldDistance / 1_000:0.#}k"
            : $"{worldDistance:0}";
        var y = Size.Y - 18;
        DrawLine(new Vector2(18, y), new Vector2(18 + width, y), MapVisualPalette.TextMuted, 1.5f);
        DrawLine(new Vector2(18, y - 4), new Vector2(18, y + 4), MapVisualPalette.TextMuted, 1.5f);
        DrawLine(new Vector2(18 + width, y - 4), new Vector2(18 + width, y + 4), MapVisualPalette.TextMuted, 1.5f);
        DrawString(ThemeDB.FallbackFont, new Vector2(22, y - 7), label,
            HorizontalAlignment.Left, -1, 12, MapVisualPalette.TextMuted);
    }

    private void DrawTooltip()
    {
        var item = _snapshot.FindComet(_hoveredObjectId);
        if (item is null || _pointerDown)
        {
            return;
        }

        var resources = item.DetectedResourceIds.Count == 0
            ? "Keine Rohstoffe erfasst"
            : string.Join(", ", item.DetectedResourceIds.Take(3).Select(resource =>
                _resourceCatalog.TryGetValue(resource.Value, out var definition)
                    ? definition.DisplayName
                    : resource.Value));
        var distance = ToVector(item.WorldPosition).DistanceTo(ToVector(_ship.Position));
        var lines = new[]
        {
            MapVisualPalette.GetCometDisplayName(item.Size),
            item.IsLandable ? "Landbar" : "Nicht landbar",
            item.IsVisited ? "Besucht" : "Noch nicht besucht",
            $"Entfernung  {FormatDistance(distance)}",
            resources,
        };
        var tooltipSize = new Vector2(260, 108);
        var position = _hoverPosition + new Vector2(16, 18);
        position.X = Mathf.Min(position.X, Size.X - tooltipSize.X - 8);
        position.Y = Mathf.Min(position.Y, Size.Y - tooltipSize.Y - 8);
        position.X = Mathf.Max(8, position.X);
        position.Y = Mathf.Max(8, position.Y);
        DrawRect(new Rect2(position, tooltipSize), new Color(0.015f, 0.035f, 0.048f, 0.97f));
        DrawRect(new Rect2(position, tooltipSize), new Color(0.10f, 0.58f, 0.74f, 0.75f), false, 1.2f);
        for (var index = 0; index < lines.Length; index++)
        {
            DrawString(ThemeDB.FallbackFont, position + new Vector2(12, 21 + (index * 19)), lines[index],
                HorizontalAlignment.Left, tooltipSize.X - 24, index == 0 ? 14 : 12,
                index == 0 ? MapVisualPalette.TextPrimary : MapVisualPalette.TextMuted);
        }
    }

    private void ZoomAt(Vector2 mousePosition, float factor)
    {
        var worldAtMouse = _isFollowingShip ? default : ScreenToWorld(mousePosition);
        _zoom = Mathf.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        _viewCenter = _isFollowingShip
            ? ToVector(_ship.Position)
            : ToVector(worldAtMouse) - ((mousePosition - (Size * 0.5f)) / _zoom);
        QueueRedraw();
    }

    private void FocusPosition(WorldPosition position)
    {
        var nextCenter = ToVector(position);
        if (_viewCenter.IsEqualApprox(nextCenter))
        {
            return;
        }

        _viewCenter = nextCenter;
        QueueRedraw();
    }

    private bool IsDifferentMapLocation(Vector2 screenPosition) =>
        screenPosition.DistanceSquaredTo(Size * 0.5f) > DragThreshold * DragThreshold;

#if DEBUG
    private void RunNavigationSmokeTest()
    {
        var originalShip = _ship;
        var originalViewCenter = _viewCenter;
        var originalZoom = _zoom;
        var originalFollowing = _isFollowingShip;
        var originalPointerDown = _pointerDown;
        var originalDidDrag = _didDrag;
        var originalPressPosition = _pressPosition;
        var originalHoverPosition = _hoverPosition;
        var originalCursor = MouseDefaultCursorShape;
        try
        {
            _ship = new MapShipState(new WorldPosition(100, 200), 0, true);
            _viewCenter = ToVector(_ship.Position);
            _zoom = DefaultZoom;
            _isFollowingShip = true;

            SetShipState(new MapShipState(new WorldPosition(420, -75), 0.75, true));
            RequireSmokeCondition(
                _viewCenter.IsEqualApprox(new Vector2(420, -75)),
                "ship following must track the live ship position");

            var zoomBefore = _zoom;
            ZoomAt(new Vector2(220, 160), 1.18f);
            RequireSmokeCondition(
                _isFollowingShip && _zoom > zoomBefore &&
                _viewCenter.IsEqualApprox(ToVector(_ship.Position)),
                "zooming must preserve ship following and map centering");

            _pointerDown = true;
            _didDrag = false;
            _pressPosition = new Vector2(100, 100);
            HandlePointerMotion(new InputEventMouseMotion
            {
                Position = new Vector2(112, 100),
                Relative = new Vector2(12, 0),
            });
            RequireSmokeCondition(
                !_isFollowingShip && _didDrag,
                "a real map drag must disable ship following");

            GD.Print("WORLD_MAP_NAVIGATION_SMOKE_OK: follow, zoom retention, drag release");
        }
        finally
        {
            _ship = originalShip;
            _viewCenter = originalViewCenter;
            _zoom = originalZoom;
            _isFollowingShip = originalFollowing;
            _pointerDown = originalPointerDown;
            _didDrag = originalDidDrag;
            _pressPosition = originalPressPosition;
            _hoverPosition = originalHoverPosition;
            MouseDefaultCursorShape = originalCursor;
        }
    }

    private static void RequireSmokeCondition(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"World-map smoke test failed: {message}.");
        }
    }
#endif

    private VisibleComet? FindCometAt(Vector2 screenPosition)
    {
        for (var index = _visibleComets.Count - 1; index >= 0; index--)
        {
            var marker = _visibleComets[index];
            if (marker.ScreenPosition.DistanceSquaredTo(screenPosition) <= marker.HitRadius * marker.HitRadius)
            {
                return marker;
            }
        }

        return null;
    }

    private bool IsDiscovered(WorldPosition position) => _snapshot.Chunks.Any(sector =>
        sector.Status != ChunkDiscoveryStatus.Unknown &&
        position.X >= sector.WorldOrigin.X &&
        position.Y >= sector.WorldOrigin.Y &&
        position.X < sector.WorldOrigin.X + _snapshot.SectorSize &&
        position.Y < sector.WorldOrigin.Y + _snapshot.SectorSize);

    private Vector2 WorldToScreen(WorldPosition position) =>
        (ToVector(position) - _viewCenter) * _zoom + (Size * 0.5f);

    private WorldPosition ScreenToWorld(Vector2 position)
    {
        var world = _viewCenter + ((position - (Size * 0.5f)) / _zoom);
        return new WorldPosition(world.X, world.Y);
    }

    private float GetGridWorldSpacing()
    {
        var desiredWorldSpacing = 120 / _zoom;
        var magnitude = Mathf.Pow(10, Mathf.Floor(Mathf.Log(desiredWorldSpacing) / Mathf.Log(10)));
        var normalized = desiredWorldSpacing / magnitude;
        var step = normalized < 2 ? 2 : normalized < 5 ? 5 : 10;
        return step * magnitude;
    }

    private static float PositiveModulo(float value, float modulus) => ((value % modulus) + modulus) % modulus;

    private static string FormatDistance(float distance) => distance >= 1_000
        ? $"{distance / 1_000:0.0}k"
        : $"{distance:0}";

    private Color GetResourceColor(string resourceId) =>
        _resourceCatalog.TryGetValue(resourceId, out var resource)
            ? MapVisualPalette.ParseColor(resource.BaseColorHex, MapVisualPalette.Cyan)
            : MapVisualPalette.GetResourceColor(resourceId);

    private static Vector2 ToVector(WorldPosition position) => new((float)position.X, (float)position.Y);

    private sealed record VisibleComet(DiscoveredCometData Item, Vector2 ScreenPosition, float HitRadius);
}

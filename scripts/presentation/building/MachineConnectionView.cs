using Godot;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Lightweight comet-local presentation of one persistent logistics edge. The
/// endpoints are read from the machine sockets, so rotation and chunk reloads do
/// not need per-frame world searches.
/// </summary>
public partial class MachineConnectionView : Node2D
{
    private const float ActiveVisualHoldSeconds = 0.32f;
    private MachineConnection? _connection;
    private MachineView? _source;
    private MachineView? _target;
    private AsteroidView? _hostComet;
    private ConnectionPresentationDefinition? _presentation;
    private float _flowPhase;
    private float _activeVisualRemaining;

    public MachineConnectionId ConnectionId => _connection?.Id ?? default;

    public ConnectionKind Kind => _connection?.Kind ?? default;

    public AsteroidView? HostComet => _hostComet;

    public override void _Ready()
    {
        ZIndex = 2;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_activeVisualRemaining <= 0)
        {
            SetProcess(false);
            return;
        }

        _activeVisualRemaining = Math.Max(0, _activeVisualRemaining - (float)delta);
        _flowPhase = Mathf.PosMod(_flowPhase + ((float)delta * 72), 72);
        QueueRedraw();
    }

    public void Configure(
        MachineConnection connection,
        MachineView source,
        MachineView target,
        AsteroidView hostComet)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(hostComet);
        if (source.HostComet != hostComet || target.HostComet != hostComet)
        {
            throw new ArgumentException("A connection view needs two machines on its host comet.");
        }

        _connection = connection;
        _source = source;
        _target = target;
        _hostComet = hostComet;
        _presentation = ConnectionPresentationCatalog.Get(connection.TypeId);
        Name = $"Connection_{connection.Id.Value}";
        if (GetParent() != hostComet)
        {
            hostComet.AddChild(this);
        }

        SetProcess(false);
        QueueRedraw();
    }

    public void ShowActivity()
    {
        _activeVisualRemaining = ActiveVisualHoldSeconds;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_connection is null || _source is null || _target is null || _hostComet is null ||
            _presentation is null || !GodotObject.IsInstanceValid(_source) ||
            !GodotObject.IsInstanceValid(_target))
        {
            return;
        }

        var sourceWorld = _source.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetSourceAnchor(_connection.Kind));
        var targetWorld = _target.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetTargetAnchor(_connection.Kind));
        var route = CreateRoute(ToLocal(sourceWorld), ToLocal(targetWorld), _connection.Kind);
        var active = _activeVisualRemaining > 0;
        switch (_connection.Kind)
        {
            case ConnectionKind.PowerCable:
                DrawPowerCable(route, active);
                break;
            case ConnectionKind.ConveyorBelt:
                DrawConveyor(route, active);
                break;
            case ConnectionKind.LiquidPipe:
            case ConnectionKind.GasPipe:
                DrawPipe(route, active);
                break;
        }

        DrawEndpoint(route[0], source: true);
        DrawEndpoint(route[^1], source: false);
    }

    private void DrawPowerCable(Vector2[] route, bool active)
    {
        var presentation = _presentation!;
        DrawPolyline(route, new Color(0, 0, 0, 0.64f), presentation.LineWidth + 3, true);
        DrawPolyline(route, presentation.PrimaryColor, presentation.LineWidth, true);
        DrawPolyline(route, new Color(presentation.FlowColor, active ? 0.88f : 0.26f), 1.35f, true);
        if (active)
        {
            DrawFlowMarkers(route, presentation.FlowColor, spacing: 54, radius: 2.4f);
        }
    }

    private void DrawConveyor(Vector2[] route, bool active)
    {
        var presentation = _presentation!;
        DrawPolyline(route, new Color(0, 0, 0, 0.72f), presentation.LineWidth + 5, true);
        DrawPolyline(route, new Color(0.29f, 0.33f, 0.34f), presentation.LineWidth + 2, true);
        DrawPolyline(route, presentation.PrimaryColor, presentation.LineWidth - 3, true);
        DrawFlowMarkers(
            route,
            active ? presentation.FlowColor : presentation.FlowColor.Darkened(0.48f),
            spacing: 28,
            radius: active ? 3.2f : 2.1f);
        DrawDirectionArrow(route, presentation.FlowColor);
    }

    private void DrawPipe(Vector2[] route, bool active)
    {
        var presentation = _presentation!;
        DrawPolyline(route, new Color(0, 0, 0, 0.68f), presentation.LineWidth + 5, true);
        DrawPolyline(route, new Color(0.32f, 0.39f, 0.41f), presentation.LineWidth + 2, true);
        DrawPolyline(route, presentation.PrimaryColor, presentation.LineWidth - 2, true);
        DrawPolyline(route, new Color(presentation.FlowColor, active ? 0.74f : 0.23f), 2, true);
        if (active)
        {
            DrawFlowMarkers(route, presentation.FlowColor, spacing: 38, radius: 2.7f);
        }

        DrawDirectionArrow(route, presentation.FlowColor);
    }

    private void DrawEndpoint(Vector2 point, bool source)
    {
        var color = source ? _presentation!.FlowColor : _presentation!.PrimaryColor.Lightened(0.32f);
        DrawCircle(point, 6.2f, new Color(0.02f, 0.035f, 0.04f, 0.95f));
        DrawArc(point, 6.2f, 0, Mathf.Tau, 18, color, 1.6f, true);
        DrawCircle(point, 2.3f, color);
    }

    private void DrawFlowMarkers(Vector2[] route, Color color, float spacing, float radius)
    {
        var totalLength = GetRouteLength(route);
        if (totalLength <= 0.01f)
        {
            return;
        }

        var offset = Mathf.PosMod(_flowPhase, spacing);
        for (var distance = offset; distance < totalLength; distance += spacing)
        {
            DrawCircle(GetPointAlongRoute(route, distance), radius, new Color(color, 0.9f));
        }
    }

    private void DrawDirectionArrow(Vector2[] route, Color color)
    {
        var totalLength = GetRouteLength(route);
        if (totalLength < 18)
        {
            return;
        }

        var center = GetPointAlongRoute(route, totalLength * 0.72f);
        var ahead = GetPointAlongRoute(route, Math.Min(totalLength, (totalLength * 0.72f) + 7));
        var direction = (ahead - center).Normalized();
        if (direction == Vector2.Zero)
        {
            return;
        }

        var side = direction.Orthogonal();
        DrawColoredPolygon(
        [
            center + (direction * 7),
            center - (direction * 5) + (side * 4),
            center - (direction * 5) - (side * 4),
        ], new Color(color, 0.82f));
    }

    private static Vector2[] CreateRoute(Vector2 source, Vector2 target, ConnectionKind kind)
    {
        var delta = target - source;
        if (kind == ConnectionKind.ConveyorBelt)
        {
            var middleX = (source.X + target.X) * 0.5f;
            return [source, new Vector2(middleX, source.Y), new Vector2(middleX, target.Y), target];
        }

        var perpendicular = delta.LengthSquared() > 0.01f ? delta.Normalized().Orthogonal() : Vector2.Up;
        var bend = Math.Min(34, delta.Length() * 0.11f);
        var control = (source + target) * 0.5f + (perpendicular * bend);
        const int segments = 18;
        var route = new Vector2[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (float)segments;
            var inverse = 1 - t;
            route[index] = (inverse * inverse * source) + (2 * inverse * t * control) + (t * t * target);
        }

        return route;
    }

    private static float GetRouteLength(IReadOnlyList<Vector2> route)
    {
        var length = 0f;
        for (var index = 1; index < route.Count; index++)
        {
            length += route[index - 1].DistanceTo(route[index]);
        }

        return length;
    }

    private static Vector2 GetPointAlongRoute(IReadOnlyList<Vector2> route, float requestedDistance)
    {
        var remaining = Math.Max(0, requestedDistance);
        for (var index = 1; index < route.Count; index++)
        {
            var segmentLength = route[index - 1].DistanceTo(route[index]);
            if (remaining <= segmentLength || index == route.Count - 1)
            {
                return route[index - 1].Lerp(route[index], segmentLength <= 0 ? 0 : remaining / segmentLength);
            }

            remaining -= segmentLength;
        }

        return route[^1];
    }
}

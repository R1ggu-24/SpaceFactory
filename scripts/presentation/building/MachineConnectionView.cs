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
    private float _dismantlingProgress;

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
        if (_activeVisualRemaining <= 0 && _dismantlingProgress <= 0)
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

    /// <summary>
    /// Applies a presentation-only teardown preview. Connection ownership and the atomic refund
    /// remain in the runtime controller; a value of zero restores the normal cable/belt/pipe.
    /// </summary>
    public void SetDismantlingProgress(float progress)
    {
        var normalized = float.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (Mathf.IsEqualApprox(_dismantlingProgress, normalized))
        {
            return;
        }

        _dismantlingProgress = normalized;
        SetProcess(_activeVisualRemaining > 0 || _dismantlingProgress > 0);
        QueueRedraw();
    }

    public float DistanceToWorldPoint(Vector2 worldPoint)
    {
        if (_connection is null || _source is null || _target is null ||
            !GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_target))
        {
            return float.PositiveInfinity;
        }

        var source = ToLocal(_source.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetSourceAnchor(_connection.Kind)));
        var target = ToLocal(_target.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetTargetAnchor(_connection.Kind)));
        return DistanceToRoute(ToLocal(worldPoint), CreateRoute(source, target, _connection.Kind));
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
        if (_dismantlingProgress > 0)
        {
            DrawDismantlingOverlay(route);
        }
    }

    private void DrawDismantlingOverlay(IReadOnlyList<Vector2> route)
    {
        var totalLength = GetRouteLength(route);
        if (totalLength <= 0.01f)
        {
            return;
        }

        var energy = new Color(0.22f, 0.91f, 1f, 0.38f + (_dismantlingProgress * 0.48f));
        var completedLength = totalLength * _dismantlingProgress;
        const float spacing = 18;
        for (var distance = 0f; distance <= completedLength; distance += spacing)
        {
            var point = GetPointAlongRoute(route, distance);
            var ahead = GetPointAlongRoute(route, Math.Min(totalLength, distance + 5));
            var direction = (ahead - point).Normalized();
            var side = direction == Vector2.Zero ? Vector2.Up : direction.Orthogonal();
            var separation = 1.5f + (_dismantlingProgress * 3.5f);
            DrawLine(point - (side * separation), ahead - (side * separation), energy, 1.8f, true);
            DrawLine(point + (side * separation), ahead + (side * separation), energy, 1.1f, true);
            DrawCircle(point + (side * Mathf.Sin(_flowPhase + distance) * 4), 1.7f, energy);
        }
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

    private static float DistanceToRoute(Vector2 point, IReadOnlyList<Vector2> route)
    {
        var closest = float.PositiveInfinity;
        for (var index = 1; index < route.Count; index++)
        {
            var start = route[index - 1];
            var delta = route[index] - start;
            var lengthSquared = delta.LengthSquared();
            var t = lengthSquared <= 0.0001f
                ? 0
                : Math.Clamp((point - start).Dot(delta) / lengthSquared, 0, 1);
            closest = Math.Min(closest, point.DistanceTo(start + (delta * t)));
        }

        return closest;
    }
}

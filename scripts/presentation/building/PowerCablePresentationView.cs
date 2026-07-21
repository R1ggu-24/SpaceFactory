using Godot;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Runtime-neutral cable endpoint. Machine sockets, pole sockets and ship
/// sockets can all expose the same small presentation contract.
/// </summary>
public sealed record PowerCableVisualEndpoint(
    string EndpointId,
    Func<Vector2> WorldPositionProvider,
    Func<bool>? ValidityProvider = null)
{
    public bool IsValid => ValidityProvider?.Invoke() ?? true;

    public Vector2 GetWorldPosition() => WorldPositionProvider();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EndpointId))
        {
            throw new ArgumentException("A cable endpoint needs a stable ID.", nameof(EndpointId));
        }

        ArgumentNullException.ThrowIfNull(WorldPositionProvider);
    }
}

/// <summary>
/// Curved, comet-local cable presentation with armoured plugs. Connection,
/// inventory and network ownership stay in gameplay code.
/// </summary>
public partial class PowerCablePresentationView : Node2D
{
    private const float PulseSpeed = 64;
    private const float ActiveVisualHoldSeconds = 0.36f;
    private PowerCableVisualEndpoint? _first;
    private PowerCableVisualEndpoint? _second;
    private string _cableId = string.Empty;
    private float _bendDirection = 1;
    private float _flowPhase;
    private float _activeVisualRemaining;
    private float _dismantlingProgress;
    private float _utilization;
    private bool _isEnabled = true;
    private bool _isEnergized;
    private bool _trackEndpoints;
    private Node2D? _coordinateSpace;

    public string CableId => _cableId;

    public PowerCableVisualEndpoint? FirstEndpoint => _first;

    public PowerCableVisualEndpoint? SecondEndpoint => _second;

    public Node2D? HostComet => _coordinateSpace;

    public override void _Ready()
    {
        ZIndex = 2;
        SetProcess(false);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var redraw = _trackEndpoints;
        if (_activeVisualRemaining > 0 || _dismantlingProgress > 0)
        {
            _activeVisualRemaining = Math.Max(0, _activeVisualRemaining - (float)delta);
            _flowPhase = Mathf.PosMod(_flowPhase + ((float)delta * PulseSpeed), 52);
            redraw = true;
        }

        if (redraw)
        {
            QueueRedraw();
        }

        RefreshProcessingState();
    }

    public void Configure(
        string cableId,
        Node2D coordinateSpace,
        PowerCableVisualEndpoint first,
        PowerCableVisualEndpoint second)
    {
        if (string.IsNullOrWhiteSpace(cableId))
        {
            throw new ArgumentException("A cable view needs a stable ID.", nameof(cableId));
        }

        ArgumentNullException.ThrowIfNull(coordinateSpace);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        first.Validate();
        second.Validate();
        if (string.Equals(first.EndpointId, second.EndpointId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A power cable needs two different endpoints.");
        }

        _cableId = cableId;
        _coordinateSpace = coordinateSpace;
        _first = first;
        _second = second;
        _bendDirection = CalculateStableBendDirection(cableId);
        Name = $"PowerCable_{SanitizeNodeName(cableId)}";
        ZIndex = 2;
        if (GetParent() is null)
        {
            coordinateSpace.AddChild(this);
        }
        else if (GetParent() != coordinateSpace)
        {
            Reparent(coordinateSpace, keepGlobalTransform: false);
        }

        Position = Vector2.Zero;
        Rotation = 0;
        QueueRedraw();
    }

    /// <summary>
    /// Enables endpoint polling only for exceptional moving endpoints. Cables
    /// between comet-local objects do not need a per-frame update.
    /// </summary>
    public void SetEndpointTracking(bool enabled)
    {
        _trackEndpoints = enabled;
        RefreshProcessingState();
    }

    public void SetVisualState(bool enabled, bool energized, float utilization)
    {
        var normalizedUtilization = float.IsFinite(utilization)
            ? Math.Clamp(utilization, 0, 1)
            : 0;
        var changed = _isEnabled != enabled || _isEnergized != energized ||
                      !Mathf.IsEqualApprox(_utilization, normalizedUtilization);
        _isEnabled = enabled;
        _isEnergized = energized;
        _utilization = normalizedUtilization;
        if (energized && enabled)
        {
            _activeVisualRemaining = ActiveVisualHoldSeconds;
        }

        if (changed)
        {
            QueueRedraw();
        }

        RefreshProcessingState();
    }

    public void ShowPowerPulse()
    {
        if (!_isEnabled)
        {
            return;
        }

        _activeVisualRemaining = ActiveVisualHoldSeconds;
        RefreshProcessingState();
        QueueRedraw();
    }

    /// <summary>
    /// Sets a non-destructive dismantling preview. The gameplay graph and inventory transaction
    /// remain untouched until the runtime commits removal after reaching one.
    /// </summary>
    public void SetDismantlingProgress(float progress)
    {
        var normalized = float.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (Mathf.IsEqualApprox(_dismantlingProgress, normalized))
        {
            return;
        }

        _dismantlingProgress = normalized;
        RefreshProcessingState();
        QueueRedraw();
    }

    public void RefreshEndpoints() => QueueRedraw();

    public float DistanceToWorldPoint(Vector2 worldPoint)
    {
        if (_first is null || _second is null || !_first.IsValid || !_second.IsValid)
        {
            return float.PositiveInfinity;
        }

        var route = PowerCableDrawing.CreateCurve(
            ToLocal(_first.GetWorldPosition()),
            ToLocal(_second.GetWorldPosition()),
            _bendDirection);
        return DistanceToRoute(ToLocal(worldPoint), route);
    }

    public override void _Draw()
    {
        if (_first is null || _second is null || !_first.IsValid || !_second.IsValid)
        {
            return;
        }

        var source = ToLocal(_first.GetWorldPosition());
        var target = ToLocal(_second.GetWorldPosition());
        var route = PowerCableDrawing.CreateCurve(source, target, _bendDirection);
        var active = _isEnabled && _isEnergized && _activeVisualRemaining > 0;
        PowerCableDrawing.DrawCable(this, route, active, _utilization, _flowPhase);
        if (_dismantlingProgress > 0)
        {
            DrawDismantlingOverlay(route);
        }
    }

    private void RefreshProcessingState() =>
        SetProcess(_trackEndpoints || _activeVisualRemaining > 0 || _dismantlingProgress > 0);

    private void DrawDismantlingOverlay(IReadOnlyList<Vector2> route)
    {
        var totalLength = PowerCableDrawing.GetRouteLength(route);
        if (totalLength <= 0.01f)
        {
            return;
        }

        var energy = new Color(0.2f, 0.92f, 1f, 0.45f + (_dismantlingProgress * 0.45f));
        var completedLength = totalLength * _dismantlingProgress;
        const float spacing = 16;
        for (var distance = 0f; distance <= completedLength; distance += spacing)
        {
            var point = PowerCableDrawing.GetPointAlongRoute(route, distance);
            var ahead = PowerCableDrawing.GetPointAlongRoute(route, Math.Min(totalLength, distance + 5));
            var direction = (ahead - point).Normalized();
            var side = direction == Vector2.Zero ? Vector2.Up : direction.Orthogonal();
            var separation = 2 + (_dismantlingProgress * 4);
            DrawLine(point - (side * separation), ahead - (side * separation), energy, 1.8f, true);
            DrawLine(point + (side * separation), ahead + (side * separation), energy, 1.1f, true);
            DrawCircle(point + (side * Mathf.Sin(_flowPhase + distance) * 4), 1.8f, energy);
        }
    }

    private static float CalculateStableBendDirection(string value)
    {
        uint hash = 2166136261;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return (hash & 1) == 0 ? -1 : 1;
    }

    private static string SanitizeNodeName(string value) =>
        value.Replace(':', '_').Replace('/', '_').Replace('\\', '_');

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

internal static class PowerCableDrawing
{
    private static readonly Color CableShadow = new(0, 0, 0, 0.72f);
    private static readonly Color CableJacket = new(0.035f, 0.048f, 0.052f, 1);
    private static readonly Color CableEdge = new(0.24f, 0.29f, 0.3f, 0.92f);
    private static readonly Color Current = new(0.12f, 0.79f, 0.96f, 1);

    public static Vector2[] CreateCurve(Vector2 source, Vector2 target, float bendDirection)
    {
        var delta = target - source;
        var perpendicular = delta.LengthSquared() > 0.001f
            ? delta.Normalized().Orthogonal()
            : Vector2.Up;
        var bend = Math.Clamp(delta.Length() * 0.12f, 10, 48) * Math.Sign(bendDirection);
        var control = ((source + target) * 0.5f) + (perpendicular * bend);
        const int segments = 24;
        var route = new Vector2[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (float)segments;
            var inverse = 1 - t;
            route[index] = (inverse * inverse * source) +
                           (2 * inverse * t * control) +
                           (t * t * target);
        }

        return route;
    }

    public static void DrawCable(
        Node2D canvas,
        IReadOnlyList<Vector2> route,
        bool active,
        float utilization,
        float flowPhase)
    {
        if (route.Count < 2)
        {
            return;
        }

        var points = route.ToArray();
        canvas.DrawPolyline(points, CableShadow, 12, true);
        canvas.DrawPolyline(points, CableJacket, 8, true);
        canvas.DrawPolyline(points, CableEdge, 2.2f, true);
        canvas.DrawPolyline(points, new Color(Current, active ? 0.28f + (0.42f * utilization) : 0.07f), 1.25f, true);
        if (active)
        {
            DrawPulseMarkers(canvas, route, flowPhase, utilization);
        }

        DrawPlug(canvas, route[0], route[1] - route[0], active);
        DrawPlug(canvas, route[^1], route[^2] - route[^1], active);
    }

    private static void DrawPlug(Node2D canvas, Vector2 point, Vector2 direction, bool active)
    {
        var normalized = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Right;
        var side = normalized.Orthogonal();
        var inner = point + (normalized * 7);
        var outer = point - (normalized * 5);
        canvas.DrawColoredPolygon(
        [
            outer + (side * 5),
            inner + (side * 5),
            inner + (side * 7),
            inner + (normalized * 4) - (side * 7),
            inner - (side * 5),
            outer - (side * 5),
        ], new Color(0.14f, 0.18f, 0.19f, 1));
        canvas.DrawLine(outer + (side * 4), outer - (side * 4), CableEdge, 2, true);
        canvas.DrawCircle(point, 2.6f, new Color(Current, active ? 0.95f : 0.34f));
    }

    private static void DrawPulseMarkers(
        Node2D canvas,
        IReadOnlyList<Vector2> route,
        float flowPhase,
        float utilization)
    {
        var totalLength = GetRouteLength(route);
        const float spacing = 52;
        var offset = Mathf.PosMod(flowPhase, spacing);
        for (var distance = offset; distance < totalLength; distance += spacing)
        {
            var point = GetPointAlongRoute(route, distance);
            canvas.DrawCircle(point, 2.1f + utilization, new Color(Current, 0.78f));
        }
    }

    internal static float GetRouteLength(IReadOnlyList<Vector2> route)
    {
        var length = 0f;
        for (var index = 1; index < route.Count; index++)
        {
            length += route[index - 1].DistanceTo(route[index]);
        }

        return length;
    }

    internal static Vector2 GetPointAlongRoute(IReadOnlyList<Vector2> route, float distance)
    {
        var remaining = Math.Max(0, distance);
        for (var index = 1; index < route.Count; index++)
        {
            var segmentLength = route[index - 1].DistanceTo(route[index]);
            if (remaining <= segmentLength || index == route.Count - 1)
            {
                return route[index - 1].Lerp(
                    route[index],
                    segmentLength <= 0 ? 0 : Math.Clamp(remaining / segmentLength, 0, 1));
            }

            remaining -= segmentLength;
        }

        return route[^1];
    }
}

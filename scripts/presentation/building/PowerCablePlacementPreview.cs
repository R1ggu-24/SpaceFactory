using Godot;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Presentation-only first-endpoint/second-endpoint cable preview. Endpoint
/// compatibility and inventory consumption remain centralized in gameplay.
/// </summary>
public partial class PowerCablePlacementPreview : Node2D
{
    private IReadOnlyList<PowerCableVisualEndpoint> _availableEndpoints = [];
    private PowerCableVisualEndpoint? _source;
    private PowerCableVisualEndpoint? _candidate;
    private Label? _hint;
    private float _maximumLength;
    private bool _candidateAllowed;

    public bool IsActive { get; private set; }

    public PowerCableVisualEndpoint? Source => _source;

    public PowerCableVisualEndpoint? Candidate => _candidate;

    public int AvailableEndpointCount => _availableEndpoints.Count;

    public override void _Ready()
    {
        ZIndex = 80;
        EnsureHint();
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (!IsActive)
        {
            return;
        }

        RefreshHintPosition();
        QueueRedraw();
    }

    public void Begin(float maximumLength)
    {
        if (!float.IsFinite(maximumLength) || maximumLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        _source = null;
        _candidate = null;
        _availableEndpoints = [];
        _candidateAllowed = false;
        _maximumLength = maximumLength;
        IsActive = true;
        Visible = true;
        SetProcess(true);
        SetHint("KABEL: ERSTEN FREIEN ANSCHLUSS WÄHLEN", valid: true);
        QueueRedraw();
    }

    public void Start(PowerCableVisualEndpoint source, float maximumLength)
    {
        Begin(maximumLength);
        SetSource(source);
    }

    public void SetSource(PowerCableVisualEndpoint source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        if (!IsActive)
        {
            throw new InvalidOperationException("Cable placement is not active.");
        }

        _source = source;
        _candidate = null;
        _candidateAllowed = false;
        SetHint("KABEL: FREIEN ZWEITEN ANSCHLUSS WÄHLEN", valid: true);
        QueueRedraw();
    }

    public void SetAvailableEndpoints(IEnumerable<PowerCableVisualEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!IsActive)
        {
            return;
        }

        var materialized = endpoints
            .Where(endpoint => endpoint is not null && endpoint.IsValid)
            .DistinctBy(endpoint => endpoint.EndpointId, StringComparer.Ordinal)
            .ToArray();
        foreach (var endpoint in materialized)
        {
            endpoint.Validate();
        }

        _availableEndpoints = materialized;
        QueueRedraw();
    }

    public void SetCandidate(
        PowerCableVisualEndpoint? candidate,
        bool isCompatible,
        string failureMessage = "")
    {
        if (!IsActive || _source is null)
        {
            throw new InvalidOperationException("Select a first cable endpoint before a target candidate.");
        }

        _candidate = candidate;
        _candidateAllowed = isCompatible;
        var withinRange = IsWithinMaximumLength(candidate);
        SetHint(
            candidate is null
                ? "KABEL: FREIEN ZWEITEN ANSCHLUSS WÄHLEN"
                : isCompatible && withinRange
                    ? "ANSCHLUSS FREI · KLICKEN ZUM VERBINDEN"
                    : string.IsNullOrWhiteSpace(failureMessage)
                        ? withinRange ? "ANSCHLUSS NICHT VERFÜGBAR" : "KABEL ZU LANG"
                        : failureMessage.ToUpperInvariant(),
            candidate is null || (isCompatible && withinRange));
        QueueRedraw();
    }

    public void SetFailure(string message)
    {
        if (IsActive)
        {
            SetHint(message.ToUpperInvariant(), valid: false);
        }
    }

    public bool IsCandidateValid =>
        IsActive && _candidate is not null && _candidateAllowed && IsWithinMaximumLength(_candidate);

    public void Cancel()
    {
        IsActive = false;
        Visible = false;
        SetProcess(false);
        _source = null;
        _candidate = null;
        _availableEndpoints = [];
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsActive)
        {
            return;
        }

        DrawAvailableEndpoints();

        if (_source is null)
        {
            var mouse = ToLocal(GetGlobalMousePosition());
            var color = new Color(0.18f, 0.9f, 1.0f, 0.92f);
            DrawCircle(mouse, 12, new Color(0.02f, 0.06f, 0.075f, 0.7f));
            DrawArc(mouse, 12, 0, Mathf.Tau, 24, color, 2, true);
            DrawLine(mouse + new Vector2(-6, 0), mouse + new Vector2(6, 0), color, 1.4f, true);
            DrawLine(mouse + new Vector2(0, -6), mouse + new Vector2(0, 6), color, 1.4f, true);
            return;
        }

        if (!_source.IsValid)
        {
            return;
        }

        var source = ToLocal(_source.GetWorldPosition());
        var target = _candidate is { IsValid: true }
            ? ToLocal(_candidate.GetWorldPosition())
            : ToLocal(GetGlobalMousePosition());
        var valid = _candidate is null || IsCandidateValid;
        var route = PowerCableDrawing.CreateCurve(source, target, bendDirection: 1);
        PowerCableDrawing.DrawCable(this, route, active: valid, utilization: valid ? 0.72f : 0, flowPhase: 0);
        if (!valid)
        {
            DrawPolyline(route, new Color(1.0f, 0.25f, 0.3f, 0.76f), 2, true);
        }

        DrawArc(target, 9, 0, Mathf.Tau, 20,
            valid ? new Color(0.18f, 0.9f, 1.0f) : new Color(1.0f, 0.3f, 0.34f), 2, true);
    }

    private void DrawAvailableEndpoints()
    {
        foreach (var endpoint in _availableEndpoints)
        {
            if (!endpoint.IsValid)
            {
                continue;
            }

            var point = ToLocal(endpoint.GetWorldPosition());
            var isSource = _source is not null &&
                           string.Equals(_source.EndpointId, endpoint.EndpointId, StringComparison.Ordinal);
            var isCandidate = _candidate is not null &&
                              string.Equals(_candidate.EndpointId, endpoint.EndpointId, StringComparison.Ordinal);
            var radius = isCandidate ? 12.0f : isSource ? 10.5f : 8.5f;
            var glow = isSource
                ? new Color(1.0f, 0.72f, 0.2f, 0.95f)
                : new Color(0.18f, 0.9f, 1.0f, isCandidate ? 1.0f : 0.72f);
            DrawCircle(point, radius + 3, new Color(0.01f, 0.055f, 0.07f, 0.68f));
            DrawArc(point, radius, 0, Mathf.Tau, 24, glow, isCandidate ? 3.0f : 2.0f, true);
            DrawCircle(point, 2.4f, glow);
        }
    }

    private bool IsWithinMaximumLength(PowerCableVisualEndpoint? endpoint)
    {
        if (_source is null || endpoint is null || !_source.IsValid || !endpoint.IsValid)
        {
            return false;
        }

        return _source.GetWorldPosition().DistanceTo(endpoint.GetWorldPosition()) <= _maximumLength;
    }

    private void EnsureHint()
    {
        if (_hint is not null)
        {
            return;
        }

        _hint = new Label
        {
            Name = "PowerCablePlacementHint",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = 2,
        };
        _hint.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_hint);
        RefreshHintPosition();
    }

    private void SetHint(string message, bool valid)
    {
        EnsureHint();
        _hint!.Text = message;
        _hint.AddThemeColorOverride("font_color",
            valid ? new Color(0.42f, 0.94f, 1.0f) : new Color(1.0f, 0.45f, 0.46f));
    }

    private void RefreshHintPosition()
    {
        if (_hint is null || !IsInsideTree())
        {
            return;
        }

        var localMouse = ToLocal(GetGlobalMousePosition());
        _hint.Position = localMouse + new Vector2(-190, 20);
        _hint.Size = new Vector2(380, 28);
    }
}

using Godot;
using SpaceFactory.Core.Production;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

public partial class MachineView : StaticBody2D
{
    public const uint MachineCollisionLayer = 1u << 3;

    private MachineState? _state;
    private MachinePresentationDefinition? _presentation;
    private AsteroidView? _hostComet;
    private CollisionShape2D? _collisionShape;
    private Label? _statusLabel;
    private float _visualConstructionProgress;
    private bool _constructionVisualRunning;

    public MachineInstanceId InstanceId =>
        _state?.InstanceId ?? throw new InvalidOperationException("Machine view is not configured.");

    public MachineDefinitionId DefinitionId =>
        _state?.Definition.Id ?? throw new InvalidOperationException("Machine view is not configured.");

    public AsteroidView HostComet =>
        _hostComet ?? throw new InvalidOperationException("Machine view is not anchored to a comet.");

    public Vector2 Footprint => _presentation?.Footprint ?? Vector2.Zero;

    public float InteractionRadius => _presentation?.InteractionRadius ?? 0;

    public bool IsConstructionVisualComplete => _visualConstructionProgress >= 0.999f;

    public event Action<MachineView>? InteractionRequested;

    public void Configure(
        MachineState state,
        AsteroidView hostComet,
        MachinePresentationCatalog? presentationCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(hostComet);
        var placement = state.Placement ??
                        throw new ArgumentException("A world machine needs a persisted placement.", nameof(state));
        if (!string.Equals(placement.CometId, hostComet.CometId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Machine placement and host comet do not match.", nameof(hostComet));
        }

        _state = state;
        _hostComet = hostComet;
        _presentation = (presentationCatalog ?? MachinePresentationCatalog.Instance).Get(state.Definition.Id);
        _visualConstructionProgress = (float)state.ConstructionProgress;
        _constructionVisualRunning = !state.IsConstructionComplete;
        Name = $"Machine_{state.InstanceId.Value.Replace(':', '_')}";
        CollisionLayer = MachineCollisionLayer;
        CollisionMask = 0;
        InputPickable = true;

        if (GetParent() is null)
        {
            hostComet.AddChild(this);
        }
        else if (GetParent() != hostComet)
        {
            Reparent(hostComet, keepGlobalTransform: false);
        }

        Position = new Vector2(
            (float)placement.RelativePositionX,
            (float)placement.RelativePositionY);
        Rotation = (float)placement.RelativeRotationRadians;
        EnsurePresentationNodes();
        Refresh(state);
    }

    public override void _Ready()
    {
        EnsurePresentationNodes();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_constructionVisualRunning || _state is null)
        {
            return;
        }

        var duration = Math.Max(0.001, _state.Definition.ConstructionDurationSeconds);
        var previous = _visualConstructionProgress;
        _visualConstructionProgress = Mathf.Min(
            1,
            _visualConstructionProgress + ((float)delta / (float)duration));
        if (_visualConstructionProgress >= 0.999f || _state.IsConstructionComplete)
        {
            _visualConstructionProgress = 1;
            _constructionVisualRunning = false;
        }

        if (!Mathf.IsEqualApprox(previous, _visualConstructionProgress))
        {
            RefreshStatusLabel();
            QueueRedraw();
        }
    }

    public void Refresh(MachineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_state is not null && state.InstanceId != _state.InstanceId)
        {
            throw new ArgumentException("A machine view cannot switch to a different instance.", nameof(state));
        }

        if (_presentation is not null && state.Definition.Id != _presentation.DefinitionId)
        {
            throw new ArgumentException("Machine state and presentation do not match.", nameof(state));
        }

        _state = state;
        _visualConstructionProgress = Math.Max(
            _visualConstructionProgress,
            (float)state.ConstructionProgress);
        if (state.IsConstructionComplete)
        {
            _visualConstructionProgress = 1;
            _constructionVisualRunning = false;
        }

        RefreshStatusLabel();
        QueueRedraw();
    }

    public bool IsWithinInteractionRange(Vector2 worldPosition) =>
        _presentation is not null && GlobalPosition.DistanceTo(worldPosition) <= _presentation.InteractionRadius;

    public bool RequestInteraction(Vector2 worldPosition)
    {
        if (!IsWithinInteractionRange(worldPosition))
        {
            return false;
        }

        InteractionRequested?.Invoke(this);
        return true;
    }

    public Vector2[] GetWorldFootprint(float padding = 0)
    {
        if (_presentation is null)
        {
            return [];
        }

        return MachinePlacementGeometry
            .CreateRectangleCorners(Vector2.Zero, _presentation.Footprint + new Vector2(padding * 2, padding * 2), 0)
            .Select(ToGlobal)
            .ToArray();
    }

    public override void _Draw()
    {
        if (_presentation is null || _state is null)
        {
            return;
        }

        var half = _presentation.Footprint * 0.5f;
        var chassis = CreateBeveledRectangle(_presentation.Footprint, Mathf.Min(10, half.X * 0.18f));
        var progressAlpha = _state.IsConstructionComplete
            ? 1
            : Mathf.Lerp(0.2f, 0.92f, _visualConstructionProgress);
        var body = new Color(_presentation.BodyColor, progressAlpha);
        var accent = new Color(_presentation.AccentColor, progressAlpha);
        var shadow = chassis.Select(point => point + new Vector2(5, 7)).ToArray();

        DrawColoredPolygon(shadow, new Color(0, 0, 0, 0.46f * progressAlpha));
        DrawColoredPolygon(chassis, body);
        DrawPolyline([.. chassis, chassis[0]], accent, 2.2f, true);

        var inset = new Rect2(-half + new Vector2(9, 9), _presentation.Footprint - new Vector2(18, 18));
        DrawRect(inset, new Color(_presentation.BodyColor.Darkened(0.35f), 0.88f * progressAlpha), true);
        DrawRect(inset, new Color(_presentation.AccentColor.Darkened(0.28f), 0.78f * progressAlpha), false, 1.2f, true);
        DrawLine(new Vector2(-half.X + 12, 0), new Vector2(half.X - 12, 0),
            new Color(_presentation.AccentColor.Darkened(0.45f), 0.6f * progressAlpha), 1, true);
        DrawMachineGlyph(_presentation.Glyph, accent, new Color(body.Lightened(0.28f), progressAlpha));

        var statusColor = GetStatusColor(_state.Status);
        DrawCircle(new Vector2(half.X - 12, -half.Y + 12), 4.2f, new Color(statusColor, progressAlpha));
        DrawArc(new Vector2(half.X - 12, -half.Y + 12), 7.2f, 0, Mathf.Tau, 16,
            new Color(statusColor, 0.36f * progressAlpha), 1.2f, true);

        if (!_state.IsConstructionComplete || _constructionVisualRunning)
        {
            DrawConstructionHologram(half, chassis);
        }
    }

    private void EnsurePresentationNodes()
    {
        if (_presentation is null)
        {
            return;
        }

        if (_collisionShape is null)
        {
            _collisionShape = new CollisionShape2D
            {
                Name = "MachineCollision",
                Shape = new RectangleShape2D { Size = _presentation.Footprint },
            };
            AddChild(_collisionShape);
        }
        else if (_collisionShape.Shape is RectangleShape2D rectangle)
        {
            rectangle.Size = _presentation.Footprint;
        }

        if (_statusLabel is null)
        {
            _statusLabel = new Label
            {
                Name = "Status",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 3,
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_statusLabel);
        }

        _statusLabel.Position = new Vector2(-90, -(_presentation.Footprint.Y * 0.5f) - 30);
        _statusLabel.Size = new Vector2(180, 22);
        RefreshStatusLabel();
    }

    private void RefreshStatusLabel()
    {
        if (_statusLabel is null || _state is null)
        {
            return;
        }

        _statusLabel.Text = !_state.IsConstructionComplete || _constructionVisualRunning
            ? $"IM BAU  {Mathf.RoundToInt(_visualConstructionProgress * 100)}%"
            : GetStatusText(_state.Status);
        _statusLabel.AddThemeColorOverride("font_color", GetStatusColor(_state.Status).Lightened(0.12f));
    }

    private void DrawConstructionHologram(Vector2 half, IReadOnlyList<Vector2> chassis)
    {
        var pulse = 0.52f + (Mathf.Sin((float)Time.GetTicksMsec() * 0.008f) * 0.18f);
        var hologram = new Color(0.12f, 0.86f, 1.0f, pulse);
        DrawPolyline([.. chassis, chassis[0]], hologram, 1.2f, true);
        for (var column = -2; column <= 2; column++)
        {
            var x = half.X * column / 2.5f;
            DrawLine(new Vector2(x, -half.Y), new Vector2(x, half.Y),
                new Color(hologram, 0.18f), 0.8f, true);
        }

        var scanY = Mathf.Lerp(half.Y, -half.Y, _visualConstructionProgress);
        DrawLine(new Vector2(-half.X, scanY), new Vector2(half.X, scanY), hologram, 2.3f, true);
    }

    private void DrawMachineGlyph(MachineGlyph glyph, Color accent, Color secondary)
    {
        switch (glyph)
        {
            case MachineGlyph.Crusher:
                DrawColoredPolygon([new(-22, -12), new(-3, 0), new(-22, 12)], accent);
                DrawColoredPolygon([new(22, -12), new(3, 0), new(22, 12)], accent);
                break;
            case MachineGlyph.Smelter:
                DrawColoredPolygon([new(0, -20), new(14, 8), new(5, 18), new(0, 9), new(-7, 18), new(-14, 8)], secondary);
                DrawCircle(new Vector2(0, 7), 7, accent);
                break;
            case MachineGlyph.Foundry:
                DrawCircle(new Vector2(-13, -7), 7, secondary);
                DrawCircle(new Vector2(13, -7), 7, secondary);
                DrawLine(new Vector2(-13, 0), new Vector2(0, 15), accent, 3, true);
                DrawLine(new Vector2(13, 0), new Vector2(0, 15), accent, 3, true);
                break;
            case MachineGlyph.Constructor:
            case MachineGlyph.Fabricator:
                DrawCircle(Vector2.Zero, 17, secondary);
                DrawCircle(Vector2.Zero, 8, _presentation!.BodyColor.Darkened(0.35f));
                for (var index = 0; index < 8; index++)
                {
                    var direction = Vector2.FromAngle(Mathf.Tau * index / 8f);
                    DrawLine(direction * 15, direction * 23, accent, 3, true);
                }
                break;
            case MachineGlyph.BasicGenerator:
            case MachineGlyph.FuelGenerator:
                DrawColoredPolygon([new(4, -22), new(-13, 2), new(-3, 2), new(-8, 22), new(15, -7), new(4, -7)], accent);
                break;
            case MachineGlyph.Storage:
                for (var row = -1; row <= 1; row++)
                {
                    DrawRect(new Rect2(-24, (row * 12) - 4, 48, 8), row == 0 ? accent : secondary, row == 0);
                }
                break;
            case MachineGlyph.Research:
                DrawCircle(Vector2.Zero, 5, accent);
                DrawArc(Vector2.Zero, 20, 0, Mathf.Tau, 24, secondary, 2, true);
                DrawArc(Vector2.Zero, 20, -0.8f, 0.8f, 12, accent, 3, true);
                break;
            case MachineGlyph.WaterProcessor:
            case MachineGlyph.Electrolyzer:
            case MachineGlyph.Refinery:
                DrawCircle(new Vector2(-11, 0), 11, secondary);
                DrawCircle(new Vector2(11, 0), 11, accent);
                DrawLine(new Vector2(0, -19), new Vector2(0, 19), secondary, 2, true);
                break;
        }
    }

    private static Vector2[] CreateBeveledRectangle(Vector2 size, float bevel)
    {
        var half = size * 0.5f;
        return
        [
            new(-half.X + bevel, -half.Y),
            new(half.X - bevel, -half.Y),
            new(half.X, -half.Y + bevel),
            new(half.X, half.Y - bevel),
            new(half.X - bevel, half.Y),
            new(-half.X + bevel, half.Y),
            new(-half.X, half.Y - bevel),
            new(-half.X, -half.Y + bevel),
        ];
    }

    private static string GetStatusText(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => "IM BAU",
        MachineOperationStatus.Disabled => "AUSGESCHALTET",
        MachineOperationStatus.NoRecipeSelected => "KEIN REZEPT",
        MachineOperationStatus.Ready => "BEREIT",
        MachineOperationStatus.Producing => "PRODUZIERT",
        MachineOperationStatus.WaitingForMaterial => "WARTET AUF MATERIAL",
        MachineOperationStatus.WaitingForEnergy => "WARTET AUF ENERGIE",
        MachineOperationStatus.OutputFull => "AUSGABE VOLL",
        _ => "STATUS UNBEKANNT",
    };

    private static Color GetStatusColor(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => new Color(0.12f, 0.86f, 1.0f),
        MachineOperationStatus.Producing => new Color(0.28f, 1.0f, 0.68f),
        MachineOperationStatus.Ready => new Color(0.35f, 0.94f, 0.67f),
        MachineOperationStatus.WaitingForMaterial or
        MachineOperationStatus.WaitingForEnergy or
        MachineOperationStatus.OutputFull => new Color(1.0f, 0.69f, 0.27f),
        _ => new Color(0.46f, 0.61f, 0.67f),
    };
}

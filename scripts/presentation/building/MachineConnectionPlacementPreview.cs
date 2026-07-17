using Godot;
using SpaceFactory.Core.Logistics;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Two-click placement feedback shared by power cables, conveyors and both
/// pipe media. It never mutates inventories or connection state itself.
/// </summary>
public partial class MachineConnectionPlacementPreview : Node2D
{
    private ConnectionTypeDefinition? _type;
    private ConnectionPresentationDefinition? _presentation;
    private MachineView? _source;
    private Label? _hint;
    private Vector2 _mouseWorldPosition;

    public bool IsActive { get; private set; }

    public ConnectionTypeDefinition? SelectedType => _type;

    public MachineView? Source =>
        _source is not null && GodotObject.IsInstanceValid(_source) ? _source : null;

    public Vector2 MouseWorldPosition => _mouseWorldPosition;

    public override void _Ready()
    {
        ZIndex = 80;
        EnsureHint();
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (!IsActive)
        {
            return;
        }

        _mouseWorldPosition = GetGlobalMousePosition();
        RefreshHintPosition();
        QueueRedraw();
    }

    public void Start(ConnectionTypeDefinition type)
    {
        ArgumentNullException.ThrowIfNull(type);
        _type = type;
        _presentation = ConnectionPresentationCatalog.Get(type.Id);
        _source = null;
        _mouseWorldPosition = IsInsideTree() ? GetGlobalMousePosition() : Vector2.Zero;
        IsActive = true;
        Visible = true;
        EnsureHint();
        SetHint($"{type.DisplayName.ToUpperInvariant()}: AUSGANGSMASCHINE WÄHLEN", valid: true);
        QueueRedraw();
    }

    public void SetSource(MachineView source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!IsActive)
        {
            throw new InvalidOperationException("Connection placement is not active.");
        }

        _source = source;
        SetHint("QUELLE GEWÄHLT · ZIELMASCHINE ANKLICKEN", valid: true);
        QueueRedraw();
    }

    public void SetFailure(string message)
    {
        if (IsActive)
        {
            SetHint(message.ToUpperInvariant(), valid: false);
        }
    }

    public void Cancel()
    {
        IsActive = false;
        Visible = false;
        _type = null;
        _presentation = null;
        _source = null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsActive || _presentation is null)
        {
            return;
        }

        var mouse = ToLocal(_mouseWorldPosition);
        if (Source is not { } source || _type is null)
        {
            DrawCircle(mouse, 13, new Color(0.02f, 0.06f, 0.075f, 0.68f));
            DrawArc(mouse, 13, 0, Mathf.Tau, 24, _presentation.FlowColor, 2, true);
            DrawLine(mouse + new Vector2(-7, 0), mouse + new Vector2(7, 0), _presentation.FlowColor, 1.4f, true);
            DrawLine(mouse + new Vector2(0, -7), mouse + new Vector2(0, 7), _presentation.FlowColor, 1.4f, true);
            return;
        }

        var sourceAnchor = ToLocal(source.GetWorldConnectionAnchor(
            ConnectionPresentationCatalog.GetSourceAnchor(_type.Kind)));
        var withinRange = sourceAnchor.DistanceTo(mouse) <= ConnectionPresentationCatalog.MaximumConnectionLength;
        var color = withinRange ? _presentation.FlowColor : new Color(1.0f, 0.31f, 0.33f);
        DrawPolyline([sourceAnchor, mouse], new Color(0, 0, 0, 0.58f), _presentation.LineWidth + 3, true);
        DrawPolyline([sourceAnchor, mouse], new Color(color, 0.72f), _presentation.LineWidth, true);
        DrawCircle(sourceAnchor, 7, new Color(color, 0.88f));
        DrawCircle(mouse, 8, new Color(0.02f, 0.05f, 0.06f, 0.88f));
        DrawArc(mouse, 8, 0, Mathf.Tau, 18, color, 2, true);
    }

    private void EnsureHint()
    {
        if (_hint is not null)
        {
            return;
        }

        _hint = new Label
        {
            Name = "ConnectionPlacementHint",
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
        _hint.AddThemeColorOverride(
            "font_color",
            valid ? new Color(0.42f, 0.94f, 1.0f) : new Color(1.0f, 0.45f, 0.46f));
        RefreshHintPosition();
    }

    private void RefreshHintPosition()
    {
        if (_hint is null)
        {
            return;
        }

        var localMouse = ToLocal(_mouseWorldPosition);
        _hint.Position = localMouse + new Vector2(-190, 20);
        _hint.Size = new Vector2(380, 28);
    }
}

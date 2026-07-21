using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Small viewport-clamped item menu. It only presents actions; all validation and mutations
/// remain in the inventory owner so an old popup can never bypass inventory rules.
/// </summary>
public partial class InventoryItemContextMenu : Control
{
    private const float MenuWidth = 218;
    private const float MenuMargin = 10;

    private PanelContainer _panel = null!;
    private Label _title = null!;
    private VBoxContainer _actions = null!;
    private InventoryItemContextMenuModel? _model;
    private Vector2 _requestedPosition;

    public bool IsOpen => Visible && _model is not null;

    public IReadOnlyList<InventoryItemContextAction> VisibleActions =>
        _model?.Actions ?? Array.Empty<InventoryItemContextAction>();

    public event Action<InventoryItemContextAction, InventoryItemContextRequest>? ActionRequested;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 80;
        BuildVisuals();
        Visible = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true })
        {
            return;
        }

        Close();
        AcceptEvent();
    }

    public void Open(InventoryItemContextMenuModel model, Vector2 screenPosition)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Actions.Count == 0)
        {
            Close();
            return;
        }

        _model = model;
        _requestedPosition = screenPosition;
        _title.Text = model.Request.DisplayName.ToUpperInvariant();
        RebuildActions(model.Actions);
        Visible = true;
        MoveToFront();
        CallDeferred(nameof(ClampToViewport));
    }

    public bool TryClose()
    {
        if (!IsOpen)
        {
            return false;
        }

        Close();
        return true;
    }

    public void Close()
    {
        Visible = false;
        _model = null;
    }

    private void BuildVisuals()
    {
        _panel = new PanelContainer
        {
            Name = "MenuPanel",
            CustomMinimumSize = new Vector2(MenuWidth, 0),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.003f, 0.023f, 0.034f, 0.985f),
            BorderColor = new Color(0.09f, 0.56f, 0.7f, 0.98f),
            BorderWidthLeft = 1,
            BorderWidthTop = 2,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ShadowColor = new Color(0, 0, 0, 0.62f),
            ShadowSize = 9,
        });
        AddChild(_panel);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 7);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 7);
        _panel.AddChild(margin);

        var layout = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 4);
        margin.AddChild(layout);
        _title = new Label
        {
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddThemeColorOverride("font_color", new Color(0.53f, 0.9f, 1, 1));
        _title.AddThemeFontSizeOverride("font_size", 11);
        layout.AddChild(_title);

        var rule = new HSeparator { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddChild(rule);
        _actions = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _actions.AddThemeConstantOverride("separation", 3);
        layout.AddChild(_actions);
    }

    private void RebuildActions(IReadOnlyList<InventoryItemContextAction> actions)
    {
        foreach (var child in _actions.GetChildren())
        {
            _actions.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var action in actions)
        {
            var captured = action;
            var button = new Button
            {
                Text = DescribeAction(action),
                CustomMinimumSize = new Vector2(0, 29),
                Alignment = HorizontalAlignment.Left,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                FocusMode = FocusModeEnum.None,
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            button.AddThemeColorOverride("font_color", new Color(0.76f, 0.91f, 0.95f, 1));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(
                new Color(0.008f, 0.04f, 0.052f, 0.94f),
                new Color(0.06f, 0.25f, 0.31f, 0.74f)));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(
                new Color(0.018f, 0.105f, 0.13f, 0.98f),
                new Color(0.15f, 0.69f, 0.84f, 1)));
            button.Pressed += () => Execute(captured);
            _actions.AddChild(button);
        }
    }

    private void Execute(InventoryItemContextAction action)
    {
        if (_model is not { } model)
        {
            return;
        }

        Close();
        ActionRequested?.Invoke(action, model.Request);
    }

    private void ClampToViewport()
    {
        if (!IsOpen)
        {
            return;
        }

        _panel.ResetSize();
        var viewport = GetViewportRect().Size;
        var position = new Vector2(
            Mathf.Clamp(_requestedPosition.X + 8, MenuMargin, Mathf.Max(MenuMargin, viewport.X - _panel.Size.X - MenuMargin)),
            Mathf.Clamp(_requestedPosition.Y + 8, MenuMargin, Mathf.Max(MenuMargin, viewport.Y - _panel.Size.Y - MenuMargin)));
        _panel.Position = position;
    }

    private static string DescribeAction(InventoryItemContextAction action) => action switch
    {
        InventoryItemContextAction.Select => "ITEM AUSWÄHLEN",
        InventoryItemContextAction.SplitStack => "STAPEL TEILEN",
        InventoryItemContextAction.TakeSingleItem => "EINZELNES NEHMEN",
        InventoryItemContextAction.DropStack => "ITEM(S) FALLEN LASSEN",
        InventoryItemContextAction.EquipToHotbar => "ITEM AUSRÜSTEN",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
    };
}

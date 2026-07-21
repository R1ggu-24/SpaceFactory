using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// One shared, mouse-transparent hover label for every item-bearing UI control. Consumers only
/// announce enter/exit; delayed presentation, replacement and fading are kept consistent here.
/// </summary>
public partial class ItemHoverNamePresenter : CanvasLayer
{
    public const double DelaySeconds = 0.2;
    public const double VisibleSeconds = 0.75;
    public const double FadeSeconds = 0.25;
    private const string PresenterNodeName = "SpaceFactoryItemHoverNamePresenter";

    private PanelContainer _panel = null!;
    private Label _label = null!;
    private ulong _sourceInstanceId;
    private string _pendingName = string.Empty;
    private double _remainingDelay;
    private Tween? _fadeTween;

    public bool IsShowing => _panel.Visible;

    public override void _Ready()
    {
        Layer = 240;
        ProcessMode = ProcessModeEnum.Always;
        BuildVisuals();
        SetProcess(false);
    }

    public override void _ExitTree() => _fadeTween?.Kill();

    public override void _Process(double delta)
    {
        PositionNearMouse();
        if (_remainingDelay <= 0)
        {
            return;
        }

        _remainingDelay -= delta;
        if (_remainingDelay > 0 || string.IsNullOrWhiteSpace(_pendingName))
        {
            return;
        }

        ShowPendingName();
    }

    public static void Begin(Control source, string itemName)
    {
        if (!GodotObject.IsInstanceValid(source) || string.IsNullOrWhiteSpace(itemName) || !source.IsInsideTree())
        {
            return;
        }

        GetOrCreate(source).BeginInternal(source.GetInstanceId(), itemName);
    }

    public static void End(Control source)
    {
        if (!GodotObject.IsInstanceValid(source) || !source.IsInsideTree())
        {
            return;
        }

        var root = source.GetTree().Root;
        var presenter = root.GetNodeOrNull<ItemHoverNamePresenter>(PresenterNodeName);
        if (presenter is not null)
        {
            presenter.EndInternal(source.GetInstanceId());
        }
    }

    private static ItemHoverNamePresenter GetOrCreate(Control source)
    {
        var root = source.GetTree().Root;
        var existing = root.GetNodeOrNull<ItemHoverNamePresenter>(PresenterNodeName);
        if (existing is not null)
        {
            return existing;
        }

        var presenter = new ItemHoverNamePresenter { Name = PresenterNodeName };
        root.AddChild(presenter);
        return presenter;
    }

    private void BeginInternal(ulong sourceInstanceId, string itemName)
    {
        _fadeTween?.Kill();
        _sourceInstanceId = sourceInstanceId;
        _pendingName = itemName;
        _remainingDelay = DelaySeconds;
        _panel.Visible = false;
        _panel.Modulate = new Color(1, 1, 1, 0);
        SetProcess(true);
        PositionNearMouse();
    }

    private void EndInternal(ulong sourceInstanceId)
    {
        if (_sourceInstanceId != sourceInstanceId)
        {
            return;
        }

        HideImmediately();
    }

    private void ShowPendingName()
    {
        _remainingDelay = 0;
        _label.Text = _pendingName;
        _panel.Visible = true;
        _panel.Modulate = new Color(1, 1, 1, 0);
        PositionNearMouse();
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.SetEase(Tween.EaseType.Out);
        _fadeTween.SetTrans(Tween.TransitionType.Cubic);
        _fadeTween.TweenProperty(_panel, new NodePath("modulate:a"), 1.0f, 0.1);
        _fadeTween.TweenInterval(VisibleSeconds);
        _fadeTween.TweenProperty(_panel, new NodePath("modulate:a"), 0.0f, FadeSeconds);
        _fadeTween.TweenCallback(Callable.From(HideAfterFade));
    }

    private void HideAfterFade()
    {
        _panel.Visible = false;
        SetProcess(false);
    }

    private void HideImmediately()
    {
        _fadeTween?.Kill();
        _sourceInstanceId = 0;
        _pendingName = string.Empty;
        _remainingDelay = 0;
        if (_panel is not null)
        {
            _panel.Visible = false;
        }
        SetProcess(false);
    }

    private void PositionNearMouse()
    {
        if (_panel is null)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var requested = GetViewport().GetMousePosition() + new Vector2(14, 18);
        _panel.Position = new Vector2(
            Mathf.Clamp(requested.X, 8, Mathf.Max(8, viewportSize.X - _panel.Size.X - 8)),
            Mathf.Clamp(requested.Y, 8, Mathf.Max(8, viewportSize.Y - _panel.Size.Y - 8)));
    }

    private void BuildVisuals()
    {
        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            CustomMinimumSize = new Vector2(90, 25),
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.002f, 0.022f, 0.032f, 0.96f),
            BorderColor = new Color(0.09f, 0.52f, 0.65f, 0.9f),
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
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = 4,
        });
        _label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _label.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.98f, 1));
        _label.AddThemeFontSizeOverride("font_size", 11);
        _panel.AddChild(_label);
        AddChild(_panel);
    }
}

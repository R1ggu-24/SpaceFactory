using Godot;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Presentation.InventoryUI;

namespace SpaceFactory.Presentation.World;

/// <summary>Small persistent zero-gravity stack with bounded inertia and lightweight collisions.</summary>
public partial class DroppedItemView : CharacterBody2D
{
    public const uint DroppedItemCollisionLayer = 1u << 5;
    private const float VisualRadius = 22;

    private ResourceIconControl _icon = null!;
    private Label _amount = null!;
    private double _angularVelocity;

    public string DroppedItemId { get; private set; } = string.Empty;

    public void Configure(DroppedItemStateData state, ItemPresentationViewModel item)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(item);
        DroppedItemId = state.Id;
        GlobalPosition = new Vector2((float)state.PositionX, (float)state.PositionY);
        GlobalRotation = (float)state.RotationRadians;
        Velocity = LimitVelocity(new Vector2((float)state.VelocityX, (float)state.VelocityY));
        _angularVelocity = state.AngularVelocityRadians;

        CollisionLayer = DroppedItemCollisionLayer;
        CollisionMask = 1u | SpaceFactory.Presentation.Building.MachineView.MachineCollisionLayer;
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = VisualRadius },
        });

        var frame = new PanelContainer
        {
            Position = new Vector2(-VisualRadius, -VisualRadius),
            Size = Vector2.One * VisualRadius * 2,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.012f, 0.045f, 0.06f, 0.96f),
            BorderColor = item.Color.Lightened(0.12f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        _icon = new ResourceIconControl { MouseFilter = Control.MouseFilterEnum.Ignore };
        _icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _icon.OffsetLeft = 4;
        _icon.OffsetTop = 4;
        _icon.OffsetRight = -4;
        _icon.OffsetBottom = -4;
        _icon.Configure(item);
        frame.AddChild(_icon);
        AddChild(frame);

        _amount = new Label
        {
            Text = state.Amount > 1 ? state.Amount.ToString() : string.Empty,
            Position = new Vector2(2, 4),
            Size = new Vector2(18, 15),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _amount.AddThemeColorOverride("font_color", Colors.White);
        _amount.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _amount.AddThemeFontSizeOverride("font_size", 10);
        AddChild(_amount);
        frame.TooltipText = $"{item.DisplayName} · {state.Amount}";
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = LimitVelocity(Velocity);
        MoveAndSlide();
        for (var index = 0; index < GetSlideCollisionCount(); index++)
        {
            var collision = GetSlideCollision(index);
            Velocity = Velocity.Bounce(collision.GetNormal()) * 0.62f;
        }

        Rotation += (float)(_angularVelocity * delta);
    }

    public DroppedItemStateData Capture(DroppedItemStateData source) => source with
    {
        PositionX = GlobalPosition.X,
        PositionY = GlobalPosition.Y,
        RotationRadians = GlobalRotation,
        VelocityX = Velocity.X,
        VelocityY = Velocity.Y,
        AngularVelocityRadians = _angularVelocity,
    };

    private static Vector2 LimitVelocity(Vector2 velocity)
    {
        var maximum = (float)WorldItemDropConfiguration.MaximumInheritedSpeed;
        return velocity.LengthSquared() > maximum * maximum
            ? velocity.Normalized() * maximum
            : velocity;
    }
}

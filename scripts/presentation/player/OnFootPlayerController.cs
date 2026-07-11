using Godot;

namespace SpaceFactory.Presentation.Player;

public partial class OnFootPlayerController : CharacterBody2D
{
    [Export]
    public float MovementSpeed { get; set; } = 280.0f;

    public bool IsControlActive { get; private set; }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsControlActive)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction * MovementSpeed;
        MoveAndSlide();

        if (direction.LengthSquared() > 0.01f)
        {
            Rotation = direction.Angle() + Mathf.Pi / 2.0f;
        }
    }

    public void SetControlActive(bool active)
    {
        IsControlActive = active;
        Visible = active;
        GetNode<Camera2D>("Camera2D").Enabled = active;
        if (!active)
        {
            Velocity = Vector2.Zero;
        }
    }
}

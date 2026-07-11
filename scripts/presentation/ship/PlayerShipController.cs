using Godot;

namespace SpaceFactory.Presentation.Ship;

public partial class PlayerShipController : CharacterBody2D
{
    [Export]
    public float MovementSpeed { get; set; } = 650.0f;

    [Export]
    public float CockpitEntryRadius { get; set; } = 70.0f;

    public bool IsControlActive { get; private set; } = true;
    public Vector2 CockpitEntryPosition => GetNode<Marker2D>("CockpitEntryPoint").GlobalPosition;

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
        GetNode<Camera2D>("Camera2D").Enabled = active;
        if (!active)
        {
            Velocity = Vector2.Zero;
        }
    }
}

using Godot;

namespace SpaceFactory.Presentation.Ship;

public partial class PlayerShipController : CharacterBody2D
{
    [Export]
    public float MovementSpeed { get; set; } = 650.0f;

    public override void _PhysicsProcess(double delta)
    {
        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction * MovementSpeed;
        MoveAndSlide();

        if (direction.LengthSquared() > 0.01f)
        {
            Rotation = direction.Angle();
        }
    }
}

using Godot;

namespace SpaceFactory.Presentation.Ship;

public partial class PlayerShipController : CharacterBody2D
{
    public const float VisualScaleMultiplier = 1.8f;
    private static readonly float[] EngineOffsets = [-48.33f * VisualScaleMultiplier, 48.33f * VisualScaleMultiplier];
    private const float EngineEmitterY = 100.0f * VisualScaleMultiplier;
    [Export]
    public float MovementSpeed { get; set; } = 650.0f;

    [Export]
    public float Acceleration { get; set; } = 1_350.0f;

    [Export]
    public float Deceleration { get; set; } = 1_750.0f;

    [Export]
    public float RotationSpeed { get; set; } = 6.0f;

    [Export]
    public float CockpitEntryRadius { get; set; } = 70.0f;

    [Export]
    public float CockpitExitClearance { get; set; } = 129.6f;

    public bool IsControlActive { get; private set; } = true;
    public Vector2 CockpitEntryPosition => GetNode<Marker2D>("CockpitEntryPoint").GlobalPosition;
    private float _engineIntensity;
    private float _enginePulse;

    public override void _PhysicsProcess(double delta)
    {
        if (!IsControlActive)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var targetVelocity = direction * MovementSpeed;
        var velocityChange = direction.LengthSquared() > 0.01f ? Acceleration : Deceleration;
        Velocity = Velocity.MoveToward(targetVelocity, velocityChange * (float)delta);
        MoveAndSlide();

        var targetEngineIntensity = direction.LengthSquared() > 0.01f ? 1.0f : 0.0f;
        _engineIntensity = Mathf.MoveToward(_engineIntensity, targetEngineIntensity, (float)delta * 4.5f);
        _enginePulse += (float)delta * 13;
        QueueRedraw();

        if (direction.LengthSquared() > 0.01f)
        {
            var targetRotation = direction.Angle() + Mathf.Pi / 2.0f;
            Rotation = Mathf.LerpAngle(Rotation, targetRotation, RotationSpeed * (float)delta);
        }
    }

    public override void _Draw()
    {
        if (_engineIntensity <= 0.01f)
        {
            return;
        }

        var pulse = 0.88f + (Mathf.Sin(_enginePulse) * 0.12f);
        foreach (var x in EngineOffsets)
        {
            var emitter = new Vector2(x, EngineEmitterY);
            var flameLength = 15 * VisualScaleMultiplier * _engineIntensity * pulse;
            DrawCircle(emitter, 8 * VisualScaleMultiplier * _engineIntensity, new Color(0.12f, 0.55f, 1, 0.28f));
            DrawLine(emitter, emitter + new Vector2(0, flameLength),
                new Color(0.15f, 0.68f, 1, 0.65f), 7 * VisualScaleMultiplier * _engineIntensity, true);
            DrawLine(emitter, emitter + new Vector2(0, flameLength * 0.72f),
                new Color(0.8f, 0.96f, 1, 0.9f), 2.2f * VisualScaleMultiplier * _engineIntensity, true);
        }
    }

    public void SetControlActive(bool active)
    {
        IsControlActive = active;
        GetNode<Camera2D>("Camera2D").Enabled = active;
        if (!active)
        {
            Velocity = Vector2.Zero;
            _engineIntensity = 0;
            QueueRedraw();
        }
    }
}

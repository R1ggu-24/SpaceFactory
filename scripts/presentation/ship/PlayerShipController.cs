using Godot;
using SpaceFactory.Core.Ships.Docking;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Presentation.Ship;

public partial class PlayerShipController : CharacterBody2D
{
    public const float BaseSpriteScale = 0.22f;
    public const float VisualScaleMultiplier = 1.8f;
    public const float ShipCameraZoom = 0.22f;
    public const float BoostMultiplier = 2.0f;
    public const float CockpitEntryOffsetY = -165.0f;
    public const float DockingHullReach = 248.0f;
    public const float DockingCenterClearance = 270.0f;
    public static readonly Vector2 DockingClearanceSize = new(
        256.0f * VisualScaleMultiplier,
        276.0f * VisualScaleMultiplier);

    private static readonly Vector2[] BaseCollisionPolygon =
    [
        new(0, -130),
        new(48, -52),
        new(128, 66),
        new(116, 100),
        new(45, 88),
        new(28, 126),
        new(0, 138),
        new(-28, 126),
        new(-45, 88),
        new(-116, 100),
        new(-128, 66),
        new(-48, -52),
    ];
    private static readonly Vector2[] ScaledCollisionPolygon = BaseCollisionPolygon
        .Select(point => point * VisualScaleMultiplier)
        .ToArray();
    private static readonly float[] EngineOffsets = [-48.33f * VisualScaleMultiplier, 48.33f * VisualScaleMultiplier];
    private const float EngineEmitterY = 100.0f * VisualScaleMultiplier;
    private const string BoostAction = "ship_boost";

    private static readonly Color NormalGlowColor = new(0.08f, 0.46f, 1.0f, 0.34f);
    private static readonly Color BoostGlowColor = new(0.08f, 0.72f, 1.0f, 0.56f);
    private static readonly Color NormalFlameColor = new(0.10f, 0.62f, 1.0f, 0.76f);
    private static readonly Color BoostFlameColor = new(0.18f, 0.82f, 1.0f, 0.96f);
    private static readonly Color NormalCoreColor = new(0.76f, 0.95f, 1.0f, 0.94f);
    private static readonly Color BoostCoreColor = new(0.92f, 1.0f, 1.0f, 1.0f);

    [Export]
    public float MovementSpeed { get; set; } = 650.0f;

    [Export]
    public float Acceleration { get; set; } = 1_350.0f;

    [Export]
    public float Deceleration { get; set; } = 1_750.0f;

    [Export]
    public float BoostAcceleration { get; set; } = 3_250.0f;

    [Export]
    public float BoostReleaseDeceleration { get; set; } = 4_200.0f;

    [Export]
    public float RotationSpeed { get; set; } = 6.0f;

    [Export]
    public float CockpitEntryRadius { get; set; } = 70.0f;

    [Export]
    public float CockpitExitClearance { get; set; } = 72.0f * VisualScaleMultiplier;

    public bool IsControlActive { get; private set; } = true;
    public bool IsBoostActive { get; private set; }
    public ShipFuelTank FuelTank { get; private set; } = new();
    public ShipDockingState DockingState { get; } = new();
    public bool IsAttached => DockingState.IsAttached;
    public Vector2 CockpitEntryPosition => GetNode<Marker2D>("CockpitEntryPoint").GlobalPosition;
    public Node2D? AttachedComet => _attachedComet;
    private float _engineIntensity;
    private float _engineBoostIntensity;
    private float _enginePulse;
    private bool _boostInputArmed = true;
    private bool _boostFuelWarningReported;
    private Node2D? _attachedComet;

    public event Action? BoostFuelUnavailable;

    public event Action<double>? FuelChanged;

    public override void _Ready()
    {
        GetNode<Sprite2D>("Sprite").Scale = Vector2.One * BaseSpriteScale * VisualScaleMultiplier;
        GetNode<CollisionPolygon2D>("CollisionPolygon2D").Polygon = ScaledCollisionPolygon;
        GetNode<Marker2D>("CockpitEntryPoint").Position = new Vector2(0, CockpitEntryOffsetY);
        GetNode<Camera2D>("Camera2D").Zoom = Vector2.One * ShipCameraZoom;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationPaused)
        {
            SuppressBoostUntilReleased();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var previousLegProgress = DockingState.LandingLegProgress;
        DockingState.AdvanceLandingLegs(delta);
        if (!Mathf.IsEqualApprox((float)previousLegProgress, (float)DockingState.LandingLegProgress))
        {
            QueueRedraw();
        }

        if (DockingState.IsAttached)
        {
            if (!GodotObject.IsInstanceValid(_attachedComet))
            {
                ForceDetachFromMissingComet();
            }
            else
            {
                ApplyAttachedPose();
                Velocity = Vector2.Zero;
                IsBoostActive = false;
                _engineIntensity = 0;
                _engineBoostIntensity = 0;
                return;
            }
        }

        if (!IsControlActive)
        {
            IsBoostActive = false;
            _engineIntensity = 0;
            _engineBoostIntensity = 0;
            if (Velocity.LengthSquared() > 0.001f)
            {
                MoveAndSlide();
            }

            return;
        }

        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var hasMovementInput = direction.LengthSquared() > 0.01f;
        var boostInputPressed = ReadBoostInput();
        var boostRequested = hasMovementInput && boostInputPressed;
        IsBoostActive = boostRequested && FuelTank.CanActivateBoost;
        if (boostRequested && !FuelTank.CanActivateBoost)
        {
            ReportMissingBoostFuelOnce();
        }
        else if (!boostRequested)
        {
            _boostFuelWarningReported = false;
        }

        var speedMultiplier = IsBoostActive ? BoostMultiplier : 1.0f;
        var targetVelocity = direction * MovementSpeed * speedMultiplier;
        var isSlowingFromBoost = !IsBoostActive && Velocity.LengthSquared() > MovementSpeed * MovementSpeed;
        var velocityChange = !hasMovementInput
            ? Deceleration
            : IsBoostActive
                ? BoostAcceleration
                : isSlowingFromBoost
                    ? BoostReleaseDeceleration
                    : Acceleration;
        Velocity = Velocity.MoveToward(targetVelocity, velocityChange * (float)delta);

        // CharacterBody2D performs a swept collision query along the full motion vector,
        // so the doubled speed remains collision-safe without manual teleport-style steps.
        MoveAndSlide();
        var boostMovedShip = IsBoostActive && GetLastMotion().LengthSquared() > 0.01f;
        if (boostMovedShip)
        {
            var consumption = FuelTank.ConsumeBoostFuel(delta, isBoostActuallyActive: true);
            if (!consumption.Succeeded)
            {
                IsBoostActive = false;
                ReportMissingBoostFuelOnce();
            }
            else
            {
                FuelChanged?.Invoke(FuelTank.CurrentFuel);
            }
        }

        var targetEngineIntensity = hasMovementInput ? 1.0f : 0.0f;
        var targetBoostIntensity = IsBoostActive ? 1.0f : 0.0f;
        _engineIntensity = Mathf.MoveToward(_engineIntensity, targetEngineIntensity, (float)delta * 6.0f);
        _engineBoostIntensity = Mathf.MoveToward(
            _engineBoostIntensity,
            targetBoostIntensity,
            (float)delta * (IsBoostActive ? 7.0f : 10.0f));
        _enginePulse += (float)delta * 13;
        QueueRedraw();

        if (hasMovementInput)
        {
            var targetRotation = direction.Angle() + Mathf.Pi / 2.0f;
            Rotation = Mathf.LerpAngle(Rotation, targetRotation, RotationSpeed * (float)delta);
        }
    }

    public override void _Draw()
    {
        if (_engineIntensity > 0.01f)
        {
            var pulse = 0.96f + (Mathf.Sin(_enginePulse) * 0.04f);
            var glowColor = NormalGlowColor.Lerp(BoostGlowColor, _engineBoostIntensity);
            var flameColor = NormalFlameColor.Lerp(BoostFlameColor, _engineBoostIntensity);
            var coreColor = NormalCoreColor.Lerp(BoostCoreColor, _engineBoostIntensity);
            foreach (var x in EngineOffsets)
            {
                var emitter = new Vector2(x, EngineEmitterY);
                var normalLength = 23.0f * VisualScaleMultiplier;
                var boostLength = 20.0f * VisualScaleMultiplier * _engineBoostIntensity;
                var flameLength = (normalLength + boostLength) * _engineIntensity * pulse;
                var glowRadius = (9.5f + (3.0f * _engineBoostIntensity)) * VisualScaleMultiplier * _engineIntensity;
                var flameWidth = (8.2f + (2.5f * _engineBoostIntensity)) * VisualScaleMultiplier * _engineIntensity;
                var coreWidth = (2.8f + (1.2f * _engineBoostIntensity)) * VisualScaleMultiplier * _engineIntensity;

                DrawCircle(emitter, glowRadius, glowColor);
                DrawLine(emitter, emitter + new Vector2(0, flameLength),
                    flameColor, flameWidth, true);
                DrawLine(emitter, emitter + new Vector2(0, flameLength * 0.76f),
                    coreColor, coreWidth, true);

                if (_engineBoostIntensity > 0.01f)
                {
                    var tailColor = new Color(0.1f, 0.62f, 1.0f, 0.22f * _engineBoostIntensity);
                    DrawLine(
                        emitter + new Vector2(0, flameLength * 0.72f),
                        emitter + new Vector2(0, flameLength * 1.12f),
                        tailColor,
                        2.0f * VisualScaleMultiplier * _engineBoostIntensity,
                        true);
                }
            }
        }

        DrawLandingLegs();
    }

    public void SetControlActive(bool active)
    {
        IsControlActive = active;
        GetNode<Camera2D>("Camera2D").Enabled = active;
        if (!active)
        {
            if (!DockingState.IsAttached)
            {
                var drift = ShipDriftRules.CalculateUnpilotedDrift(
                    new ShipVelocity(Velocity.X, Velocity.Y));
                Velocity = new Vector2((float)drift.X, (float)drift.Y);
            }

            _engineIntensity = 0;
            SuppressBoostUntilReleased();
        }
    }

    public void RestoreFuel(double currentFuel)
    {
        FuelTank = new ShipFuelTank(currentFuel);
        _boostFuelWarningReported = false;
        FuelChanged?.Invoke(FuelTank.CurrentFuel);
    }

    public ShipDockingDecision ExecuteDocking(
        ShipDockingContext context,
        Node2D? candidateComet)
    {
        var wasAttached = DockingState.IsAttached;
        var previousComet = _attachedComet;
        var decision = DockingState.TryExecute(context);
        if (!decision.IsAllowed)
        {
            return decision;
        }

        Velocity = Vector2.Zero;
        IsBoostActive = false;
        _engineIntensity = 0;
        _engineBoostIntensity = 0;
        SuppressBoostUntilReleased();
        if (!wasAttached && decision.Action == ShipDockingAction.Attach)
        {
            _attachedComet = candidateComet ??
                throw new InvalidOperationException("Attaching requires a live comet node.");
            ApplyAttachedPose();
        }
        else if (wasAttached && decision.Action == ShipDockingAction.Detach)
        {
            var outward = GodotObject.IsInstanceValid(previousComet)
                ? previousComet!.GlobalPosition.DirectionTo(GlobalPosition).Normalized()
                : Vector2.Up.Rotated(Rotation);
            _attachedComet = null;
            GlobalPosition += outward * 18.0f;
        }

        QueueRedraw();
        return decision;
    }

    /// <summary>
    /// Checks an exit clearance circle against the ship's real scaled hull at
    /// an arbitrary pose. This also supports proposed docking poses which are
    /// not yet represented in the physics world.
    /// </summary>
    public static bool IsCircleClearOfHull(
        Vector2 circleCenter,
        float circleRadius,
        Vector2 shipPosition,
        float shipRotation)
    {
        var localCenter = (circleCenter - shipPosition).Rotated(-shipRotation);
        if (IsPointInsideHull(localCenter))
        {
            return false;
        }

        var radiusSquared = circleRadius * circleRadius;
        for (var index = 0; index < ScaledCollisionPolygon.Length; index++)
        {
            var start = ScaledCollisionPolygon[index];
            var end = ScaledCollisionPolygon[(index + 1) % ScaledCollisionPolygon.Length];
            var edge = end - start;
            var edgeLengthSquared = edge.LengthSquared();
            var interpolation = edgeLengthSquared <= 0.0001f
                ? 0
                : Mathf.Clamp((localCenter - start).Dot(edge) / edgeLengthSquared, 0, 1);
            var closest = start + (edge * interpolation);
            if (closest.DistanceSquaredTo(localCenter) <= radiusSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointInsideHull(Vector2 point)
    {
        var inside = false;
        for (var index = 0; index < ScaledCollisionPolygon.Length; index++)
        {
            var start = ScaledCollisionPolygon[index];
            var end = ScaledCollisionPolygon[
                (index + ScaledCollisionPolygon.Length - 1) % ScaledCollisionPolygon.Length];
            if ((start.Y > point.Y) == (end.Y > point.Y))
            {
                continue;
            }

            var edgeIntersectionX = ((end.X - start.X) * (point.Y - start.Y) /
                                     (end.Y - start.Y)) + start.X;
            if (point.X < edgeIntersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void ApplyAttachedPose()
    {
        if (!GodotObject.IsInstanceValid(_attachedComet))
        {
            return;
        }

        var relative = DockingState.RelativeAttachmentPosition;
        GlobalPosition = _attachedComet!.ToGlobal(new Vector2((float)relative.X, (float)relative.Y));
        GlobalRotation = _attachedComet.GlobalRotation + (float)DockingState.AttachmentRotationRadians;
    }

    private void ForceDetachFromMissingComet()
    {
        var context = new ShipDockingContext(
            IsShipControlled: true,
            IsPauseMenuOpen: false,
            ShipSpeed: 0,
            Candidate: null);
        DockingState.TryExecute(context);
        _attachedComet = null;
        Velocity = Vector2.Zero;
        QueueRedraw();
    }

    private void DrawLandingLegs()
    {
        var progress = (float)DockingState.LandingLegProgress;
        if (progress <= 0.001f)
        {
            return;
        }

        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1.0f : 1.0f;
            var hinge = new Vector2(82 * side, 142);
            var knee = hinge.Lerp(new Vector2(102 * side, 205), progress);
            var foot = hinge.Lerp(new Vector2(112 * side, 258), progress);
            DrawLine(hinge, knee, new Color(0.035f, 0.055f, 0.072f, 0.98f), 12, true);
            DrawLine(hinge, knee, new Color(0.42f, 0.55f, 0.62f, 1), 5, true);
            DrawLine(knee, foot, new Color(0.025f, 0.045f, 0.060f, 0.98f), 11, true);
            DrawLine(knee, foot, new Color(0.36f, 0.50f, 0.58f, 1), 4, true);
            DrawCircle(hinge, 6, new Color(0.08f, 0.55f, 0.76f, 1));
            DrawCircle(knee, 5, new Color(0.12f, 0.68f, 0.88f, 1));
            DrawLine(
                foot + new Vector2(-10, 0),
                foot + new Vector2(10, 0),
                new Color(0.18f, 0.28f, 0.33f, 1),
                8,
                true);
        }
    }

    private bool ReadBoostInput()
    {
        var isPressed = Input.IsActionPressed(BoostAction);
        if (!_boostInputArmed)
        {
            if (!isPressed)
            {
                _boostInputArmed = true;
            }

            return false;
        }

        return isPressed;
    }

    private void SuppressBoostUntilReleased()
    {
        _boostInputArmed = false;
        IsBoostActive = false;
        _engineBoostIntensity = 0;
        if (Velocity.LengthSquared() > MovementSpeed * MovementSpeed)
        {
            Velocity = Velocity.Normalized() * MovementSpeed;
        }

        QueueRedraw();
    }

    private void ReportMissingBoostFuelOnce()
    {
        if (_boostFuelWarningReported)
        {
            return;
        }

        _boostFuelWarningReported = true;
        BoostFuelUnavailable?.Invoke();
    }
}

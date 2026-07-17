using Godot;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Ships;
using SpaceFactory.Core.Ships.Docking;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Presentation.Ship;

public partial class PlayerShipController : CharacterBody2D
{
    public const float BaseSpriteScale = 0.22f;
    public const float VisualScaleMultiplier = 1.8f;
    public const float ShipCameraZoom = 0.22f;
    public const float NormalFlightSpeed = (float)ShipFlightConfiguration.NormalFlightSpeed;
    public const float BoostFlightSpeed = (float)ShipFlightConfiguration.BoostFlightSpeed;
    public const float BoostMultiplier = (float)ShipFlightConfiguration.BoostSpeedMultiplier;
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

    // The sockets sit just outside the aft side armour. Keeping the coordinates
    // local to the CharacterBody makes them follow translation and rotation
    // without a per-frame presentation update.
    private static readonly Vector2[] PowerPortAnchors =
    [
        new(-238, 92),
        new(238, 92),
    ];

    private static readonly Color NormalGlowColor = new(0.08f, 0.46f, 1.0f, 0.34f);
    private static readonly Color BoostGlowColor = new(0.08f, 0.72f, 1.0f, 0.56f);
    private static readonly Color NormalFlameColor = new(0.10f, 0.62f, 1.0f, 0.76f);
    private static readonly Color BoostFlameColor = new(0.18f, 0.82f, 1.0f, 0.96f);
    private static readonly Color NormalCoreColor = new(0.76f, 0.95f, 1.0f, 0.94f);
    private static readonly Color BoostCoreColor = new(0.92f, 1.0f, 1.0f, 1.0f);

    [Export]
    public float MovementSpeed { get; set; } = NormalFlightSpeed;

    [Export]
    public float BoostMovementSpeed { get; set; } = BoostFlightSpeed;

    [Export]
    public float Acceleration { get; set; } = (float)ShipFlightConfiguration.NormalAcceleration;

    [Export]
    public float Deceleration { get; set; } = (float)ShipFlightConfiguration.NormalDeceleration;

    [Export]
    public float BoostAcceleration { get; set; } = (float)ShipFlightConfiguration.BoostAcceleration;

    [Export]
    public float BoostReleaseDeceleration { get; set; } =
        (float)ShipFlightConfiguration.BoostReleaseDeceleration;

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
    private readonly ShipPowerPortVisualState[] _powerPortStates =
    [
        ShipPowerPortVisualState.Free,
        ShipPowerPortVisualState.Free,
    ];

    public event Action? BoostFuelUnavailable;

    public event Action<double>? FuelChanged;

    public override void _Ready()
    {
        if (PowerPortAnchors.Length != PowerGridConfiguration.ShipPortCount)
        {
            throw new InvalidOperationException(
                $"Ship presentation exposes {PowerPortAnchors.Length} power ports, " +
                $"but gameplay is configured for {PowerGridConfiguration.ShipPortCount}.");
        }

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

        var targetSpeed = IsBoostActive ? BoostMovementSpeed : MovementSpeed;
        var targetVelocity = direction * targetSpeed;
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
        // so the increased target speeds remain collision-safe without manual teleport-style steps.
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
        DrawPowerPorts();
    }

    /// <summary>
    /// Returns the stable local cable anchor for socket A or B.
    /// </summary>
    public Vector2 GetLocalPowerPortAnchor(ShipPowerPortId port) =>
        PowerPortAnchors[GetPowerPortIndex(port)];

    /// <summary>
    /// Returns the socket position in world space. This is the canonical visual
    /// endpoint for cable previews and installed cable views.
    /// </summary>
    public Vector2 GetWorldPowerPortAnchor(ShipPowerPortId port) =>
        ToGlobal(GetLocalPowerPortAnchor(port));

    public ShipPowerPortVisualState GetPowerPortVisualState(ShipPowerPortId port) =>
        _powerPortStates[GetPowerPortIndex(port)];

    public void SetPowerPortVisualState(
        ShipPowerPortId port,
        ShipPowerPortVisualState state)
    {
        var index = GetPowerPortIndex(port);
        var normalized = state.Normalized();
        if (_powerPortStates[index] == normalized)
        {
            return;
        }

        _powerPortStates[index] = normalized;
        QueueRedraw();
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
        FuelTank.RestoreFuel(currentFuel);
        _boostFuelWarningReported = false;
        FuelChanged?.Invoke(FuelTank.CurrentFuel);
    }

    public void RestoreFreePose(
        Vector2 globalPosition,
        float globalRotation,
        double landingLegProgress = 0)
    {
        if (!globalPosition.IsFinite() || !float.IsFinite(globalRotation))
        {
            throw new ArgumentException("The persisted free-flight pose is invalid.");
        }

        _attachedComet = null;
        DockingState.RestoreDetached(landingLegProgress);
        GlobalPosition = globalPosition;
        GlobalRotation = globalRotation;
        Velocity = Vector2.Zero;
        IsBoostActive = false;
        _engineIntensity = 0;
        _engineBoostIntensity = 0;
        QueueRedraw();
    }

    public void RestoreAttachedPose(
        Node2D comet,
        string cometId,
        Vector2 relativePosition,
        float relativeRotation,
        double landingLegProgress)
    {
        ArgumentNullException.ThrowIfNull(comet);
        if (!GodotObject.IsInstanceValid(comet) || !relativePosition.IsFinite() ||
            !float.IsFinite(relativeRotation))
        {
            throw new ArgumentException("The persisted attached ship pose is invalid.");
        }

        _attachedComet = comet;
        DockingState.RestoreAttached(
            cometId,
            new SpaceFactory.Core.Common.WorldPosition(relativePosition.X, relativePosition.Y),
            relativeRotation,
            landingLegProgress);
        Velocity = Vector2.Zero;
        IsBoostActive = false;
        _engineIntensity = 0;
        _engineBoostIntensity = 0;
        ApplyAttachedPose();
        QueueRedraw();
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

    private void DrawPowerPorts()
    {
        for (var index = 0; index < PowerPortAnchors.Length; index++)
        {
            var port = (ShipPowerPortId)index;
            var state = _powerPortStates[index];
            var anchor = PowerPortAnchors[index];
            var side = index == 0 ? -1.0f : 1.0f;
            var operational = IsAttached && state.IsEnabled;
            var active = operational && state.IsConnected;
            var cyan = active
                ? new Color(0.13f, 0.83f, 1.0f, 0.88f + (0.12f * state.OutputRatio))
                : operational
                    ? new Color(0.10f, 0.54f, 0.68f, 0.68f)
                    : new Color(0.16f, 0.29f, 0.33f, 0.55f);

            // Armoured bracket, recessed connector and two tiny status bars.
            var bracketCenter = anchor - new Vector2(side * 8, 0);
            DrawSetTransform(bracketCenter, 0, Vector2.One);
            DrawColoredPolygon(
            [
                new Vector2(-14, -12),
                new Vector2(9, -12),
                new Vector2(14, -7),
                new Vector2(14, 7),
                new Vector2(9, 12),
                new Vector2(-14, 12),
            ], new Color(0.035f, 0.055f, 0.063f, 0.98f));
            DrawPolyline(
            [
                new Vector2(-14, -12),
                new Vector2(9, -12),
                new Vector2(14, -7),
                new Vector2(14, 7),
                new Vector2(9, 12),
                new Vector2(-14, 12),
                new Vector2(-14, -12),
            ], new Color(0.38f, 0.48f, 0.52f, 0.92f), 2, true);
            DrawRect(new Rect2(-5, -8, 13, 16), new Color(0.008f, 0.018f, 0.024f, 1), true);
            DrawRect(new Rect2(-3, -6, 9, 12), new Color(cyan, active ? 0.34f : 0.14f), true);
            DrawCircle(new Vector2(10, -6), 1.8f, cyan);
            DrawCircle(new Vector2(10, 6), 1.8f, cyan.Darkened(0.2f));
            DrawLine(new Vector2(-10, -7), new Vector2(-10, 7), new Color(0.56f, 0.63f, 0.65f, 0.7f), 1.4f, true);

            // Labels remain readable at gameplay zoom while staying subordinate
            // to the socket silhouette.
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(-10, 4),
                port.ToString(),
                HorizontalAlignment.Center,
                9,
                9,
                new Color(0.77f, 0.87f, 0.89f, 0.92f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private static int GetPowerPortIndex(ShipPowerPortId port)
    {
        var index = (int)port;
        if (index < 0 || index >= PowerPortAnchors.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, null);
        }

        return index;
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

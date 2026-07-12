using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Player;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Presentation.UI;
using SpaceFactory.Presentation.World;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.Player;

public partial class OnFootPlayerController : CharacterBody2D
{
    [Export]
    public float MovementSpeed { get; set; } = 175.0f;

    [Export]
    public float Acceleration { get; set; } = 380.0f;

    [Export]
    public float Deceleration { get; set; } = 520.0f;

    [Export]
    public float RotationSpeed { get; set; } = 7.0f;

    [Export]
    public float MiningRange { get; set; } = 210.0f;

    private Area2D _cursorProbe = null!;
    private Sprite2D _astronautSprite = null!;
    private Node2D _miningTool = null!;
    private Marker2D _miningMuzzle = null!;
    private Vector2 _toolRestPosition;
    private SlotInventory _inventory = null!;
    private IReadOnlyList<ResourceDefinition> _resources = [];
    private ResourceHud _hud = null!;
    private Func<bool> _isUiBlocking = static () => false;
    private ResourceDepositView? _miningTarget;
    private readonly MiningSession _miningSession = new();
    private float _toolPulse;
    private float _walkPhase;
    private Vector2 _spriteRestPosition;
    private Vector2 _spriteRestScale;
    private Tween? _visibilityTween;

    public bool IsControlActive { get; private set; }
    public OnFootActionState ActionState { get; private set; } = OnFootActionState.Exploring;
    public bool IsMining => ActionState == OnFootActionState.Mining;

    public override void _Ready()
    {
        _astronautSprite = GetNode<Sprite2D>("Sprite");
        _spriteRestPosition = _astronautSprite.Position;
        _spriteRestScale = _astronautSprite.Scale;
        _miningTool = GetNode<Node2D>("MiningTool");
        _miningMuzzle = GetNode<Marker2D>("MiningTool/Muzzle");
        _toolRestPosition = _miningTool.Position;
        _cursorProbe = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = ResourceDepositView.ResourceCollisionLayer,
            Monitoring = true,
            Monitorable = false,
        };
        _cursorProbe.AddChild(new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 4 },
        });
        AddChild(_cursorProbe);
        SetCollisionActive(IsControlActive);
    }

    public void Initialize(
        SlotInventory inventory,
        IReadOnlyList<ResourceDefinition> resources,
        ResourceHud hud,
        Func<bool> isUiBlocking)
    {
        _inventory = inventory;
        _resources = resources;
        _hud = hud;
        _isUiBlocking = isUiBlocking;
        _hud.UpdateInventory(inventory, resources);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsControlActive)
        {
            Velocity = Vector2.Zero;
            return;
        }

        _cursorProbe.GlobalPosition = GetGlobalMousePosition();
        var target = FindAimedDeposit();
        var uiBlocked = _isUiBlocking();
        UpdateResourcePrompt(target, uiBlocked);

        if (uiBlocked)
        {
            CancelMining();
            SlowToStop((float)delta);
            return;
        }

        var miningRequested = Input.IsActionPressed("use_mining_tool");
        if (target is not null && MiningInteractionRules.CanMine(
                IsControlActive,
                uiBlocked,
                miningRequested,
                !target.IsExhausted,
                new WorldPosition(GlobalPosition.X, GlobalPosition.Y),
                new WorldPosition(target.GlobalPosition.X, target.GlobalPosition.Y),
                MiningRange))
        {
            ContinueMining(target, (float)delta);
            return;
        }

        CancelMining();
        MoveNormally((float)delta);
    }

    public override void _Draw()
    {
        if (!IsMining || !GodotObject.IsInstanceValid(_miningTarget))
        {
            return;
        }

        var muzzlePosition = ToLocal(_miningMuzzle.GlobalPosition);
        var targetPosition = ToLocal(_miningTarget!.GlobalPosition);
        var pulse = 0.82f + (Mathf.Sin(_toolPulse) * 0.18f);
        DrawLine(muzzlePosition, targetPosition, new Color(0.08f, 0.55f, 1, 0.18f), 9, true);
        DrawLine(muzzlePosition, targetPosition, new Color(0.15f, 0.76f, 1, 0.82f), 4, true);
        DrawLine(muzzlePosition, targetPosition, new Color(0.86f, 0.98f, 1, 0.96f), 1.4f, true);
        DrawCircle(muzzlePosition, 6 * pulse, new Color(0.35f, 0.86f, 1, 0.86f));
        DrawCircle(targetPosition, 9 * pulse, new Color(0.65f, 0.92f, 1, 0.72f));

        for (var particleIndex = 0; particleIndex < 3; particleIndex++)
        {
            var travel = Mathf.PosMod((_toolPulse * 0.055f) + (particleIndex / 3.0f), 1);
            DrawCircle(muzzlePosition.Lerp(targetPosition, travel), 1.8f,
                new Color(0.78f, 0.96f, 1, 0.72f));
        }
    }

    public void SetControlActive(bool active)
    {
        if (!active)
        {
            CancelMining();
            if (_hud is not null)
            {
                _hud.SetResourcePrompt(null);
            }
        }

        IsControlActive = active;
        AnimateVisibility(active);
        SetCollisionActive(active);
        GetNode<Camera2D>("Camera2D").Enabled = active;
        if (!active)
        {
            Velocity = Vector2.Zero;
            UpdateMovementAnimation(0, false);
        }
    }

    public void InterruptCurrentAction() => CancelMining();

    private void SetCollisionActive(bool active)
    {
        GetNode<CollisionShape2D>("CollisionShape2D")
            .SetDeferred(CollisionShape2D.PropertyName.Disabled, !active);
        if (_cursorProbe is not null)
        {
            _cursorProbe.SetDeferred(Area2D.PropertyName.Monitoring, active);
        }
    }

    private ResourceDepositView? FindAimedDeposit()
    {
        ResourceDepositView? closest = null;
        var closestDistance = float.MaxValue;
        foreach (var area in _cursorProbe.GetOverlappingAreas())
        {
            if (area is not ResourceDepositView deposit || deposit.IsExhausted)
            {
                continue;
            }

            var distance = deposit.GlobalPosition.DistanceSquaredTo(_cursorProbe.GlobalPosition);
            if (distance < closestDistance)
            {
                closest = deposit;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void ContinueMining(ResourceDepositView target, float delta)
    {
        if (_miningTarget != target)
        {
            CancelMining();
            _miningTarget = target;
            target.RequestMiningAudioCue();
        }

        ActionState = OnFootActionState.Mining;
        _miningSession.Begin(target.DepositId, target.MiningTimeSeconds);
        var completed = _miningSession.Advance(delta);
        _toolPulse += delta * 16;
        UpdateMiningToolPose();
        SlowToStop(delta);
        var targetRotation = GlobalPosition.DirectionTo(target.GlobalPosition).Angle() + Mathf.Pi / 2;
        Rotation = Mathf.LerpAngle(Rotation, targetRotation, RotationSpeed * delta);
        var progress = (float)_miningSession.Progress;
        target.SetMiningProgress(progress);
        _hud.SetMining(target.DisplayName, progress);
        QueueRedraw();

        if (!completed)
        {
            return;
        }

        var addResult = _inventory.Add(target.Resource.Id, target.YieldAmount);
        if (!addResult.Succeeded)
        {
            _hud.ShowMessage("Inventar voll – Abbau abgebrochen");
            CancelMining();
            return;
        }

        var amount = target.YieldAmount;
        var displayName = target.DisplayName;
        target.MarkExhausted();
        _hud.ShowMessage($"+{amount} {displayName}");
        _hud.UpdateInventory(_inventory, _resources);
        CancelMining(resetTargetVisual: false);
    }

    private void CancelMining(bool resetTargetVisual = true)
    {
        if (resetTargetVisual && GodotObject.IsInstanceValid(_miningTarget))
        {
            _miningTarget!.SetMiningProgress(0);
        }

        _miningTarget = null;
        _miningSession.Cancel();
        _toolPulse = 0;
        ResetMiningToolPose();
        ActionState = OnFootActionState.Exploring;
        if (_hud is not null)
        {
            _hud.SetMining(null, 0);
        }
        QueueRedraw();
    }

    private void MoveNormally(float delta)
    {
        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var targetVelocity = direction * MovementSpeed;
        var velocityChange = direction.LengthSquared() > 0.01f ? Acceleration : Deceleration;
        Velocity = Velocity.MoveToward(targetVelocity, velocityChange * delta);
        MoveAndSlide();
        UpdateMovementAnimation(delta, direction.LengthSquared() > 0.01f);

        if (direction.LengthSquared() > 0.01f)
        {
            var targetRotation = direction.Angle() + Mathf.Pi / 2.0f;
            Rotation = Mathf.LerpAngle(Rotation, targetRotation, RotationSpeed * delta);
        }
    }

    private void SlowToStop(float delta)
    {
        Velocity = Velocity.MoveToward(Vector2.Zero, Deceleration * delta);
        MoveAndSlide();
        UpdateMovementAnimation(delta, false);
    }

    private void UpdateMovementAnimation(float delta, bool isMoving)
    {
        if (delta <= 0)
        {
            _astronautSprite.Position = _spriteRestPosition;
            _astronautSprite.Scale = _spriteRestScale;
            return;
        }

        if (isMoving)
        {
            _walkPhase += delta * 9.5f;
            var step = Mathf.Sin(_walkPhase);
            _astronautSprite.Position = _spriteRestPosition + new Vector2(0, step * 1.15f);
            var compression = Mathf.Cos(_walkPhase * 2) * 0.012f;
            _astronautSprite.Scale = _spriteRestScale * new Vector2(1 + compression, 1 - compression);
            return;
        }

        _astronautSprite.Position = _astronautSprite.Position.Lerp(_spriteRestPosition, Mathf.Clamp(delta * 12, 0, 1));
        _astronautSprite.Scale = _astronautSprite.Scale.Lerp(_spriteRestScale, Mathf.Clamp(delta * 12, 0, 1));
    }

    private void AnimateVisibility(bool active)
    {
        _visibilityTween?.Kill();
        if (active)
        {
            Visible = true;
            Modulate = new Color(1, 1, 1, 0);
            _visibilityTween = CreateTween();
            _visibilityTween.TweenProperty(this, new NodePath("modulate"), Colors.White, 0.16)
                .SetEase(Tween.EaseType.Out);
            return;
        }

        _visibilityTween = CreateTween();
        _visibilityTween.TweenProperty(this, new NodePath("modulate"), new Color(1, 1, 1, 0), 0.12)
            .SetEase(Tween.EaseType.In);
        _visibilityTween.TweenCallback(Callable.From(() =>
        {
            if (!IsControlActive)
            {
                Visible = false;
                Modulate = Colors.White;
            }
        }));
    }

    private void UpdateMiningToolPose()
    {
        var recoil = Mathf.Sin(_toolPulse * 1.7f);
        _miningTool.Position = _toolRestPosition + new Vector2(recoil * 0.7f, recoil * 0.35f);
        _miningTool.Rotation = recoil * 0.018f;
        var energyPulse = 1.0f + (Mathf.Sin(_toolPulse) * 0.035f);
        _miningTool.Scale = Vector2.One * energyPulse;
    }

    private void ResetMiningToolPose()
    {
        if (_miningTool is null)
        {
            return;
        }

        _miningTool.Position = _toolRestPosition;
        _miningTool.Rotation = 0;
        _miningTool.Scale = Vector2.One;
    }

    private void UpdateResourcePrompt(ResourceDepositView? target, bool uiBlocked)
    {
        if (uiBlocked || target is null || IsMining)
        {
            _hud.SetResourcePrompt(null);
            return;
        }

        _hud.SetResourcePrompt(IsWithinMiningRange(target)
            ? $"{InputBindingFormatter.FormatAction("use_mining_tool")} halten: {target.DisplayName}"
            : $"Zu weit entfernt: {target.DisplayName}");
    }

    private bool IsWithinMiningRange(ResourceDepositView target) =>
        GlobalPosition.DistanceTo(target.GlobalPosition) <= MiningRange;
}

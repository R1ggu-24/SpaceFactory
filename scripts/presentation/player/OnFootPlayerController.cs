using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Player;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Presentation.UI;
using SpaceFactory.Presentation.World;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.Player;

public partial class OnFootPlayerController : CharacterBody2D
{
    public const float CameraZoom = 0.36f;

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
    private Node2D _dismantlingTool = null!;
    private Marker2D _miningMuzzle = null!;
    private Marker2D _dismantlingMuzzle = null!;
    private Vector2 _toolRestPosition;
    private Vector2 _dismantlingToolRestPosition;
    private SlotInventory _inventory = null!;
    private IReadOnlyList<ResourceDefinition> _resources = [];
    private ResourceHud _hud = null!;
    private Func<bool> _isUiBlocking = static () => false;
    private Func<bool> _isMovementBlocking = static () => false;
    private ResourceDepositView? _miningTarget;
    private readonly MiningSession _miningSession = new();
    private float _toolPulse;
    private float _walkPhase;
    private Vector2 _spriteRestPosition;
    private Vector2 _spriteRestScale;
    private Vector2 _inheritedDriftVelocity;
    private Tween? _visibilityTween;
    private float _radiationMovementMultiplier = 1;
    private float _radiationMiningMultiplier = 1;
    private bool _dismantlingEffectActive;
    private Vector2 _dismantlingTargetWorld;
    private float _dismantlingProgress;

    public bool IsControlActive { get; private set; }
    public bool IsMiningToolEquipped { get; private set; }
    public bool IsDismantlingToolEquipped { get; private set; }
    public ItemId? ActiveMiningToolId { get; private set; }
    public MiningToolTier? ActiveMiningToolTier { get; private set; }
    public OnFootActionState ActionState { get; private set; } = OnFootActionState.Exploring;
    public bool IsMining => ActionState == OnFootActionState.Mining;

    public override void _Ready()
    {
        _astronautSprite = GetNode<Sprite2D>("Sprite");
        _spriteRestPosition = _astronautSprite.Position;
        _spriteRestScale = _astronautSprite.Scale;
        _miningTool = GetNode<Node2D>("MiningTool");
        _dismantlingTool = GetNode<Node2D>("DismantlingTool");
        _miningMuzzle = GetNode<Marker2D>("MiningTool/Muzzle");
        _dismantlingMuzzle = GetNode<Marker2D>("DismantlingTool/Muzzle");
        _toolRestPosition = _miningTool.Position;
        _dismantlingToolRestPosition = _dismantlingTool.Position;
        _miningTool.Visible = IsMiningToolEquipped;
        _dismantlingTool.Visible = IsDismantlingToolEquipped;
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
        GetNode<Camera2D>("Camera2D").Zoom = Vector2.One * CameraZoom;
        SetCollisionActive(IsControlActive);
    }

    public void Initialize(
        SlotInventory inventory,
        IReadOnlyList<ResourceDefinition> resources,
        ResourceHud hud,
        Func<bool> isUiBlocking,
        Func<bool>? isMovementBlocking = null)
    {
        _inventory = inventory;
        _resources = resources;
        _hud = hud;
        _isUiBlocking = isUiBlocking;
        _isMovementBlocking = isMovementBlocking ?? isUiBlocking;
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
            ClearDismantlingEffect();
            if (_isMovementBlocking())
            {
                SlowToStop((float)delta);
            }
            else
            {
                MoveNormally((float)delta);
            }
            return;
        }

        if (_dismantlingEffectActive && IsDismantlingToolEquipped)
        {
            CancelMining();
            SlowToStop((float)delta);
            var targetRotation = GlobalPosition.DirectionTo(_dismantlingTargetWorld).Angle() + Mathf.Pi / 2;
            Rotation = Mathf.LerpAngle(Rotation, targetRotation, RotationSpeed * (float)delta);
            UpdateDismantlingToolPose((float)delta);
            QueueRedraw();
            return;
        }

        var miningRequested = IsMiningToolEquipped && Input.IsActionPressed("use_mining_tool");
        if (target is not null && ActiveMiningToolTier is { } toolTier &&
            MiningToolRules.CanMine(toolTier, target.Resource) &&
            MiningInteractionRules.CanMine(
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
        if (_dismantlingEffectActive && IsDismantlingToolEquipped)
        {
            DrawDismantlingBeam();
        }

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
            _inheritedDriftVelocity = Vector2.Zero;
            UpdateMovementAnimation(0, false);
        }
    }

    public void InterruptCurrentAction() => CancelMining();

    public void SetMiningToolEquipped(bool equipped) =>
        SetMiningTool(equipped ? ProductionItemIds.MiningTool : null);

    public void SetActiveTool(ItemId? itemId)
    {
        SetMiningTool(itemId);
        IsDismantlingToolEquipped = itemId == ProductionItemIds.MachineDismantlingTool;
        if (!IsDismantlingToolEquipped)
        {
            ClearDismantlingEffect();
        }
        if (_dismantlingTool is not null)
        {
            _dismantlingTool.Visible = IsDismantlingToolEquipped;
        }
    }

    public void SetDismantlingEffect(Vector2 targetWorldPosition, float progress)
    {
        if (!targetWorldPosition.IsFinite() || !float.IsFinite(progress))
        {
            throw new ArgumentException("Dismantling effect values must be finite.");
        }

        _dismantlingEffectActive = true;
        _dismantlingTargetWorld = targetWorldPosition;
        _dismantlingProgress = Math.Clamp(progress, 0, 1);
        QueueRedraw();
    }

    public void ClearDismantlingEffect()
    {
        if (!_dismantlingEffectActive && _dismantlingProgress <= 0)
        {
            return;
        }

        _dismantlingEffectActive = false;
        _dismantlingProgress = 0;
        ResetDismantlingToolPose();
        QueueRedraw();
    }

    public void SetMiningTool(ItemId? itemId)
    {
        var tier = default(MiningToolTier);
        var equipped = itemId is { } selected && MiningToolRules.TryGetTier(selected, out tier);
        if (IsMiningToolEquipped == equipped)
        {
            ActiveMiningToolId = equipped ? itemId : null;
            ActiveMiningToolTier = equipped ? tier : null;
            return;
        }

        IsMiningToolEquipped = equipped;
        ActiveMiningToolId = equipped ? itemId : null;
        ActiveMiningToolTier = equipped ? tier : null;
        if (!equipped)
        {
            CancelMining();
        }

        if (_miningTool is not null)
        {
            _miningTool.Visible = equipped;
        }
    }

    public void StopMovementImmediately()
    {
        Velocity = Vector2.Zero;
        _inheritedDriftVelocity = Vector2.Zero;
        UpdateMovementAnimation(0, false);
    }

    public void ApplyInheritedVelocity(Vector2 velocity)
    {
        _inheritedDriftVelocity = velocity;
        Velocity = velocity;
    }

    public void SetRadiationEffects(double movementMultiplier, double miningEfficiencyMultiplier)
    {
        _radiationMovementMultiplier = (float)Math.Clamp(movementMultiplier, 0.1, 1);
        _radiationMiningMultiplier = (float)Math.Clamp(miningEfficiencyMultiplier, 0.1, 1);
    }

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
        _inheritedDriftVelocity = Vector2.Zero;
        if (_miningTarget != target)
        {
            CancelMining();
            _miningTarget = target;
            target.RequestMiningAudioCue();
        }

        ActionState = OnFootActionState.Mining;
        var speedMultiplier = ActiveMiningToolTier is { } tier
            ? MiningToolRules.GetSpeedMultiplier(tier)
            : 1;
        speedMultiplier *= _radiationMiningMultiplier;
        _miningSession.Begin(target.DepositId, target.MiningTimeSeconds / speedMultiplier);
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
        target.CompleteManualHarvest();
        _hud.ShowMessage($"+{amount} {displayName}");
        _hud.UpdateInventory(_inventory, _resources);
        CancelMining();
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
        var targetVelocity = (direction * MovementSpeed * _radiationMovementMultiplier) + _inheritedDriftVelocity;
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

    private void UpdateDismantlingToolPose(float delta)
    {
        _toolPulse += delta * 19;
        var recoil = Mathf.Sin(_toolPulse * 1.35f);
        _dismantlingTool.Position = _dismantlingToolRestPosition + new Vector2(recoil * 0.8f, recoil * 0.3f);
        _dismantlingTool.Rotation = recoil * 0.022f;
        _dismantlingTool.Scale = Vector2.One * (1 + (Mathf.Sin(_toolPulse * 0.72f) * 0.025f));
    }

    private void ResetDismantlingToolPose()
    {
        if (_dismantlingTool is null)
        {
            return;
        }

        _dismantlingTool.Position = _dismantlingToolRestPosition;
        _dismantlingTool.Rotation = 0;
        _dismantlingTool.Scale = Vector2.One;
    }

    private void DrawDismantlingBeam()
    {
        var muzzle = ToLocal(_dismantlingMuzzle.GlobalPosition);
        var target = ToLocal(_dismantlingTargetWorld);
        var pulse = 0.76f + (Mathf.Sin(_toolPulse * 1.2f) * 0.2f);
        DrawLine(muzzle, target, new Color(0.02f, 0.42f, 0.58f, 0.22f), 11, true);
        DrawLine(muzzle, target, new Color(0.08f, 0.82f, 1f, 0.82f), 4.2f, true);
        DrawLine(muzzle, target, new Color(0.86f, 0.99f, 1f, 0.96f), 1.25f, true);
        DrawCircle(target, 8.5f * pulse, new Color(0.16f, 0.88f, 1f, 0.7f));
        DrawArc(target, 16, -Mathf.Pi / 2, (-Mathf.Pi / 2) + (Mathf.Tau * _dismantlingProgress),
            32, new Color(0.34f, 0.96f, 1f, 0.95f), 3.2f, true);
        DrawArc(target, 16, 0, Mathf.Tau, 32, new Color(0.04f, 0.2f, 0.27f, 0.82f), 1.2f, true);

        for (var particleIndex = 0; particleIndex < 7; particleIndex++)
        {
            var phase = Mathf.PosMod((_toolPulse * 0.028f) + (particleIndex * 0.173f), 1);
            var angle = (particleIndex * 2.399f) + (_toolPulse * 0.11f);
            var distance = 5 + (phase * 22 * (0.35f + _dismantlingProgress));
            var particle = target + (Vector2.FromAngle(angle) * distance);
            DrawLine(particle, particle + (Vector2.FromAngle(angle) * 4),
                new Color(0.35f, 0.92f, 1f, 1 - phase), 1.4f, true);
        }
    }

    private void UpdateResourcePrompt(ResourceDepositView? target, bool uiBlocked)
    {
        if (uiBlocked || target is null || IsMining)
        {
            _hud.SetResourcePrompt(null);
            return;
        }

        if (IsWithinMiningRange(target) && IsMiningToolEquipped &&
            ActiveMiningToolTier is { } selectedTier &&
            !MiningToolRules.CanMine(selectedTier, target.Resource))
        {
            _hud.SetResourcePrompt("Stärkeres Abbauwerkzeug erforderlich");
            return;
        }

        _hud.SetResourcePrompt(!IsWithinMiningRange(target)
            ? $"Zu weit entfernt: {target.DisplayName}"
            : IsMiningToolEquipped
                ? $"{InputBindingFormatter.FormatAction("use_mining_tool")} halten: {target.DisplayName}"
                : $"{InputBindingFormatter.FormatAction("activate_hand_slot")}: Hand-Slot und Abbauwerkzeug auswählen");
    }

    private bool IsWithinMiningRange(ResourceDepositView target) =>
        GlobalPosition.DistanceTo(target.GlobalPosition) <= MiningRange;
}

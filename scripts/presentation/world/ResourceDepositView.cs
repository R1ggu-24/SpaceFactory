using Godot;
using SpaceFactory.Application.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.World;

public partial class ResourceDepositView : Area2D
{
    public const uint ResourceCollisionLayer = 1u << 2;
#if DEBUG
    private static bool _headlessCollisionSmokeCompleted;
#endif
    private ResourceDepositDefinition _deposit = null!;
    private ResourceDefinition _resource = null!;
    private IResourceStateStore _stateStore = null!;
    private Color _color;
    private float _radius;
    private int _remainingAmount;
    private int _sectorX;
    private int _sectorY;
    private int _visibleDamageCrackCount;
    private int _completedHarvestCycles;
    private bool _interactionActive = true;
    private CollisionShape2D? _interactionCollision;
    private StaticBody2D? _physicalBody;
    private CollisionShape2D? _physicalCollision;

    public string DepositId => _deposit.Id;
    public string DisplayName => _deposit.IsFiniteOreStone
        ? $"Kleiner Erzstein: {_resource.DisplayName}"
        : _deposit.IsInfinite
            ? $"{_resource.DisplayName} ({MiningConfiguration.GetPurityDisplayName(_deposit.Purity)})"
            : _resource.DisplayName;
    public double MiningTimeSeconds => _deposit.EffectiveManualMiningTimeSeconds;
    public int YieldAmount => ResourceExtractionRules.GetManualYield(_deposit, _remainingAmount);
    public bool IsExhausted => !_deposit.IsInfinite && _remainingAmount <= 0;
    public bool IsInfinite => _deposit.IsInfinite;
    public bool IsFiniteOreStone => _deposit.IsFiniteOreStone;
    public int RemainingHits => _deposit.IsFiniteOreStone ? _remainingAmount : 0;
    public ResourcePurity Purity => _deposit.Purity;
    public double ExtractionUnitsPerMinute => _deposit.EffectiveExtractionUnitsPerMinute;
    public ResourceDepositDefinition Deposit => _deposit;
    public ResourceDefinition Resource => _resource;

    /// <summary>
    /// Hook for a later audio service, without coupling world objects to a
    /// concrete sound implementation or final audio assets.
    /// </summary>
    public event Action<string>? AudioCueRequested;

    public event Action<string>? Exhausted;

    public void Configure(
        ResourceDepositDefinition deposit,
        ResourceDefinition resource,
        float cometRadius,
        int sectorX,
        int sectorY,
        IResourceStateStore stateStore)
    {
        _deposit = deposit;
        _resource = resource;
        _stateStore = stateStore;
        _sectorX = sectorX;
        _sectorY = sectorY;
        _remainingAmount = stateStore.GetRemainingAmount(deposit);
        _color = ApplyPurityColor(Color.FromHtml(resource.BaseColorHex), deposit.Purity);
        _radius = Mathf.Max(8, (float)deposit.GetRadiusWorldUnits(cometRadius));
        Position = new Vector2(
            (float)deposit.NormalizedPosition.X * cometRadius,
            (float)deposit.NormalizedPosition.Y * cometRadius);
        CollisionLayer = ResourceCollisionLayer;
        CollisionMask = 0;
        Monitoring = false;
        Monitorable = true;
        InputPickable = true;
    }

    public override void _Ready()
    {
        if (IsExhausted)
        {
            QueueFree();
            return;
        }

        var collisionShape = new CircleShape2D { Radius = _radius };
        _interactionCollision = new CollisionShape2D
        {
            Name = "InteractionCollision",
            Shape = collisionShape,
            Disabled = !_interactionActive,
        };
        AddChild(_interactionCollision);

        _physicalBody = new StaticBody2D
        {
            Name = "PhysicalBody",
            CollisionLayer = _interactionActive ? ResourceCollisionLayer : 0,
            CollisionMask = 0,
        };
        _physicalCollision = new CollisionShape2D
        {
            Name = "PhysicalCollision",
            Shape = collisionShape,
            Disabled = !_interactionActive,
        };
        _physicalBody.AddChild(_physicalCollision);
        AddChild(_physicalBody);
#if DEBUG
        if (!_headlessCollisionSmokeCompleted &&
            (OS.HasFeature("headless") ||
             DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase)))
        {
            RunHeadlessCollisionSmokeTest();
        }
#endif
        QueueRedraw();
    }

    public void SetMiningProgress(float progress)
    {
        var clampedProgress = Mathf.Clamp(progress, 0, 1);
        var crackCount = GetDamageCrackCount(clampedProgress);
        if (crackCount == _visibleDamageCrackCount)
        {
            return;
        }

        _visibleDamageCrackCount = crackCount;
        QueueRedraw();
    }

    public void SetInteractionActive(bool active)
    {
        if (_interactionActive == active || IsExhausted)
        {
            return;
        }

        _interactionActive = active;
        ApplyInteractionState();
    }

    private void ApplyInteractionState()
    {
        CollisionLayer = _interactionActive ? ResourceCollisionLayer : 0;
        Monitorable = _interactionActive;
        InputPickable = _interactionActive;
        if (_interactionCollision is not null)
        {
            _interactionCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled, !_interactionActive);
        }

        if (_physicalBody is not null)
        {
            _physicalBody.CollisionLayer = _interactionActive ? ResourceCollisionLayer : 0;
        }

        if (_physicalCollision is not null)
        {
            _physicalCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled, !_interactionActive);
        }
    }

#if DEBUG
    private void RunHeadlessCollisionSmokeTest()
    {
        SetInteractionActive(true);
        RequireCollisionSmokeCondition(
            CollisionLayer == ResourceCollisionLayer &&
            _physicalBody?.CollisionLayer == ResourceCollisionLayer,
            "active deposits must expose matching interaction and physical collision layers");

        SetInteractionActive(false);
        RequireCollisionSmokeCondition(
            CollisionLayer == 0 &&
            _physicalBody?.CollisionLayer == 0 &&
            !Monitorable &&
            !InputPickable,
            "inactive deposits must disable interaction and physical collision together");

        SetInteractionActive(true);
        RequireCollisionSmokeCondition(
            CollisionLayer == ResourceCollisionLayer &&
            _physicalBody?.CollisionLayer == ResourceCollisionLayer &&
            Monitorable &&
            InputPickable,
            "reactivated deposits must restore both collision roles");

        _headlessCollisionSmokeCompleted = true;
        GD.Print("RESOURCE_PHYSICAL_COLLISION_SMOKE_OK: shared layer, active toggle, ship-blocking body");
    }

    private static void RequireCollisionSmokeCondition(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Resource collision smoke test failed: {message}.");
        }
    }
#endif

    public void RequestMiningAudioCue()
    {
        AudioCueRequested?.Invoke($"mining:{_resource.Id.Value}");
    }

    public void MarkExhausted()
    {
        if (_deposit.IsInfinite)
        {
            CompleteManualHarvest();
            return;
        }

        if (IsExhausted)
        {
            return;
        }

        _remainingAmount = 0;
        _interactionActive = false;
        ApplyInteractionState();
        _stateStore.SetRemainingAmount(_deposit, 0, _sectorX, _sectorY);
        FinishExhaustion();
    }

    private void FinishExhaustion()
    {
        Exhausted?.Invoke(_deposit.Id);
        AudioCueRequested?.Invoke($"mining_complete:{_resource.Id.Value}");
        var burst = new MiningParticleBurst(_color, _deposit.VisualSeed ^ 0x5041525449434C45UL)
        {
            GlobalPosition = GlobalPosition,
        };
        GetTree().CurrentScene.AddChild(burst);
        Visible = false;
        Monitoring = false;
        Monitorable = false;
        QueueFree();
    }

    public void CompleteManualHarvest()
    {
        if (IsExhausted)
        {
            return;
        }

        if (!_deposit.IsInfinite)
        {
            if (!_deposit.IsFiniteOreStone)
            {
                MarkExhausted();
                return;
            }

            _remainingAmount = ResourceExtractionRules.GetRemainingAmountAfterManualHarvest(
                _deposit,
                _remainingAmount);
            _stateStore.SetRemainingAmount(_deposit, _remainingAmount, _sectorX, _sectorY);
            _completedHarvestCycles++;
            _visibleDamageCrackCount = 0;
            if (_remainingAmount <= 0)
            {
                _interactionActive = false;
                ApplyInteractionState();
                FinishExhaustion();
                return;
            }

            EmitHarvestFeedback();
            QueueRedraw();
            return;
        }

        _remainingAmount = ResourceExtractionRules.GetRemainingAmountAfterManualHarvest(
            _deposit,
            _remainingAmount);
        _completedHarvestCycles++;
        _visibleDamageCrackCount = 0;
        EmitHarvestFeedback();
        QueueRedraw();
    }

    private void EmitHarvestFeedback()
    {
        AudioCueRequested?.Invoke($"mining_complete:{_resource.Id.Value}");
        var burst = new MiningParticleBurst(
            _color,
            _deposit.VisualSeed ^ (ulong)_completedHarvestCycles ^ 0x5041525449434C45UL)
        {
            GlobalPosition = GlobalPosition,
        };
        GetTree().CurrentScene.AddChild(burst);
    }

    public override void _Draw()
    {
        var random = new RandomNumberGenerator { Seed = _deposit.VisualSeed };
        var hostRock = CreatePatch(Vector2.Zero, _radius * 1.08f, random);
        DrawColoredPolygon(hostRock, new Color(0.06f, 0.055f, 0.05f, 0.88f));
        DrawPolyline([.. hostRock, hostRock[0]], new Color(0.015f, 0.014f, 0.013f, 0.92f),
            Mathf.Max(1, _radius * 0.055f), true);
        switch (_resource.VisualStyle)
        {
            case ResourceVisualStyle.Vein:
                DrawVeins(random);
                break;
            case ResourceVisualStyle.Crystal:
                DrawCrystals(random);
                break;
            case ResourceVisualStyle.MetallicInclusion:
                DrawMetallicInclusions(random);
                break;
            case ResourceVisualStyle.FrozenDeposit:
                DrawFrozenDeposit(random);
                break;
            case ResourceVisualStyle.MineralLayer:
                DrawMineralLayers(random);
                break;
        }

        DrawSurfaceHighlights(random);

        if (_visibleDamageCrackCount > 0)
        {
            var damageColor = new Color(0.04f, 0.035f, 0.03f, 0.9f);
            for (var index = 0; index < _visibleDamageCrackCount; index++)
            {
                var direction = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau));
                DrawLine(Vector2.Zero, direction * _radius * random.RandfRange(0.3f, 0.95f),
                    damageColor, Mathf.Max(1, _radius * 0.045f), true);
            }
        }
    }

    private void DrawSurfaceHighlights(RandomNumberGenerator random)
    {
        var highlightCount = _deposit.Purity switch
        {
            ResourcePurity.Impure => 2,
            ResourcePurity.Normal => 4,
            ResourcePurity.Pure => 6,
            _ => 4,
        };
        for (var index = 0; index < highlightCount; index++)
        {
            var center = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) *
                random.RandfRange(_radius * 0.18f, _radius * 0.72f);
            var radius = Mathf.Max(0.75f, random.RandfRange(_radius * 0.025f, _radius * 0.065f));
            DrawCircle(center + new Vector2(radius * 0.5f, radius * 0.7f), radius * 1.15f,
                new Color(0.01f, 0.012f, 0.014f, 0.48f));
            DrawCircle(center, radius,
                new Color(_color.Lightened(random.RandfRange(0.28f, 0.52f)), 0.78f));
        }
    }

    private static int GetDamageCrackCount(float progress) => progress <= 0
        ? 0
        : 2 + Mathf.RoundToInt(progress * 7);

    private static Color ApplyPurityColor(Color color, ResourcePurity purity) => purity switch
    {
        ResourcePurity.Impure => color.Darkened(0.24f).Lerp(new Color(0.30f, 0.29f, 0.27f), 0.20f),
        ResourcePurity.Normal => color,
        ResourcePurity.Pure => color.Lightened(0.18f),
        _ => color,
    };

    private void DrawVeins(RandomNumberGenerator random)
    {
        for (var vein = 0; vein < 4; vein++)
        {
            var points = new List<Vector2>();
            var position = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) * _radius * 0.75f;
            var direction = -position.Normalized();
            points.Add(position);
            for (var segment = 0; segment < 5; segment++)
            {
                direction = direction.Rotated(random.RandfRange(-0.5f, 0.5f));
                position += direction * _radius * random.RandfRange(0.18f, 0.32f);
                points.Add(position);
            }

            DrawPolyline([.. points], _color.Darkened(0.38f), Mathf.Max(2, _radius * 0.22f), true);
            DrawPolyline([.. points], _color.Lightened(0.18f), Mathf.Max(1, _radius * 0.09f), true);
        }
    }

    private void DrawCrystals(RandomNumberGenerator random)
    {
        var crystalCount = random.RandiRange(5, 10);
        for (var index = 0; index < crystalCount; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var center = Vector2.FromAngle(angle) * random.RandfRange(0, _radius * 0.55f);
            var length = random.RandfRange(_radius * 0.35f, _radius * 0.85f);
            var width = random.RandfRange(_radius * 0.12f, _radius * 0.25f);
            var direction = Vector2.FromAngle(angle);
            var side = direction.Orthogonal() * width;
            var polygon = new[]
            {
                center - side,
                center + side,
                center + (direction * length),
            };
            DrawColoredPolygon(polygon, _color.Lightened(random.RandfRange(0.05f, 0.32f)));
            DrawPolyline([.. polygon, polygon[0]], new Color(Colors.White, 0.36f), 1, true);
        }
    }

    private void DrawMetallicInclusions(RandomNumberGenerator random)
    {
        for (var index = 0; index < 9; index++)
        {
            var center = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) *
                random.RandfRange(0, _radius * 0.72f);
            var patch = CreatePatch(center, random.RandfRange(_radius * 0.12f, _radius * 0.34f), random);
            DrawColoredPolygon(patch, index % 3 == 0 ? _color.Lightened(0.35f) : _color.Darkened(0.12f));
            DrawPolyline([.. patch, patch[0]], _color.Lightened(0.45f), 1, true);
        }
    }

    private void DrawFrozenDeposit(RandomNumberGenerator random)
    {
        DrawColoredPolygon(CreatePatch(Vector2.Zero, _radius * 0.92f, random), new Color(_color, 0.8f));
        for (var index = 0; index < 7; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var direction = Vector2.FromAngle(angle);
            DrawLine(direction * _radius * 0.12f, direction * _radius * random.RandfRange(0.55f, 0.95f),
                _color.Lightened(0.46f), Mathf.Max(1, _radius * 0.055f), true);
        }
    }

    private void DrawMineralLayers(RandomNumberGenerator random)
    {
        for (var layer = -3; layer <= 3; layer++)
        {
            var y = layer * _radius * 0.22f;
            var points = new Vector2[7];
            for (var index = 0; index < points.Length; index++)
            {
                var x = Mathf.Lerp(-_radius * 0.85f, _radius * 0.85f, index / 6.0f);
                points[index] = new Vector2(x, y + random.RandfRange(-_radius * 0.09f, _radius * 0.09f));
            }

            DrawPolyline(points, layer % 2 == 0 ? _color.Lightened(0.22f) : _color.Darkened(0.25f),
                Mathf.Max(2, _radius * 0.13f), true);
        }
    }

    private static Vector2[] CreatePatch(Vector2 center, float radius, RandomNumberGenerator random)
    {
        var count = random.RandiRange(6, 10);
        var points = new Vector2[count];
        for (var index = 0; index < count; index++)
        {
            var angle = Mathf.Tau * index / count;
            points[index] = center + (Vector2.FromAngle(angle) * radius * random.RandfRange(0.65f, 1.18f));
        }

        return points;
    }
}

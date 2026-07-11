using Godot;
using SpaceFactory.Application.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.World;

public partial class ResourceDepositView : Area2D
{
    public const uint ResourceCollisionLayer = 1u << 2;
    private ResourceDepositDefinition _deposit = null!;
    private ResourceDefinition _resource = null!;
    private IResourceStateStore _stateStore = null!;
    private Color _color;
    private float _radius;
    private int _remainingAmount;
    private int _sectorX;
    private int _sectorY;
    private float _miningVisualProgress;

    public string DepositId => _deposit.Id;
    public string DisplayName => _resource.DisplayName;
    public double MiningTimeSeconds => _deposit.MiningTimeSeconds;
    public int YieldAmount => _remainingAmount;
    public bool IsExhausted => _remainingAmount <= 0;
    public ResourceDefinition Resource => _resource;

    /// <summary>
    /// Hook for a later audio service, without coupling world objects to a
    /// concrete sound implementation or final audio assets.
    /// </summary>
    public event Action<string>? AudioCueRequested;

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
        _color = Color.FromHtml(resource.BaseColorHex);
        _radius = Mathf.Max(8, (float)deposit.RadiusFactor * cometRadius);
        Position = new Vector2(
            (float)deposit.NormalizedPosition.X * cometRadius,
            (float)deposit.NormalizedPosition.Y * cometRadius);
        CollisionLayer = ResourceCollisionLayer;
        CollisionMask = 0;
        InputPickable = true;
    }

    public override void _Ready()
    {
        if (IsExhausted)
        {
            QueueFree();
            return;
        }

        var collision = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = _radius },
        };
        AddChild(collision);
        QueueRedraw();
    }

    public void SetMiningProgress(float progress)
    {
        _miningVisualProgress = Mathf.Clamp(progress, 0, 1);
        QueueRedraw();
    }

    public void RequestMiningAudioCue()
    {
        AudioCueRequested?.Invoke($"mining:{_resource.Id.Value}");
    }

    public void MarkExhausted()
    {
        if (IsExhausted)
        {
            return;
        }

        _remainingAmount = 0;
        _stateStore.SetRemainingAmount(_deposit, 0, _sectorX, _sectorY);
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

    public override void _Draw()
    {
        var random = new RandomNumberGenerator { Seed = _deposit.VisualSeed };
        DrawCircle(Vector2.Zero, _radius * 1.08f, new Color(0.06f, 0.055f, 0.05f, 0.82f));
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

        if (_miningVisualProgress > 0)
        {
            var damageColor = new Color(0.04f, 0.035f, 0.03f, 0.9f);
            var crackCount = 2 + Mathf.RoundToInt(_miningVisualProgress * 7);
            for (var index = 0; index < crackCount; index++)
            {
                var direction = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau));
                DrawLine(Vector2.Zero, direction * _radius * random.RandfRange(0.3f, 0.95f),
                    damageColor, Mathf.Max(1, _radius * 0.045f), true);
            }
        }
    }

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

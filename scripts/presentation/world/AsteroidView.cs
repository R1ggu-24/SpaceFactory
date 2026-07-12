using Godot;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.Settings;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.World;

public partial class AsteroidView : StaticBody2D
{
    private static readonly Vector2 LightDirection = new Vector2(-0.68f, -0.74f).Normalized();
    private AsteroidDefinition _definition = null!;
    private Vector2[] _outline = [];
    private Vector2[] _simplifiedOutline = [];
    private Color _baseColor;
    private CollisionPolygon2D? _collision;
    private bool _useDetailedCollision = true;

    public void Configure(AsteroidDefinition definition)
    {
        _definition = definition;
        Rotation = (float)definition.RotationRadians;
        _baseColor = CreateBaseColor(definition);
        _outline = CreateOutline(definition);
        _simplifiedOutline = SimplifyPolygon(_outline, 10);
    }

    public override void _Ready()
    {
        AddToGroup("quality_sensitive_visuals");
        _collision = new CollisionPolygon2D
        {
            Polygon = _useDetailedCollision ? _outline : _simplifiedOutline,
        };
        AddChild(_collision);
        QueueRedraw();
    }

    public void SetDetailedCollision(bool detailed)
    {
        if (_useDetailedCollision == detailed)
        {
            return;
        }

        _useDetailedCollision = detailed;
        if (_collision is not null)
        {
            _collision.Polygon = detailed ? _outline : _simplifiedOutline;
        }
    }

    public override void _Draw()
    {
        if (_definition is null || _outline.Length == 0)
        {
            return;
        }

        DrawColoredPolygon(_outline, _baseColor.Darkened(0.42f));
        var litBody = OffsetPolygon(
            ScalePolygon(_outline, 0.94f),
            LightDirection * (float)_definition.Radius * 0.025f);
        DrawColoredPolygon(litBody, _baseColor);

        if (GraphicsQualityRuntime.ShadowQuality != QualityLevel.Low)
        {
            DrawMacroFacets();
        }

        if ((int)GraphicsQualityRuntime.TextureQuality >= (int)QualityLevel.Medium)
        {
            DrawRockStrata();
        }

        if ((int)GraphicsQualityRuntime.TextureQuality >= (int)QualityLevel.High)
        {
            DrawSurfaceGrain();
            DrawMicroCraters();
        }

        DrawCraters();
        if ((int)GraphicsQualityRuntime.EffectQuality >= (int)QualityLevel.Medium)
        {
            DrawCracks();
        }

        if ((int)GraphicsQualityRuntime.EffectQuality >= (int)QualityLevel.High)
        {
            DrawRidges();
        }

        DrawLandingPlateaus();
        DrawEdgeLighting();
    }

    private void DrawMacroFacets()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x464143455453UL };
        var facetCount = 10 + Mathf.RoundToInt((float)_definition.SurfaceRoughness * 18);
        for (var index = 0; index < facetCount; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var distance = Mathf.Sqrt(random.Randf()) * (float)_definition.Radius * 0.62f;
            var center = Vector2.FromAngle(angle) * distance;
            var facetRadius = random.RandfRange(0.055f, 0.17f) * (float)_definition.Radius;
            var illumination = center.LengthSquared() > 0
                ? center.Normalized().Dot(LightDirection)
                : 0;
            var randomTone = random.RandfRange(-0.08f, 0.08f);
            var tone = (illumination * 0.12f) + randomTone;
            var color = tone >= 0
                ? _baseColor.Lightened(tone)
                : _baseColor.Darkened(-tone);
            var patch = CreateRockPatch(center, facetRadius, random, 6, 11);
            DrawColoredPolygon(patch, color);

            var ridgeColor = illumination > 0
                ? color.Lightened(0.13f)
                : color.Darkened(0.17f);
            DrawPolyline([.. patch, patch[0]], ridgeColor,
                Mathf.Max(0.9f, (float)_definition.Radius * 0.0025f), true);
        }
    }

    private void DrawRockStrata()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x535452415441UL };
        var layerCount = 3 + Mathf.RoundToInt((float)_definition.ElevationVariation * 5);
        for (var layer = 0; layer < layerCount; layer++)
        {
            var points = new List<Vector2>();
            var tangent = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau));
            var normal = tangent.Orthogonal();
            var center = normal * random.RandfRange(-0.42f, 0.42f) * (float)_definition.Radius;
            var length = random.RandfRange(0.22f, 0.58f) * (float)_definition.Radius;
            var segments = random.RandiRange(4, 8);
            for (var index = 0; index <= segments; index++)
            {
                var progress = index / (float)segments;
                var offset = tangent * Mathf.Lerp(-length, length, progress);
                offset += normal * Mathf.Sin(progress * Mathf.Pi * random.RandfRange(1.0f, 2.5f)) *
                    random.RandfRange(0.008f, 0.035f) * (float)_definition.Radius;
                if ((center + offset).Length() <= _definition.Radius * 0.69)
                {
                    points.Add(center + offset);
                }
            }

            if (points.Count >= 2)
            {
                var lightLayer = random.Randf() > 0.48f;
                var color = lightLayer ? _baseColor.Lightened(0.16f) : _baseColor.Darkened(0.22f);
                DrawPolyline([.. points], new Color(color, random.RandfRange(0.28f, 0.52f)),
                    Mathf.Max(1.0f, (float)_definition.Radius * random.RandfRange(0.002f, 0.005f)), true);
            }
        }
    }

    private void DrawSurfaceGrain()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x475241494EUL };
        var grainCount = 28 + Mathf.RoundToInt((float)_definition.SurfaceRoughness * 48);
        for (var index = 0; index < grainCount; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var distance = Mathf.Sqrt(random.Randf()) * (float)_definition.Radius * 0.67f;
            var center = Vector2.FromAngle(angle) * distance;
            var grainRadius = random.RandfRange(0.006f, 0.026f) * (float)_definition.Radius;
            var lightFacing = center.LengthSquared() > 0 && center.Normalized().Dot(LightDirection) > 0;
            var color = lightFacing
                ? _baseColor.Lightened(random.RandfRange(0.08f, 0.24f))
                : _baseColor.Darkened(random.RandfRange(0.12f, 0.30f));
            var grain = CreateRockPatch(center, grainRadius, random, 4, 7);
            DrawColoredPolygon(grain, new Color(color, random.RandfRange(0.45f, 0.82f)));
        }
    }

    private void DrawMicroCraters()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x504954535F5631UL };
        var pitCount = 5 + Mathf.RoundToInt((float)_definition.SurfaceRoughness * 10);
        for (var index = 0; index < pitCount; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var distance = Mathf.Sqrt(random.Randf()) * (float)_definition.Radius * 0.67f;
            var center = Vector2.FromAngle(angle) * distance;
            var radius = Mathf.Max(0.8f,
                random.RandfRange(0.008f, 0.028f) * (float)_definition.Radius);
            var depth = random.RandfRange(0.14f, 0.34f);

            DrawSetTransform(center, random.RandfRange(0, Mathf.Tau),
                new Vector2(random.RandfRange(0.78f, 1.25f), random.RandfRange(0.58f, 0.9f)));
            DrawCircle(Vector2.Zero, radius * 1.14f, _baseColor.Lightened(depth * 0.42f));
            DrawCircle(-LightDirection * radius * 0.12f, radius, _baseColor.Darkened(depth));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawCraters()
    {
        foreach (var crater in _definition.Craters)
        {
            var position = new Vector2(
                (float)(crater.XFactor * _definition.Radius),
                (float)(crater.YFactor * _definition.Radius));
            var radius = (float)(crater.RadiusFactor * _definition.Radius);
            var depth = (float)crater.Depth;
            var ellipseScale = new Vector2(1.05f + (depth * 0.28f), 0.72f + (depth * 0.18f));

            DrawSetTransform(position, (float)crater.RotationRadians, ellipseScale);
            DrawCircle(Vector2.Zero, radius * 1.16f, _baseColor.Lightened(0.13f));
            DrawCircle(-LightDirection * radius * 0.09f, radius,
                _baseColor.Darkened(0.24f + (depth * 0.24f)));
            DrawCircle(-LightDirection * radius * 0.13f, radius * 0.62f,
                _baseColor.Darkened(0.42f + (depth * 0.18f)));
            DrawArc(Vector2.Zero, radius * 1.12f, Mathf.Pi * 0.85f, Mathf.Pi * 1.85f, 18,
                _baseColor.Lightened(0.24f), Mathf.Max(1.1f, radius * 0.075f), true);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawCracks()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x435241434B53UL };
        var crackCount = 3 + Mathf.RoundToInt((float)_definition.SurfaceRoughness * 9);
        for (var crackIndex = 0; crackIndex < crackCount; crackIndex++)
        {
            var points = new List<Vector2>();
            var position = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) *
                random.RandfRange(0.06f, 0.38f) * (float)_definition.Radius;
            var direction = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau));
            var segments = random.RandiRange(3, 8);
            points.Add(position);

            for (var segment = 0; segment < segments; segment++)
            {
                direction = direction.Rotated(random.RandfRange(-0.52f, 0.52f)).Normalized();
                position += direction * random.RandfRange(0.03f, 0.075f) * (float)_definition.Radius;
                if (position.Length() > _definition.Radius * 0.70)
                {
                    break;
                }

                points.Add(position);
            }

            if (points.Count >= 2)
            {
                var width = Mathf.Max(1.0f, (float)_definition.Radius * 0.004f);
                DrawPolyline([.. points], _baseColor.Lightened(0.06f), width * 1.7f, true);
                DrawPolyline([.. points], _baseColor.Darkened(0.52f), width, true);
            }
        }
    }

    private void DrawRidges()
    {
        var random = new RandomNumberGenerator { Seed = _definition.VisualSeed ^ 0x524944474553UL };
        var ridgeCount = 4 + Mathf.RoundToInt((float)_definition.ElevationVariation * 8);
        for (var index = 0; index < ridgeCount; index++)
        {
            var center = Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) *
                random.RandfRange(0.16f, 0.62f) * (float)_definition.Radius;
            var ridgeRadius = random.RandfRange(0.025f, 0.085f) * (float)_definition.Radius;
            var startAngle = random.RandfRange(0, Mathf.Tau);
            var width = Mathf.Max(1.0f, ridgeRadius * 0.11f);
            DrawArc(center - (LightDirection * width), ridgeRadius, startAngle,
                startAngle + random.RandfRange(1.1f, 2.7f), 10,
                _baseColor.Darkened(0.28f), width * 1.5f, true);
            DrawArc(center, ridgeRadius, startAngle,
                startAngle + random.RandfRange(1.1f, 2.7f), 10,
                _baseColor.Lightened(0.19f), width, true);
        }
    }

    private void DrawLandingPlateaus()
    {
        var profile = _definition.SurfaceProfile;
        if (profile is null)
        {
            return;
        }

        var random = new RandomNumberGenerator { Seed = profile.TerrainSeed ^ 0x504C4154454155UL };
        for (var index = 0; index < profile.SuggestedLandingZoneCount; index++)
        {
            var angle = random.RandfRange(0, Mathf.Tau);
            var distance = random.RandfRange(0.15f, 0.46f) * (float)profile.BuildableRadius;
            var center = Vector2.FromAngle(angle) * distance;
            var radius = random.RandfRange(0.075f, 0.13f) * (float)profile.BuildableRadius;
            DrawSetTransform(center, random.RandfRange(0, Mathf.Tau), new Vector2(1.45f, 0.72f));
            DrawCircle(Vector2.Zero, radius, _baseColor.Lightened(0.055f));
            DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 18,
                _baseColor.Darkened(0.10f), Mathf.Max(1.0f, radius * 0.045f), true);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawEdgeLighting()
    {
        var darkEdge = _baseColor.Darkened(0.56f);
        DrawPolyline([.. _outline, _outline[0]], darkEdge,
            Mathf.Max(2.0f, (float)_definition.Radius * 0.006f), true);

        for (var index = 0; index < _outline.Length; index++)
        {
            var nextIndex = (index + 1) % _outline.Length;
            var middle = (_outline[index] + _outline[nextIndex]) * 0.5f;
            if (middle.LengthSquared() > 0 && middle.Normalized().Dot(LightDirection) > 0.18f)
            {
                DrawLine(_outline[index], _outline[nextIndex], _baseColor.Lightened(0.28f),
                    Mathf.Max(1.3f, (float)_definition.Radius * 0.004f), true);
            }
        }
    }

    private static Color CreateBaseColor(AsteroidDefinition definition)
    {
        var paletteColor = definition.ResourceType.Value == "iron_ore"
            ? new Color(0.19f, 0.185f, 0.175f)
            : new Color(0.23f, 0.19f, 0.155f);
        var random = new RandomNumberGenerator { Seed = definition.VisualSeed ^ 0x434F4C4F52UL };
        var grayMix = new Color(0.17f, 0.17f, 0.165f);
        paletteColor = paletteColor.Lerp(grayMix, random.RandfRange(0.12f, 0.55f));
        return random.Randf() > 0.5f
            ? paletteColor.Lightened(random.RandfRange(0.01f, 0.10f))
            : paletteColor.Darkened(random.RandfRange(0.01f, 0.09f));
    }

    private static Vector2[] CreateOutline(AsteroidDefinition definition)
    {
        var random = new RandomNumberGenerator { Seed = definition.VisualSeed };
        var pointCount = random.RandiRange(26, 42);
        var outline = new Vector2[pointCount];
        var firstWave = random.RandfRange(2.0f, 4.5f);
        var secondWave = random.RandfRange(5.0f, 9.0f);
        var thirdWave = random.RandfRange(10.0f, 15.0f);
        var firstPhase = random.RandfRange(0, Mathf.Tau);
        var secondPhase = random.RandfRange(0, Mathf.Tau);
        var thirdPhase = random.RandfRange(0, Mathf.Tau);
        var minimumAspect = definition.SupportsLanding ? 0.72f : 0.56f;
        var minorAspect = random.RandfRange(minimumAspect, 0.96f);
        var stretchAlongX = random.Randf() > 0.5f;
        var axisScale = stretchAlongX ? new Vector2(1, minorAspect) : new Vector2(minorAspect, 1);

        for (var index = 0; index < pointCount; index++)
        {
            var angle = Mathf.Tau * index / pointCount;
            var wave = Mathf.Sin((angle * firstWave) + firstPhase) * 0.07f +
                Mathf.Sin((angle * secondWave) + secondPhase) * 0.038f +
                Mathf.Sin((angle * thirdWave) + thirdPhase) * 0.018f;
            var noise = random.RandfRange(-0.028f, 0.028f) *
                (0.72f + (float)definition.SurfaceRoughness);
            var radius = (float)definition.Radius * (0.87f + wave + noise);
            outline[index] = Vector2.FromAngle(angle) * radius * axisScale;
        }

        return outline;
    }

    private static Vector2[] CreateRockPatch(
        Vector2 center,
        float radius,
        RandomNumberGenerator random,
        int minimumPoints,
        int maximumPoints)
    {
        var pointCount = random.RandiRange(minimumPoints, maximumPoints);
        var points = new Vector2[pointCount];
        var rotation = random.RandfRange(0, Mathf.Tau);
        var aspect = random.RandfRange(0.58f, 1.0f);
        for (var index = 0; index < pointCount; index++)
        {
            var angle = rotation + (Mathf.Tau * index / pointCount);
            var local = Vector2.FromAngle(angle) * radius * random.RandfRange(0.62f, 1.18f);
            local.Y *= aspect;
            points[index] = center + local;
        }

        return points;
    }

    private static Vector2[] SimplifyPolygon(IReadOnlyList<Vector2> polygon, int targetPointCount)
    {
        var simplified = new Vector2[targetPointCount];
        var step = polygon.Count / (double)targetPointCount;
        for (var index = 0; index < targetPointCount; index++)
        {
            simplified[index] = polygon[(int)Math.Floor(index * step)];
        }

        return simplified;
    }

    private static Vector2[] ScalePolygon(IReadOnlyList<Vector2> polygon, float factor)
    {
        var scaled = new Vector2[polygon.Count];
        for (var index = 0; index < polygon.Count; index++)
        {
            scaled[index] = polygon[index] * factor;
        }

        return scaled;
    }

    private static Vector2[] OffsetPolygon(IReadOnlyList<Vector2> polygon, Vector2 offset)
    {
        var shifted = new Vector2[polygon.Count];
        for (var index = 0; index < polygon.Count; index++)
        {
            shifted[index] = polygon[index] + offset;
        }

        return shifted;
    }
}

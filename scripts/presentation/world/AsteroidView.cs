using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.Settings;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.World;

public partial class AsteroidView : StaticBody2D
{
    private static readonly Vector2 LightDirection = new Vector2(-0.68f, -0.74f).Normalized();
#if DEBUG
    private static bool _buildGeometrySmokeCompleted;
#endif
    private AsteroidDefinition _definition = null!;
    private Vector2[] _outline = [];
    private IReadOnlyList<Vector2> _readOnlyOutline = Array.Empty<Vector2>();
    private Vector2[] _simplifiedOutline = [];
    private Color _baseColor;
    private CollisionPolygon2D? _collision;
    private bool _useDetailedCollision = true;

    public string CometId => _definition.Id;

    public double Radius => _definition.Radius;

    public AsteroidSize Size => _definition.Size;

    public double BuildableRadius => _definition.SurfaceProfile?.BuildableRadius ?? 0;

    public IReadOnlyList<Vector2> LocalOutline => _readOnlyOutline;

    public IReadOnlyList<AsteroidCrater> Craters => _definition.Craters;

    public bool SupportsBuilding =>
        _definition.Size is AsteroidSize.Large or AsteroidSize.Huge &&
        _definition.SurfaceProfile is not null;

    public bool SupportsShipDocking =>
        _definition.Size is AsteroidSize.Large or AsteroidSize.Huge;

    public void Configure(AsteroidDefinition definition)
    {
        _definition = definition;
        Rotation = (float)definition.RotationRadians;
        _baseColor = CreateBaseColor(definition);
        _outline = CreateOutline(definition);
        _readOnlyOutline = Array.AsReadOnly(_outline);
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
#if DEBUG
        if (!_buildGeometrySmokeCompleted && IsHeadlessRuntime())
        {
            RunBuildGeometrySmokeTest();
            SpaceFactory.Presentation.Building.MachinePlacementPreview.RunGeometrySmokeTest();
        }
#endif
        QueueRedraw();
    }

    public bool ContainsWorldPoint(Vector2 worldPosition) => ContainsLocalPoint(ToLocal(worldPosition));

    public bool ContainsLocalPoint(Vector2 localPosition) =>
        _outline.Length >= 3 && IsPointInsidePolygon(localPosition, _outline);

    public AsteroidBuildSurfaceFailure EvaluateBuildFootprint(IReadOnlyList<Vector2> localFootprint)
    {
        ArgumentNullException.ThrowIfNull(localFootprint);
        if (localFootprint.Count < 3)
        {
            throw new ArgumentException("A machine footprint needs at least three points.", nameof(localFootprint));
        }

        if (_definition.Size is not (AsteroidSize.Large or AsteroidSize.Huge))
        {
            return AsteroidBuildSurfaceFailure.UnsupportedCometSize;
        }

        if (_definition.SurfaceProfile is null)
        {
            return AsteroidBuildSurfaceFailure.MissingSurfaceProfile;
        }

        var samples = SampleFootprint(localFootprint);
        var buildableRadiusSquared = (float)(_definition.SurfaceProfile.BuildableRadius *
                                              _definition.SurfaceProfile.BuildableRadius);
        if (samples.Any(sample => sample.LengthSquared() > buildableRadiusSquared))
        {
            return AsteroidBuildSurfaceFailure.OutsideBuildableRadius;
        }

        if (samples.Any(sample => !ContainsLocalPoint(sample)))
        {
            return AsteroidBuildSurfaceFailure.OutsideOutline;
        }

        if (_definition.Craters.Any(crater => CraterIntersectsFootprint(crater, localFootprint)))
        {
            return AsteroidBuildSurfaceFailure.Crater;
        }

        return IsTerrainUneven(samples)
            ? AsteroidBuildSurfaceFailure.UnevenTerrain
            : AsteroidBuildSurfaceFailure.None;
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

    public bool TryGetClosestSurfacePoint(
        Vector2 worldPosition,
        out Vector2 surfacePoint,
        out Vector2 outwardNormal,
        out float distance)
    {
        surfacePoint = default;
        outwardNormal = Vector2.Up;
        distance = float.PositiveInfinity;
        if (_outline.Length < 2)
        {
            return false;
        }

        var localPosition = ToLocal(worldPosition);
        var closestLocal = Vector2.Zero;
        var closestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < _outline.Length; index++)
        {
            var start = _outline[index];
            var end = _outline[(index + 1) % _outline.Length];
            var closest = ClosestPointOnSegment(localPosition, start, end);
            var distanceSquared = localPosition.DistanceSquaredTo(closest);
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestLocal = closest;
        }

        surfacePoint = ToGlobal(closestLocal);
        var localNormal = closestLocal.LengthSquared() > 0.001f
            ? closestLocal.Normalized()
            : localPosition.Normalized();
        outwardNormal = (ToGlobal(closestLocal + localNormal) - surfacePoint).Normalized();
        distance = worldPosition.DistanceTo(surfacePoint);
        return outwardNormal.LengthSquared() > 0.5f;
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

    private IReadOnlyList<Vector2> SampleFootprint(IReadOnlyList<Vector2> footprint)
    {
        var samples = new List<Vector2>(footprint.Count * 5 + 1);
        var center = Vector2.Zero;
        for (var index = 0; index < footprint.Count; index++)
        {
            var start = footprint[index];
            var end = footprint[(index + 1) % footprint.Count];
            center += start;
            var segmentCount = Math.Max(1, Mathf.CeilToInt(start.DistanceTo(end) / 12f));
            for (var segment = 0; segment < segmentCount; segment++)
            {
                samples.Add(start.Lerp(end, segment / (float)segmentCount));
            }
        }

        samples.Add(center / footprint.Count);
        return samples;
    }

    private bool CraterIntersectsFootprint(
        AsteroidCrater crater,
        IReadOnlyList<Vector2> footprint)
    {
        var center = new Vector2(
            (float)(crater.XFactor * _definition.Radius),
            (float)(crater.YFactor * _definition.Radius));
        var unsafeRadius = (float)(crater.RadiusFactor * _definition.Radius) *
                           (1.10f + ((float)crater.Depth * 0.20f));

        if (IsPointInsidePolygon(center, footprint))
        {
            return true;
        }

        var unsafeRadiusSquared = unsafeRadius * unsafeRadius;
        for (var index = 0; index < footprint.Count; index++)
        {
            var start = footprint[index];
            var end = footprint[(index + 1) % footprint.Count];
            if (ClosestPointOnSegment(center, start, end).DistanceSquaredTo(center) <= unsafeRadiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTerrainUneven(IReadOnlyList<Vector2> samples)
    {
        var minimumHeight = float.PositiveInfinity;
        var maximumHeight = float.NegativeInfinity;
        foreach (var sample in samples)
        {
            var height = GetTerrainHeight(sample);
            minimumHeight = Math.Min(minimumHeight, height);
            maximumHeight = Math.Max(maximumHeight, height);
        }

        return maximumHeight - minimumHeight > 0.24f;
    }

    private float GetTerrainHeight(Vector2 localPosition)
    {
        var radius = Mathf.Max(1, (float)_definition.Radius);
        var normalized = localPosition / radius;
        var seed = _definition.SurfaceProfile?.TerrainSeed ?? _definition.VisualSeed;
        var phaseA = ((seed & 0xffffUL) / 65_535f) * Mathf.Tau;
        var phaseB = (((seed >> 16) & 0xffffUL) / 65_535f) * Mathf.Tau;
        var roughnessAmplitude = 0.055f + ((float)_definition.SurfaceRoughness * 0.11f);
        var elevationAmplitude = 0.045f + ((float)_definition.ElevationVariation * 0.105f);
        return Mathf.Sin((normalized.X * 5.2f) + phaseA) * roughnessAmplitude +
               Mathf.Cos((normalized.Y * 4.4f) + phaseB) * elevationAmplitude +
               Mathf.Sin(((normalized.X + normalized.Y) * 7.1f) + phaseA - phaseB) * 0.035f;
    }

    private static bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var previous = polygon[(index + polygon.Count - 1) % polygon.Count];
            if (ClosestPointOnSegment(point, previous, current).DistanceSquaredTo(point) <= 0.0001f)
            {
                return true;
            }

            var crossesHorizontalRay = (current.Y > point.Y) != (previous.Y > point.Y);
            if (!crossesHorizontalRay)
            {
                continue;
            }

            var intersectionX = ((previous.X - current.X) * (point.Y - current.Y) /
                                 (previous.Y - current.Y)) + current.X;
            if (point.X < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

#if DEBUG
    public void RunBuildGeometrySmokeTest()
    {
        var testSquare = new[]
        {
            new Vector2(-10, -10),
            new Vector2(10, -10),
            new Vector2(10, 10),
            new Vector2(-10, 10),
        };
        RequireBuildGeometryCondition(IsPointInsidePolygon(Vector2.Zero, testSquare),
            "polygon center must be contained");
        RequireBuildGeometryCondition(!IsPointInsidePolygon(new Vector2(30, 0), testSquare),
            "point beyond polygon must be rejected");
        RequireBuildGeometryCondition(ContainsLocalPoint(Vector2.Zero),
            "generated comet outline must contain its center");

        var buildableSmokeComet = CreateBuildSmokeComet(AsteroidSize.Large, includeSurface: true, includeCrater: false);
        var craterSmokeComet = CreateBuildSmokeComet(AsteroidSize.Huge, includeSurface: true, includeCrater: true);
        var smallSmokeComet = CreateBuildSmokeComet(AsteroidSize.Medium, includeSurface: false, includeCrater: false);
        var machineFootprint = new[]
        {
            new Vector2(-48, -36),
            new Vector2(48, -36),
            new Vector2(48, 36),
            new Vector2(-48, 36),
        };
        RequireBuildGeometryCondition(
            buildableSmokeComet.EvaluateBuildFootprint(machineFootprint) == AsteroidBuildSurfaceFailure.None,
            "flat large-comet center must accept a complete machine footprint");
        RequireBuildGeometryCondition(
            craterSmokeComet.EvaluateBuildFootprint(machineFootprint) == AsteroidBuildSurfaceFailure.Crater,
            "a crater crossing the footprint must reject construction");
        RequireBuildGeometryCondition(
            smallSmokeComet.EvaluateBuildFootprint(machineFootprint) == AsteroidBuildSurfaceFailure.UnsupportedCometSize,
            "medium comets must reject construction by actual size");
        buildableSmokeComet.Free();
        craterSmokeComet.Free();
        smallSmokeComet.Free();

        var tinyFootprint = new[]
        {
            new Vector2(-6, -6),
            new Vector2(6, -6),
            new Vector2(6, 6),
            new Vector2(-6, 6),
        };
        var result = EvaluateBuildFootprint(tinyFootprint);
        if (SupportsBuilding)
        {
            RequireBuildGeometryCondition(result is not (
                    AsteroidBuildSurfaceFailure.UnsupportedCometSize or
                    AsteroidBuildSurfaceFailure.MissingSurfaceProfile),
                "landable large comets must reach geometric surface validation");
        }
        else
        {
            RequireBuildGeometryCondition(result is
                    AsteroidBuildSurfaceFailure.UnsupportedCometSize or
                    AsteroidBuildSurfaceFailure.MissingSurfaceProfile,
                "unsupported comets must reject construction before geometry checks");
        }

        _buildGeometrySmokeCompleted = true;
        GD.Print("ASTEROID_BUILD_GEOMETRY_SMOKE_OK: size/profile, outline, radius, craters, terrain");
    }

    private static AsteroidView CreateBuildSmokeComet(
        AsteroidSize size,
        bool includeSurface,
        bool includeCrater)
    {
        const double radius = 1_000;
        IReadOnlyList<AsteroidCrater> craters = includeCrater
            ? [new AsteroidCrater(0, 0, 0.08, 0.5, 0)]
            : [];
        var profile = includeSurface
            ? new AsteroidSurfaceProfile("smoke:surface", 820, 650, 2, 112_358, 132_134, "smoke")
            : null;
        var view = new AsteroidView();
        view.Configure(new AsteroidDefinition(
            "smoke:build",
            new WorldPosition(0, 0),
            radius,
            size,
            "smoke",
            new ItemId("iron_ore"),
            314_159,
            0,
            0.18,
            0.12,
            craters,
            profile));
        return view;
    }

    private static void RequireBuildGeometryCondition(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Asteroid build geometry smoke test failed: {message}.");
        }
    }

    private static bool IsHeadlessRuntime() =>
        OS.HasFeature("headless") ||
        DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase);
#endif

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

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return start;
        }

        var factor = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0, 1);
        return start + (segment * factor);
    }
}

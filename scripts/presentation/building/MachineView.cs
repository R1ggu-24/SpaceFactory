using Godot;
using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

public partial class MachineView : StaticBody2D
{
    public const uint MachineCollisionLayer = 1u << 3;

    private static readonly Color Titanium = new(0.22f, 0.25f, 0.27f);
    private static readonly Color DarkMetal = new(0.055f, 0.07f, 0.078f);
    private static readonly Color Recess = new(0.012f, 0.021f, 0.026f);
    private static readonly Color Warning = new(0.92f, 0.68f, 0.18f);

    private MachineState? _state;
    private MachinePresentationDefinition? _presentation;
    private AsteroidView? _hostComet;
    private CollisionShape2D? _collisionShape;
    private Label? _statusLabel;
    private float _visualConstructionProgress;
    private bool _constructionVisualRunning;
    private float _animationSeconds;
    private float _dismantlingProgress;

    public MachineInstanceId InstanceId =>
        _state?.InstanceId ?? throw new InvalidOperationException("Machine view is not configured.");

    public MachineDefinitionId DefinitionId =>
        _state?.Definition.Id ?? throw new InvalidOperationException("Machine view is not configured.");

    public AsteroidView HostComet =>
        _hostComet ?? throw new InvalidOperationException("Machine view is not anchored to a comet.");

    public Vector2 Footprint => _presentation?.Footprint ?? Vector2.Zero;

    public float InteractionRadius => _presentation?.InteractionRadius ?? 0;

    public bool IsConstructionVisualComplete => _visualConstructionProgress >= 0.999f;

    public event Action<MachineView>? InteractionRequested;

    public void Configure(
        MachineState state,
        AsteroidView hostComet,
        MachinePresentationCatalog? presentationCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(hostComet);
        var placement = state.Placement ??
                        throw new ArgumentException("A world machine needs a persisted placement.", nameof(state));
        if (!string.Equals(placement.CometId, hostComet.CometId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Machine placement and host comet do not match.", nameof(hostComet));
        }

        _state = state;
        _hostComet = hostComet;
        _presentation = (presentationCatalog ?? MachinePresentationCatalog.Instance).Get(state.Definition.Id);
        _visualConstructionProgress = (float)state.ConstructionProgress;
        _constructionVisualRunning = !state.IsConstructionComplete;
        Name = $"Machine_{state.InstanceId.Value.Replace(':', '_')}";
        CollisionLayer = MachineCollisionLayer;
        CollisionMask = 0;
        InputPickable = true;
        ZIndex = 4;

        if (GetParent() is null)
        {
            hostComet.AddChild(this);
        }
        else if (GetParent() != hostComet)
        {
            Reparent(hostComet, keepGlobalTransform: false);
        }

        Position = new Vector2(
            (float)placement.RelativePositionX,
            (float)placement.RelativePositionY);
        Rotation = (float)placement.RelativeRotationRadians;
        EnsurePresentationNodes();
        Refresh(state);
    }

    public override void _Ready()
    {
        EnsurePresentationNodes();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_state is null)
        {
            return;
        }

        var redraw = false;
        if (_state.IsConstructionComplete && _state.Status == MachineOperationStatus.Producing)
        {
            _animationSeconds = Mathf.PosMod(_animationSeconds + (float)delta, 120f);
            redraw = true;
        }

        if (_constructionVisualRunning)
        {
            var duration = Math.Max(0.001, _state.Definition.ConstructionDurationSeconds);
            var previous = _visualConstructionProgress;
            _visualConstructionProgress = Mathf.Min(
                1,
                _visualConstructionProgress + ((float)delta / (float)duration));
            if (_visualConstructionProgress >= 0.999f || _state.IsConstructionComplete)
            {
                _visualConstructionProgress = 1;
                _constructionVisualRunning = false;
            }

            if (!Mathf.IsEqualApprox(previous, _visualConstructionProgress))
            {
                RefreshStatusLabel();
                redraw = true;
            }
        }

        if (redraw)
        {
            QueueRedraw();
        }
    }

    public void Refresh(MachineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_state is not null && state.InstanceId != _state.InstanceId)
        {
            throw new ArgumentException("A machine view cannot switch to a different instance.", nameof(state));
        }

        if (_presentation is not null && state.Definition.Id != _presentation.DefinitionId)
        {
            throw new ArgumentException("Machine state and presentation do not match.", nameof(state));
        }

        _state = state;
        _visualConstructionProgress = Math.Max(
            _visualConstructionProgress,
            (float)state.ConstructionProgress);
        if (state.IsConstructionComplete)
        {
            _visualConstructionProgress = 1;
            _constructionVisualRunning = false;
        }

        RefreshStatusLabel();
        QueueRedraw();
    }

    public bool IsWithinInteractionRange(Vector2 worldPosition) =>
        _presentation is not null && GlobalPosition.DistanceTo(worldPosition) <= _presentation.InteractionRadius;

    public bool RequestInteraction(Vector2 worldPosition)
    {
        if (!IsWithinInteractionRange(worldPosition))
        {
            return false;
        }

        InteractionRequested?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Stable local anchor used by cable, conveyor and pipe presentation. The
    /// returned point follows machine rotation because it lives in local space.
    /// </summary>
    public Vector2 GetLocalConnectionAnchor(MachineConnectionAnchorKind anchor)
    {
        if (_presentation is null)
        {
            throw new InvalidOperationException("Machine view is not configured.");
        }

        return _presentation.GetConnectionAnchor(anchor);
    }

    public Vector2 GetWorldConnectionAnchor(MachineConnectionAnchorKind anchor) =>
        ToGlobal(GetLocalConnectionAnchor(anchor));

    /// <summary>
    /// Number of physical power sockets represented by this machine. Regular
    /// machines expose one socket; the compact distribution pole exposes six.
    /// </summary>
    public int PowerPortCount => _presentation?.PowerPortCount ?? 0;

    public Vector2 GetLocalPowerPortAnchor(int portIndex)
    {
        if (_presentation is null)
        {
            throw new InvalidOperationException("Machine view is not configured.");
        }

        return _presentation.GetPowerPortAnchor(portIndex);
    }

    public Vector2 GetWorldPowerPortAnchor(int portIndex) =>
        ToGlobal(GetLocalPowerPortAnchor(portIndex));

    public Vector2[] GetWorldFootprint(float padding = 0)
    {
        if (_presentation is null)
        {
            return [];
        }

        return MachinePlacementGeometry
            .CreateRectangleCorners(Vector2.Zero, _presentation.Footprint + new Vector2(padding * 2, padding * 2), 0)
            .Select(ToGlobal)
            .ToArray();
    }

    public bool ContainsWorldPoint(Vector2 worldPoint, float padding = 8)
    {
        if (_presentation is null)
        {
            return false;
        }

        var local = ToLocal(worldPoint);
        var half = (_presentation.Footprint * 0.5f) + new Vector2(padding, padding);
        return Math.Abs(local.X) <= half.X && Math.Abs(local.Y) <= half.Y;
    }

    /// <summary>
    /// Applies a non-destructive teardown preview. Actual removal and refunds remain atomic in
    /// <see cref="DismantlingRules"/> and only happen after the hold interaction completes.
    /// </summary>
    public void SetDismantlingProgress(float progress)
    {
        var normalized = float.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (Mathf.IsEqualApprox(_dismantlingProgress, normalized))
        {
            return;
        }

        _dismantlingProgress = normalized;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_presentation is null || _state is null)
        {
            return;
        }

        var half = _presentation.Footprint * 0.5f;
        var silhouette = CreateMachineSilhouette(_presentation.Glyph, _presentation.Footprint);
        var progressAlpha = _state.IsConstructionComplete
            ? 1
            : Mathf.Lerp(0.2f, 0.92f, _visualConstructionProgress);
        var body = new Color(_presentation.BodyColor, progressAlpha);
        var accent = new Color(_presentation.AccentColor, progressAlpha);
        var shadow = silhouette.Select(point => point + new Vector2(5, 7)).ToArray();

        DrawColoredPolygon(shadow, new Color(0, 0, 0, 0.48f * progressAlpha));
        DrawColoredPolygon(silhouette, body);
        DrawPolyline(Close(silhouette), new Color(Titanium.Lightened(0.2f), progressAlpha), 2.2f, true);

        var innerSize = _presentation.Footprint - new Vector2(12, 12);
        var inner = CreateMachineSilhouette(_presentation.Glyph, innerSize);
        DrawColoredPolygon(inner, new Color(DarkMetal, 0.92f * progressAlpha));
        DrawPolyline(Close(inner), new Color(_presentation.AccentColor.Darkened(0.4f), 0.7f * progressAlpha), 1.2f, true);

        DrawCommonHardware(half, accent, progressAlpha);
        DrawMachineAssembly(_presentation.Glyph, accent, progressAlpha);
        DrawConnectionSockets(accent, progressAlpha);
        DrawStatusIndicator(half, progressAlpha);

        if (!_state.IsConstructionComplete || _constructionVisualRunning)
        {
            DrawConstructionHologram(half, silhouette);
        }

        if (_dismantlingProgress > 0)
        {
            DrawDismantlingOverlay(half, silhouette);
        }
    }

    private void DrawDismantlingOverlay(Vector2 half, IReadOnlyList<Vector2> silhouette)
    {
        var pulse = 0.58f + (Mathf.Sin((float)Time.GetTicksMsec() * 0.018f) * 0.18f);
        var energy = new Color(0.18f, 0.88f, 1f, pulse);
        DrawPolyline(Close(silhouette), energy, 2.2f, true);

        // Sequential scan lines make panels appear to detach without changing collision or state.
        const int segmentCount = 7;
        var visibleSegments = Math.Clamp(Mathf.CeilToInt(_dismantlingProgress * segmentCount), 1, segmentCount);
        for (var segment = 0; segment < visibleSegments; segment++)
        {
            var y = Mathf.Lerp(-half.Y + 6, half.Y - 6, segment / (float)(segmentCount - 1));
            var phase = Mathf.PosMod((float)Time.GetTicksMsec() * 0.0025f + (segment * 0.19f), 1);
            var inset = 9 + (Mathf.Sin((segment * 1.9f) + phase) * 4);
            DrawLine(
                new Vector2(-half.X + inset, y),
                new Vector2(half.X - inset, y + Mathf.Sin(segment) * 2),
                new Color(energy, 0.18f + (_dismantlingProgress * 0.44f)),
                1.15f,
                true);
        }

        var separation = _dismantlingProgress * 3.5f;
        DrawLine(new Vector2(-half.X * 0.55f - separation, -half.Y * 0.3f),
            new Vector2(-half.X * 0.1f - separation, half.Y * 0.34f), energy, 1.4f, true);
        DrawLine(new Vector2(half.X * 0.18f + separation, -half.Y * 0.42f),
            new Vector2(half.X * 0.62f + separation, half.Y * 0.25f), energy, 1.4f, true);
    }

    private void EnsurePresentationNodes()
    {
        if (_presentation is null)
        {
            return;
        }

        if (_collisionShape is null)
        {
            _collisionShape = new CollisionShape2D
            {
                Name = "MachineCollision",
                Shape = new RectangleShape2D { Size = _presentation.Footprint },
            };
            AddChild(_collisionShape);
        }
        else if (_collisionShape.Shape is RectangleShape2D rectangle)
        {
            rectangle.Size = _presentation.Footprint;
        }

        if (_statusLabel is null)
        {
            _statusLabel = new Label
            {
                Name = "Status",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 3,
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_statusLabel);
        }

        _statusLabel.Position = new Vector2(-100, -(_presentation.Footprint.Y * 0.5f) - 48);
        _statusLabel.Size = new Vector2(200, 42);
        RefreshStatusLabel();
    }

    private void RefreshStatusLabel()
    {
        if (_statusLabel is null || _state is null)
        {
            return;
        }

        var status = !_state.IsConstructionComplete || _constructionVisualRunning
            ? $"IM BAU  {Mathf.RoundToInt(_visualConstructionProgress * 100)}%"
            : GetStatusText(_state.Status);
        _statusLabel.Text = $"{_state.Definition.DisplayName}\n{status}";
        _statusLabel.AddThemeColorOverride("font_color", GetStatusColor(_state.Status).Lightened(0.12f));
    }

    private void DrawCommonHardware(Vector2 half, Color accent, float alpha)
    {
        var screwColor = new Color(Titanium.Lightened(0.38f), 0.8f * alpha);
        foreach (var point in new[]
                 {
                     new Vector2(-half.X + 9, -half.Y + 9),
                     new Vector2(half.X - 9, -half.Y + 9),
                     new Vector2(half.X - 9, half.Y - 9),
                     new Vector2(-half.X + 9, half.Y - 9),
                 })
        {
            DrawCircle(point, 1.7f, screwColor);
            DrawLine(point + new Vector2(-1, 0), point + new Vector2(1, 0), new Color(Recess, alpha), 0.8f, true);
        }

        // Sparse seam and wear lines keep the metal readable without noisy texture.
        DrawLine(new Vector2(-half.X + 14, -half.Y + 6), new Vector2(-half.X + 25, -half.Y + 6),
            new Color(accent, 0.28f * alpha), 1, true);
        DrawLine(new Vector2(half.X - 28, half.Y - 7), new Vector2(half.X - 14, half.Y - 7),
            new Color(Titanium, 0.46f * alpha), 1, true);
        DrawLine(new Vector2(-half.X + 18, half.Y - 9), new Vector2(-half.X + 26, half.Y - 12),
            new Color(Titanium.Darkened(0.35f), 0.65f * alpha), 1, true);
    }

    private void DrawConnectionSockets(Color accent, float alpha)
    {
        if (_presentation is null)
        {
            return;
        }

        if (_presentation.Glyph == MachineGlyph.PowerPole)
        {
            for (var portIndex = 0; portIndex < _presentation.PowerPortCount; portIndex++)
            {
                DrawSocket(
                    _presentation.GetPowerPortAnchor(portIndex),
                    new Color(0.13f, 0.82f, 0.98f, 0.9f * alpha),
                    4.1f);
            }

            return;
        }

        DrawSocket(_presentation.GetPowerPortAnchor(0), new Color(0.96f, 0.7f, 0.2f, alpha));
        DrawSocket(_presentation.GetConnectionAnchor(MachineConnectionAnchorKind.ItemInput),
            new Color(accent, 0.72f * alpha));
        DrawSocket(_presentation.GetConnectionAnchor(MachineConnectionAnchorKind.ItemOutput),
            new Color(0.32f, 0.88f, 0.65f, 0.78f * alpha));
        DrawSocket(_presentation.GetConnectionAnchor(MachineConnectionAnchorKind.PipeInput),
            new Color(0.22f, 0.65f, 0.96f, 0.68f * alpha), 3.2f);
        DrawSocket(_presentation.GetConnectionAnchor(MachineConnectionAnchorKind.PipeOutput),
            new Color(0.34f, 0.86f, 0.9f, 0.68f * alpha), 3.2f);
    }

    private void DrawSocket(Vector2 position, Color color, float radius = 3.8f)
    {
        DrawCircle(position, radius + 1.5f, new Color(Recess, color.A));
        DrawArc(position, radius, 0, Mathf.Tau, 12, color, 1.3f, true);
        DrawCircle(position, 1.25f, new Color(color, color.A * 0.75f));
    }

    private void DrawStatusIndicator(Vector2 half, float alpha)
    {
        if (_state is null)
        {
            return;
        }

        var statusColor = GetStatusColor(_state.Status);
        var position = new Vector2(half.X - 13, -half.Y + 13);
        var pulse = _state.Status == MachineOperationStatus.Producing
            ? 0.72f + (Mathf.Sin(_animationSeconds * 5f) * 0.18f)
            : 0.82f;
        DrawRect(new Rect2(position - new Vector2(10, 5), new Vector2(20, 10)),
            new Color(Recess, 0.88f * alpha), true);
        for (var index = 0; index < 3; index++)
        {
            var lamp = position + new Vector2((index - 1) * 6, 0);
            var lit = index == 1;
            DrawCircle(lamp, 2.2f, lit
                ? new Color(statusColor, pulse * alpha)
                : new Color(statusColor.Darkened(0.72f), 0.5f * alpha));
        }
    }

    private void DrawMachineAssembly(MachineGlyph glyph, Color accent, float alpha)
    {
        var producing = _state?.Status == MachineOperationStatus.Producing;
        switch (glyph)
        {
            case MachineGlyph.Crusher:
                DrawCrusher(accent, alpha, producing);
                break;
            case MachineGlyph.Smelter:
                DrawSmelter(accent, alpha, producing);
                break;
            case MachineGlyph.Foundry:
                DrawFoundry(accent, alpha, producing);
                break;
            case MachineGlyph.Constructor:
                DrawConstructor(accent, alpha, producing);
                break;
            case MachineGlyph.Fabricator:
                DrawFabricator(accent, alpha, producing);
                break;
            case MachineGlyph.WaterProcessor:
                DrawWaterProcessor(accent, alpha, producing);
                break;
            case MachineGlyph.Electrolyzer:
                DrawElectrolyzer(accent, alpha, producing);
                break;
            case MachineGlyph.Refinery:
                DrawRefinery(accent, alpha, producing);
                break;
            case MachineGlyph.BasicGenerator:
                DrawBasicGenerator(accent, alpha, producing);
                break;
            case MachineGlyph.FuelGenerator:
                DrawFuelGenerator(accent, alpha, producing);
                break;
            case MachineGlyph.PowerPole:
                DrawPowerPole(accent, alpha, producing);
                break;
            case MachineGlyph.Storage:
                DrawStorage(accent, alpha);
                break;
            case MachineGlyph.Research:
                DrawResearch(accent, alpha, producing);
                break;
            case MachineGlyph.MobileMiner:
                DrawMiner(accent, alpha, producing, automatic: false);
                break;
            case MachineGlyph.AutomaticMiner:
                DrawMiner(accent, alpha, producing, automatic: true);
                break;
            case MachineGlyph.ChemicalPlant:
                DrawChemicalPlant(accent, alpha, producing);
                break;
            case MachineGlyph.Assembler:
                DrawAssembler(accent, alpha, producing);
                break;
            case MachineGlyph.AdvancedFabricator:
                DrawAdvancedFabricator(accent, alpha, producing);
                break;
            case MachineGlyph.PrecisionManufacturer:
                DrawPrecisionManufacturer(accent, alpha, producing);
                break;
            case MachineGlyph.LiquidTank:
                DrawFluidTank(accent, alpha, producing, gas: false);
                break;
            case MachineGlyph.GasTank:
                DrawFluidTank(accent, alpha, producing, gas: true);
                break;
            case MachineGlyph.PumpStation:
                DrawPumpStation(accent, alpha, producing);
                break;
            case MachineGlyph.BatteryBank:
                DrawBatteryBank(accent, alpha, producing);
                break;
            case MachineGlyph.UraniumProcessor:
                DrawUraniumProcessor(accent, alpha, producing);
                break;
            case MachineGlyph.FuelCellFabricator:
                DrawFuelCellFabricator(accent, alpha, producing);
                break;
            case MachineGlyph.NuclearReactor:
                DrawNuclearReactor(accent, alpha, producing);
                break;
            case MachineGlyph.WasteProcessor:
                DrawWasteProcessor(accent, alpha, producing);
                break;
            case MachineGlyph.NuclearWasteStorage:
                DrawNuclearWasteStorage(accent, alpha);
                break;
        }
    }

    private void DrawCrusher(Color accent, float alpha, bool producing)
    {
        var hopper = new Vector2[] { new(-39, -24), new(-19, -18), new(-19, 18), new(-39, 24) };
        DrawColoredPolygon(hopper, new Color(Recess, alpha));
        DrawPolyline(Close(hopper), new Color(Titanium, alpha), 1.6f, true);
        for (var index = -1; index <= 1; index += 2)
        {
            var x = index * 9f;
            DrawRect(new Rect2(x - 5, -21, 10, 42), new Color(Titanium.Darkened(0.15f), alpha), true);
            var offset = producing ? Mathf.PosMod(_animationSeconds * 18f * index, 8f) : 0;
            for (var y = -17f; y <= 17; y += 8)
            {
                var toothY = Mathf.PosMod(y + offset + 21, 42) - 21;
                DrawLine(new Vector2(x - 5, toothY), new Vector2(x + 5, toothY + (index * 2)),
                    new Color(accent, alpha), 1.5f, true);
            }
        }
        DrawRect(new Rect2(20, -15, 20, 30), new Color(Recess, alpha), true);
        DrawRect(new Rect2(23, -11, 14, 22), new Color(Titanium.Darkened(0.25f), alpha), false, 1.4f, true);
        DrawWarningStripes(new Vector2(-18, -27), 36, accent, alpha);
        if (producing)
        {
            DrawParticles(new Vector2(38, 0), new Vector2(8, 15), new Color(0.52f, 0.48f, 0.41f, alpha), 5, 2.2f);
        }
    }

    private void DrawSmelter(Color accent, float alpha, bool producing)
    {
        var pulse = producing ? 0.58f + (Mathf.Sin(_animationSeconds * 4.2f) * 0.12f) : 0.18f;
        DrawCircle(Vector2.Zero, 30, new Color(Titanium.Darkened(0.12f), alpha));
        DrawArc(Vector2.Zero, 30, 0, Mathf.Tau, 40, new Color(Titanium.Lightened(0.18f), alpha), 3, true);
        DrawCircle(Vector2.Zero, 21, new Color(Recess, alpha));
        DrawCircle(Vector2.Zero, 14, new Color(accent, pulse * alpha));
        for (var index = 0; index < 8; index++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * index / 8f);
            DrawLine(direction * 23, direction * 30, new Color(Titanium, alpha), 3, true);
        }
        DrawVents(new Vector2(-33, 0), vertical: true, accent.Darkened(0.45f), alpha);
        DrawVents(new Vector2(33, 0), vertical: true, accent.Darkened(0.45f), alpha);
        if (producing)
        {
            DrawParticles(new Vector2(0, -30), new Vector2(15, 10), new Color(0.62f, 0.68f, 0.7f, alpha), 4, 1.7f);
        }
    }

    private void DrawFoundry(Color accent, float alpha, bool producing)
    {
        foreach (var position in new[] { new Vector2(-38, -18), new Vector2(-38, 18) })
        {
            DrawCircle(position, 11, new Color(Titanium.Darkened(0.1f), alpha));
            DrawCircle(position, 7, new Color(Recess, alpha));
            DrawArc(position, 11, 0, Mathf.Tau, 20, new Color(Titanium.Lightened(0.15f), alpha), 1.8f, true);
        }
        DrawCircle(new Vector2(0, 0), 20, new Color(Titanium.Darkened(0.18f), alpha));
        DrawCircle(new Vector2(0, 0), 12, new Color(accent, (producing ? 0.38f : 0.12f) * alpha));
        var slide = producing ? Mathf.Sin(_animationSeconds * 3.5f) * 5f : 0;
        DrawMechanicalArm(new Vector2(-29, -18), new Vector2(-7 + slide, -5), accent, alpha);
        DrawMechanicalArm(new Vector2(-29, 18), new Vector2(-7 + slide, 5), accent, alpha);
        DrawLine(new Vector2(20, 0), new Vector2(36, 0), new Color(accent, alpha), 4, true);
        DrawRect(new Rect2(36, -18, 18, 36), new Color(Recess, alpha), true);
        for (var y = -12; y <= 12; y += 8)
        {
            DrawLine(new Vector2(39, y), new Vector2(51, y), new Color(Titanium, alpha), 1.4f, true);
        }
    }

    private void DrawConstructor(Color accent, float alpha, bool producing)
    {
        var press = producing ? Mathf.Abs(Mathf.Sin(_animationSeconds * 4.5f)) * 6f : 0;
        DrawRect(new Rect2(-17, -23, 34, 46), new Color(Recess, alpha), true);
        DrawRect(new Rect2(-17, -23, 34, 46), new Color(Titanium, alpha), false, 2, true);
        DrawRect(new Rect2(-11 + press, -7, 22 - (press * 2), 14), new Color(accent, 0.72f * alpha), true);
        DrawMechanicalArm(new Vector2(-36, -19), new Vector2(-15, -7), accent, alpha);
        DrawMechanicalArm(new Vector2(36, 19), new Vector2(15, 7), accent, alpha);
        DrawRect(new Rect2(-43, -10, 10, 20), new Color(Titanium.Darkened(0.2f), alpha), true);
        DrawRect(new Rect2(33, -10, 10, 20), new Color(Titanium.Darkened(0.2f), alpha), true);
    }

    private void DrawFabricator(Color accent, float alpha, bool producing)
    {
        Vector2[] chamber = [new(-20, -27), new(20, -27), new(29, -18), new(29, 18), new(20, 27), new(-20, 27), new(-29, 18), new(-29, -18)];
        DrawColoredPolygon(chamber, new Color(Recess, alpha));
        DrawPolyline(Close(chamber), new Color(accent, 0.9f * alpha), 2, true);
        DrawCircle(Vector2.Zero, 12, new Color(Titanium.Darkened(0.1f), alpha));
        DrawCircle(Vector2.Zero, 5, new Color(accent, (producing ? 0.82f : 0.32f) * alpha));
        for (var index = 0; index < 4; index++)
        {
            var angle = (Mathf.Tau * index / 4f) + (producing ? Mathf.Sin(_animationSeconds * 2.5f + index) * 0.18f : 0);
            var direction = Vector2.FromAngle(angle);
            DrawMechanicalArm(direction * 25, direction * 10, accent, alpha);
        }
        foreach (var y in new[] { -18f, 0f, 18f })
        {
            DrawLine(new Vector2(-51, y), new Vector2(-30, y * 0.55f), new Color(Titanium, alpha), 3, true);
        }
        DrawRect(new Rect2(35, -18, 17, 36), new Color(accent, 0.16f * alpha), true);
        var scanY = producing ? Mathf.Sin(_animationSeconds * 3.1f) * 13f : -10f;
        DrawLine(new Vector2(38, scanY), new Vector2(49, scanY), new Color(accent, 0.9f * alpha), 1.5f, true);
    }

    private void DrawWaterProcessor(Color accent, float alpha, bool producing)
    {
        Vector2[] hopper = [new(-40, -24), new(-20, -17), new(-20, 17), new(-40, 24)];
        DrawColoredPolygon(hopper, new Color(Titanium.Darkened(0.08f), alpha));
        DrawPolyline(Close(hopper), new Color(Titanium.Lightened(0.16f), alpha), 1.6f, true);
        DrawTank(new Vector2(2, 0), new Vector2(16, 24), accent, alpha, producing ? 0.62f : 0.36f);
        DrawCircle(new Vector2(28, 0), 11, new Color(Recess, alpha));
        DrawArc(new Vector2(28, 0), 11, 0, Mathf.Tau, 20, new Color(accent, alpha), 2, true);
        var rotorAngle = producing ? _animationSeconds * 4f : 0;
        for (var index = 0; index < 4; index++)
        {
            var direction = Vector2.FromAngle(rotorAngle + (Mathf.Tau * index / 4f));
            DrawLine(new Vector2(28, 0), new Vector2(28, 0) + direction * 8, new Color(accent, alpha), 2, true);
        }
        DrawLine(new Vector2(18, 0), new Vector2(17, 0), new Color(accent, alpha), 3, true);
        if (producing)
        {
            DrawParticles(new Vector2(4, -13), new Vector2(11, 16), new Color(0.4f, 0.82f, 1f, alpha), 4, 1.6f);
        }
    }

    private void DrawElectrolyzer(Color accent, float alpha, bool producing)
    {
        DrawTank(new Vector2(-15, 0), new Vector2(18, 25), accent, alpha, producing ? 0.48f : 0.24f);
        var hydrogen = new Color(0.2f, 0.82f, 1f, alpha);
        var oxygen = new Color(0.72f, 0.9f, 1f, alpha);
        DrawTank(new Vector2(25, -15), new Vector2(12, 10), hydrogen, alpha, 0.28f);
        DrawTank(new Vector2(25, 15), new Vector2(12, 10), oxygen, alpha, 0.22f);
        DrawLine(new Vector2(3, -7), new Vector2(13, -15), hydrogen, 3, true);
        DrawLine(new Vector2(3, 7), new Vector2(13, 15), oxygen, 3, true);
        DrawGauge(new Vector2(-15, -17), accent, alpha);
        if (producing)
        {
            DrawBubbles(new Vector2(-15, 0), 14, 20, accent, alpha);
            DrawFlowPulse(new Vector2(4, -8), new Vector2(14, -15), hydrogen);
            DrawFlowPulse(new Vector2(4, 8), new Vector2(14, 15), oxygen);
        }
    }

    private void DrawRefinery(Color accent, float alpha, bool producing)
    {
        DrawTank(new Vector2(-43, 6), new Vector2(14, 27), accent.Darkened(0.2f), alpha, 0.28f);
        DrawTank(new Vector2(-5, -4), new Vector2(17, 35), accent, alpha, producing ? 0.46f : 0.24f);
        DrawTank(new Vector2(38, 10), new Vector2(14, 23), accent.Lightened(0.12f), alpha, 0.22f);
        DrawLine(new Vector2(-29, 4), new Vector2(-22, 0), new Color(Titanium, alpha), 5, true);
        DrawLine(new Vector2(12, 2), new Vector2(24, 8), new Color(Titanium, alpha), 5, true);
        DrawCircle(new Vector2(-25, 1), 3.5f, new Color(accent, alpha));
        DrawCircle(new Vector2(20, 6), 3.5f, new Color(accent, alpha));
        DrawGauge(new Vector2(-43, -16), accent, alpha);
        DrawGauge(new Vector2(38, -9), accent, alpha);
        DrawVents(new Vector2(-4, 38), vertical: false, Titanium, alpha);
        if (producing)
        {
            DrawParticles(new Vector2(-4, -38), new Vector2(19, 11), new Color(0.55f, 0.68f, 0.68f, alpha), 5, 1.8f);
            DrawFlowPulse(new Vector2(-27, 2), new Vector2(-21, 0), new Color(accent, alpha));
            DrawFlowPulse(new Vector2(14, 3), new Vector2(23, 8), new Color(accent, alpha));
        }
    }

    private void DrawBasicGenerator(Color accent, float alpha, bool producing)
    {
        DrawCircle(Vector2.Zero, 22, new Color(Recess, alpha));
        DrawArc(Vector2.Zero, 22, 0, Mathf.Tau, 32, new Color(Titanium, alpha), 3, true);
        var rotation = producing ? _animationSeconds * 1.7f : 0;
        DrawTurbine(Vector2.Zero, 17, 6, rotation, accent, alpha);
        DrawRect(new Rect2(-19, 24, 38, 6), new Color(accent, 0.18f * alpha), true);
        DrawRect(new Rect2(-17, 26, producing ? 27 : 9, 2), new Color(accent, alpha), true);
    }

    private void DrawFuelGenerator(Color accent, float alpha, bool producing)
    {
        DrawTank(new Vector2(-31, 0), new Vector2(11, 25), accent, alpha, 0.25f);
        DrawLine(new Vector2(-20, 0), new Vector2(-15, 0), new Color(accent, alpha), 4, true);
        DrawCircle(new Vector2(7, 0), 24, new Color(Recess, alpha));
        DrawArc(new Vector2(7, 0), 24, 0, Mathf.Tau, 32, new Color(Titanium, alpha), 3, true);
        DrawTurbine(new Vector2(7, 0), 19, 8, producing ? _animationSeconds * 4.2f : 0, accent, alpha);
        DrawVents(new Vector2(38, 0), vertical: true, Titanium.Lightened(0.08f), alpha);
        if (producing)
        {
            DrawParticles(new Vector2(43, 0), new Vector2(10, 18), new Color(0.82f, 0.49f, 0.2f, alpha), 4, 1.8f);
        }
    }

    private void DrawPowerPole(Color accent, float alpha, bool energized)
    {
        // Six independent branch conductors feed a recessed central bus. The
        // angular spine and service hatch keep the silhouette mechanical rather
        // than reading as a placeholder circle.
        DrawRect(new Rect2(-12, -18, 24, 36), new Color(Recess, alpha), true);
        DrawRect(new Rect2(-12, -18, 24, 36), new Color(Titanium.Lightened(0.12f), alpha), false, 2, true);
        DrawRect(new Rect2(-5, -15, 10, 30), new Color(accent, (energized ? 0.28f : 0.1f) * alpha), true);
        DrawLine(new Vector2(-2, -13), new Vector2(-2, 13), new Color(accent, 0.78f * alpha), 1.4f, true);
        DrawLine(new Vector2(2, -13), new Vector2(2, 13), new Color(accent, 0.42f * alpha), 1.1f, true);

        var portsPerSide = PowerGridConfiguration.PowerPolePortCount / 2;
        for (var row = 0; row < portsPerSide; row++)
        {
            var leftAnchor = _presentation!.GetPowerPortAnchor(row);
            var rightAnchor = _presentation.GetPowerPortAnchor(
                (PowerGridConfiguration.PowerPolePortCount - 1) - row);
            var y = leftAnchor.Y;
            DrawLine(leftAnchor, new Vector2(-12, y), new Color(Titanium, alpha), 4, true);
            DrawLine(new Vector2(12, y), rightAnchor, new Color(Titanium, alpha), 4, true);
            DrawLine(leftAnchor + new Vector2(4, 0), new Vector2(-5, y * 0.72f), new Color(accent, 0.72f * alpha), 1.3f, true);
            DrawLine(new Vector2(5, y * 0.72f), rightAnchor - new Vector2(4, 0), new Color(accent, 0.72f * alpha), 1.3f, true);
        }

        DrawRect(new Rect2(-9, -5, 18, 10), new Color(0.16f, 0.19f, 0.2f, alpha), true);
        DrawRect(new Rect2(-7, -3, 14, 6), new Color(0.025f, 0.04f, 0.046f, alpha), true);
        for (var index = 0; index < 3; index++)
        {
            var lit = energized || index == 0;
            DrawRect(
                new Rect2(-5 + (index * 4), -1, 2.5f, 2),
                lit
                    ? new Color(accent, (energized ? 0.9f : 0.4f) * alpha)
                    : new Color(accent.Darkened(0.7f), 0.35f * alpha),
                true);
        }
    }

    private void DrawStorage(Color accent, float alpha)
    {
        for (var column = -2; column <= 2; column++)
        {
            var x = column * 20f;
            DrawRect(new Rect2(x - 8, -25, 16, 50), new Color(Recess, alpha), true);
            DrawRect(new Rect2(x - 8, -25, 16, 50), new Color(Titanium.Darkened(0.08f), alpha), false, 1.6f, true);
            DrawLine(new Vector2(x, -19), new Vector2(x, 19), new Color(Titanium, 0.7f * alpha), 1, true);
            DrawCircle(new Vector2(x + 4, 0), 1.7f, new Color(accent, 0.8f * alpha));
        }
        for (var index = 0; index < 8; index++)
        {
            DrawRect(new Rect2(-21 + (index * 6), 28, 4, 3),
                index < 5 ? new Color(accent, alpha) : new Color(Recess, alpha), true);
        }
    }

    private void DrawResearch(Color accent, float alpha, bool producing)
    {
        DrawCircle(Vector2.Zero, 24, new Color(Recess, alpha));
        DrawCircle(Vector2.Zero, 12, new Color(accent, (producing ? 0.34f : 0.16f) * alpha));
        DrawArc(Vector2.Zero, 30, 0, Mathf.Tau, 40, new Color(accent, 0.7f * alpha), 2, true);
        var rotation = producing ? _animationSeconds * 0.9f : 0;
        for (var index = 0; index < 3; index++)
        {
            var direction = Vector2.FromAngle(rotation - (Mathf.Pi * 0.5f) + (Mathf.Tau * index / 3f));
            var module = direction * 30;
            DrawLine(direction * 23, module, new Color(Titanium, alpha), 3, true);
            DrawCircle(module, 6, new Color(Titanium.Darkened(0.05f), alpha));
            DrawCircle(module, 2.2f, new Color(accent, alpha));
        }
        foreach (var panel in new[] { new Rect2(-48, -24, 16, 13), new Rect2(32, -24, 16, 13), new Rect2(32, 12, 16, 13) })
        {
            DrawRect(panel, new Color(Recess, alpha), true);
            DrawRect(panel.Grow(-3), new Color(accent, 0.25f * alpha), true);
        }
        if (producing)
        {
            var scanAngle = _animationSeconds * 2.1f;
            DrawLine(Vector2.Zero, Vector2.FromAngle(scanAngle) * 20, new Color(accent, 0.76f * alpha), 1.4f, true);
        }
    }

    private void DrawMiner(Color accent, float alpha, bool producing, bool automatic)
    {
        var drillCenter = new Vector2(automatic ? -10 : 0, 0);
        var drillRadius = automatic ? 23f : 19f;
        DrawCircle(drillCenter, drillRadius, new Color(Recess, alpha));
        DrawArc(drillCenter, drillRadius, 0, Mathf.Tau, 30, new Color(Titanium, alpha), 3, true);
        var rotation = producing ? _animationSeconds * 2.4f : 0;
        for (var index = 0; index < 7; index++)
        {
            var direction = Vector2.FromAngle(rotation + (Mathf.Tau * index / 7f));
            DrawLine(drillCenter + direction * 7, drillCenter + direction * (drillRadius - 3),
                new Color(accent, alpha), 3, true);
        }
        DrawCircle(drillCenter, 6, new Color(accent.Darkened(0.2f), alpha));
        foreach (var foot in new[] { new Vector2(-34, -28), new Vector2(-34, 28), new Vector2(31, -28), new Vector2(31, 28) })
        {
            DrawLine(drillCenter + foot * 0.42f, foot, new Color(Titanium, alpha), 5, true);
            DrawCircle(foot, 5, new Color(accent, alpha));
        }
        if (automatic)
        {
            DrawRect(new Rect2(18, -13, 42, 26), new Color(Recess, alpha), true);
            DrawRect(new Rect2(18, -13, 42, 26), new Color(Titanium, alpha), false, 2, true);
            for (var x = 24f; x < 58; x += 8)
                DrawLine(new Vector2(x, -9), new Vector2(x, 9), new Color(accent, 0.7f * alpha), 2, true);
        }
        else
        {
            DrawRect(new Rect2(-13, 22, 26, 12), new Color(Recess, alpha), true);
            DrawRect(new Rect2(-9, 25, 18, 5), new Color(accent, 0.65f * alpha), true);
        }
    }

    private void DrawChemicalPlant(Color accent, float alpha, bool producing)
    {
        DrawTank(new Vector2(-37, 6), new Vector2(17, 34), accent.Darkened(0.18f), alpha, producing ? 0.48f : 0.25f);
        DrawTank(new Vector2(35, -8), new Vector2(20, 37), accent, alpha, producing ? 0.64f : 0.3f);
        DrawCircle(Vector2.Zero, 15, new Color(Recess, alpha));
        DrawArc(Vector2.Zero, 15, 0, Mathf.Tau, 24, new Color(accent, alpha), 2.5f, true);
        DrawLine(new Vector2(-20, 5), new Vector2(-14, 2), new Color(Titanium, alpha), 5, true);
        DrawLine(new Vector2(14, -2), new Vector2(15, -5), new Color(Titanium, alpha), 5, true);
        DrawGauge(new Vector2(-37, -24), accent, alpha);
        if (producing)
        {
            DrawBubbles(new Vector2(35, -8), 25, 42, accent, alpha);
            DrawFlowPulse(new Vector2(-18, 4), new Vector2(-13, 2), new Color(accent, alpha));
        }
    }

    private void DrawAssembler(Color accent, float alpha, bool producing)
    {
        DrawRect(new Rect2(-22, -17, 44, 34), new Color(Recess, alpha), true);
        DrawRect(new Rect2(-22, -17, 44, 34), new Color(accent, alpha), false, 2, true);
        var travel = producing ? Mathf.Sin(_animationSeconds * 3.2f) * 5 : 0;
        DrawMechanicalArm(new Vector2(-47, -30), new Vector2(-12 + travel, -8), accent, alpha);
        DrawMechanicalArm(new Vector2(47, 30), new Vector2(12 - travel, 8), accent, alpha);
        DrawCircle(Vector2.Zero, 7, new Color(accent, 0.7f * alpha));
    }

    private void DrawAdvancedFabricator(Color accent, float alpha, bool producing)
    {
        DrawFabricator(accent, alpha, producing);
        foreach (var module in new[] { new Vector2(-58, -29), new Vector2(-58, 29), new Vector2(58, -29), new Vector2(58, 29) })
        {
            DrawLine(module * 0.7f, module, new Color(Titanium, alpha), 4, true);
            DrawCircle(module, 7, new Color(Recess, alpha));
            DrawArc(module, 7, 0, Mathf.Tau, 14, new Color(accent, alpha), 1.7f, true);
        }
        DrawArc(Vector2.Zero, 30, 0, Mathf.Tau, 28, new Color(accent, 0.45f * alpha), 1.5f, true);
    }

    private void DrawPrecisionManufacturer(Color accent, float alpha, bool producing)
    {
        var chip = new Rect2(-23, -23, 46, 46);
        DrawRect(chip, new Color(Recess, alpha), true);
        DrawRect(chip, new Color(accent, alpha), false, 2.2f, true);
        for (var index = -3; index <= 3; index++)
        {
            var offset = index * 7f;
            DrawLine(new Vector2(-39, offset), new Vector2(-23, offset), new Color(Titanium, alpha), 3, true);
            DrawLine(new Vector2(23, offset), new Vector2(39, offset), new Color(Titanium, alpha), 3, true);
        }
        DrawMechanicalArm(new Vector2(-53, -34), new Vector2(-12, -12), accent, alpha);
        var scanX = producing ? Mathf.Sin(_animationSeconds * 3f) * 15 : -12;
        DrawLine(new Vector2(scanX, -19), new Vector2(scanX, 19), new Color(accent, 0.88f * alpha), 1.5f, true);
    }

    private void DrawFluidTank(Color accent, float alpha, bool producing, bool gas)
    {
        DrawCircle(Vector2.Zero, 34, new Color(Recess, alpha));
        DrawArc(Vector2.Zero, 34, 0, Mathf.Tau, 40, new Color(Titanium, alpha), 3, true);
        DrawArc(Vector2.Zero, 25, 0, Mathf.Tau, 36, new Color(accent, 0.72f * alpha), 2, true);
        if (gas)
        {
            foreach (var bubble in new[] { new Vector2(-11, 7), new Vector2(8, -10), new Vector2(13, 13) })
                DrawArc(bubble, 4 + (Mathf.Abs(bubble.X) % 3), 0, Mathf.Tau, 14, new Color(accent, 0.7f * alpha), 1.4f, true);
        }
        else
        {
            DrawRect(new Rect2(-22, 5, 44, 18), new Color(accent, (producing ? 0.42f : 0.25f) * alpha), true);
            DrawLine(new Vector2(-22, 5), new Vector2(22, 5), new Color(accent, alpha), 1.5f, true);
        }
        DrawGauge(new Vector2(0, -25), accent, alpha);
    }

    private void DrawPumpStation(Color accent, float alpha, bool producing)
    {
        DrawLine(new Vector2(-40, 0), new Vector2(-21, 0), new Color(Titanium, alpha), 11, true);
        DrawLine(new Vector2(21, 0), new Vector2(40, 0), new Color(Titanium, alpha), 11, true);
        DrawCircle(Vector2.Zero, 23, new Color(Recess, alpha));
        DrawArc(Vector2.Zero, 23, 0, Mathf.Tau, 30, new Color(accent, alpha), 2.5f, true);
        DrawTurbine(Vector2.Zero, 18, 6, producing ? _animationSeconds * 4.5f : 0, accent, alpha);
    }

    private void DrawBatteryBank(Color accent, float alpha, bool producing)
    {
        for (var column = -3; column <= 3; column++)
        {
            var x = column * 15f;
            DrawRect(new Rect2(x - 6, -24, 12, 48), new Color(Recess, alpha), true);
            DrawRect(new Rect2(x - 6, -24, 12, 48), new Color(Titanium, alpha), false, 1.7f, true);
            var fill = producing ? 31f : 18f;
            DrawRect(new Rect2(x - 3, 20 - fill, 6, fill), new Color(accent, (0.35f + ((column + 3) * 0.07f)) * alpha), true);
        }
        DrawLine(new Vector2(-49, 29), new Vector2(49, 29), new Color(accent, alpha), 3, true);
    }

    private void DrawUraniumProcessor(Color accent, float alpha, bool producing)
    {
        DrawTank(new Vector2(-39, 0), new Vector2(17, 34), accent.Darkened(0.2f), alpha, producing ? 0.55f : 0.25f);
        DrawCircle(new Vector2(18, 0), 31, new Color(Recess, alpha));
        DrawArc(new Vector2(18, 0), 31, 0, Mathf.Tau, 38, new Color(Titanium, alpha), 3, true);
        DrawRadiationMark(new Vector2(18, 0), 18, accent, alpha);
        DrawGauge(new Vector2(-39, -27), accent, alpha);
    }

    private void DrawFuelCellFabricator(Color accent, float alpha, bool producing)
    {
        var motion = producing ? Mathf.Sin(_animationSeconds * 3.4f) * 5 : 0;
        DrawMechanicalArm(new Vector2(-52, -32), new Vector2(-14 + motion, -10), accent, alpha);
        DrawMechanicalArm(new Vector2(52, 32), new Vector2(14 - motion, 10), accent, alpha);
        for (var column = -2; column <= 2; column++)
        {
            var x = column * 16f;
            DrawRect(new Rect2(x - 5, -27, 10, 54), new Color(Recess, alpha), true);
            DrawRect(new Rect2(x - 5, -27, 10, 54), new Color(accent, alpha), false, 1.8f, true);
            DrawRect(new Rect2(x - 3, -13, 6, 18), new Color(accent, 0.35f * alpha), true);
        }
    }

    private void DrawNuclearReactor(Color accent, float alpha, bool producing)
    {
        DrawCircle(Vector2.Zero, 42, new Color(Recess, alpha));
        DrawArc(Vector2.Zero, 42, 0, Mathf.Tau, 48, new Color(Titanium, alpha), 4, true);
        DrawArc(Vector2.Zero, 31, 0, Mathf.Tau, 40, new Color(accent, alpha), 3, true);
        var pulse = producing ? 0.45f + (Mathf.Sin(_animationSeconds * 4f) * 0.18f) : 0.2f;
        DrawCircle(Vector2.Zero, 15, new Color(accent, pulse * alpha));
        for (var index = 0; index < 8; index++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * index / 8f);
            DrawLine(direction * 42, direction * 59, new Color(Titanium, alpha), 8, true);
            DrawLine(direction * 47, direction * 56, new Color(accent, 0.8f * alpha), 2, true);
        }
    }

    private void DrawWasteProcessor(Color accent, float alpha, bool producing)
    {
        foreach (var x in new[] { -35f, 35f })
        {
            DrawCircle(new Vector2(x, 0), 25, new Color(Recess, alpha));
            DrawArc(new Vector2(x, 0), 25, 0, Mathf.Tau, 32,
                new Color(x < 0 ? Titanium : accent, alpha), 3, true);
        }
        DrawRadiationMark(new Vector2(-35, 0), 14, accent, alpha);
        DrawLine(new Vector2(-9, 0), new Vector2(15, 0), new Color(accent, alpha), 4, true);
        DrawLine(new Vector2(7, -7), new Vector2(16, 0), new Color(accent, alpha), 3, true);
        DrawLine(new Vector2(7, 7), new Vector2(16, 0), new Color(accent, alpha), 3, true);
        if (producing)
            DrawParticles(new Vector2(35, -12), new Vector2(18, 18), new Color(accent, alpha), 5, 1.8f);
    }

    private void DrawNuclearWasteStorage(Color accent, float alpha)
    {
        for (var column = -2; column <= 2; column++)
        {
            var x = column * 24f;
            DrawRect(new Rect2(x - 9, -30, 18, 60), new Color(Recess, alpha), true);
            DrawRect(new Rect2(x - 9, -30, 18, 60), new Color(Titanium, alpha), false, 2, true);
            DrawLine(new Vector2(x - 8, -13), new Vector2(x + 8, -13), new Color(accent, alpha), 3, true);
            DrawRadiationMark(new Vector2(x, 9), 8, accent, alpha);
        }
    }

    private void DrawRadiationMark(Vector2 center, float radius, Color accent, float alpha)
    {
        DrawCircle(center, radius * 0.2f, new Color(accent, alpha));
        for (var index = 0; index < 3; index++)
        {
            var direction = Vector2.FromAngle((-Mathf.Pi * 0.5f) + (Mathf.Tau * index / 3f));
            Vector2[] blade =
            [
                center + direction * radius * 0.36f,
                center + direction * radius,
                center + direction.Rotated(0.42f) * radius * 0.7f,
            ];
            DrawColoredPolygon(blade, new Color(accent, alpha));
        }
        DrawCircle(center, radius * 0.1f, new Color(Recess, alpha));
    }

    private void DrawTank(Vector2 center, Vector2 radius, Color accent, float alpha, float fill)
    {
        var tank = new Rect2(center - radius, radius * 2);
        DrawRect(tank, new Color(Recess, alpha), true);
        DrawRect(tank, new Color(Titanium.Darkened(0.08f), alpha), false, 2, true);
        DrawRect(new Rect2(tank.Position + new Vector2(4, tank.Size.Y * (1 - fill)),
                new Vector2(tank.Size.X - 8, (tank.Size.Y * fill) - 4)),
            new Color(accent, 0.3f * alpha), true);
        DrawLine(new Vector2(tank.Position.X, center.Y), new Vector2(tank.End.X, center.Y),
            new Color(accent, 0.48f * alpha), 1, true);
    }

    private void DrawTurbine(Vector2 center, float radius, int blades, float rotation, Color accent, float alpha)
    {
        DrawCircle(center, radius * 0.34f, new Color(Titanium, alpha));
        for (var index = 0; index < blades; index++)
        {
            var direction = Vector2.FromAngle(rotation + (Mathf.Tau * index / blades));
            var side = direction.Rotated(0.42f);
            Vector2[] blade =
            [
                center + direction * radius * 0.3f,
                center + direction * radius,
                center + side * radius * 0.65f,
            ];
            DrawColoredPolygon(blade, new Color(accent, 0.72f * alpha));
        }
        DrawCircle(center, radius * 0.2f, new Color(accent.Lightened(0.18f), alpha));
    }

    private void DrawMechanicalArm(Vector2 pivot, Vector2 target, Color accent, float alpha)
    {
        var elbow = pivot.Lerp(target, 0.55f) + new Vector2(0, (target.X - pivot.X) * 0.16f);
        DrawLine(pivot, elbow, new Color(Titanium, alpha), 4, true);
        DrawLine(elbow, target, new Color(Titanium, alpha), 4, true);
        DrawCircle(pivot, 4.5f, new Color(DarkMetal, alpha));
        DrawCircle(pivot, 2.2f, new Color(accent, alpha));
        DrawCircle(elbow, 3.2f, new Color(accent.Darkened(0.16f), alpha));
    }

    private void DrawGauge(Vector2 center, Color accent, float alpha)
    {
        DrawCircle(center, 6, new Color(Recess, alpha));
        DrawArc(center, 6, Mathf.Pi, Mathf.Tau, 12, new Color(accent, alpha), 1.5f, true);
        DrawLine(center, center + new Vector2(3.5f, -2.5f), new Color(accent, alpha), 1.3f, true);
    }

    private void DrawVents(Vector2 center, bool vertical, Color color, float alpha)
    {
        for (var index = -2; index <= 2; index++)
        {
            var offset = index * 5f;
            var from = vertical ? center + new Vector2(-3, offset) : center + new Vector2(offset, -3);
            var to = vertical ? center + new Vector2(3, offset) : center + new Vector2(offset, 3);
            DrawLine(from, to, new Color(color, alpha), 1.4f, true);
        }
    }

    private void DrawWarningStripes(Vector2 origin, float width, Color accent, float alpha)
    {
        var warningColor = accent.Lerp(Warning, 0.78f);
        for (var x = 0f; x < width; x += 8)
        {
            DrawLine(origin + new Vector2(x, 0), origin + new Vector2(x + 5, 5),
                new Color(warningColor, alpha), 2, true);
        }
    }

    private void DrawParticles(Vector2 origin, Vector2 spread, Color color, int count, float radius)
    {
        for (var index = 0; index < count; index++)
        {
            var phase = Mathf.PosMod((_animationSeconds * (0.7f + (index * 0.08f))) + (index * 0.23f), 1f);
            var offset = new Vector2(
                Mathf.Sin((phase * 8f) + index) * spread.X * 0.55f,
                (-phase * spread.Y) + (Mathf.Cos(index * 2.2f) * spread.Y * 0.16f));
            DrawCircle(origin + offset, Mathf.Lerp(radius, 0.4f, phase), new Color(color, color.A * (1 - phase)));
        }
    }

    private void DrawBubbles(Vector2 center, float width, float height, Color color, float alpha)
    {
        for (var index = 0; index < 5; index++)
        {
            var phase = Mathf.PosMod((_animationSeconds * (0.45f + (index * 0.05f))) + (index * 0.19f), 1f);
            var position = center + new Vector2(
                Mathf.Sin((index * 2.3f) + phase) * width * 0.55f,
                (height * 0.5f) - (phase * height));
            DrawArc(position, 1.4f + (index % 2), 0, Mathf.Tau, 10,
                new Color(color, (1 - phase) * 0.72f * alpha), 1, true);
        }
    }

    private void DrawFlowPulse(Vector2 from, Vector2 to, Color color)
    {
        var phase = Mathf.PosMod(_animationSeconds * 1.4f, 1f);
        DrawCircle(from.Lerp(to, phase), 2.2f, new Color(color, 0.9f));
    }

    private void DrawConstructionHologram(Vector2 half, IReadOnlyList<Vector2> silhouette)
    {
        var pulse = 0.52f + (Mathf.Sin((float)Time.GetTicksMsec() * 0.008f) * 0.18f);
        var hologram = new Color(0.12f, 0.86f, 1.0f, pulse);
        DrawPolyline(Close(silhouette), hologram, 1.2f, true);
        for (var column = -2; column <= 2; column++)
        {
            var x = half.X * column / 2.5f;
            DrawLine(new Vector2(x, -half.Y), new Vector2(x, half.Y),
                new Color(hologram, 0.18f), 0.8f, true);
        }

        var scanY = Mathf.Lerp(half.Y, -half.Y, _visualConstructionProgress);
        DrawLine(new Vector2(-half.X, scanY), new Vector2(half.X, scanY), hologram, 2.3f, true);
    }

    private static Vector2[] CreateMachineSilhouette(MachineGlyph glyph, Vector2 size)
    {
        var half = size * 0.5f;
        return glyph switch
        {
            MachineGlyph.Crusher => [new(-half.X, -half.Y + 12), new(-half.X + 12, -half.Y), new(half.X - 18, -half.Y), new(half.X, -half.Y + 18), new(half.X, half.Y - 18), new(half.X - 18, half.Y), new(-half.X + 12, half.Y), new(-half.X, half.Y - 12)],
            MachineGlyph.Smelter => [new(-half.X + 18, -half.Y), new(half.X - 18, -half.Y), new(half.X, -half.Y + 18), new(half.X, half.Y - 18), new(half.X - 18, half.Y), new(-half.X + 18, half.Y), new(-half.X, half.Y - 18), new(-half.X, -half.Y + 18)],
            MachineGlyph.Foundry => [new(-half.X, -half.Y + 12), new(-half.X + 25, -half.Y + 12), new(-half.X + 25, -half.Y), new(half.X - 14, -half.Y), new(half.X, -half.Y + 14), new(half.X, half.Y - 14), new(half.X - 14, half.Y), new(-half.X + 25, half.Y), new(-half.X + 25, half.Y - 12), new(-half.X, half.Y - 12)],
            MachineGlyph.Constructor => [new(-half.X + 12, -half.Y), new(half.X - 21, -half.Y), new(half.X - 21, -half.Y + 8), new(half.X, -half.Y + 8), new(half.X, half.Y - 8), new(half.X - 21, half.Y - 8), new(half.X - 21, half.Y), new(-half.X + 12, half.Y), new(-half.X, half.Y - 12), new(-half.X, -half.Y + 12)],
            MachineGlyph.Fabricator or MachineGlyph.Research or MachineGlyph.AdvancedFabricator or
                MachineGlyph.PrecisionManufacturer or MachineGlyph.FuelCellFabricator or MachineGlyph.NuclearReactor => [new(-half.X + 20, -half.Y), new(half.X - 20, -half.Y), new(half.X, -half.Y + 20), new(half.X, half.Y - 20), new(half.X - 20, half.Y), new(-half.X + 20, half.Y), new(-half.X, half.Y - 20), new(-half.X, -half.Y + 20)],
            MachineGlyph.WaterProcessor or MachineGlyph.Electrolyzer or MachineGlyph.ChemicalPlant or
                MachineGlyph.UraniumProcessor or MachineGlyph.WasteProcessor or MachineGlyph.LiquidTank or
                MachineGlyph.GasTank => [new(-half.X + 14, -half.Y), new(half.X - 14, -half.Y), new(half.X, -half.Y + 14), new(half.X, half.Y - 14), new(half.X - 14, half.Y), new(-half.X + 14, half.Y), new(-half.X, half.Y - 14), new(-half.X, -half.Y + 14)],
            MachineGlyph.Refinery => [new(-half.X, -half.Y + 14), new(-half.X + 20, -half.Y + 14), new(-half.X + 20, -half.Y), new(half.X - 20, -half.Y), new(half.X - 20, -half.Y + 14), new(half.X, -half.Y + 14), new(half.X, half.Y - 14), new(half.X - 20, half.Y - 14), new(half.X - 20, half.Y), new(-half.X + 20, half.Y), new(-half.X + 20, half.Y - 14), new(-half.X, half.Y - 14)],
            MachineGlyph.BasicGenerator or MachineGlyph.MobileMiner or MachineGlyph.AutomaticMiner or
                MachineGlyph.PumpStation => [new(-half.X + 12, -half.Y), new(half.X - 12, -half.Y), new(half.X, -half.Y + 12), new(half.X, half.Y - 12), new(half.X - 12, half.Y), new(-half.X + 12, half.Y), new(-half.X, half.Y - 12), new(-half.X, -half.Y + 12)],
            MachineGlyph.PowerPole => [new(-half.X + 7, -half.Y), new(half.X - 7, -half.Y), new(half.X, -half.Y + 7), new(half.X, half.Y - 7), new(half.X - 7, half.Y), new(-half.X + 7, half.Y), new(-half.X, half.Y - 7), new(-half.X, -half.Y + 7)],
            MachineGlyph.FuelGenerator => [new(-half.X, -half.Y + 10), new(-half.X + 19, -half.Y + 10), new(-half.X + 19, -half.Y), new(half.X - 13, -half.Y), new(half.X, -half.Y + 13), new(half.X, half.Y - 13), new(half.X - 13, half.Y), new(-half.X + 19, half.Y), new(-half.X + 19, half.Y - 10), new(-half.X, half.Y - 10)],
            MachineGlyph.Storage or MachineGlyph.BatteryBank or MachineGlyph.NuclearWasteStorage or
                MachineGlyph.Assembler => [new(-half.X + 9, -half.Y), new(half.X - 9, -half.Y), new(half.X, -half.Y + 9), new(half.X, half.Y - 9), new(half.X - 9, half.Y), new(-half.X + 9, half.Y), new(-half.X, half.Y - 9), new(-half.X, -half.Y + 9)],
            _ => [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)],
        };
    }

    private static Vector2[] Close(IReadOnlyList<Vector2> polygon) => [.. polygon, polygon[0]];

    private static string GetStatusText(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => "IM BAU",
        MachineOperationStatus.Disabled => "AUSGESCHALTET",
        MachineOperationStatus.NoRecipeSelected => "KEIN REZEPT",
        MachineOperationStatus.Ready => "BEREIT",
        MachineOperationStatus.Producing => "PRODUZIERT",
        MachineOperationStatus.WaitingForMaterial => "WARTET AUF MATERIAL",
        MachineOperationStatus.WaitingForEnergy => "WARTET AUF ENERGIE",
        MachineOperationStatus.OutputFull => "AUSGABE VOLL",
        _ => "STATUS UNBEKANNT",
    };

    private static Color GetStatusColor(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => new Color(0.12f, 0.86f, 1.0f),
        MachineOperationStatus.Producing => new Color(0.28f, 1.0f, 0.68f),
        MachineOperationStatus.Ready => new Color(0.35f, 0.94f, 0.67f),
        MachineOperationStatus.WaitingForMaterial or
        MachineOperationStatus.WaitingForEnergy or
        MachineOperationStatus.OutputFull => new Color(1.0f, 0.69f, 0.27f),
        _ => new Color(0.46f, 0.61f, 0.67f),
    };
}

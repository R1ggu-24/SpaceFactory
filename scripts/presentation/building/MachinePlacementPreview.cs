using Godot;
using SpaceFactory.Core.Production;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

public enum MachinePlacementFailureReason
{
    None,
    PlacementNotActive,
    FreeSpace,
    CometTooSmall,
    SurfaceUnavailable,
    OutsideBuildableArea,
    SurfaceUneven,
    MachineBlocked,
    MaterialsMissing,
}

public partial class MachinePlacementPreview : Node2D
{
    private const float MachineClearance = 6;
#if DEBUG
    private static bool _geometrySmokeCompleted;
#endif

    private Func<IReadOnlyList<AsteroidView>>? _cometProvider;
    private Func<IReadOnlyList<MachineView>>? _machineProvider;
    private Func<MachineDefinitionId, bool>? _materialAvailability;
    private MachinePresentationDefinition? _presentation;
    private MachineDefinitionId? _selectedDefinitionId;
    private AsteroidView? _targetComet;
    private Label? _reasonLabel;
    private MachineGlyphControl? _machineGlyphPreview;
    private Vector2 _lastWorldMousePosition;
    private Vector2 _snappedLocalPosition;
    private float _relativeRotation;
    private bool _hasMousePosition;

    public bool IsActive { get; private set; }

    public bool IsPlacementValid => IsActive && CurrentFailure == MachinePlacementFailureReason.None;

    public MachineDefinitionId? SelectedDefinitionId => _selectedDefinitionId;

    public AsteroidView? TargetComet => _targetComet;

    public MachinePlacementFailureReason CurrentFailure { get; private set; } =
        MachinePlacementFailureReason.PlacementNotActive;

    public string CurrentReason => GetReasonText(CurrentFailure);

    public float RelativeRotationRadians => _relativeRotation;

    public override void _Ready()
    {
        ZIndex = 50;
        Visible = false;
        EnsureMachineGlyphPreview();
        EnsureReasonLabel();
#if DEBUG
        if (!_geometrySmokeCompleted && IsHeadlessRuntime())
        {
            RunGeometrySmokeTest();
        }
#endif
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (IsActive)
        {
            Refresh();
        }
    }

    public void ConfigureWorldSources(
        Func<IReadOnlyList<AsteroidView>> cometProvider,
        Func<IReadOnlyList<MachineView>> machineProvider)
    {
        ArgumentNullException.ThrowIfNull(cometProvider);
        ArgumentNullException.ThrowIfNull(machineProvider);
        _cometProvider = cometProvider;
        _machineProvider = machineProvider;
    }

    public void Start(
        string machineDefinitionId,
        Func<MachineDefinitionId, bool> materialAvailability) =>
        Start(new MachineDefinitionId(machineDefinitionId), materialAvailability);

    public void Start(
        MachineDefinitionId machineDefinitionId,
        Func<MachineDefinitionId, bool> materialAvailability)
    {
        ArgumentNullException.ThrowIfNull(materialAvailability);
        _presentation = MachinePresentationCatalog.Instance.Get(machineDefinitionId);
        _selectedDefinitionId = machineDefinitionId;
        _materialAvailability = materialAvailability;
        _targetComet = null;
        _relativeRotation = 0;
        CurrentFailure = MachinePlacementFailureReason.FreeSpace;
        IsActive = true;
        Visible = true;
        EnsureMachineGlyphPreview();
        RefreshMachineGlyphPreview();
        EnsureReasonLabel();
        if (IsInsideTree())
        {
            Refresh();
        }
        else
        {
            RefreshReasonLabel();
            QueueRedraw();
        }
    }

    public void Cancel()
    {
        IsActive = false;
        Visible = false;
        _selectedDefinitionId = null;
        _presentation = null;
        _materialAvailability = null;
        _targetComet = null;
        _hasMousePosition = false;
        CurrentFailure = MachinePlacementFailureReason.PlacementNotActive;
        QueueRedraw();
    }

    public void Rotate(int stepDirection)
    {
        if (!IsActive || stepDirection == 0)
        {
            return;
        }

        _relativeRotation = Mathf.Wrap(
            _relativeRotation + (MachinePresentationCatalog.RotationStepRadians * stepDirection),
            -Mathf.Pi,
            Mathf.Pi);
        if (_hasMousePosition)
        {
            Refresh(_lastWorldMousePosition);
        }
    }

    public void Refresh()
    {
        if (!IsActive || !IsInsideTree())
        {
            return;
        }

        Refresh(GetGlobalMousePosition());
    }

    public void Refresh(Vector2 worldMousePosition)
    {
        if (!IsActive || _presentation is null || _selectedDefinitionId is null)
        {
            return;
        }

        _lastWorldMousePosition = worldMousePosition;
        _hasMousePosition = true;
        var comets = _cometProvider?.Invoke() ?? [];
        var machines = _machineProvider?.Invoke() ?? [];
        Evaluate(worldMousePosition, comets, machines);
        RefreshMachineGlyphPreview();
        RefreshReasonLabel();
        QueueRedraw();
    }

    public bool TryGetPlacement(out MachinePlacement? placement, out string reason)
    {
        var valid = TryGetPlacement(out placement, out _, out var failure);
        reason = GetReasonText(failure);
        return valid;
    }

    public bool TryGetPlacement(
        out MachinePlacement? placement,
        out AsteroidView? comet,
        out MachinePlacementFailureReason reason)
    {
        reason = CurrentFailure;
        comet = _targetComet;
        if (!IsPlacementValid || _targetComet is null)
        {
            placement = null;
            return false;
        }

        placement = new MachinePlacement(
            _targetComet.CometId,
            _snappedLocalPosition.X,
            _snappedLocalPosition.Y,
            _relativeRotation);
        return true;
    }

    public override void _Draw()
    {
        if (!IsActive || _presentation is null)
        {
            return;
        }

        var color = IsPlacementValid
            ? new Color(0.28f, 1.0f, 0.61f)
            : new Color(1.0f, 0.25f, 0.31f);
        var half = _presentation.Footprint * 0.5f;
        var rectangle = new Rect2(-half, _presentation.Footprint);
        DrawRect(rectangle, new Color(color, 0.16f), true);
        DrawRect(rectangle, new Color(color, 0.94f), false, 2.3f, true);

        for (var x = -half.X + MachinePresentationCatalog.PlacementGridSize;
             x < half.X;
             x += MachinePresentationCatalog.PlacementGridSize)
        {
            DrawLine(new Vector2(x, -half.Y), new Vector2(x, half.Y), new Color(color, 0.18f), 0.8f);
        }

        for (var y = -half.Y + MachinePresentationCatalog.PlacementGridSize;
             y < half.Y;
             y += MachinePresentationCatalog.PlacementGridSize)
        {
            DrawLine(new Vector2(-half.X, y), new Vector2(half.X, y), new Color(color, 0.18f), 0.8f);
        }

        DrawCircle(Vector2.Zero, 6, new Color(color, 0.75f));
        DrawLine(new Vector2(-14, 0), new Vector2(14, 0), color, 1.5f, true);
        DrawLine(new Vector2(0, -14), new Vector2(0, 14), color, 1.5f, true);
        DrawCornerBrackets(half, color);
    }

    private void Evaluate(
        Vector2 worldMousePosition,
        IReadOnlyList<AsteroidView> comets,
        IReadOnlyList<MachineView> machines)
    {
        if (_selectedDefinitionId is not { } selectedDefinitionId || _materialAvailability is null)
        {
            CurrentFailure = MachinePlacementFailureReason.PlacementNotActive;
            return;
        }

        var containingComet = comets
            .Where(comet => GodotObject.IsInstanceValid(comet) && comet.ContainsWorldPoint(worldMousePosition))
            .OrderBy(comet => comet.GlobalPosition.DistanceSquaredTo(worldMousePosition))
            .FirstOrDefault();

        if (containingComet is null)
        {
            _targetComet = null;
            _snappedLocalPosition = MachinePlacementGeometry.SnapToGrid(
                worldMousePosition,
                MachinePresentationCatalog.PlacementGridSize);
            GlobalPosition = _snappedLocalPosition;
            GlobalRotation = _relativeRotation;
            CurrentFailure = MachinePlacementFailureReason.FreeSpace;
            return;
        }

        _targetComet = containingComet;
        _snappedLocalPosition = MachinePlacementGeometry.SnapToGrid(
            containingComet.ToLocal(worldMousePosition),
            MachinePresentationCatalog.PlacementGridSize);
        GlobalPosition = containingComet.ToGlobal(_snappedLocalPosition);
        GlobalRotation = containingComet.GlobalRotation + _relativeRotation;

        var localFootprint = MachinePlacementGeometry.CreateRectangleCorners(
            _snappedLocalPosition,
            _presentation!.Footprint,
            _relativeRotation);
        var surfaceFailure = containingComet.EvaluateBuildFootprint(localFootprint);
        CurrentFailure = MapSurfaceFailure(surfaceFailure);
        if (CurrentFailure != MachinePlacementFailureReason.None)
        {
            return;
        }

        var paddedFootprint = MachinePlacementGeometry.CreateRectangleCorners(
                _snappedLocalPosition,
                _presentation.Footprint + new Vector2(MachineClearance * 2, MachineClearance * 2),
                _relativeRotation)
            .Select(containingComet.ToGlobal)
            .ToArray();
        foreach (var machine in machines)
        {
            if (!GodotObject.IsInstanceValid(machine) || machine.GetParent() != containingComet)
            {
                continue;
            }

            var existingFootprint = machine.GetWorldFootprint(MachineClearance);
            if (MachinePlacementGeometry.ConvexPolygonsOverlap(paddedFootprint, existingFootprint))
            {
                CurrentFailure = MachinePlacementFailureReason.MachineBlocked;
                return;
            }
        }

        if (!_materialAvailability(selectedDefinitionId))
        {
            CurrentFailure = MachinePlacementFailureReason.MaterialsMissing;
        }
    }

    private void EnsureReasonLabel()
    {
        if (_reasonLabel is not null)
        {
            return;
        }

        _reasonLabel = new Label
        {
            Name = "PlacementReason",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = 2,
        };
        _reasonLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_reasonLabel);
        RefreshReasonLabel();
    }

    private void EnsureMachineGlyphPreview()
    {
        if (_machineGlyphPreview is not null)
        {
            return;
        }

        _machineGlyphPreview = new MachineGlyphControl
        {
            Name = "MachineGlyphPreview",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0.72f),
            ZIndex = 1,
        };
        AddChild(_machineGlyphPreview);
    }

    private void RefreshMachineGlyphPreview()
    {
        if (_machineGlyphPreview is null || _presentation is null)
        {
            return;
        }

        var color = IsPlacementValid
            ? new Color(0.28f, 1.0f, 0.61f)
            : new Color(1.0f, 0.25f, 0.31f);
        _machineGlyphPreview.Position = _presentation.Footprint * -0.5f;
        _machineGlyphPreview.Size = _presentation.Footprint;
        _machineGlyphPreview.Configure(_presentation.Glyph, available: true, color);
    }

    private void RefreshReasonLabel()
    {
        if (_reasonLabel is null)
        {
            return;
        }

        var height = _presentation?.Footprint.Y ?? 72;
        _reasonLabel.Position = new Vector2(-130, (height * 0.5f) + 12);
        _reasonLabel.Size = new Vector2(260, 24);
        _reasonLabel.Text = IsPlacementValid ? "PLATZIERUNG BEREIT" : GetReasonText(CurrentFailure).ToUpperInvariant();
        _reasonLabel.AddThemeColorOverride(
            "font_color",
            IsPlacementValid ? new Color(0.42f, 1.0f, 0.7f) : new Color(1.0f, 0.47f, 0.49f));
    }

    private static MachinePlacementFailureReason MapSurfaceFailure(AsteroidBuildSurfaceFailure failure) => failure switch
    {
        AsteroidBuildSurfaceFailure.None => MachinePlacementFailureReason.None,
        AsteroidBuildSurfaceFailure.UnsupportedCometSize => MachinePlacementFailureReason.CometTooSmall,
        AsteroidBuildSurfaceFailure.MissingSurfaceProfile => MachinePlacementFailureReason.SurfaceUnavailable,
        AsteroidBuildSurfaceFailure.OutsideBuildableRadius or
        AsteroidBuildSurfaceFailure.OutsideOutline => MachinePlacementFailureReason.OutsideBuildableArea,
        AsteroidBuildSurfaceFailure.Crater or
        AsteroidBuildSurfaceFailure.UnevenTerrain => MachinePlacementFailureReason.SurfaceUneven,
        _ => MachinePlacementFailureReason.SurfaceUnavailable,
    };

    public static string GetReasonText(MachinePlacementFailureReason reason) => reason switch
    {
        MachinePlacementFailureReason.None => string.Empty,
        MachinePlacementFailureReason.PlacementNotActive => "Platzierungsmodus nicht aktiv",
        MachinePlacementFailureReason.FreeSpace => "Nur auf grossen Kometen baubar",
        MachinePlacementFailureReason.CometTooSmall => "Komet zu klein",
        MachinePlacementFailureReason.SurfaceUnavailable => "Oberfläche ungeeignet",
        MachinePlacementFailureReason.OutsideBuildableArea => "Nicht genügend Platz",
        MachinePlacementFailureReason.SurfaceUneven => "Krater oder unebene Fläche",
        MachinePlacementFailureReason.MachineBlocked => "Maschine blockiert",
        MachinePlacementFailureReason.MaterialsMissing => "Materialien fehlen",
        _ => "Platzierung ungültig",
    };

    private void DrawCornerBrackets(Vector2 half, Color color)
    {
        const float length = 12;
        foreach (var corner in new[]
                 {
                     new Vector2(-half.X, -half.Y),
                     new Vector2(half.X, -half.Y),
                     new Vector2(half.X, half.Y),
                     new Vector2(-half.X, half.Y),
                 })
        {
            var horizontalDirection = new Vector2(-Mathf.Sign(corner.X), 0);
            var verticalDirection = new Vector2(0, -Mathf.Sign(corner.Y));
            DrawLine(corner, corner + (horizontalDirection * length), color, 3, true);
            DrawLine(corner, corner + (verticalDirection * length), color, 3, true);
        }
    }

#if DEBUG
    public static void RunGeometrySmokeTest()
    {
        var catalog = MachinePresentationCatalog.Instance;
        var coreIds = DefaultMachineCatalog.Instance.All.Select(definition => definition.Id).OrderBy(id => id.Value).ToArray();
        var presentationIds = catalog.All.Select(definition => definition.DefinitionId).OrderBy(id => id.Value).ToArray();
        RequireGeometrySmokeCondition(coreIds.SequenceEqual(presentationIds),
            "every core machine needs exactly one presentation definition");
        RequireGeometrySmokeCondition(catalog.All.All(definition =>
                Mathf.IsZeroApprox(definition.Footprint.X % MachinePresentationCatalog.PlacementGridSize) &&
                Mathf.IsZeroApprox(definition.Footprint.Y % MachinePresentationCatalog.PlacementGridSize)),
            "machine footprints must align to the 24-unit placement grid");

        var snapped = MachinePlacementGeometry.SnapToGrid(new Vector2(37, 10), 24);
        RequireGeometrySmokeCondition(snapped == new Vector2(48, 0), "world position must snap to grid");
        var first = MachinePlacementGeometry.CreateRectangleCorners(Vector2.Zero, new Vector2(96, 72), 0);
        var overlapping = MachinePlacementGeometry.CreateRectangleCorners(new Vector2(30, 0), new Vector2(72, 72), 0);
        var separate = MachinePlacementGeometry.CreateRectangleCorners(new Vector2(120, 0), new Vector2(72, 72), 0);
        RequireGeometrySmokeCondition(MachinePlacementGeometry.ConvexPolygonsOverlap(first, overlapping),
            "overlapping machines must be detected");
        RequireGeometrySmokeCondition(!MachinePlacementGeometry.ConvexPolygonsOverlap(first, separate),
            "separated machines must remain placeable");
        RequireGeometrySmokeCondition(Mathf.IsEqualApprox(MachinePresentationCatalog.RotationStepDegrees, 10),
            "rotation step must remain ten degrees");

        _geometrySmokeCompleted = true;
        GD.Print("MACHINE_PLACEMENT_GEOMETRY_SMOKE_OK: catalog, 24-grid, 10-degree rotation, overlap SAT");
    }

    private static void RequireGeometrySmokeCondition(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Machine placement geometry smoke test failed: {message}.");
        }
    }

    private static bool IsHeadlessRuntime() =>
        OS.HasFeature("headless") ||
        DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase);
#endif
}

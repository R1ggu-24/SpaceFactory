using Godot;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.World.Exploration;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Core.World.Sectors;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.WorldMap;

public partial class WorldMapController : Control
{
    private const double RouteSampleDistance = 450;
    private const double MinimapDataRefreshDistance = 500;
    private const int MaximumRoutePoints = 320;

    private readonly List<WorldPosition> _flightRoute = [];
    private Control _mapOverlay = null!;
    private WorldMapCanvas _mapCanvas = null!;
    private CircularMinimapControl _minimap = null!;
    private NavigationTargetArrow _navigationArrow = null!;
    private Label _title = null!;
    private Label _status = null!;
    private Button _focusShipButton = null!;
    private Button _focusLastButton = null!;
    private Button _clearTargetButton = null!;
    private MapViewState _snapshot = MapViewState.Empty;
    private MapShipState _ship = MapViewState.Empty.Ship;
    private ExplorationMapService? _dataSource;
    private WorldPosition? _minimapDataCenter;
    private int _automaticMarkerNumber = 1;

    public bool IsOpen => _mapOverlay.Visible;
    public bool IsAvailable => _ship.IsInShip;
    public bool IsFollowingShip => _mapCanvas.IsFollowingShip;

    public event Action<bool>? MapVisibilityChanged;
    public event Action? MapOpenRequested;
    public event Action<string?>? TargetChanged;
    public event Action<bool>? ShipFollowingChanged;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        _mapOverlay = GetNode<Control>("MapOverlay");
        _mapCanvas = GetNode<WorldMapCanvas>("MapOverlay/Frame/MapArea");
        _minimap = GetNode<CircularMinimapControl>("Minimap");
        _navigationArrow = GetNode<NavigationTargetArrow>("NavigationArrow");
        _title = GetNode<Label>("MapOverlay/Frame/Title");
        _status = GetNode<Label>("MapOverlay/Frame/Status");
        _focusShipButton = GetNode<Button>("MapOverlay/Frame/ButtonRow/FocusShip");
        _focusLastButton = GetNode<Button>("MapOverlay/Frame/ButtonRow/FocusLast");
        _clearTargetButton = GetNode<Button>("MapOverlay/Frame/ButtonRow/ClearTarget");
        var landableFilter = GetNode<CheckButton>("MapOverlay/Frame/ButtonRow/LandableOnly");
        var closeButton = GetNode<Button>("MapOverlay/Frame/CloseButton");

        _focusShipButton.ToggleMode = true;
        _focusShipButton.Pressed += ToggleShipFollowing;
        _focusLastButton.Pressed += FocusLastDiscoveredComet;
        _clearTargetButton.Pressed += () => HandleTargetSelection(null);
        landableFilter.Toggled += _mapCanvas.SetLandableOnly;
        closeButton.Pressed += Close;
        _mapCanvas.TargetSelectionRequested += HandleTargetSelection;
        _mapCanvas.MarkerTargetSelectionRequested += HandleMarkerTargetSelection;
        _mapCanvas.MarkerPlacementRequested += HandleMarkerPlacement;
        _mapCanvas.ShipFollowingChanged += HandleShipFollowingChanged;
        _minimap.OpenMapRequested += RequestOpenFromMinimap;
        GetViewport().SizeChanged += UpdateResponsiveLayout;
        RefreshBinding();
        _mapOverlay.Visible = false;
        UpdateResponsiveLayout();
        UpdateShipFollowingButton(_mapCanvas.IsFollowingShip);
        ApplySnapshot(_snapshot);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= UpdateResponsiveLayout;
        _minimap.OpenMapRequested -= RequestOpenFromMinimap;
        _mapCanvas.ShipFollowingChanged -= HandleShipFollowingChanged;
        DetachDataSource();
    }

    /// <summary>
    /// Connects minimap and world map directly to the shared Core exploration
    /// service. This UI performs no procedural generation or hidden-world query.
    /// </summary>
    public void SetDataSource(ExplorationMapService dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (ReferenceEquals(_dataSource, dataSource))
        {
            return;
        }

        DetachDataSource();
        _dataSource = dataSource;
        _dataSource.Changed += HandleDiscoveryChanged;
        ApplySnapshot(BuildViewState());
    }

    public void SetResourceCatalog(IReadOnlyList<ResourceDefinition> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        _mapCanvas.SetResourceCatalog(resources);
        _minimap.SetResourceCatalog(resources);
    }

    /// <summary>Snapshot-only seam for construction tests and preview scenes.</summary>
    public void SetSnapshot(MapViewState snapshot) => ApplySnapshot(snapshot);

    public void SetShipState(Vector2 worldPosition, float rotationRadians, bool isInShip) =>
        SetShipState(new MapShipState(
            new WorldPosition(worldPosition.X, worldPosition.Y),
            rotationRadians,
            isInShip));

    public void SetShipState(MapShipState ship)
    {
        _ship = ship;
        var routeChanged = ship.IsInShip && ShouldSampleRoute(ship.Position);
        if (routeChanged)
        {
            _flightRoute.Add(ship.Position);
            if (_flightRoute.Count > MaximumRoutePoints)
            {
                _flightRoute.RemoveAt(0);
            }

            _snapshot = _snapshot with { FlightRoute = _flightRoute.ToArray() };
            if (IsOpen)
            {
                _mapCanvas.SetSnapshot(_snapshot);
            }
        }

        _snapshot = _snapshot with { Ship = ship };
        if (IsOpen)
        {
            _mapCanvas.SetShipState(ship);
        }

        if (ship.IsInShip && _dataSource?.TryCompleteNavigation(ship.Position) == true)
        {
            TargetChanged?.Invoke(null);
        }

        RefreshMinimapData();
        _navigationArrow.SetShipState(ship);
        if (!ship.IsInShip && IsOpen)
        {
            Close();
        }
    }

    public bool Toggle()
    {
        if (IsOpen)
        {
            Close();
            return false;
        }

        return TryOpen();
    }

    public bool TryOpen(bool centerOnShip = true)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (_dataSource is not null)
        {
            ApplySnapshot(BuildViewState());
        }

        _mapCanvas.SetShipState(_ship);
        if (centerOnShip)
        {
            _mapCanvas.CenterOnShip();
        }

        _mapCanvas.CancelPointerInteraction();
        if (IsOpen)
        {
            return true;
        }

        _mapOverlay.Visible = true;
        MapVisibilityChanged?.Invoke(true);
        return true;
    }

    public void Open() => TryOpen();

    /// <summary>
    /// Requests opening through the owning primary-UI coordinator. This is the
    /// single seam used by the clickable minimap and avoids bypassing inventory
    /// or pause-menu state transitions.
    /// </summary>
    public void RequestOpenFromMinimap()
    {
        if (!IsAvailable || IsOpen)
        {
            return;
        }

        _mapCanvas.CenterOnShip();
        MapOpenRequested?.Invoke();
    }

    public void SetShipFollowing(bool enabled) => _mapCanvas.SetShipFollowing(enabled);

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _mapCanvas.CancelPointerInteraction();
        _mapOverlay.Visible = false;
        MapVisibilityChanged?.Invoke(false);
    }

    public void RefreshBinding()
    {
        if (_title is not null)
        {
            _title.Text = $"STERNENKARTE  |  {InputBindingFormatter.FormatAction("open_map")} SCHLIESSEN";
        }
    }

    // Compatibility with the existing bootstrap. Coordinates stay internal and
    // are deliberately no longer drawn as a substitute for actual map content.
    public void SetCurrentSector(SectorCoordinate coordinate) =>
        AccessibilityDescription = $"Aktueller Sektor {coordinate.X}, {coordinate.Y}";

    private MapViewState BuildViewState()
    {
        if (_dataSource is null)
        {
            return _snapshot with { Ship = _ship, FlightRoute = _flightRoute.ToArray() };
        }

        return new MapViewState(
            _dataSource.ScannedChunks,
            _dataSource.DiscoveredComets,
            _dataSource.DiscoveredResources,
            _dataSource.Markers,
            _dataSource.SectorSize,
            _flightRoute.ToArray(),
            _ship,
            _dataSource.SelectedTargetCometId,
            _dataSource.SelectedTargetMarkerId,
            _dataSource.LastDiscoveredComet?.Id);
    }

    private void ApplySnapshot(MapViewState snapshot)
    {
        _snapshot = snapshot;
        _ship = snapshot.Ship;
        _mapCanvas.SetSnapshot(snapshot);
        RefreshMinimapData(force: true);
        _navigationArrow.SetSnapshot(snapshot);
        _status.Text = $"{snapshot.Chunks.Count} BEREICHE GESCANNT  |  {snapshot.Comets.Count} KOMETEN ENTDECKT";
        _focusLastButton.Disabled = snapshot.FindComet(snapshot.LastDiscoveredCometId) is null;
        _clearTargetButton.Disabled = !snapshot.HasActiveTarget;
    }

    private void HandleDiscoveryChanged() => ApplySnapshot(BuildViewState());

    private void HandleTargetSelection(string? cometId)
    {
        if (_dataSource is not null)
        {
            if (!_dataSource.SelectTarget(cometId))
            {
                return;
            }
        }
        else
        {
            _snapshot = _snapshot with
            {
                ActiveTargetCometId = cometId,
                ActiveTargetMarkerId = null,
            };
            ApplySnapshot(_snapshot);
        }

        TargetChanged?.Invoke(cometId);
    }

    private void HandleMarkerTargetSelection(string? markerId)
    {
        if (_dataSource is not null)
        {
            if (!_dataSource.SelectMarkerTarget(markerId))
            {
                return;
            }
        }
        else
        {
            _snapshot = _snapshot with
            {
                ActiveTargetCometId = null,
                ActiveTargetMarkerId = markerId,
            };
            ApplySnapshot(_snapshot);
        }

        TargetChanged?.Invoke(markerId);
    }

    private void HandleMarkerPlacement(WorldPosition position)
    {
        if (_dataSource is null)
        {
            return;
        }

        var removalRadius = 12.0 / Math.Max(_mapCanvas.Zoom, 0.001f);
        var closestMarker = _dataSource.Markers
            .Select(marker => new
            {
                Marker = marker,
                DistanceSquared = DistanceSquared(marker.WorldPosition, position),
            })
            .OrderBy(candidate => candidate.DistanceSquared)
            .FirstOrDefault();
        if (closestMarker is not null &&
            closestMarker.DistanceSquared <= removalRadius * removalRadius)
        {
            _dataSource.RemoveMarker(closestMarker.Marker.Id);
            return;
        }

        var number = _automaticMarkerNumber++;
        _dataSource.TryAddMarker(
            $"player-marker:{number}",
            $"Marker {number}",
            position,
            "pin",
            "#48CFFF",
            out _);
    }

    private static double DistanceSquared(WorldPosition first, WorldPosition second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private void UpdateResponsiveLayout()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var diameter = Mathf.Clamp(Mathf.Min(viewportSize.X, viewportSize.Y) * 0.24f, 176, 248);
        var margin = Mathf.Clamp(Mathf.Min(viewportSize.X, viewportSize.Y) * 0.025f, 14, 24);
        _minimap.OffsetLeft = -diameter - margin;
        _minimap.OffsetTop = -diameter - margin;
        _minimap.OffsetRight = -margin;
        _minimap.OffsetBottom = -margin;
    }

    private void RefreshMinimapData(bool force = false)
    {
        if (!_ship.IsInShip)
        {
            _minimapDataCenter = null;
            _minimap.SetShipState(_ship);
            return;
        }

        if (_dataSource is null)
        {
            _minimap.SetSnapshot(_snapshot);
            return;
        }

        if (!force && _minimapDataCenter is { } previous &&
            DistanceSquared(previous, _ship.Position) <
            MinimapDataRefreshDistance * MinimapDataRefreshDistance)
        {
            _minimap.SetShipState(_ship);
            return;
        }

        var nearbyChunks = _dataSource.GetScannedChunksAround(
            _ship.Position,
            CircularMinimapControl.DefaultScanRadius);
        var nearbySnapshot = _snapshot with
        {
            Chunks = nearbyChunks,
            Comets = nearbyChunks.SelectMany(chunk => chunk.Comets).ToArray(),
            Ship = _ship,
        };
        _minimapDataCenter = _ship.Position;
        _minimap.SetSnapshot(nearbySnapshot);
    }

    private void FocusLastDiscoveredComet() =>
        _mapCanvas.FocusObject(_snapshot.LastDiscoveredCometId);

    private void ToggleShipFollowing() =>
        _mapCanvas.SetShipFollowing(!_mapCanvas.IsFollowingShip);

    private void HandleShipFollowingChanged(bool isFollowing)
    {
        UpdateShipFollowingButton(isFollowing);
        ShipFollowingChanged?.Invoke(isFollowing);
    }

    private void UpdateShipFollowingButton(bool isFollowing)
    {
        _focusShipButton.SetPressedNoSignal(isFollowing);
        _focusShipButton.Text = isFollowing
            ? "RAUMSCHIFF WIRD VERFOLGT"
            : "ZUM RAUMSCHIFF";
        _focusShipButton.TooltipText = isFollowing
            ? "Verfolgung deaktivieren"
            : "Raumschiff dauerhaft verfolgen";
    }

    private bool ShouldSampleRoute(WorldPosition position)
    {
        if (_flightRoute.Count == 0)
        {
            return true;
        }

        var previous = _flightRoute[^1];
        var deltaX = position.X - previous.X;
        var deltaY = position.Y - previous.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) >= RouteSampleDistance * RouteSampleDistance;
    }

    private void DetachDataSource()
    {
        if (_dataSource is null)
        {
            return;
        }

        _dataSource.Changed -= HandleDiscoveryChanged;
        _dataSource = null;
        _minimapDataCenter = null;
    }
}

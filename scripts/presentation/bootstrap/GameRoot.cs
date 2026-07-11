using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Common;
using SpaceFactory.Core.Player;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Resources;
using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;
using SpaceFactory.Infrastructure.Data;
using SpaceFactory.Infrastructure.Persistence;
using SpaceFactory.Presentation.Player;
using SpaceFactory.Presentation.Settings;
using SpaceFactory.Presentation.Ship;
using SpaceFactory.Presentation.UI;
using SpaceFactory.Presentation.World;
using SpaceFactory.Presentation.WorldMap;

namespace SpaceFactory.Presentation.Bootstrap;

public partial class GameRoot : Node
{
    private const long Seed = 741029384;
    private const int SectorSize = 5000;
    private const int StreamingRadius = 2;
    private const double StreamingCheckIntervalSeconds = 0.2;
    private static readonly WorldGenerationSettings GenerationSettings = new(
        SectorSize,
        0,
        8,
        new Dictionary<AsteroidSize, double>
        {
            [AsteroidSize.Tiny] = 0.20,
            [AsteroidSize.Small] = 0.45,
            [AsteroidSize.Medium] = 0.25,
            [AsteroidSize.Large] = 0.09,
            [AsteroidSize.Huge] = 0.01,
        },
        MinimumCometSpacing: 180,
        FieldCellSizeInSectors: 16,
        FieldSpawnChance: 0.68,
        MinimumFieldRadiusInSectors: 1.5,
        MaximumFieldRadiusInSectors: 3.8,
        MinimumFieldGapInSectors: 3,
        MaximumFieldGapInSectors: 7,
        LoneCometChancePerSector: 0.018,
        ExtremeCometSeparationInSectors: 6,
        EnableStartingDiscoveryField: true,
        StartingFieldCenterSectorX: 2.75,
        StartingFieldCenterSectorY: 0.5,
        StartingFieldMajorRadiusInSectors: 2.2,
        StartingFieldMinorRadiusInSectors: 1.45,
        StartingFieldIntensity: 0.85,
        StartingSafeRadius: 900,
        StartingSafeCenterX: SectorSize / 2.0,
        StartingSafeCenterY: SectorSize / 2.0);
    private readonly DeterministicWorldGenerator _generator = new();
    private readonly Dictionary<SectorCoordinate, SectorView> _loadedSectors = [];
    private readonly Inventory _astronautInventory = new(240);
    private Node2D _sectorContainer = null!;
    private PlayerShipController _ship = null!;
    private OnFootPlayerController _onFootPlayer = null!;
    private DebugOverlay _overlay = null!;
    private WorldMapController _worldMap = null!;
    private ResourceHud _resourceHud = null!;
    private SettingsMenuController _settingsMenu = null!;
    private IReadOnlyList<ResourceDefinition> _resourceCatalog = [];
    private JsonResourceStateStore _resourceStateStore = null!;
    private SectorCoordinate _currentSector;
    private SectorCoordinate _streamingCenter;
    private double _streamingCheckElapsed;
    private bool _canEnterShip;

    public override void _Ready()
    {
        _sectorContainer = GetNode<Node2D>("World/Sectors");
        _ship = GetNode<PlayerShipController>("World/PlayerShip");
        _onFootPlayer = GetNode<OnFootPlayerController>("World/OnFootPlayer");
        _overlay = GetNode<DebugOverlay>("DebugOverlay");
        _worldMap = GetNode<WorldMapController>("WorldMap/Controller");
        _resourceHud = GetNode<ResourceHud>("ResourceHud");
        _settingsMenu = GetNode<SettingsMenuController>("SettingsMenu");
        _settingsMenu.Closed += HandleSettingsClosed;
        _settingsMenu.InputBindingsChanged += HandleInputBindingsChanged;
        _resourceCatalog = new JsonResourceCatalogLoader().Load("res://data/resources/resource_definitions.json");
        _resourceStateStore = new JsonResourceStateStore();
        _onFootPlayer.Initialize(
            _astronautInventory,
            _resourceCatalog,
            _resourceHud,
            () => _worldMap.IsOpen || _settingsMenu.IsOpen);
        LoadAround(new SectorCoordinate(0, 0), new SectorCoordinate(0, 0));
        UpdateUi();
#if DEBUG
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            RunSettingsSmokeTest();
        }
#endif
    }

    public override void _Process(double delta)
    {
        if (_settingsMenu.IsOpen)
        {
            return;
        }

        RefreshShipInteractionState();
        _streamingCheckElapsed += delta;
        if (_streamingCheckElapsed < StreamingCheckIntervalSeconds)
        {
            return;
        }

        _streamingCheckElapsed = 0;
        var activePosition = _onFootPlayer.IsControlActive ? _onFootPlayer.GlobalPosition : _ship.GlobalPosition;
        var coordinate = new SectorCoordinate(
            Mathf.FloorToInt(activePosition.X / SectorSize),
            Mathf.FloorToInt(activePosition.Y / SectorSize));
        var streamingCenter = GetStreamingCenter(coordinate);
        if (coordinate != _currentSector || streamingCenter != _streamingCenter)
        {
            LoadAround(coordinate, streamingCenter);
            UpdateUi();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsSingleActionPress(@event))
        {
            return;
        }

        if (_settingsMenu.IsOpen)
        {
            return;
        }

        if (@event.IsActionPressed("pause"))
        {
            OpenSettings();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("exit_ship") && _ship.IsControlActive && !_worldMap.IsOpen)
        {
            TryExitShip();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("enter_ship") && _onFootPlayer.IsControlActive && !_worldMap.IsOpen)
        {
            RefreshShipInteractionState();
            TryEnterShip();
            GetViewport().SetInputAsHandled();
        }
    }

    private void LoadAround(SectorCoordinate currentSector, SectorCoordinate streamingCenter)
    {
        _currentSector = currentSector;
        _streamingCenter = streamingCenter;
        var required = new HashSet<SectorCoordinate>();
        for (var y = streamingCenter.Y - StreamingRadius; y <= streamingCenter.Y + StreamingRadius; y++)
        {
            for (var x = streamingCenter.X - StreamingRadius; x <= streamingCenter.X + StreamingRadius; x++)
            {
                var coordinate = new SectorCoordinate(x, y);
                required.Add(coordinate);
                if (_loadedSectors.ContainsKey(coordinate))
                {
                    continue;
                }

                var sector = new SectorView { Name = $"Sector_{x}_{y}" };
                _sectorContainer.AddChild(sector);
                sector.Display(
                    _generator.Generate(CreateRequest(coordinate)),
                    SectorSize,
                    _resourceCatalog,
                    _resourceStateStore);
                _loadedSectors.Add(coordinate, sector);
            }
        }

        foreach (var coordinate in _loadedSectors.Keys.Where(key => !required.Contains(key)).ToArray())
        {
            _loadedSectors[coordinate].QueueFree();
            _loadedSectors.Remove(coordinate);
        }

        UpdateCollisionDetails(currentSector);
    }

    private static SectorGenerationRequest CreateRequest(SectorCoordinate coordinate) => new(
        new WorldSeed(Seed),
        coordinate,
        GenerationSettings);

    private SectorCoordinate GetStreamingCenter(SectorCoordinate currentSector)
    {
        if (!_ship.IsControlActive || _ship.Velocity.LengthSquared() < 1)
        {
            return currentSector;
        }

        var direction = _ship.Velocity.Normalized();
        return new SectorCoordinate(
            currentSector.X + Math.Sign(direction.X),
            currentSector.Y + Math.Sign(direction.Y));
    }

    private void UpdateUi()
    {
        _overlay.UpdateSector(Seed, _currentSector);
        _overlay.UpdateControlMode(_onFootPlayer.IsControlActive);
        _worldMap.SetCurrentSector(_currentSector);
    }

    private void UpdateCollisionDetails(SectorCoordinate currentSector)
    {
        foreach (var pair in _loadedSectors)
        {
            var distance = Math.Max(
                Math.Abs(pair.Key.X - currentSector.X),
                Math.Abs(pair.Key.Y - currentSector.Y));
            pair.Value.SetDetailedCollisions(distance <= 1);
        }
    }

    private void TryExitShip()
    {
        var safePosition = FindSafeExitPosition();
        if (safePosition is null)
        {
            _resourceHud.ShowMessage("Kein sicherer Platz zum Aussteigen");
            return;
        }

        _onFootPlayer.GlobalPosition = safePosition.Value;
        _onFootPlayer.Rotation = _ship.Rotation;
        _ship.SetControlActive(false);
        _onFootPlayer.SetControlActive(true);
        RefreshShipInteractionState();
        UpdateUi();
    }

    private void TryEnterShip()
    {
        if (!_canEnterShip)
        {
            return;
        }

        _canEnterShip = false;
        _resourceHud.SetInteractionPrompt(null);
        _onFootPlayer.SetControlActive(false);
        _ship.SetControlActive(true);
        UpdateUi();
    }

    private bool CanEnterShip() => ShipInteractionRules.CanEnterShip(
        _onFootPlayer.IsControlActive,
        _onFootPlayer.IsMining,
        new WorldPosition(_onFootPlayer.GlobalPosition.X, _onFootPlayer.GlobalPosition.Y),
        new WorldPosition(_ship.CockpitEntryPosition.X, _ship.CockpitEntryPosition.Y),
        _ship.CockpitEntryRadius);

    private void RefreshShipInteractionState()
    {
        _canEnterShip = CanEnterShip() && !_worldMap.IsOpen && !_settingsMenu.IsOpen;
        _resourceHud.SetInteractionPrompt(_canEnterShip
            ? $"{InputBindingFormatter.FormatAction("enter_ship")} drücken, um in das Raumschiff einzusteigen"
            : null);
    }

    private void OpenSettings()
    {
        _worldMap.Close();
        _onFootPlayer.InterruptCurrentAction();
        _resourceHud.SetInteractionPrompt(null);
        _settingsMenu.Open();
        GetTree().Paused = true;
        _overlay.SetPaused(false);
    }

    private void HandleSettingsClosed()
    {
        GetTree().Paused = false;
        RefreshShipInteractionState();
    }

    private void HandleInputBindingsChanged()
    {
        _overlay.RefreshBindings();
        _worldMap.RefreshBinding();
        RefreshShipInteractionState();
    }

#if DEBUG
    private void RunSettingsSmokeTest()
    {
        GetTree().Paused = true;
        _settingsMenu.RunConstructionSmokeTest();
        _settingsMenu.Open();
        GetTree().Paused = true;
    }
#endif

    private static bool IsSingleActionPress(InputEvent @event) =>
        @event is not InputEventKey keyEvent || !keyEvent.Echo;

    private Vector2? FindSafeExitPosition()
    {
        var spaceState = GetViewport().World2D.DirectSpaceState;
        var cockpitPosition = _ship.CockpitEntryPosition;
        var distances = new[]
        {
            _ship.CockpitEntryRadius + 2,
            _ship.CockpitEntryRadius + 22,
            _ship.CockpitEntryRadius + 44,
        };
        for (var distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
        {
            for (var directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                var angle = _ship.Rotation + (Mathf.Tau * directionIndex / 8.0f);
                var candidate = cockpitPosition + (Vector2.FromAngle(angle) * distances[distanceIndex]);
                var query = new PhysicsShapeQueryParameters2D
                {
                    Shape = new CircleShape2D { Radius = 24 },
                    Transform = new Transform2D(0, candidate),
                    CollisionMask = 1u | ResourceDepositView.ResourceCollisionLayer,
                    CollideWithAreas = true,
                    CollideWithBodies = true,
                };
                if (spaceState.IntersectShape(query, 1).Count == 0)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}

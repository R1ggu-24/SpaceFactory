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
using SpaceFactory.Presentation.InventoryUI;
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
        FieldSpawnChance: WorldGenerationDefaults.FieldSpawnChance,
        MinimumFieldRadiusInSectors: 1.5,
        MaximumFieldRadiusInSectors: 3.8,
        MinimumFieldGapInSectors: 3,
        MaximumFieldGapInSectors: 7,
        LoneCometChancePerSector: WorldGenerationDefaults.LoneCometChancePerSector,
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
    private readonly SlotInventory _astronautInventory = new(InventoryConfiguration.AstronautSlotCount);
    private readonly SlotInventory _shipInventory = new(InventoryConfiguration.ShipSlotCount);
    private readonly ShipInteractionPressGate _shipInteractionPressGate = new();
    private Node2D _sectorContainer = null!;
    private PlayerShipController _ship = null!;
    private OnFootPlayerController _onFootPlayer = null!;
    private DebugOverlay _overlay = null!;
    private WorldMapController _worldMap = null!;
    private ResourceHud _resourceHud = null!;
    private InventoryMenuController _inventoryMenu = null!;
    private SettingsMenuController _settingsMenu = null!;
    private IReadOnlyList<ResourceDefinition> _resourceCatalog = [];
    private JsonResourceStateStore _resourceStateStore = null!;
    private SectorCoordinate _currentSector;
    private SectorCoordinate _streamingCenter;
    private double _streamingCheckElapsed;
    private PlayerControlMode _controlMode = PlayerControlMode.Ship;
    private ShipInteractionAction _availableShipInteraction;

    public override void _Ready()
    {
        _sectorContainer = GetNode<Node2D>("World/Sectors");
        _ship = GetNode<PlayerShipController>("World/PlayerShip");
        _onFootPlayer = GetNode<OnFootPlayerController>("World/OnFootPlayer");
        _overlay = GetNode<DebugOverlay>("DebugOverlay");
        _worldMap = GetNode<WorldMapController>("WorldMap/Controller");
        _resourceHud = GetNode<ResourceHud>("ResourceHud");
        _inventoryMenu = GetNode<InventoryMenuController>("InventoryMenu");
        _settingsMenu = GetNode<SettingsMenuController>("SettingsMenu");
        _inventoryMenu.Closed += HandleInventoryClosed;
        _settingsMenu.Closed += HandleSettingsClosed;
        _settingsMenu.InputBindingsChanged += HandleInputBindingsChanged;
        _resourceCatalog = new JsonResourceCatalogLoader().Load("res://data/resources/resource_definitions.json");
        _resourceStateStore = new JsonResourceStateStore();
        _inventoryMenu.Initialize(_astronautInventory, _shipInventory, _resourceCatalog);
        _onFootPlayer.Initialize(
            _astronautInventory,
            _resourceCatalog,
            _resourceHud,
            IsGameplayInputBlocked);
        SetControlMode(PlayerControlMode.Ship, updateUi: false);
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
        _shipInteractionPressGate.Advance(delta);
        if (_settingsMenu.IsOpen || _inventoryMenu.IsOpen)
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
        if (@event.IsActionReleased("ship_interaction"))
        {
            _shipInteractionPressGate.Release();
            return;
        }

        if (!IsSingleActionPress(@event))
        {
            return;
        }

        if (_inventoryMenu.IsOpen)
        {
            if (@event.IsActionPressed("inventory") || IsEscapePress(@event))
            {
                _inventoryMenu.Close();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (_settingsMenu.IsOpen)
        {
            return;
        }

        if (@event.IsActionPressed("inventory"))
        {
            OpenInventory();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("pause"))
        {
            OpenSettings();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ship_interaction"))
        {
            RefreshShipInteractionState();
            if (_availableShipInteraction != ShipInteractionAction.None)
            {
                if (_shipInteractionPressGate.TryPress())
                {
                    ExecuteShipInteraction();
                }

                GetViewport().SetInputAsHandled();
            }
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
        if (_availableShipInteraction != ShipInteractionAction.ExitShip)
        {
            return;
        }

        var safePosition = FindSafeExitPosition();
        if (safePosition is null)
        {
            _resourceHud.ShowMessage("Kein sicherer Platz zum Aussteigen");
            return;
        }

        _onFootPlayer.GlobalPosition = safePosition.Value;
        _onFootPlayer.Rotation = _ship.Rotation;
        SetControlMode(PlayerControlMode.OnFoot);
    }

    private void TryEnterShip()
    {
        if (_availableShipInteraction != ShipInteractionAction.EnterShip)
        {
            return;
        }

        SetControlMode(PlayerControlMode.Ship);
    }

    private void ExecuteShipInteraction()
    {
        switch (_availableShipInteraction)
        {
            case ShipInteractionAction.EnterShip:
                TryEnterShip();
                break;
            case ShipInteractionAction.ExitShip:
                TryExitShip();
                break;
        }
    }

    private void SetControlMode(PlayerControlMode mode, bool updateUi = true)
    {
        _controlMode = mode;
        _ship.SetControlActive(mode == PlayerControlMode.Ship);
        _onFootPlayer.SetControlActive(mode == PlayerControlMode.OnFoot);
        RefreshShipInteractionState();
        if (updateUi)
        {
            UpdateUi();
        }
    }

    private void RefreshShipInteractionState(bool forcePromptUpdate = false)
    {
        var availableAction = ShipInteractionRules.GetAvailableAction(
            _controlMode,
            _onFootPlayer.IsMining,
            IsGameplayInputBlocked(),
            new WorldPosition(_onFootPlayer.GlobalPosition.X, _onFootPlayer.GlobalPosition.Y),
            new WorldPosition(_ship.CockpitEntryPosition.X, _ship.CockpitEntryPosition.Y),
            _ship.CockpitEntryRadius);
        if (!forcePromptUpdate && availableAction == _availableShipInteraction)
        {
            return;
        }

        _availableShipInteraction = availableAction;
        var binding = InputBindingFormatter.FormatAction("ship_interaction");
        _resourceHud.SetInteractionPrompt(availableAction switch
        {
            ShipInteractionAction.EnterShip => $"{binding} drücken, um einzusteigen",
            ShipInteractionAction.ExitShip => $"{binding} drücken, um auszusteigen",
            _ => null,
        });
    }

    private void OpenSettings()
    {
        _shipInteractionPressGate.Reset();
        _worldMap.Close();
        _onFootPlayer.InterruptCurrentAction();
        _settingsMenu.Open();
        RefreshShipInteractionState();
        GetTree().Paused = true;
        _overlay.SetPaused(false);
    }

    private void OpenInventory()
    {
        _shipInteractionPressGate.Reset();
        _worldMap.Close();
        _onFootPlayer.InterruptCurrentAction();
        if (_controlMode == PlayerControlMode.Ship)
        {
            _inventoryMenu.OpenShipInventory();
        }
        else
        {
            _inventoryMenu.OpenAstronautInventory();
        }

        RefreshShipInteractionState(forcePromptUpdate: true);
        GetTree().Paused = true;
        _overlay.SetPaused(false);
    }

    private void HandleInventoryClosed()
    {
        GetTree().Paused = false;
        RefreshShipInteractionState(forcePromptUpdate: true);
    }

    private void HandleSettingsClosed()
    {
        GetTree().Paused = false;
        RefreshShipInteractionState();
    }

    private void HandleInputBindingsChanged()
    {
        _shipInteractionPressGate.Reset();
        _overlay.RefreshBindings();
        _worldMap.RefreshBinding();
        RefreshShipInteractionState(forcePromptUpdate: true);
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

    private bool IsGameplayInputBlocked() =>
        _worldMap.IsOpen || _settingsMenu.IsOpen || _inventoryMenu.IsOpen;

    private static bool IsEscapePress(InputEvent @event) =>
        @event is InputEventKey { Pressed: true, Echo: false } keyEvent &&
        (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape);

    private Vector2? FindSafeExitPosition()
    {
        var spaceState = GetViewport().World2D.DirectSpaceState;
        var cockpitPosition = _ship.CockpitEntryPosition;
        var distances = new[]
        {
            _ship.CockpitExitClearance,
            _ship.CockpitExitClearance + 22,
            _ship.CockpitExitClearance + 44,
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

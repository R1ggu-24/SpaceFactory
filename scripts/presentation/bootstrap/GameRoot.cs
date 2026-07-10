using Godot;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Seeds;
using SpaceFactory.Core.World.Sectors;
using SpaceFactory.Presentation.UI;
using SpaceFactory.Presentation.World;
using SpaceFactory.Presentation.WorldMap;

namespace SpaceFactory.Presentation.Bootstrap;

public partial class GameRoot : Node
{
    private const long Seed = 741029384;
    private const int SectorSize = 5000;
    private readonly DeterministicWorldGenerator _generator = new();
    private readonly Dictionary<SectorCoordinate, SectorView> _loadedSectors = [];
    private Node2D _sectorContainer = null!;
    private Node2D _ship = null!;
    private DebugOverlay _overlay = null!;
    private WorldMapController _worldMap = null!;
    private SectorCoordinate _currentSector;

    public override void _Ready()
    {
        _sectorContainer = GetNode<Node2D>("World/Sectors");
        _ship = GetNode<Node2D>("World/PlayerShip");
        _overlay = GetNode<DebugOverlay>("DebugOverlay");
        _worldMap = GetNode<WorldMapController>("WorldMap");
        LoadAround(new SectorCoordinate(0, 0));
        UpdateUi();
    }

    public override void _Process(double delta)
    {
        var coordinate = new SectorCoordinate(
            Mathf.FloorToInt(_ship.GlobalPosition.X / SectorSize),
            Mathf.FloorToInt(_ship.GlobalPosition.Y / SectorSize));
        if (coordinate != _currentSector)
        {
            LoadAround(coordinate);
            UpdateUi();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            GetTree().Paused = !GetTree().Paused;
            _overlay.SetPaused(GetTree().Paused);
            GetViewport().SetInputAsHandled();
        }
    }

    private void LoadAround(SectorCoordinate center)
    {
        _currentSector = center;
        var required = new HashSet<SectorCoordinate>();
        for (var y = center.Y - 1; y <= center.Y + 1; y++)
        {
            for (var x = center.X - 1; x <= center.X + 1; x++)
            {
                var coordinate = new SectorCoordinate(x, y);
                required.Add(coordinate);
                if (_loadedSectors.ContainsKey(coordinate))
                {
                    continue;
                }

                var sector = new SectorView { Name = $"Sector_{x}_{y}" };
                _sectorContainer.AddChild(sector);
                sector.Display(_generator.Generate(CreateRequest(coordinate)), SectorSize);
                _loadedSectors.Add(coordinate, sector);
            }
        }

        foreach (var coordinate in _loadedSectors.Keys.Where(key => !required.Contains(key)).ToArray())
        {
            _loadedSectors[coordinate].QueueFree();
            _loadedSectors.Remove(coordinate);
        }
    }

    private static SectorGenerationRequest CreateRequest(SectorCoordinate coordinate) => new(
        new WorldSeed(Seed),
        coordinate,
        new WorldGenerationSettings(
            SectorSize,
            3,
            12,
            new Dictionary<AsteroidSize, double>
            {
                [AsteroidSize.Tiny] = 0.25,
                [AsteroidSize.Small] = 0.35,
                [AsteroidSize.Medium] = 0.25,
                [AsteroidSize.Large] = 0.12,
                [AsteroidSize.Huge] = 0.03,
            }));

    private void UpdateUi()
    {
        _overlay.UpdateSector(Seed, _currentSector);
        _worldMap.SetCurrentSector(_currentSector);
    }
}

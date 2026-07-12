using Godot;
using SpaceFactory.Application.Exploration;
using SpaceFactory.Core.World.Generation;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.World;

public partial class SectorView : Node2D
{
    private readonly List<AsteroidView> _cometViews = [];
    private readonly List<ResourceDepositView> _resourceViews = [];
    private readonly ResourceDepositGenerator _resourceGenerator = new();
    private bool? _detailedCollisions;

    public void Display(
        GeneratedSector sector,
        int sectorSize,
        IReadOnlyList<ResourceDefinition> resourceCatalog,
        IResourceStateStore resourceStateStore)
    {
        var resourcesById = resourceCatalog.ToDictionary(resource => resource.Id);
        foreach (var asteroid in sector.Asteroids)
        {
            var view = new AsteroidView
            {
                Name = $"Comet_{asteroid.Id.Replace(':', '_')}",
                Position = new Vector2((float)asteroid.Position.X, (float)asteroid.Position.Y),
            };
            view.Configure(asteroid);
            _cometViews.Add(view);
            AddChild(view);

            foreach (var deposit in _resourceGenerator.Generate(asteroid, resourceCatalog))
            {
                if (!resourcesById.TryGetValue(deposit.ResourceId, out var resource))
                {
                    continue;
                }

                var depositView = new ResourceDepositView
                {
                    Name = $"Resource_{deposit.Id.Replace(':', '_')}",
                };
                depositView.Configure(
                    deposit,
                    resource,
                    (float)asteroid.Radius,
                    sector.Coordinate.X,
                    sector.Coordinate.Y,
                    resourceStateStore);
                _resourceViews.Add(depositView);
                view.AddChild(depositView);
            }
        }

        Position = new Vector2(sector.Coordinate.X * sectorSize, sector.Coordinate.Y * sectorSize);
    }

    public void SetDetailedCollisions(bool detailed)
    {
        if (_detailedCollisions == detailed)
        {
            return;
        }

        _detailedCollisions = detailed;
        foreach (var comet in _cometViews)
        {
            comet.SetDetailedCollision(detailed);
        }

        for (var index = _resourceViews.Count - 1; index >= 0; index--)
        {
            var resource = _resourceViews[index];
            if (!GodotObject.IsInstanceValid(resource))
            {
                _resourceViews.RemoveAt(index);
                continue;
            }

            resource.SetInteractionActive(detailed);
        }
    }
}

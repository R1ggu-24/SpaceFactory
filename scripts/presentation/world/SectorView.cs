using Godot;
using SpaceFactory.Core.World.Generation;

namespace SpaceFactory.Presentation.World;

public partial class SectorView : Node2D
{
    public void Display(GeneratedSector sector, int sectorSize)
    {
        foreach (var asteroid in sector.Asteroids)
        {
            var view = new AsteroidView
            {
                Name = $"Asteroid_{asteroid.Id.Replace(':', '_')}",
                Position = new Vector2((float)asteroid.Position.X, (float)asteroid.Position.Y),
                Radius = (float)asteroid.Radius,
                Color = asteroid.ResourceType.Value == "iron_ore"
                    ? new Color(0.37f, 0.42f, 0.5f)
                    : new Color(0.48f, 0.31f, 0.24f),
            };
            AddChild(view);
        }

        Position = new Vector2(sector.Coordinate.X * sectorSize, sector.Coordinate.Y * sectorSize);
    }
}

using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Generation;

public sealed record WorldGenerationSettings(
    int SectorSize,
    int MinimumAsteroidsPerSector,
    int MaximumAsteroidsPerSector,
    IReadOnlyDictionary<AsteroidSize, double> AsteroidSizeWeights)
{
    public void Validate()
    {
        if (SectorSize <= 0 || MinimumAsteroidsPerSector < 0 ||
            MaximumAsteroidsPerSector < MinimumAsteroidsPerSector)
        {
            throw new ArgumentException("World generation ranges are invalid.");
        }

        if (AsteroidSizeWeights.Count == 0 || AsteroidSizeWeights.Any(pair => pair.Value < 0) ||
            AsteroidSizeWeights.Values.Sum() <= 0)
        {
            throw new ArgumentException("Asteroid weights are invalid.");
        }
    }
}

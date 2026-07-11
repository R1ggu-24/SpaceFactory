using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Generation;

public sealed record WorldGenerationSettings(
    int SectorSize,
    int MinimumAsteroidsPerSector,
    int MaximumAsteroidsPerSector,
    IReadOnlyDictionary<AsteroidSize, double> AsteroidSizeWeights,
    double MinimumCometSpacing = 120,
    int FieldCellSizeInSectors = 16,
    double FieldSpawnChance = 0.68,
    double MinimumFieldRadiusInSectors = 1.5,
    double MaximumFieldRadiusInSectors = 3.8,
    double MinimumFieldGapInSectors = 3,
    double MaximumFieldGapInSectors = 7,
    double LoneCometChancePerSector = 0.018,
    int ExtremeCometSeparationInSectors = 6,
    bool EnableStartingDiscoveryField = true,
    double StartingFieldCenterSectorX = 2.75,
    double StartingFieldCenterSectorY = 0.5,
    double StartingFieldMajorRadiusInSectors = 2.2,
    double StartingFieldMinorRadiusInSectors = 1.45,
    double StartingFieldIntensity = 0.85,
    double StartingSafeRadius = 900,
    double StartingSafeCenterX = 2500,
    double StartingSafeCenterY = 2500)
{
    public const double MaximumSupportedRadius = 1800;

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

        if (MinimumCometSpacing < 0 || FieldCellSizeInSectors <= 0 ||
            FieldSpawnChance is < 0 or > 1 || MinimumFieldRadiusInSectors <= 0 ||
            MaximumFieldRadiusInSectors < MinimumFieldRadiusInSectors ||
            MinimumFieldGapInSectors < 0 || MaximumFieldGapInSectors < MinimumFieldGapInSectors ||
            LoneCometChancePerSector is < 0 or > 1 || ExtremeCometSeparationInSectors < 1 ||
            StartingFieldMajorRadiusInSectors <= 0 || StartingFieldMinorRadiusInSectors <= 0 ||
            StartingFieldIntensity is <= 0 or > 1 ||
            StartingSafeRadius < 0)
        {
            throw new ArgumentException("Comet field settings are invalid.");
        }

        if (FieldCellSizeInSectors <= (MaximumFieldRadiusInSectors * 2))
        {
            throw new ArgumentException("Field cells need enough room for jittered field centers.");
        }

        if (FieldCellSizeInSectors <=
            (MaximumFieldRadiusInSectors * 2) + MaximumFieldGapInSectors)
        {
            throw new ArgumentException("Field cells are too small to validate gaps using neighboring cells.");
        }

        if (SectorSize <= (MaximumSupportedRadius * 2) + MinimumCometSpacing)
        {
            throw new ArgumentException("The sector is too small for cross-sector spacing checks.");
        }
    }
}

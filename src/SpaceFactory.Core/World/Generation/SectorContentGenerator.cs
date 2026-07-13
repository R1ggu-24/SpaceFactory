using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.World.Generation;

/// <summary>
/// Builds the shared world result consumed by rendering, collisions and maps.
/// </summary>
public sealed class SectorContentGenerator(
    IWorldGenerator worldGenerator,
    ResourceDepositGenerator resourceGenerator)
{
    public GeneratedSectorContent Generate(
        SectorGenerationRequest request,
        IReadOnlyList<ResourceDefinition> resourceCatalog)
    {
        var sector = worldGenerator.Generate(request);
        var resourcesByComet = sector.Asteroids.ToDictionary(
            comet => comet.Id,
            comet => resourceGenerator.Generate(comet, resourceCatalog));
        return new GeneratedSectorContent(sector, resourcesByComet);
    }
}

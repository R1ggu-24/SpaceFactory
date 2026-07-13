using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.World.Generation;

/// <summary>
/// The complete deterministic result for one sector. Presentation systems and
/// exploration maps share this instance so resource deposits are never rolled
/// independently from the visible world.
/// </summary>
public sealed record GeneratedSectorContent(
    GeneratedSector Sector,
    IReadOnlyDictionary<string, IReadOnlyList<ResourceDepositDefinition>> ResourceDepositsByComet)
{
    public IReadOnlyList<ResourceDepositDefinition> GetResourceDeposits(string cometId) =>
        ResourceDepositsByComet.TryGetValue(cometId, out var deposits) ? deposits : [];
}

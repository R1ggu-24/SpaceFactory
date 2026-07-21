using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Application.Exploration;

public interface IResourceStateStore
{
    /// <summary>
    /// Returns the mutable remaining amount for legacy deposits or remaining hit count for
    /// finite ore stones. Infinite sources return their stable cycle marker.
    /// </summary>
    int GetRemainingAmount(ResourceDepositDefinition deposit);

    /// <summary>
    /// Persists finite depletion/hit progress only. Implementations must not persist an
    /// exhausted state for an infinite source.
    /// </summary>
    void SetRemainingAmount(ResourceDepositDefinition deposit, int remainingAmount, int sectorX, int sectorY);
}

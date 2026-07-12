using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Application.Exploration;

public interface IResourceStateStore
{
    int GetRemainingAmount(ResourceDepositDefinition deposit);

    void SetRemainingAmount(ResourceDepositDefinition deposit, int remainingAmount, int sectorX, int sectorY);
}

using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public sealed record ItemAmount(ItemId ItemId, int Amount)
{
    public void Validate()
    {
        if (Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "An item amount must be positive.");
        }
    }
}

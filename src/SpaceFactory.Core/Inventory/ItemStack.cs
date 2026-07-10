using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public readonly record struct ItemStack
{
    public ItemStack(ItemId itemId, int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        ItemId = itemId;
        Amount = amount;
    }

    public ItemId ItemId { get; }

    public int Amount { get; }
}

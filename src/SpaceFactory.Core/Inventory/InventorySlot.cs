using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public sealed class InventorySlot
{
    internal InventorySlot(int index, int maximumAmount)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (maximumAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAmount));
        }

        Index = index;
        MaximumAmount = maximumAmount;
    }

    public int Index { get; }

    public ItemId? ItemId { get; private set; }

    public int Amount { get; private set; }

    public int MaximumAmount { get; }

    public bool IsEmpty => ItemId is null;

    public ItemStack? Stack => ItemId is { } itemId
        ? new ItemStack(itemId, Amount)
        : null;

    internal void Assign(ItemId itemId, int amount)
    {
        if (amount <= 0 || amount > MaximumAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        ItemId = itemId;
        Amount = amount;
    }

    internal void ChangeAmount(int amount)
    {
        if (amount < 0 || amount > MaximumAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount == 0)
        {
            Clear();
            return;
        }

        if (ItemId is null)
        {
            throw new InvalidOperationException("An empty slot cannot contain an amount.");
        }

        Amount = amount;
    }

    internal void Clear()
    {
        ItemId = null;
        Amount = 0;
    }
}

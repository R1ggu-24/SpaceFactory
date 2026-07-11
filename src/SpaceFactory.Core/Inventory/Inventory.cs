using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public sealed class Inventory
{
    private readonly Dictionary<ItemId, int> _amounts = [];

    public Inventory(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
    }

    public int Capacity { get; private set; }

    public int UsedCapacity => _amounts.Values.Sum();

    public int GetAmount(ItemId itemId) => _amounts.GetValueOrDefault(itemId);

    public InventoryResult Add(ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (amount > Capacity - UsedCapacity)
        {
            return InventoryResult.Failed(InventoryFailure.CapacityExceeded);
        }

        _amounts[itemId] = GetAmount(itemId) + amount;
        return InventoryResult.Success();
    }

    public InventoryResult Add(ItemId itemId, int amount, int maximumStackSize)
    {
        if (maximumStackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStackSize));
        }

        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (GetAmount(itemId) + amount > maximumStackSize)
        {
            return InventoryResult.Failed(InventoryFailure.StackLimitExceeded);
        }

        return Add(itemId, amount);
    }

    public InventoryResult Remove(ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        var current = GetAmount(itemId);
        if (amount > current)
        {
            return InventoryResult.Failed(InventoryFailure.InsufficientItems);
        }

        if (amount == current)
        {
            _amounts.Remove(itemId);
        }
        else
        {
            _amounts[itemId] = current - amount;
        }

        return InventoryResult.Success();
    }

    public void IncreaseCapacity(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Capacity += amount;
    }
}

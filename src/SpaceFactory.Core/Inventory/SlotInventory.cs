using System.Collections.ObjectModel;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public sealed class SlotInventory
{
    private readonly InventorySlot[] _slots;
    private readonly ReadOnlyCollection<InventorySlot> _readOnlySlots;

    public SlotInventory(
        int slotCount,
        int maximumStackSize = InventoryConfiguration.MaximumStackSize)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        if (maximumStackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStackSize));
        }

        MaximumStackSize = maximumStackSize;
        _slots = Enumerable.Range(0, slotCount)
            .Select(index => new InventorySlot(index, maximumStackSize))
            .ToArray();
        _readOnlySlots = Array.AsReadOnly(_slots);
    }

    public int SlotCount => _slots.Length;

    public int MaximumStackSize { get; }

    public IReadOnlyList<InventorySlot> Slots => _readOnlySlots;

    public int UsedSlotCount => _slots.Count(slot => !slot.IsEmpty);

    public int TotalItemCount => _slots.Sum(slot => slot.Amount);

    public bool IsFull => _slots.All(slot => !slot.IsEmpty && slot.Amount == MaximumStackSize);

    public InventorySlot GetSlot(int index)
    {
        ValidateSlotIndex(index);
        return _slots[index];
    }

    public int GetAmount(ItemId itemId) => _slots
        .Where(slot => slot.ItemId == itemId)
        .Sum(slot => slot.Amount);

    public InventoryResult Add(ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (GetAvailableCapacity(itemId) < amount)
        {
            return InventoryResult.Failed(InventoryFailure.CapacityExceeded);
        }

        var remaining = amount;
        foreach (var slot in _slots.Where(slot => slot.ItemId == itemId && slot.Amount < MaximumStackSize))
        {
            var added = Math.Min(MaximumStackSize - slot.Amount, remaining);
            slot.ChangeAmount(slot.Amount + added);
            remaining -= added;

            if (remaining == 0)
            {
                return InventoryResult.Success();
            }
        }

        foreach (var slot in _slots.Where(slot => slot.IsEmpty))
        {
            var added = Math.Min(MaximumStackSize, remaining);
            slot.Assign(itemId, added);
            remaining -= added;

            if (remaining == 0)
            {
                return InventoryResult.Success();
            }
        }

        throw new InvalidOperationException("Capacity was checked before adding, but no space remained.");
    }

    public InventoryResult Remove(ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (GetAmount(itemId) < amount)
        {
            return InventoryResult.Failed(InventoryFailure.InsufficientItems);
        }

        var remaining = amount;
        for (var index = _slots.Length - 1; index >= 0 && remaining > 0; index--)
        {
            var slot = _slots[index];
            if (slot.ItemId != itemId)
            {
                continue;
            }

            var removed = Math.Min(slot.Amount, remaining);
            slot.ChangeAmount(slot.Amount - removed);
            remaining -= removed;
        }

        return InventoryResult.Success();
    }

    internal InventorySlot GetMutableSlot(int index) => GetSlot(index);

    private int GetAvailableCapacity(ItemId itemId)
    {
        var capacityInExistingStacks = _slots
            .Where(slot => slot.ItemId == itemId)
            .Sum(slot => MaximumStackSize - slot.Amount);
        var capacityInEmptySlots = _slots.Count(slot => slot.IsEmpty) * MaximumStackSize;
        return capacityInExistingStacks + capacityInEmptySlots;
    }

    private void ValidateSlotIndex(int index)
    {
        if (index < 0 || index >= _slots.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

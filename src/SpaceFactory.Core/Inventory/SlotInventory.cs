using System.Collections.ObjectModel;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public sealed class SlotInventory
{
    private readonly InventorySlot[] _slots;
    private readonly ReadOnlyCollection<InventorySlot> _readOnlySlots;
    private readonly Func<ItemId, int>? _itemStackSizeResolver;
    private readonly Func<ItemId, bool>? _itemAcceptanceResolver;

    public SlotInventory(
        int slotCount,
        int maximumStackSize = InventoryConfiguration.MaximumStackSize,
        Func<ItemId, int>? itemStackSizeResolver = null,
        Func<ItemId, bool>? itemAcceptanceResolver = null)
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
        _itemStackSizeResolver = itemStackSizeResolver;
        _itemAcceptanceResolver = itemAcceptanceResolver;
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

    public bool IsFull => _slots.All(slot =>
        !slot.IsEmpty && slot.Amount >= GetMaximumStackSize(slot.ItemId!.Value));

    public InventorySlot GetSlot(int index)
    {
        ValidateSlotIndex(index);
        return _slots[index];
    }

    /// <summary>
    /// Returns the effective per-item limit while retaining the inventory-wide hard ceiling.
    /// This keeps reusable inventories independent from the production catalog and lets their
    /// owner opt into non-stackable tools without introducing a second slot implementation.
    /// </summary>
    public int GetMaximumStackSize(ItemId itemId)
    {
        var resolved = _itemStackSizeResolver?.Invoke(itemId) ?? MaximumStackSize;
        if (resolved <= 0)
        {
            throw new InvalidOperationException($"The stack-size resolver returned an invalid limit for '{itemId}'.");
        }

        return Math.Min(resolved, MaximumStackSize);
    }

    public int GetAmount(ItemId itemId) => _slots
        .Where(slot => slot.ItemId == itemId)
        .Sum(slot => slot.Amount);

    /// <summary>
    /// Returns whether this inventory accepts an item. General-purpose inventories accept
    /// everything, while specialised inventories such as the tool belt provide one central
    /// category predicate that is honoured by additions and transfers.
    /// </summary>
    public bool AcceptsItem(ItemId itemId) => _itemAcceptanceResolver?.Invoke(itemId) ?? true;

    public InventoryResult Add(ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (!AcceptsItem(itemId))
        {
            return InventoryResult.Failed(InventoryFailure.ItemNotAccepted);
        }

        if (GetAvailableCapacity(itemId) < amount)
        {
            return InventoryResult.Failed(InventoryFailure.CapacityExceeded);
        }

        var remaining = amount;
        var itemStackSize = GetMaximumStackSize(itemId);
        foreach (var slot in _slots.Where(slot => slot.ItemId == itemId && slot.Amount < itemStackSize))
        {
            var added = Math.Min(itemStackSize - slot.Amount, remaining);
            slot.ChangeAmount(slot.Amount + added);
            remaining -= added;

            if (remaining == 0)
            {
                return InventoryResult.Success();
            }
        }

        foreach (var slot in _slots.Where(slot => slot.IsEmpty))
        {
            var added = Math.Min(itemStackSize, remaining);
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

    /// <summary>
    /// Consumes an item from one explicit slot. Hotbar actions use this path so selecting
    /// one stack can never consume an equal item from a different quick-access slot.
    /// </summary>
    public InventoryResult RemoveFromSlot(int slotIndex, ItemId expectedItemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        var slot = GetSlot(slotIndex);
        if (slot.ItemId != expectedItemId || slot.Amount < amount)
        {
            return InventoryResult.Failed(InventoryFailure.InsufficientItems);
        }

        slot.ChangeAmount(slot.Amount - amount);
        return InventoryResult.Success();
    }

    /// <summary>
    /// Restores or inserts an item into one explicit slot without spilling into another stack.
    /// Construction rollback uses this to preserve the selected hotbar slot exactly.
    /// </summary>
    public InventoryResult AddToSlot(int slotIndex, ItemId itemId, int amount)
    {
        if (amount <= 0)
        {
            return InventoryResult.Failed(InventoryFailure.InvalidAmount);
        }

        if (!AcceptsItem(itemId))
        {
            return InventoryResult.Failed(InventoryFailure.ItemNotAccepted);
        }

        var slot = GetSlot(slotIndex);
        var maximum = GetMaximumStackSize(itemId);
        if ((!slot.IsEmpty && slot.ItemId != itemId) || slot.Amount + amount > maximum)
        {
            return InventoryResult.Failed(InventoryFailure.CapacityExceeded);
        }

        if (slot.IsEmpty)
        {
            slot.Assign(itemId, amount);
        }
        else
        {
            slot.ChangeAmount(slot.Amount + amount);
        }

        return InventoryResult.Success();
    }

    internal InventorySlot GetMutableSlot(int index) => GetSlot(index);

    private int GetAvailableCapacity(ItemId itemId)
    {
        if (!AcceptsItem(itemId))
        {
            return 0;
        }

        var itemStackSize = GetMaximumStackSize(itemId);
        var capacityInExistingStacks = _slots
            .Where(slot => slot.ItemId == itemId)
            .Sum(slot => itemStackSize - slot.Amount);
        var capacityInEmptySlots = _slots.Count(slot => slot.IsEmpty) * itemStackSize;
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

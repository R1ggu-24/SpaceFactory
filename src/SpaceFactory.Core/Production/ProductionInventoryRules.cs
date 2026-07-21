using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public static class ProductionInventoryRules
{
    public static bool ContainsAll(SlotInventory inventory, IEnumerable<ItemAmount> items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return Group(items).All(item => inventory.GetAmount(item.ItemId) >= item.Amount);
    }

    public static bool CanStoreAll(SlotInventory inventory, IEnumerable<ItemAmount> items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var grouped = Group(items);
        var emptySlotCount = inventory.Slots.Count(slot => slot.IsEmpty);
        var additionalSlotsNeeded = 0;

        foreach (var item in grouped)
        {
            var stackSize = inventory.GetMaximumStackSize(item.ItemId);
            var existingCapacity = inventory.Slots
                .Where(slot => slot.ItemId == item.ItemId)
                .Sum(slot => stackSize - slot.Amount);
            var amountRequiringEmptySlots = Math.Max(0, item.Amount - existingCapacity);
            additionalSlotsNeeded += DivideRoundUp(amountRequiringEmptySlots, stackSize);
        }

        return additionalSlotsNeeded <= emptySlotCount;
    }

    public static int GetAvailableCapacity(SlotInventory inventory, ItemId itemId)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var stackSize = inventory.GetMaximumStackSize(itemId);
        var existingCapacity = inventory.Slots
            .Where(slot => slot.ItemId == itemId)
            .Sum(slot => stackSize - slot.Amount);
        return existingCapacity + inventory.Slots.Count(slot => slot.IsEmpty) * stackSize;
    }

    public static bool TryRemoveAll(SlotInventory inventory, IEnumerable<ItemAmount> items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var grouped = Group(items);
        if (!ContainsAll(inventory, grouped))
        {
            return false;
        }

        foreach (var item in grouped)
        {
            var result = inventory.Remove(item.ItemId, item.Amount);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("A prevalidated production inventory removal failed.");
            }
        }

        return true;
    }

    public static bool TryAddAll(SlotInventory inventory, IEnumerable<ItemAmount> items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var grouped = Group(items);
        if (!CanStoreAll(inventory, grouped))
        {
            return false;
        }

        foreach (var item in grouped)
        {
            var result = inventory.Add(item.ItemId, item.Amount);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("A prevalidated production inventory addition failed.");
            }
        }

        return true;
    }

    public static IReadOnlyList<ItemAmount> Group(IEnumerable<ItemAmount> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items.ToArray();
        foreach (var item in materialized)
        {
            item.Validate();
        }

        return materialized
            .GroupBy(item => item.ItemId)
            .Select(group => new ItemAmount(group.Key, checked(group.Sum(item => item.Amount))))
            .ToArray();
    }

    private static int DivideRoundUp(int value, int divisor) => value == 0
        ? 0
        : checked((value + divisor - 1) / divisor);
}

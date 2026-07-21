using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

/// <summary>
/// Plans recipe output into physical machine slots. Radioactive/chemical waste uses reserved
/// tail slots while ordinary products and returned containers use the normal output area.
/// The generated plan is committed atomically, preventing partial cycles or lost byproducts.
/// </summary>
public static class MachineOutputInventoryRules
{
    public static bool CanStoreAll(
        SlotInventory inventory,
        IEnumerable<ItemAmount> outputs,
        ProductionItemCatalog? items = null) =>
        TryCreatePlan(inventory, outputs, items ?? DefaultProductionItemCatalog.Instance, out _);

    public static bool TryAddAll(
        SlotInventory inventory,
        IEnumerable<ItemAmount> outputs,
        ProductionItemCatalog? items = null)
    {
        if (!TryCreatePlan(
                inventory,
                outputs,
                items ?? DefaultProductionItemCatalog.Instance,
                out var plan))
        {
            return false;
        }

        var committed = new List<PlannedAddition>(plan.Count);
        foreach (var addition in plan)
        {
            if (!inventory.AddToSlot(
                    addition.SlotIndex,
                    addition.ItemId,
                    addition.Amount).Succeeded)
            {
                foreach (var rollback in committed.AsEnumerable().Reverse())
                {
                    if (!inventory.RemoveFromSlot(
                            rollback.SlotIndex,
                            rollback.ItemId,
                            rollback.Amount).Succeeded)
                    {
                        throw new InvalidOperationException("Machine-output rollback failed.");
                    }
                }

                throw new InvalidOperationException("A prevalidated machine-output plan could not be committed.");
            }

            committed.Add(addition);
        }

        return true;
    }

    public static bool IsWaste(ItemId itemId, ProductionItemCatalog? items = null) =>
        (items ?? DefaultProductionItemCatalog.Instance).TryGet(itemId, out var definition) &&
        definition is { Category: ProductionItemCategory.Waste };

    public static int GetReservedWasteSlotCount(
        int slotCount,
        IEnumerable<ItemAmount> outputs,
        ProductionItemCatalog? items = null)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        var catalog = items ?? DefaultProductionItemCatalog.Instance;
        return Math.Min(
            slotCount,
            outputs.Where(output => IsWaste(output.ItemId, catalog))
                .Select(output => output.ItemId)
                .Distinct()
                .Count());
    }

    /// <summary>
    /// Returns the capacity that is actually available to one machine output item. Waste is
    /// deliberately restricted to its reserved tail slot, so long generator/offline ticks can
    /// never count ordinary product slots as valid space for spent fuel or other waste.
    /// </summary>
    public static int GetAvailableCapacity(
        SlotInventory inventory,
        ItemId itemId,
        ProductionItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var catalog = items ?? DefaultProductionItemCatalog.Instance;
        var startIndex = IsWaste(itemId, catalog) ? inventory.SlotCount - 1 : 0;
        var capacity = 0;
        for (var index = startIndex; index < inventory.SlotCount; index++)
        {
            var slot = inventory.GetSlot(index);
            if (!slot.IsEmpty && slot.ItemId != itemId)
            {
                continue;
            }

            capacity = checked(capacity + inventory.GetMaximumStackSize(itemId) -
                               (slot.IsEmpty ? 0 : slot.Amount));
        }

        return capacity;
    }

    private static bool TryCreatePlan(
        SlotInventory inventory,
        IEnumerable<ItemAmount> outputs,
        ProductionItemCatalog items,
        out List<PlannedAddition> plan)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(items);
        var grouped = ProductionInventoryRules.Group(outputs);
        var wasteSlotCount = GetReservedWasteSlotCount(inventory.SlotCount, grouped, items);
        if (wasteSlotCount == 0)
        {
            plan = [];
            return TryPlanGroup(inventory, grouped, 0, inventory.SlotCount, plan);
        }

        var normalSlotCount = inventory.SlotCount - wasteSlotCount;
        var normalOutputs = grouped.Where(output => !IsWaste(output.ItemId, items)).ToArray();
        var wasteOutputs = grouped.Where(output => IsWaste(output.ItemId, items)).ToArray();
        plan = [];
        return TryPlanGroup(inventory, normalOutputs, 0, normalSlotCount, plan) &&
               TryPlanGroup(inventory, wasteOutputs, normalSlotCount, inventory.SlotCount, plan);
    }

    private static bool TryPlanGroup(
        SlotInventory inventory,
        IReadOnlyList<ItemAmount> outputs,
        int startIndex,
        int endIndex,
        List<PlannedAddition> plan)
    {
        foreach (var output in outputs)
        {
            var remaining = output.Amount;
            for (var index = startIndex; index < endIndex && remaining > 0; index++)
            {
                var slot = inventory.GetSlot(index);
                var plannedForSlot = plan.Where(item => item.SlotIndex == index).ToArray();
                if (plannedForSlot.Any(item => item.ItemId != output.ItemId))
                {
                    continue;
                }

                var plannedInSlot = plannedForSlot.Sum(item => item.Amount);
                if (!slot.IsEmpty && slot.ItemId != output.ItemId)
                {
                    continue;
                }

                var currentAmount = slot.IsEmpty ? 0 : slot.Amount;
                var capacity = inventory.GetMaximumStackSize(output.ItemId) - currentAmount - plannedInSlot;
                if (capacity <= 0)
                {
                    continue;
                }

                var moved = Math.Min(remaining, capacity);
                plan.Add(new PlannedAddition(index, output.ItemId, moved));
                remaining -= moved;
            }

            if (remaining > 0)
            {
                plan.Clear();
                return false;
            }
        }

        return true;
    }

    private readonly record struct PlannedAddition(int SlotIndex, ItemId ItemId, int Amount);
}

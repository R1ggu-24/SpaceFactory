using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Transactional inventory operations used by item context menus. The presentation layer
/// supplies an expected item ID so a menu opened before a production or transfer update can
/// never mutate a different stack that later occupied the same slot.
/// </summary>
public static class InventoryContextActions
{
    public readonly record struct MoveResult(
        InventoryTransferResult Transfer,
        int? TargetSlotIndex)
    {
        public bool Succeeded => Transfer.Succeeded;

        public static MoveResult Failed(InventoryTransferFailure failure) =>
            new(InventoryTransferResult.Failed(failure), null);
    }

    /// <summary>Moves half of a stack into the first empty slot of the same inventory.</summary>
    public static MoveResult SplitStack(
        SlotInventory inventory,
        int sourceSlotIndex,
        ItemId expectedItemId) =>
        MovePartToFirstEmptySlot(inventory, sourceSlotIndex, expectedItemId, takeSingleItem: false);

    /// <summary>Moves exactly one unit into the first empty slot of the same inventory.</summary>
    public static MoveResult TakeSingleItem(
        SlotInventory inventory,
        int sourceSlotIndex,
        ItemId expectedItemId) =>
        MovePartToFirstEmptySlot(inventory, sourceSlotIndex, expectedItemId, takeSingleItem: true);

    /// <summary>
    /// Moves a complete stack into the first free target slot. If the target has no free slot,
    /// its fallback slot is swapped only when the source can accept the displaced stack.
    /// </summary>
    public static MoveResult MoveToFirstFreeOrFallback(
        SlotInventory source,
        int sourceSlotIndex,
        ItemId expectedItemId,
        SlotInventory target,
        int fallbackSlotIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var sourceSlot = source.GetSlot(sourceSlotIndex);
        if (sourceSlot.ItemId != expectedItemId)
        {
            return MoveResult.Failed(InventoryTransferFailure.SourceEmpty);
        }

        var targetIndex = target.Slots
            .Where(slot => !ReferenceEquals(source, target) || slot.Index != sourceSlotIndex)
            .FirstOrDefault(slot => slot.IsEmpty)?.Index;
        if (targetIndex is null)
        {
            if (fallbackSlotIndex < 0 || fallbackSlotIndex >= target.SlotCount ||
                ReferenceEquals(source, target) && fallbackSlotIndex == sourceSlotIndex)
            {
                return MoveResult.Failed(InventoryTransferFailure.TargetStackFull);
            }

            targetIndex = fallbackSlotIndex;
        }

        var transfer = InventoryTransfer.Transfer(
            source,
            sourceSlotIndex,
            target,
            targetIndex.Value);
        return new MoveResult(transfer, transfer.Succeeded ? targetIndex : null);
    }

    private static MoveResult MovePartToFirstEmptySlot(
        SlotInventory inventory,
        int sourceSlotIndex,
        ItemId expectedItemId,
        bool takeSingleItem)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var sourceSlot = inventory.GetSlot(sourceSlotIndex);
        if (sourceSlot.ItemId != expectedItemId)
        {
            return MoveResult.Failed(InventoryTransferFailure.SourceEmpty);
        }

        if (sourceSlot.Amount <= 1)
        {
            return MoveResult.Failed(InventoryTransferFailure.InsufficientItems);
        }

        var target = inventory.Slots.FirstOrDefault(slot => slot.IsEmpty);
        if (target is null)
        {
            return MoveResult.Failed(InventoryTransferFailure.TargetStackFull);
        }

        var amount = takeSingleItem ? 1 : sourceSlot.Amount / 2;
        var transfer = InventoryTransfer.Transfer(
            inventory,
            sourceSlotIndex,
            inventory,
            target.Index,
            amount);
        return new MoveResult(transfer, transfer.Succeeded ? target.Index : null);
    }
}

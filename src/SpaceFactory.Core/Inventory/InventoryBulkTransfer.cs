using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

public enum InventoryBulkTransferFailure
{
    None,
    NothingToMove,
    TargetFull,
}

public readonly record struct InventoryBulkTransferResult(
    bool Succeeded,
    InventoryBulkTransferFailure Failure,
    int MovedItemCount,
    int MovedStackCount)
{
    public static InventoryBulkTransferResult Success(int items, int stacks) =>
        new(true, InventoryBulkTransferFailure.None, items, stacks);

    public static InventoryBulkTransferResult Failed(InventoryBulkTransferFailure failure) =>
        new(false, failure, 0, 0);
}

/// <summary>
/// Central best-effort bulk transfer used by storage UIs. Each stack is committed only
/// through <see cref="InventoryTransfer"/>, so a full target can never delete or duplicate
/// the untransferred remainder.
/// </summary>
public static class InventoryBulkTransfer
{
    public static InventoryBulkTransferResult TransferAllThatFits(
        SlotInventory source,
        SlotInventory target,
        Func<ItemId, bool>? targetAcceptance = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(source, target))
        {
            return InventoryBulkTransferResult.Failed(InventoryBulkTransferFailure.NothingToMove);
        }

        var hadEligibleItems = false;
        var movedItems = 0;
        var movedStacks = 0;
        for (var sourceIndex = 0; sourceIndex < source.SlotCount; sourceIndex++)
        {
            var sourceSlot = source.GetSlot(sourceIndex);
            if (sourceSlot.ItemId is not { } itemId ||
                !target.AcceptsItem(itemId) ||
                (targetAcceptance is not null && !targetAcceptance(itemId)))
            {
                continue;
            }

            hadEligibleItems = true;
            var before = sourceSlot.Amount;
            var targetIndex = FindPreferredTarget(target, itemId);
            if (targetIndex < 0)
            {
                continue;
            }

            var transfer = InventoryTransfer.TransferPrioritizingExistingStacks(
                source,
                sourceIndex,
                target,
                targetIndex);
            if (!transfer.Succeeded)
            {
                continue;
            }

            movedItems += transfer.MovedAmount;
            if (source.GetSlot(sourceIndex).Amount != before)
            {
                movedStacks++;
            }
        }

        if (movedItems > 0)
        {
            return InventoryBulkTransferResult.Success(movedItems, movedStacks);
        }

        return InventoryBulkTransferResult.Failed(
            hadEligibleItems ? InventoryBulkTransferFailure.TargetFull : InventoryBulkTransferFailure.NothingToMove);
    }

    private static int FindPreferredTarget(SlotInventory target, ItemId itemId)
    {
        var maximum = target.GetMaximumStackSize(itemId);
        var existing = target.Slots.FirstOrDefault(slot =>
            slot.ItemId == itemId && slot.Amount < maximum);
        if (existing is not null)
        {
            return existing.Index;
        }

        return target.Slots.FirstOrDefault(slot => slot.IsEmpty)?.Index ?? -1;
    }
}

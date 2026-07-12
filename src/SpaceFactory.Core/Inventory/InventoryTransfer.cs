namespace SpaceFactory.Core.Inventory;

public enum InventoryTransferFailure
{
    None,
    InvalidAmount,
    SourceEmpty,
    InsufficientItems,
    TargetStackFull,
    IncompatibleStacks,
    StackLimitExceeded,
}

public readonly record struct InventoryTransferResult(
    bool Succeeded,
    InventoryTransferFailure Failure,
    int RequestedAmount,
    int MovedAmount,
    bool Swapped)
{
    public int RemainingAmount => RequestedAmount - MovedAmount;

    public static InventoryTransferResult Success(int requestedAmount, int movedAmount, bool swapped = false) =>
        new(true, InventoryTransferFailure.None, requestedAmount, movedAmount, swapped);

    public static InventoryTransferResult Failed(InventoryTransferFailure failure, int requestedAmount = 0) =>
        new(false, failure, requestedAmount, 0, false);
}

public static class InventoryTransfer
{
    public static InventoryTransferResult Transfer(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceSlot = source.GetMutableSlot(sourceIndex);
        var targetSlot = target.GetMutableSlot(targetIndex);

        if (sourceSlot.IsEmpty)
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.SourceEmpty);
        }

        var requestedAmount = amount ?? sourceSlot.Amount;
        if (requestedAmount <= 0)
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.InvalidAmount, requestedAmount);
        }

        if (requestedAmount > sourceSlot.Amount)
        {
            return InventoryTransferResult.Failed(InventoryTransferFailure.InsufficientItems, requestedAmount);
        }

        if (ReferenceEquals(source, target) && sourceIndex == targetIndex)
        {
            return InventoryTransferResult.Success(requestedAmount, 0);
        }

        if (targetSlot.IsEmpty)
        {
            return MoveIntoEmptySlot(sourceSlot, targetSlot, requestedAmount);
        }

        if (sourceSlot.ItemId == targetSlot.ItemId)
        {
            return MergeStacks(sourceSlot, targetSlot, requestedAmount);
        }

        return SwapStacks(sourceSlot, targetSlot, requestedAmount);
    }

    private static InventoryTransferResult MoveIntoEmptySlot(
        InventorySlot sourceSlot,
        InventorySlot targetSlot,
        int requestedAmount)
    {
        if (requestedAmount > targetSlot.MaximumAmount)
        {
            return InventoryTransferResult.Failed(
                InventoryTransferFailure.StackLimitExceeded,
                requestedAmount);
        }

        var itemId = sourceSlot.ItemId!.Value;
        targetSlot.Assign(itemId, requestedAmount);
        sourceSlot.ChangeAmount(sourceSlot.Amount - requestedAmount);
        return InventoryTransferResult.Success(requestedAmount, requestedAmount);
    }

    private static InventoryTransferResult MergeStacks(
        InventorySlot sourceSlot,
        InventorySlot targetSlot,
        int requestedAmount)
    {
        var available = targetSlot.MaximumAmount - targetSlot.Amount;
        if (available == 0)
        {
            return InventoryTransferResult.Failed(
                InventoryTransferFailure.TargetStackFull,
                requestedAmount);
        }

        var movedAmount = Math.Min(available, requestedAmount);
        targetSlot.ChangeAmount(targetSlot.Amount + movedAmount);
        sourceSlot.ChangeAmount(sourceSlot.Amount - movedAmount);
        return InventoryTransferResult.Success(requestedAmount, movedAmount);
    }

    private static InventoryTransferResult SwapStacks(
        InventorySlot sourceSlot,
        InventorySlot targetSlot,
        int requestedAmount)
    {
        if (requestedAmount != sourceSlot.Amount)
        {
            return InventoryTransferResult.Failed(
                InventoryTransferFailure.IncompatibleStacks,
                requestedAmount);
        }

        if (sourceSlot.Amount > targetSlot.MaximumAmount || targetSlot.Amount > sourceSlot.MaximumAmount)
        {
            return InventoryTransferResult.Failed(
                InventoryTransferFailure.StackLimitExceeded,
                requestedAmount);
        }

        var sourceItemId = sourceSlot.ItemId!.Value;
        var sourceAmount = sourceSlot.Amount;
        var targetItemId = targetSlot.ItemId!.Value;
        var targetAmount = targetSlot.Amount;

        sourceSlot.Assign(targetItemId, targetAmount);
        targetSlot.Assign(sourceItemId, sourceAmount);
        return InventoryTransferResult.Success(requestedAmount, requestedAmount, swapped: true);
    }
}

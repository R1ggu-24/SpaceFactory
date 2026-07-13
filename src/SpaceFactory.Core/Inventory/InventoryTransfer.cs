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
    public static InventoryTransferResult Preview(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount = null) =>
        Evaluate(source, sourceIndex, target, targetIndex, amount).Result;

    public static InventoryTransferResult Transfer(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount = null)
    {
        var plan = Evaluate(source, sourceIndex, target, targetIndex, amount);
        if (!plan.Result.Succeeded || plan.Operation == InventoryTransferOperation.None)
        {
            return plan.Result;
        }

        var sourceSlot = source.GetMutableSlot(sourceIndex);
        var targetSlot = target.GetMutableSlot(targetIndex);

        Apply(plan, sourceSlot, targetSlot);
        return plan.Result;
    }

    private static InventoryTransferPlan Evaluate(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceSlot = source.GetSlot(sourceIndex);
        var targetSlot = target.GetSlot(targetIndex);

        if (sourceSlot.IsEmpty)
        {
            return InventoryTransferPlan.Failed(InventoryTransferFailure.SourceEmpty);
        }

        var requestedAmount = amount ?? sourceSlot.Amount;
        if (requestedAmount <= 0)
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.InvalidAmount,
                requestedAmount);
        }

        if (requestedAmount > sourceSlot.Amount)
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.InsufficientItems,
                requestedAmount);
        }

        if (ReferenceEquals(source, target) && sourceIndex == targetIndex)
        {
            return InventoryTransferPlan.Success(
                InventoryTransferOperation.None,
                requestedAmount,
                movedAmount: 0);
        }

        if (targetSlot.IsEmpty)
        {
            if (requestedAmount > targetSlot.MaximumAmount)
            {
                return InventoryTransferPlan.Failed(
                    InventoryTransferFailure.StackLimitExceeded,
                    requestedAmount);
            }

            return InventoryTransferPlan.Success(
                InventoryTransferOperation.MoveIntoEmptySlot,
                requestedAmount,
                requestedAmount);
        }

        if (sourceSlot.ItemId == targetSlot.ItemId)
        {
            var available = targetSlot.MaximumAmount - targetSlot.Amount;
            if (available == 0)
            {
                return InventoryTransferPlan.Failed(
                    InventoryTransferFailure.TargetStackFull,
                    requestedAmount);
            }

            return InventoryTransferPlan.Success(
                InventoryTransferOperation.MergeStacks,
                requestedAmount,
                Math.Min(available, requestedAmount));
        }

        if (requestedAmount != sourceSlot.Amount)
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.IncompatibleStacks,
                requestedAmount);
        }

        if (sourceSlot.Amount > targetSlot.MaximumAmount || targetSlot.Amount > sourceSlot.MaximumAmount)
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.StackLimitExceeded,
                requestedAmount);
        }

        return InventoryTransferPlan.Success(
            InventoryTransferOperation.SwapStacks,
            requestedAmount,
            requestedAmount,
            swapped: true);
    }

    private static void Apply(
        InventoryTransferPlan plan,
        InventorySlot sourceSlot,
        InventorySlot targetSlot)
    {
        switch (plan.Operation)
        {
            case InventoryTransferOperation.MoveIntoEmptySlot:
                targetSlot.Assign(sourceSlot.ItemId!.Value, plan.Result.MovedAmount);
                sourceSlot.ChangeAmount(sourceSlot.Amount - plan.Result.MovedAmount);
                break;
            case InventoryTransferOperation.MergeStacks:
                targetSlot.ChangeAmount(targetSlot.Amount + plan.Result.MovedAmount);
                sourceSlot.ChangeAmount(sourceSlot.Amount - plan.Result.MovedAmount);
                break;
            case InventoryTransferOperation.SwapStacks:
                SwapStacks(sourceSlot, targetSlot);
                break;
            case InventoryTransferOperation.None:
            default:
                throw new InvalidOperationException("A successful transfer plan must contain an operation.");
        }
    }

    private static void SwapStacks(InventorySlot sourceSlot, InventorySlot targetSlot)
    {
        var sourceItemId = sourceSlot.ItemId!.Value;
        var sourceAmount = sourceSlot.Amount;
        var targetItemId = targetSlot.ItemId!.Value;
        var targetAmount = targetSlot.Amount;

        sourceSlot.Assign(targetItemId, targetAmount);
        targetSlot.Assign(sourceItemId, sourceAmount);
    }

    private enum InventoryTransferOperation
    {
        None,
        MoveIntoEmptySlot,
        MergeStacks,
        SwapStacks,
    }

    private readonly record struct InventoryTransferPlan(
        InventoryTransferResult Result,
        InventoryTransferOperation Operation)
    {
        public static InventoryTransferPlan Success(
            InventoryTransferOperation operation,
            int requestedAmount,
            int movedAmount,
            bool swapped = false) =>
            new(
                InventoryTransferResult.Success(requestedAmount, movedAmount, swapped),
                operation);

        public static InventoryTransferPlan Failed(
            InventoryTransferFailure failure,
            int requestedAmount = 0) =>
            new(
                InventoryTransferResult.Failed(failure, requestedAmount),
                InventoryTransferOperation.None);
    }
}

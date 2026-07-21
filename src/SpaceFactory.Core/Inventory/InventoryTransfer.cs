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
    ItemNotAccepted,
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
    /// <summary>
    /// Previews a transfer between two different inventories while preferring every
    /// partially filled stack of the same item before the explicitly selected empty
    /// slot. Transfers inside one inventory retain the normal slot-to-slot semantics.
    /// </summary>
    public static InventoryTransferResult PreviewPrioritizingExistingStacks(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var sourceSlot = source.GetSlot(sourceIndex);
        var targetSlot = target.GetSlot(targetIndex);
        return ReferenceEquals(source, target) ||
               (!sourceSlot.IsEmpty && !targetSlot.IsEmpty && targetSlot.ItemId != sourceSlot.ItemId)
            ? Preview(source, sourceIndex, target, targetIndex, amount)
            : EvaluatePrioritizingExistingStacks(source, sourceIndex, target, targetIndex, amount).Result;
    }

    /// <summary>
    /// Moves a stack transactionally between inventories. Existing compatible stacks
    /// are filled first and only the remainder is assigned to the selected empty slot.
    /// The complete mutation plan is validated before either inventory is changed.
    /// </summary>
    public static InventoryTransferResult TransferPrioritizingExistingStacks(
        SlotInventory source,
        int sourceIndex,
        SlotInventory target,
        int targetIndex,
        int? amount = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var sourceSlotBefore = source.GetSlot(sourceIndex);
        var targetSlotBefore = target.GetSlot(targetIndex);
        if (ReferenceEquals(source, target) ||
            (!sourceSlotBefore.IsEmpty && !targetSlotBefore.IsEmpty &&
             targetSlotBefore.ItemId != sourceSlotBefore.ItemId))
        {
            return Transfer(source, sourceIndex, target, targetIndex, amount);
        }

        var plan = EvaluatePrioritizingExistingStacks(source, sourceIndex, target, targetIndex, amount);
        if (!plan.Result.Succeeded || plan.TargetChanges.Count == 0)
        {
            return plan.Result;
        }

        var sourceSlot = source.GetMutableSlot(sourceIndex);
        foreach (var change in plan.TargetChanges)
        {
            var targetSlot = target.GetMutableSlot(change.SlotIndex);
            if (targetSlot.IsEmpty)
            {
                targetSlot.Assign(sourceSlot.ItemId!.Value, change.Amount);
            }
            else
            {
                targetSlot.ChangeAmount(targetSlot.Amount + change.Amount);
            }
        }

        sourceSlot.ChangeAmount(sourceSlot.Amount - plan.Result.MovedAmount);
        return plan.Result;
    }

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

        var sourceItemId = sourceSlot.ItemId!.Value;
        if (!target.AcceptsItem(sourceItemId))
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.ItemNotAccepted,
                requestedAmount);
        }

        if (targetSlot.IsEmpty)
        {
            var targetMaximum = target.GetMaximumStackSize(sourceItemId);
            if (requestedAmount > targetMaximum)
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
            var targetMaximum = target.GetMaximumStackSize(sourceSlot.ItemId!.Value);
            var available = targetMaximum - targetSlot.Amount;
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

        if (!source.AcceptsItem(targetSlot.ItemId!.Value))
        {
            return InventoryTransferPlan.Failed(
                InventoryTransferFailure.ItemNotAccepted,
                requestedAmount);
        }

        if (sourceSlot.Amount > target.GetMaximumStackSize(sourceItemId) ||
            targetSlot.Amount > source.GetMaximumStackSize(targetSlot.ItemId!.Value))
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

    private static PrioritizedTransferPlan EvaluatePrioritizingExistingStacks(
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
            return PrioritizedTransferPlan.Failed(InventoryTransferFailure.SourceEmpty);
        }

        var requestedAmount = amount ?? sourceSlot.Amount;
        if (requestedAmount <= 0)
        {
            return PrioritizedTransferPlan.Failed(
                InventoryTransferFailure.InvalidAmount,
                requestedAmount);
        }

        if (requestedAmount > sourceSlot.Amount)
        {
            return PrioritizedTransferPlan.Failed(
                InventoryTransferFailure.InsufficientItems,
                requestedAmount);
        }

        var itemId = sourceSlot.ItemId!.Value;
        if (!target.AcceptsItem(itemId))
        {
            return PrioritizedTransferPlan.Failed(
                InventoryTransferFailure.ItemNotAccepted,
                requestedAmount);
        }

        var maximum = target.GetMaximumStackSize(itemId);
        var remaining = requestedAmount;
        var targetChanges = new List<PrioritizedTargetChange>();

        foreach (var existing in target.Slots.Where(slot =>
                     slot.ItemId == itemId && slot.Amount < maximum))
        {
            var moved = Math.Min(maximum - existing.Amount, remaining);
            if (moved <= 0)
            {
                break;
            }

            targetChanges.Add(new PrioritizedTargetChange(existing.Index, moved));
            remaining -= moved;
        }

        if (remaining > 0 && targetSlot.IsEmpty)
        {
            var moved = Math.Min(maximum, remaining);
            targetChanges.Add(new PrioritizedTargetChange(targetIndex, moved));
            remaining -= moved;
        }

        var movedAmount = requestedAmount - remaining;
        return movedAmount <= 0
            ? PrioritizedTransferPlan.Failed(
                InventoryTransferFailure.TargetStackFull,
                requestedAmount)
            : new PrioritizedTransferPlan(
                InventoryTransferResult.Success(requestedAmount, movedAmount),
                targetChanges);
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

    private readonly record struct PrioritizedTargetChange(int SlotIndex, int Amount);

    private readonly record struct PrioritizedTransferPlan(
        InventoryTransferResult Result,
        IReadOnlyList<PrioritizedTargetChange> TargetChanges)
    {
        public static PrioritizedTransferPlan Failed(
            InventoryTransferFailure failure,
            int requestedAmount = 0) =>
            new(InventoryTransferResult.Failed(failure, requestedAmount), []);

    }
}

using SpaceFactory.Core.Inventory;

namespace SpaceFactory.Core.Production;

public enum MachineInventoryTransferFailure
{
    None,
    MissingSourceItems,
    TargetFull,
    NothingToTransfer,
    InvalidBatchCount,
}

public readonly record struct MachineInventoryTransferResult(
    bool Succeeded,
    MachineInventoryTransferFailure Failure,
    int TransferredItemCount)
{
    public static MachineInventoryTransferResult Success(int transferredItemCount) =>
        new(true, MachineInventoryTransferFailure.None, transferredItemCount);

    public static MachineInventoryTransferResult Failed(MachineInventoryTransferFailure failure) =>
        new(false, failure, 0);
}

/// <summary>
/// Atomic inventory transactions shared by every machine UI. All capacity and
/// source checks happen before either inventory is mutated.
/// </summary>
public static class MachineInventoryTransfer
{
    public static MachineInventoryTransferResult LoadRecipeInputs(
        SlotInventory source,
        SlotInventory machineInput,
        RecipeDefinition recipe,
        int batchCount = 1)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (batchCount <= 0)
        {
            return MachineInventoryTransferResult.Failed(
                MachineInventoryTransferFailure.InvalidBatchCount);
        }

        var requested = recipe.Inputs
            .Select(item => new ItemAmount(item.ItemId, checked(item.Amount * batchCount)))
            .ToArray();
        return TransferExact(source, machineInput, requested);
    }

    public static MachineInventoryTransferResult TransferAll(
        SlotInventory source,
        SlotInventory target)
    {
        ArgumentNullException.ThrowIfNull(source);
        var items = source.Slots
            .Where(slot => !slot.IsEmpty)
            .GroupBy(slot => slot.ItemId!.Value)
            .Select(group => new ItemAmount(group.Key, group.Sum(slot => slot.Amount)))
            .ToArray();
        return items.Length == 0
            ? MachineInventoryTransferResult.Failed(MachineInventoryTransferFailure.NothingToTransfer)
            : TransferExact(source, target, items);
    }

    /// <summary>
    /// Moves every stack that currently fits and leaves the remainder untouched. This is used
    /// by large storage containers so a smaller player inventory can retrieve their contents in
    /// several safe passes without making any stack unreachable.
    /// </summary>
    public static MachineInventoryTransferResult TransferAsMuchAsPossible(
        SlotInventory source,
        SlotInventory target) =>
        TransferAsMuchAsPossible(source, target, static _ => true);

    public static MachineInventoryTransferResult TransferAsMuchAsPossible(
        SlotInventory source,
        SlotInventory target,
        Func<SpaceFactory.Core.Items.ItemId, bool> itemFilter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(itemFilter);
        if (ReferenceEquals(source, target))
        {
            return MachineInventoryTransferResult.Failed(MachineInventoryTransferFailure.NothingToTransfer);
        }

        var sourceHadItems = source.TotalItemCount > 0;
        var transferred = 0;
        for (var sourceIndex = 0; sourceIndex < source.SlotCount; sourceIndex++)
        {
            while (!source.GetSlot(sourceIndex).IsEmpty)
            {
                var itemId = source.GetSlot(sourceIndex).ItemId!.Value;
                if (!itemFilter(itemId))
                {
                    break;
                }
                var targetMaximum = target.GetMaximumStackSize(itemId);
                var targetSlot = target.Slots.FirstOrDefault(slot =>
                                     slot.ItemId == itemId && slot.Amount < targetMaximum) ??
                                 target.Slots.FirstOrDefault(slot => slot.IsEmpty);
                if (targetSlot is null)
                {
                    break;
                }

                var availableTargetAmount = targetSlot.IsEmpty
                    ? targetMaximum
                    : targetMaximum - targetSlot.Amount;
                var transferAmount = Math.Min(source.GetSlot(sourceIndex).Amount, availableTargetAmount);
                var result = InventoryTransfer.Transfer(
                    source,
                    sourceIndex,
                    target,
                    targetSlot.Index,
                    transferAmount);
                if (!result.Succeeded || result.MovedAmount <= 0)
                {
                    throw new InvalidOperationException("A validated partial machine transfer failed.");
                }

                transferred += result.MovedAmount;
            }
        }

        if (transferred > 0)
        {
            return MachineInventoryTransferResult.Success(transferred);
        }

        return MachineInventoryTransferResult.Failed(
            sourceHadItems
                ? MachineInventoryTransferFailure.TargetFull
                : MachineInventoryTransferFailure.NothingToTransfer);
    }

    public static MachineInventoryTransferResult TransferExact(
        SlotInventory source,
        SlotInventory target,
        IEnumerable<ItemAmount> requestedItems)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var items = ProductionInventoryRules.Group(requestedItems);
        if (items.Count == 0)
        {
            return MachineInventoryTransferResult.Failed(MachineInventoryTransferFailure.NothingToTransfer);
        }

        if (!ProductionInventoryRules.ContainsAll(source, items))
        {
            return MachineInventoryTransferResult.Failed(MachineInventoryTransferFailure.MissingSourceItems);
        }

        if (!ProductionInventoryRules.CanStoreAll(target, items))
        {
            return MachineInventoryTransferResult.Failed(MachineInventoryTransferFailure.TargetFull);
        }

        if (!ProductionInventoryRules.TryRemoveAll(source, items))
        {
            throw new InvalidOperationException("A prevalidated machine transfer lost its source items.");
        }

        if (!ProductionInventoryRules.TryAddAll(target, items))
        {
            var rollback = ProductionInventoryRules.TryAddAll(source, items);
            if (!rollback)
            {
                throw new InvalidOperationException("A machine transfer failed and could not be rolled back.");
            }

            throw new InvalidOperationException("A prevalidated machine transfer target rejected its items.");
        }

        return MachineInventoryTransferResult.Success(items.Sum(item => item.Amount));
    }
}

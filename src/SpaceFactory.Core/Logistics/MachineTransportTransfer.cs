using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Logistics;

public enum TransportTransferFailure
{
    None,
    InsufficientTransferCredit,
    InvalidMedium,
    NoCompatibleItem,
    TargetFull,
}

public readonly record struct TransportTransferResult(
    TransportTransferFailure Failure,
    ItemId? ItemId,
    int TransferredAmount)
{
    public bool Succeeded => Failure == TransportTransferFailure.None;

    public static TransportTransferResult Success(ItemId itemId, int amount) =>
        new(TransportTransferFailure.None, itemId, amount);

    public static TransportTransferResult Failed(TransportTransferFailure failure) =>
        new(failure, null, 0);
}

public static class MachineTransportTransfer
{
    public static TransportTransferResult TransferFirstCompatible(
        SlotInventory source,
        SlotInventory target,
        TransportMedium medium,
        int maximumAmount,
        ProductionItemCatalog itemCatalog,
        IReadOnlyCollection<ItemId>? allowedItemIds = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(itemCatalog);
        if (maximumAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAmount));
        }

        if (medium == TransportMedium.Power)
        {
            return TransportTransferResult.Failed(TransportTransferFailure.InvalidMedium);
        }

        var compatibleItems = source.Slots
            .Where(slot => slot.ItemId is not null)
            .Select(slot => slot.ItemId!.Value)
            .Distinct()
            .Where(itemId => (allowedItemIds is null || allowedItemIds.Contains(itemId)) &&
                             itemCatalog.TryGet(itemId, out var definition) &&
                             definition is not null &&
                             IsCompatible(definition.Phase, medium))
            .ToArray();
        if (compatibleItems.Length == 0)
        {
            return TransportTransferResult.Failed(TransportTransferFailure.NoCompatibleItem);
        }

        foreach (var itemId in compatibleItems)
        {
            var availableCapacity = ProductionInventoryRules.GetAvailableCapacity(target, itemId);
            if (availableCapacity <= 0)
            {
                continue;
            }

            var amount = Math.Min(maximumAmount, Math.Min(source.GetAmount(itemId), availableCapacity));
            if (amount <= 0)
            {
                continue;
            }

            var removal = source.Remove(itemId, amount);
            if (!removal.Succeeded)
            {
                throw new InvalidOperationException("A prevalidated transport removal failed.");
            }

            var addition = target.Add(itemId, amount);
            if (addition.Succeeded)
            {
                return TransportTransferResult.Success(itemId, amount);
            }

            var rollback = source.Add(itemId, amount);
            if (!rollback.Succeeded)
            {
                throw new InvalidOperationException("A failed transport could not restore its source inventory.");
            }
        }

        return TransportTransferResult.Failed(TransportTransferFailure.TargetFull);
    }

    private static bool IsCompatible(ProductionItemPhase phase, TransportMedium medium) =>
        (phase, medium) switch
        {
            (ProductionItemPhase.Solid, TransportMedium.Solid) => true,
            (ProductionItemPhase.Liquid, TransportMedium.Liquid) => true,
            (ProductionItemPhase.Gas, TransportMedium.Gas) => true,
            _ => false,
        };
}

using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Ships.Fuel;

public enum ShipRefuelFailure
{
    None,
    InvalidContainerLimit,
    InvalidFuelType,
    NoFilledFuelContainers,
    TankCannotFitFullContainer,
    NoSpaceForReturnedContainers,
    TankContainsDifferentFuel,
    SelectedSlotDoesNotContainFuel,
    SelectedSlotDoesNotContainEmptyContainer,
    TankEmpty,
    TankCannotFillContainer,
}

public readonly record struct ShipRefuelResult(
    bool Succeeded,
    ShipRefuelFailure Failure,
    int TransferredContainerCount,
    double TransferredFuel)
{
    public ShipFuelType? FuelType { get; init; }

    public static ShipRefuelResult Success(
        int containerCount,
        double transferredFuel,
        ShipFuelType fuelType) =>
        new(true, ShipRefuelFailure.None, containerCount, transferredFuel)
        {
            FuelType = fuelType,
        };

    public static ShipRefuelResult Failed(ShipRefuelFailure failure) =>
        new(false, failure, 0, 0);
}

public static class ShipRefuelService
{
    private const double ComparisonTolerance = 0.000_000_001;

    public static ShipRefuelResult TransferFilledContainers(
        ShipFuelTank tank,
        SlotInventory sourceInventory,
        int? maximumContainerCount = null)
    {
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(sourceInventory);

        var fuelType = ResolveAutomaticFuelType(tank, sourceInventory);
        return fuelType is { } resolved
            ? TransferFilledContainers(tank, sourceInventory, resolved, maximumContainerCount)
            : ShipRefuelResult.Failed(ShipRefuelFailure.NoFilledFuelContainers);
    }

    public static ShipRefuelResult TransferFilledContainerFromSlot(
        ShipFuelTank tank,
        SlotInventory inventory,
        int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(inventory);
        var slot = inventory.GetSlot(slotIndex);
        if (slot.ItemId is not { } itemId ||
            !ShipFuelConfiguration.TryGetFuelTypeForContainer(itemId, out var fuelType))
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.SelectedSlotDoesNotContainFuel);
        }

        if (tank.CurrentFuel > ComparisonTolerance && tank.CurrentFuelType != fuelType)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankContainsDifferentFuel);
        }

        if (tank.RemainingCapacity + ComparisonTolerance < ShipFuelConfiguration.FuelPerContainer)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankCannotFitFullContainer);
        }

        var inventoryBefore = CaptureInventory(inventory);
        if (!TryExchangeSelectedContainer(
                inventory,
                slotIndex,
                itemId,
                ProductionItemIds.EmptyFuelContainer))
        {
            RestoreInventory(inventory, inventoryBefore);
            return ShipRefuelResult.Failed(ShipRefuelFailure.NoSpaceForReturnedContainers);
        }

        if (!tank.TryAddFuel(ShipFuelConfiguration.FuelPerContainer, fuelType))
        {
            RestoreInventory(inventory, inventoryBefore);
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankCannotFitFullContainer);
        }

        return ShipRefuelResult.Success(1, ShipFuelConfiguration.FuelPerContainer, fuelType);
    }

    public static ShipRefuelResult TransferTankToContainerInSlot(
        ShipFuelTank tank,
        SlotInventory inventory,
        int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(inventory);
        var slot = inventory.GetSlot(slotIndex);
        if (slot.ItemId != ProductionItemIds.EmptyFuelContainer)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.SelectedSlotDoesNotContainEmptyContainer);
        }

        if (tank.CurrentFuel <= ComparisonTolerance)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankEmpty);
        }

        if (tank.CurrentFuel + ComparisonTolerance < ShipFuelConfiguration.FuelPerContainer)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankCannotFillContainer);
        }

        var fuelType = tank.CurrentFuelType;
        var filledContainerId = ShipFuelConfiguration.GetFilledContainerItemId(fuelType);
        var inventoryBefore = CaptureInventory(inventory);
        if (!TryExchangeSelectedContainer(
                inventory,
                slotIndex,
                ProductionItemIds.EmptyFuelContainer,
                filledContainerId))
        {
            RestoreInventory(inventory, inventoryBefore);
            return ShipRefuelResult.Failed(ShipRefuelFailure.NoSpaceForReturnedContainers);
        }

        if (!tank.TryRemoveFuel(ShipFuelConfiguration.FuelPerContainer))
        {
            RestoreInventory(inventory, inventoryBefore);
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankCannotFillContainer);
        }

        return ShipRefuelResult.Success(1, ShipFuelConfiguration.FuelPerContainer, fuelType);
    }

    public static ShipRefuelResult TransferFilledContainers(
        ShipFuelTank tank,
        SlotInventory sourceInventory,
        ShipFuelType fuelType,
        int? maximumContainerCount = null)
    {
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(sourceInventory);

        if (!Enum.IsDefined(fuelType))
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.InvalidFuelType);
        }

        if (maximumContainerCount is <= 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.InvalidContainerLimit);
        }

        if (tank.CurrentFuel > ComparisonTolerance && tank.CurrentFuelType != fuelType)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankContainsDifferentFuel);
        }

        var filledContainerId = ShipFuelConfiguration.GetFilledContainerItemId(fuelType);
        var availableContainers = sourceInventory.GetAmount(filledContainerId);
        if (availableContainers == 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.NoFilledFuelContainers);
        }

        var containersFittingTank = (int)Math.Floor(
            (tank.RemainingCapacity + ComparisonTolerance) / ShipFuelConfiguration.FuelPerContainer);
        if (containersFittingTank == 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.TankCannotFitFullContainer);
        }

        var requestedContainers = Math.Min(availableContainers, containersFittingTank);
        if (maximumContainerCount is { } limit)
        {
            requestedContainers = Math.Min(requestedContainers, limit);
        }

        var transferableContainers = FindLargestSafeContainerCount(
            sourceInventory,
            filledContainerId,
            requestedContainers);
        if (transferableContainers == 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.NoSpaceForReturnedContainers);
        }

        var fuelToTransfer = transferableContainers * ShipFuelConfiguration.FuelPerContainer;
        var removal = sourceInventory.Remove(filledContainerId, transferableContainers);
        if (!removal.Succeeded)
        {
            throw new InvalidOperationException("The validated filled fuel containers could not be removed.");
        }

        var emptyContainerReturn = sourceInventory.Add(
            ProductionItemIds.EmptyFuelContainer,
            transferableContainers);
        if (!emptyContainerReturn.Succeeded)
        {
            RestoreFilledContainers(sourceInventory, filledContainerId, transferableContainers);
            throw new InvalidOperationException("The validated empty fuel containers could not be returned.");
        }

        if (!tank.TryAddFuel(fuelToTransfer, fuelType))
        {
            RestoreInventoryAfterRejectedTankFill(
                sourceInventory,
                filledContainerId,
                transferableContainers);
            throw new InvalidOperationException("The validated fuel amount no longer fits into the tank.");
        }

        return ShipRefuelResult.Success(transferableContainers, fuelToTransfer, fuelType);
    }

    private static ShipFuelType? ResolveAutomaticFuelType(
        ShipFuelTank tank,
        SlotInventory sourceInventory)
    {
        if (tank.CurrentFuel > ComparisonTolerance)
        {
            var currentContainer = ShipFuelConfiguration.GetFilledContainerItemId(tank.CurrentFuelType);
            if (sourceInventory.GetAmount(currentContainer) > 0)
            {
                return tank.CurrentFuelType;
            }

            foreach (var fuelType in Enum.GetValues<ShipFuelType>())
            {
                if (fuelType != tank.CurrentFuelType && sourceInventory.GetAmount(
                        ShipFuelConfiguration.GetFilledContainerItemId(fuelType)) > 0)
                {
                    return fuelType;
                }
            }

            return null;
        }

        if (sourceInventory.GetAmount(
                ShipFuelConfiguration.GetFilledContainerItemId(tank.CurrentFuelType)) > 0)
        {
            return tank.CurrentFuelType;
        }

        foreach (var fuelType in Enum.GetValues<ShipFuelType>())
        {
            if (sourceInventory.GetAmount(
                    ShipFuelConfiguration.GetFilledContainerItemId(fuelType)) > 0)
            {
                return fuelType;
            }
        }

        return null;
    }

    private static int FindLargestSafeContainerCount(
        SlotInventory inventory,
        ItemId filledContainerId,
        int requestedContainers)
    {
        for (var count = requestedContainers; count > 0; count--)
        {
            if (GetEmptyContainerCapacityAfterFuelRemoval(
                    inventory,
                    filledContainerId,
                    count) >= count)
            {
                return count;
            }
        }

        return 0;
    }

    private static int GetEmptyContainerCapacityAfterFuelRemoval(
        SlotInventory inventory,
        ItemId filledContainerId,
        int removedFuelContainers)
    {
        var emptyContainerCapacity = inventory.Slots
            .Where(slot => slot.ItemId == ProductionItemIds.EmptyFuelContainer)
            .Sum(slot => slot.MaximumAmount - slot.Amount);
        emptyContainerCapacity += inventory.Slots.Count(slot => slot.IsEmpty) * inventory.MaximumStackSize;

        var remainingRemoval = removedFuelContainers;
        for (var index = inventory.SlotCount - 1; index >= 0 && remainingRemoval > 0; index--)
        {
            var slot = inventory.GetSlot(index);
            if (slot.ItemId != filledContainerId)
            {
                continue;
            }

            var removedFromSlot = Math.Min(slot.Amount, remainingRemoval);
            remainingRemoval -= removedFromSlot;
            if (removedFromSlot == slot.Amount)
            {
                emptyContainerCapacity += inventory.MaximumStackSize;
            }
        }

        return emptyContainerCapacity;
    }

    private static void RestoreFilledContainers(
        SlotInventory inventory,
        ItemId filledContainerId,
        int containerCount)
    {
        var restore = inventory.Add(filledContainerId, containerCount);
        if (!restore.Succeeded)
        {
            throw new InvalidOperationException("The fuel-container transaction could not be rolled back.");
        }
    }

    private static void RestoreInventoryAfterRejectedTankFill(
        SlotInventory inventory,
        ItemId filledContainerId,
        int containerCount)
    {
        var removeReturned = inventory.Remove(ProductionItemIds.EmptyFuelContainer, containerCount);
        if (!removeReturned.Succeeded)
        {
            throw new InvalidOperationException("The empty-container return could not be rolled back.");
        }

        RestoreFilledContainers(inventory, filledContainerId, containerCount);
    }

    /// <summary>
    /// Replaces exactly one container in the selected slot. If the selected stack contains
    /// additional containers, the remainder is first relocated through the inventory's normal
    /// stacking rules. The caller owns the full snapshot rollback, so this helper never leaves a
    /// half-applied exchange behind.
    /// </summary>
    private static bool TryExchangeSelectedContainer(
        SlotInventory inventory,
        int slotIndex,
        ItemId sourceItemId,
        ItemId replacementItemId)
    {
        var selected = inventory.GetSlot(slotIndex);
        if (selected.ItemId != sourceItemId || selected.Amount <= 0)
        {
            return false;
        }

        var sourceAmount = selected.Amount;
        if (!inventory.RemoveFromSlot(slotIndex, sourceItemId, sourceAmount).Succeeded ||
            !inventory.AddToSlot(slotIndex, replacementItemId, 1).Succeeded)
        {
            return false;
        }

        return sourceAmount == 1 || inventory.Add(sourceItemId, sourceAmount - 1).Succeeded;
    }

    private static InventorySlotSnapshot[] CaptureInventory(SlotInventory inventory) => inventory.Slots
        .Select(slot => new InventorySlotSnapshot(slot.ItemId, slot.Amount))
        .ToArray();

    private static void RestoreInventory(
        SlotInventory inventory,
        IReadOnlyList<InventorySlotSnapshot> snapshot)
    {
        if (snapshot.Count != inventory.SlotCount)
        {
            throw new InvalidOperationException("A fuel-container rollback snapshot has the wrong slot count.");
        }

        for (var index = 0; index < inventory.SlotCount; index++)
        {
            inventory.GetMutableSlot(index).Clear();
        }

        for (var index = 0; index < snapshot.Count; index++)
        {
            if (snapshot[index] is { ItemId: { } itemId, Amount: > 0 } slot)
            {
                inventory.GetMutableSlot(index).Assign(itemId, slot.Amount);
            }
        }
    }

    private readonly record struct InventorySlotSnapshot(ItemId? ItemId, int Amount);
}

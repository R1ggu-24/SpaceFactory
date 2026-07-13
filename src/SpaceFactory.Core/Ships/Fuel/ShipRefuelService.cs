using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Ships.Fuel;

public enum ShipRefuelFailure
{
    None,
    InvalidContainerLimit,
    NoFilledFuelContainers,
    TankCannotFitFullContainer,
    NoSpaceForReturnedContainers,
}

public readonly record struct ShipRefuelResult(
    bool Succeeded,
    ShipRefuelFailure Failure,
    int TransferredContainerCount,
    double TransferredFuel)
{
    public static ShipRefuelResult Success(int containerCount, double transferredFuel) =>
        new(true, ShipRefuelFailure.None, containerCount, transferredFuel);

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

        if (maximumContainerCount is <= 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.InvalidContainerLimit);
        }

        var availableContainers = sourceInventory.GetAmount(ProductionItemIds.FuelContainer);
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

        var transferableContainers = FindLargestSafeContainerCount(sourceInventory, requestedContainers);
        if (transferableContainers == 0)
        {
            return ShipRefuelResult.Failed(ShipRefuelFailure.NoSpaceForReturnedContainers);
        }

        var fuelToTransfer = transferableContainers * ShipFuelConfiguration.FuelPerContainer;
        var removal = sourceInventory.Remove(ProductionItemIds.FuelContainer, transferableContainers);
        if (!removal.Succeeded)
        {
            throw new InvalidOperationException("The validated filled fuel containers could not be removed.");
        }

        var emptyContainerReturn = sourceInventory.Add(
            ProductionItemIds.EmptyFuelContainer,
            transferableContainers);
        if (!emptyContainerReturn.Succeeded)
        {
            RestoreFilledContainers(sourceInventory, transferableContainers);
            throw new InvalidOperationException("The validated empty fuel containers could not be returned.");
        }

        if (!tank.TryAddFuel(fuelToTransfer))
        {
            RestoreInventoryAfterRejectedTankFill(sourceInventory, transferableContainers);
            throw new InvalidOperationException("The validated fuel amount no longer fits into the tank.");
        }

        return ShipRefuelResult.Success(transferableContainers, fuelToTransfer);
    }

    private static int FindLargestSafeContainerCount(SlotInventory inventory, int requestedContainers)
    {
        for (var count = requestedContainers; count > 0; count--)
        {
            if (GetEmptyContainerCapacityAfterFuelRemoval(inventory, count) >= count)
            {
                return count;
            }
        }

        return 0;
    }

    private static int GetEmptyContainerCapacityAfterFuelRemoval(SlotInventory inventory, int removedFuelContainers)
    {
        var emptyContainerCapacity = inventory.Slots
            .Where(slot => slot.ItemId == ProductionItemIds.EmptyFuelContainer)
            .Sum(slot => slot.MaximumAmount - slot.Amount);
        emptyContainerCapacity += inventory.Slots.Count(slot => slot.IsEmpty) * inventory.MaximumStackSize;

        var remainingRemoval = removedFuelContainers;
        for (var index = inventory.SlotCount - 1; index >= 0 && remainingRemoval > 0; index--)
        {
            var slot = inventory.GetSlot(index);
            if (slot.ItemId != ProductionItemIds.FuelContainer)
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

    private static void RestoreFilledContainers(SlotInventory inventory, int containerCount)
    {
        var restore = inventory.Add(ProductionItemIds.FuelContainer, containerCount);
        if (!restore.Succeeded)
        {
            throw new InvalidOperationException("The fuel-container transaction could not be rolled back.");
        }
    }

    private static void RestoreInventoryAfterRejectedTankFill(SlotInventory inventory, int containerCount)
    {
        var removeReturned = inventory.Remove(ProductionItemIds.EmptyFuelContainer, containerCount);
        if (!removeReturned.Succeeded)
        {
            throw new InvalidOperationException("The empty-container return could not be rolled back.");
        }

        RestoreFilledContainers(inventory, containerCount);
    }
}

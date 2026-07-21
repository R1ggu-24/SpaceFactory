using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public enum GeneratorFuelTankTransferFailure
{
    None,
    UnsupportedMachine,
    TankFull,
    TankEmpty,
    TankCannotFillContainer,
    MissingFilledContainer,
    MissingEmptyContainer,
    WrongContent,
    TransferSlotBlocked,
}

public readonly record struct GeneratorFuelTankTransferResult(
    bool Succeeded,
    GeneratorFuelTankTransferFailure Failure,
    double TransferredFuelSeconds)
{
    public static GeneratorFuelTankTransferResult Success(double transferredFuelSeconds) =>
        new(true, GeneratorFuelTankTransferFailure.None, transferredFuelSeconds);

    public static GeneratorFuelTankTransferResult Failed(GeneratorFuelTankTransferFailure failure) =>
        new(false, failure, 0);
}

/// <summary>
/// Exchanges exactly one physical fuel container with a fuel generator's internal reservoir.
/// Containers are deliberately discrete: a transfer is only allowed when a complete container
/// fits in the tank (fill) or can be filled from it (drain). This avoids inventing partially filled
/// item instances in the stack-based inventory model.
/// </summary>
public static class GeneratorFuelTankTransfer
{
    private const double Tolerance = 0.000_000_001;

    public static GeneratorFuelTankTransferResult FillFromInput(MachineState machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (!IsSupported(machine))
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.UnsupportedMachine);
        }

        var secondsPerContainer = machine.Definition.GeneratorFuelSecondsPerItem;
        if (machine.GeneratorFuelSecondsRemaining + secondsPerContainer >
            machine.GeneratorFuelTankCapacitySeconds + Tolerance)
        {
            return GeneratorFuelTankTransferResult.Failed(GeneratorFuelTankTransferFailure.TankFull);
        }

        var filledContainer = machine.Definition.GeneratorFuelItemId!.Value;
        var transferSlot = GetTransferSlot(machine);
        if (transferSlot.IsEmpty)
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.MissingFilledContainer);
        }

        if (transferSlot.ItemId != filledContainer)
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.WrongContent);
        }

        return CommitInputSlotExchange(
            machine,
            filledContainer,
            machine.Definition.GeneratorReturnedContainerItemId!.Value,
            secondsPerContainer);
    }

    public static GeneratorFuelTankTransferResult DrainToInputContainer(MachineState machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (!IsSupported(machine))
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.UnsupportedMachine);
        }

        var secondsPerContainer = machine.Definition.GeneratorFuelSecondsPerItem;
        if (machine.GeneratorFuelSecondsRemaining <= Tolerance)
        {
            return GeneratorFuelTankTransferResult.Failed(GeneratorFuelTankTransferFailure.TankEmpty);
        }

        if (machine.GeneratorFuelSecondsRemaining + Tolerance < secondsPerContainer)
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.TankCannotFillContainer);
        }

        var emptyContainer = machine.Definition.GeneratorReturnedContainerItemId!.Value;
        var transferSlot = GetTransferSlot(machine);
        if (transferSlot.IsEmpty)
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.MissingEmptyContainer);
        }

        if (transferSlot.ItemId != emptyContainer)
        {
            return GeneratorFuelTankTransferResult.Failed(
                GeneratorFuelTankTransferFailure.WrongContent);
        }

        return CommitInputSlotExchange(
            machine,
            emptyContainer,
            machine.Definition.GeneratorFuelItemId!.Value,
            -secondsPerContainer);
    }

    private static bool IsSupported(MachineState machine) =>
        machine.Definition.Id == MachineDefinitionIds.FuelGenerator &&
        machine.Definition.IsFuelledGenerator;

    private static GeneratorFuelTankTransferResult CommitInputSlotExchange(
        MachineState machine,
        ItemId inputItem,
        ItemId replacementItem,
        double fuelSecondsDelta)
    {
        var inputSnapshot = Capture(machine.InputInventory);
        var fuelBefore = machine.GeneratorFuelSecondsRemaining;
        var transferSlot = GetTransferSlot(machine);
        try
        {
            var sourceAmount = transferSlot.Amount;
            if (!machine.InputInventory.RemoveFromSlot(
                    transferSlot.Index,
                    inputItem,
                    sourceAmount).Succeeded ||
                !machine.InputInventory.AddToSlot(
                    transferSlot.Index,
                    replacementItem,
                    1).Succeeded ||
                (sourceAmount > 1 &&
                 !machine.InputInventory.Add(inputItem, sourceAmount - 1).Succeeded))
            {
                Restore(machine.InputInventory, inputSnapshot);
                return GeneratorFuelTankTransferResult.Failed(
                    GeneratorFuelTankTransferFailure.TransferSlotBlocked);
            }

            machine.SetGeneratorFuelSecondsForTransfer(fuelBefore + fuelSecondsDelta);
            return GeneratorFuelTankTransferResult.Success(Math.Abs(fuelSecondsDelta));
        }
        catch
        {
            Restore(machine.InputInventory, inputSnapshot);
            machine.SetGeneratorFuelSecondsForTransfer(fuelBefore);
            throw;
        }
    }

    private static InventorySlot GetTransferSlot(MachineState machine) =>
        machine.InputInventory.GetSlot(ProductionConfiguration.FuelGeneratorTankTransferSlotIndex);

    private static SlotSnapshot[] Capture(SlotInventory inventory) => inventory.Slots
        .Select(slot => new SlotSnapshot(slot.ItemId, slot.Amount))
        .ToArray();

    private static void Restore(SlotInventory inventory, IReadOnlyList<SlotSnapshot> snapshot)
    {
        if (snapshot.Count != inventory.SlotCount)
        {
            throw new InvalidOperationException("A generator tank rollback snapshot has the wrong size.");
        }

        for (var index = 0; index < inventory.SlotCount; index++)
        {
            inventory.GetMutableSlot(index).Clear();
            if (snapshot[index] is { ItemId: { } itemId, Amount: > 0 } slot)
            {
                inventory.GetMutableSlot(index).Assign(itemId, slot.Amount);
            }
        }
    }

    private readonly record struct SlotSnapshot(ItemId? ItemId, int Amount);
}

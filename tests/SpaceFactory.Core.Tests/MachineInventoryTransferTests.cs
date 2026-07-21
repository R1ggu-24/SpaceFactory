using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class MachineInventoryTransferTests
{
    [Fact]
    public void RecipeLoad_IsAtomicWhenOneIngredientIsMissing()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var astronaut = new SlotInventory(4);
        var machine = new SlotInventory(4);
        astronaut.Add(ProductionItemIds.WaterContainer, 1);

        var result = MachineInventoryTransfer.LoadRecipeInputs(astronaut, machine, recipe);

        Assert.Equal(MachineInventoryTransferFailure.MissingSourceItems, result.Failure);
        Assert.Equal(1, astronaut.TotalItemCount);
        Assert.Equal(0, machine.TotalItemCount);
    }

    [Fact]
    public void RecipeLoad_MovesExactlyOneBatchWithoutDuplication()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var astronaut = new SlotInventory(4);
        var machine = new SlotInventory(4);
        astronaut.Add(ProductionItemIds.WaterContainer, 2);
        astronaut.Add(ProductionItemIds.EmptyGasContainer, 4);

        var result = MachineInventoryTransfer.LoadRecipeInputs(astronaut, machine, recipe);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.TransferredItemCount);
        Assert.Equal(3, astronaut.TotalItemCount);
        Assert.Equal(3, machine.TotalItemCount);
    }

    [Fact]
    public void CollectAll_LeavesBothInventoriesUntouchedWhenTargetIsFull()
    {
        var output = new SlotInventory(2);
        var astronaut = new SlotInventory(1);
        output.Add(ProductionItemIds.IronIngot, 10);
        astronaut.Add(ProductionItemIds.CopperOre, 200);

        var result = MachineInventoryTransfer.TransferAll(output, astronaut);

        Assert.Equal(MachineInventoryTransferFailure.TargetFull, result.Failure);
        Assert.Equal(10, output.GetAmount(ProductionItemIds.IronIngot));
        Assert.Equal(200, astronaut.GetAmount(ProductionItemIds.CopperOre));
    }

    [Fact]
    public void StorageTransfer_MovesWhatFitsAndLeavesTheRestRetrievable()
    {
        var storage = new SlotInventory(30);
        var astronaut = new SlotInventory(20);
        var itemIds = Enumerable.Range(0, 21)
            .Select(index => new ItemId($"storage_item_{index}"))
            .ToArray();
        foreach (var itemId in itemIds)
        {
            Assert.True(storage.Add(itemId, 1).Succeeded);
        }

        var first = MachineInventoryTransfer.TransferAsMuchAsPossible(storage, astronaut);

        Assert.True(first.Succeeded);
        Assert.Equal(20, first.TransferredItemCount);
        Assert.Equal(1, storage.TotalItemCount);
        Assert.Equal(20, astronaut.TotalItemCount);

        Assert.True(astronaut.Remove(itemIds[0], 1).Succeeded);
        var second = MachineInventoryTransfer.TransferAsMuchAsPossible(storage, astronaut);

        Assert.True(second.Succeeded);
        Assert.Equal(1, second.TransferredItemCount);
        Assert.Equal(0, storage.TotalItemCount);
        Assert.Equal(20, astronaut.TotalItemCount);
    }

    [Fact]
    public void PartialTransfer_RespectsPerItemStackLimitAndUsesNextFreeSlot()
    {
        static int StackSize(ItemId itemId) =>
            itemId == ProductionItemIds.MachineDismantlingTool ? 1 : 200;

        var output = new SlotInventory(2, itemStackSizeResolver: StackSize);
        var astronaut = new SlotInventory(2, itemStackSizeResolver: StackSize);
        Assert.True(output.Add(ProductionItemIds.MachineDismantlingTool, 1).Succeeded);
        Assert.True(astronaut.Add(ProductionItemIds.MachineDismantlingTool, 1).Succeeded);

        var result = MachineInventoryTransfer.TransferAsMuchAsPossible(output, astronaut);

        Assert.True(result.Succeeded);
        Assert.Equal(0, output.TotalItemCount);
        Assert.Equal(2, astronaut.GetAmount(ProductionItemIds.MachineDismantlingTool));
        Assert.All(astronaut.Slots.Where(slot => !slot.IsEmpty), slot => Assert.Equal(1, slot.Amount));
    }

    [Fact]
    public void PartialTransfer_SplitsALegacyOverstackAcrossNonStackableTargetSlots()
    {
        static int StackSize(ItemId itemId) =>
            itemId == ProductionItemIds.MachineDismantlingTool ? 1 : 200;

        var legacyOutput = new SlotInventory(1);
        var astronaut = new SlotInventory(2, itemStackSizeResolver: StackSize);
        Assert.True(legacyOutput.Add(ProductionItemIds.MachineDismantlingTool, 2).Succeeded);

        var result = MachineInventoryTransfer.TransferAsMuchAsPossible(legacyOutput, astronaut);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.TransferredItemCount);
        Assert.Equal(0, legacyOutput.TotalItemCount);
        Assert.Equal(2, astronaut.GetAmount(ProductionItemIds.MachineDismantlingTool));
        Assert.All(astronaut.Slots, slot => Assert.Equal(1, slot.Amount));
    }
}

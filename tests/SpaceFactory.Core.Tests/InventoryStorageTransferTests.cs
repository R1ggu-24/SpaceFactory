using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryStorageTransferTests
{
    private static readonly ItemId Iron = new("iron");
    private static readonly ItemId Copper = new("copper");

    [Fact]
    public void CrossInventoryTransfer_FillsExistingStackBeforeSelectedEmptySlot()
    {
        var personal = new SlotInventory(2);
        var storage = new SlotInventory(3);
        Assert.True(personal.Add(Iron, 25).Succeeded);
        Assert.True(storage.Add(Iron, 190).Succeeded);

        var result = InventoryTransfer.TransferPrioritizingExistingStacks(
            personal,
            sourceIndex: 0,
            storage,
            targetIndex: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(25, result.MovedAmount);
        Assert.Equal(200, storage.GetSlot(0).Amount);
        Assert.Equal(Iron, storage.GetSlot(2).ItemId);
        Assert.Equal(15, storage.GetSlot(2).Amount);
        Assert.True(personal.GetSlot(0).IsEmpty);
        Assert.Equal(215, personal.TotalItemCount + storage.TotalItemCount);
    }

    [Fact]
    public void CrossInventoryTransfer_ToNearlyFullStack_LeavesOverflowInSource()
    {
        var personal = new SlotInventory(2);
        var storage = new SlotInventory(1);
        Assert.True(personal.Add(Iron, 25).Succeeded);
        Assert.True(storage.Add(Iron, 199).Succeeded);

        var result = InventoryTransfer.TransferPrioritizingExistingStacks(
            personal,
            sourceIndex: 0,
            storage,
            targetIndex: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.MovedAmount);
        Assert.Equal(24, result.RemainingAmount);
        Assert.Equal(24, personal.GetSlot(0).Amount);
        Assert.Equal(200, storage.GetSlot(0).Amount);
        Assert.Equal(224, personal.TotalItemCount + storage.TotalItemCount);
    }

    [Fact]
    public void DifferentItems_KeepAtomicSwapSemantics()
    {
        var personal = new SlotInventory(1);
        var storage = new SlotInventory(1);
        Assert.True(personal.Add(Iron, 17).Succeeded);
        Assert.True(storage.Add(Copper, 23).Succeeded);

        var result = InventoryTransfer.TransferPrioritizingExistingStacks(
            personal,
            sourceIndex: 0,
            storage,
            targetIndex: 0);

        Assert.True(result.Succeeded);
        Assert.True(result.Swapped);
        Assert.Equal(Copper, personal.GetSlot(0).ItemId);
        Assert.Equal(23, personal.GetSlot(0).Amount);
        Assert.Equal(Iron, storage.GetSlot(0).ItemId);
        Assert.Equal(17, storage.GetSlot(0).Amount);
        Assert.Equal(40, personal.TotalItemCount + storage.TotalItemCount);
    }
}

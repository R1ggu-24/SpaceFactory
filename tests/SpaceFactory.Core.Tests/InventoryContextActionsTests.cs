using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryContextActionsTests
{
    private static readonly ItemId Iron = new("iron");
    private static readonly ItemId Copper = new("copper");

    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(3, 2, 1)]
    [InlineData(200, 100, 100)]
    public void SplitStack_MovesHalfIntoFirstEmptySlot(int initial, int expectedSource, int expectedTarget)
    {
        var inventory = new SlotInventory(3);
        Assert.True(inventory.AddToSlot(0, Iron, initial).Succeeded);

        var result = InventoryContextActions.SplitStack(inventory, 0, Iron);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.TargetSlotIndex);
        Assert.Equal(expectedSource, inventory.GetSlot(0).Amount);
        Assert.Equal(expectedTarget, inventory.GetSlot(1).Amount);
        Assert.Equal(initial, inventory.TotalItemCount);
    }

    [Fact]
    public void TakeSingleItem_MovesExactlyOneWithoutChangingTotal()
    {
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(0, Iron, 200).Succeeded);

        var result = InventoryContextActions.TakeSingleItem(inventory, 0, Iron);

        Assert.True(result.Succeeded);
        Assert.Equal(199, inventory.GetSlot(0).Amount);
        Assert.Equal(1, inventory.GetSlot(1).Amount);
        Assert.Equal(200, inventory.TotalItemCount);
    }

    [Fact]
    public void SplitStack_WhenInventoryIsFull_DoesNotMutateAnything()
    {
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(0, Iron, 12).Succeeded);
        Assert.True(inventory.AddToSlot(1, Copper, 7).Succeeded);

        var result = InventoryContextActions.SplitStack(inventory, 0, Iron);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.TargetStackFull, result.Transfer.Failure);
        Assert.Equal(12, inventory.GetSlot(0).Amount);
        Assert.Equal(7, inventory.GetSlot(1).Amount);
    }

    [Fact]
    public void MoveToFirstFreeOrFallback_UsesFirstFreeHotbarSlot()
    {
        var source = new SlotInventory(2);
        var hotbar = new SlotInventory(6);
        Assert.True(source.AddToSlot(0, Iron, 14).Succeeded);
        Assert.True(hotbar.AddToSlot(0, Copper, 5).Succeeded);

        var result = InventoryContextActions.MoveToFirstFreeOrFallback(source, 0, Iron, hotbar);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.TargetSlotIndex);
        Assert.True(source.GetSlot(0).IsEmpty);
        Assert.Equal(Iron, hotbar.GetSlot(1).ItemId);
        Assert.Equal(19, source.TotalItemCount + hotbar.TotalItemCount);
    }

    [Fact]
    public void MoveToFirstFreeOrFallback_FullTargetSwapsWithSlotOneWithoutLoss()
    {
        var source = new SlotInventory(1);
        var hotbar = new SlotInventory(6);
        Assert.True(source.AddToSlot(0, Iron, 14).Succeeded);
        for (var index = 0; index < hotbar.SlotCount; index++)
        {
            Assert.True(hotbar.AddToSlot(index, Copper, index + 1).Succeeded);
        }

        var totalBefore = source.TotalItemCount + hotbar.TotalItemCount;
        var result = InventoryContextActions.MoveToFirstFreeOrFallback(source, 0, Iron, hotbar);

        Assert.True(result.Succeeded);
        Assert.True(result.Transfer.Swapped);
        Assert.Equal(0, result.TargetSlotIndex);
        Assert.Equal(Copper, source.GetSlot(0).ItemId);
        Assert.Equal(Iron, hotbar.GetSlot(0).ItemId);
        Assert.Equal(totalBefore, source.TotalItemCount + hotbar.TotalItemCount);
    }

    [Fact]
    public void ContextAction_WithStaleExpectedItem_DoesNotMutateReplacementStack()
    {
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(0, Copper, 9).Succeeded);

        var result = InventoryContextActions.SplitStack(inventory, 0, Iron);

        Assert.False(result.Succeeded);
        Assert.Equal(9, inventory.GetSlot(0).Amount);
        Assert.True(inventory.GetSlot(1).IsEmpty);
    }

    [Fact]
    public void FullHotbar_WhenSourceRejectsDisplacedItem_FailsWithoutMutation()
    {
        var source = new SlotInventory(1, itemAcceptanceResolver: itemId => itemId == Iron);
        var hotbar = new SlotInventory(6);
        Assert.True(source.AddToSlot(0, Iron, 4).Succeeded);
        for (var index = 0; index < hotbar.SlotCount; index++)
        {
            Assert.True(hotbar.AddToSlot(index, Copper, index + 1).Succeeded);
        }

        var totalBefore = source.TotalItemCount + hotbar.TotalItemCount;
        var result = InventoryContextActions.MoveToFirstFreeOrFallback(source, 0, Iron, hotbar);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.ItemNotAccepted, result.Transfer.Failure);
        Assert.Equal(Iron, source.GetSlot(0).ItemId);
        Assert.Equal(4, source.GetSlot(0).Amount);
        Assert.Equal(Copper, hotbar.GetSlot(0).ItemId);
        Assert.Equal(totalBefore, source.TotalItemCount + hotbar.TotalItemCount);
    }
}

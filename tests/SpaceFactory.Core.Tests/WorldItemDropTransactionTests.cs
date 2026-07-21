using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class WorldItemDropTransactionTests
{
    private static readonly ItemId Iron = new("iron_ore");

    [Fact]
    public void ReserveEntireStack_RemovesOnlyTheExplicitSourceSlot()
    {
        var inventory = new SlotInventory(3);
        Assert.True(inventory.AddToSlot(0, Iron, 40).Succeeded);
        Assert.True(inventory.AddToSlot(1, Iron, 75).Succeeded);

        Assert.True(WorldItemDropTransaction.TryReserveEntireStack(inventory, 1, out var reservation));

        Assert.Equal(new WorldItemDropReservation(1, Iron, 75), reservation);
        Assert.Equal(40, inventory.GetSlot(0).Amount);
        Assert.True(inventory.GetSlot(1).IsEmpty);
    }

    [Fact]
    public void Rollback_RestoresExactSourceSlotWithoutSpilling()
    {
        var inventory = new SlotInventory(3);
        Assert.True(inventory.AddToSlot(0, Iron, 200).Succeeded);
        Assert.True(inventory.AddToSlot(2, Iron, 83).Succeeded);
        Assert.True(WorldItemDropTransaction.TryReserveEntireStack(inventory, 2, out var reservation));

        Assert.True(WorldItemDropTransaction.Rollback(inventory, reservation));

        Assert.Equal(200, inventory.GetSlot(0).Amount);
        Assert.True(inventory.GetSlot(1).IsEmpty);
        Assert.Equal(83, inventory.GetSlot(2).Amount);
    }

    [Fact]
    public void Reserve_RejectsEmptyAndInvalidSlotsWithoutMutation()
    {
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(0, Iron, 12).Succeeded);

        Assert.False(WorldItemDropTransaction.TryReserveEntireStack(inventory, 1, out _));
        Assert.False(WorldItemDropTransaction.TryReserveEntireStack(inventory, -1, out _));

        Assert.Equal(12, inventory.GetSlot(0).Amount);
        Assert.True(inventory.GetSlot(1).IsEmpty);
    }
}

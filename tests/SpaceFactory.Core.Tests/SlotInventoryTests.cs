using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class SlotInventoryTests
{
    private static readonly ItemId IronOre = new("iron_ore");
    private static readonly ItemId WaterIce = new("water_ice");

    [Fact]
    public void Configuration_UsesRequestedInventorySizesAndStackLimit()
    {
        Assert.Equal(20, InventoryConfiguration.AstronautSlotCount);
        Assert.Equal(50, InventoryConfiguration.ShipSlotCount);
        Assert.Equal(200, InventoryConfiguration.MaximumStackSize);
    }

    [Fact]
    public void Constructor_CreatesStableIndexedEmptySlots()
    {
        var inventory = new SlotInventory(InventoryConfiguration.AstronautSlotCount);

        Assert.Equal(20, inventory.SlotCount);
        Assert.Equal(Enumerable.Range(0, 20), inventory.Slots.Select(slot => slot.Index));
        Assert.All(inventory.Slots, slot =>
        {
            Assert.True(slot.IsEmpty);
            Assert.Null(slot.ItemId);
            Assert.Null(slot.Stack);
            Assert.Equal(0, slot.Amount);
            Assert.Equal(200, slot.MaximumAmount);
        });
    }

    [Fact]
    public void Add_FillsPartialStacksBeforeUsingAnEmptySlot()
    {
        var inventory = new SlotInventory(3);
        Assert.True(inventory.Add(IronOre, 250).Succeeded);

        var result = inventory.Add(IronOre, 75);

        Assert.True(result.Succeeded);
        Assert.Equal(200, inventory.GetSlot(0).Amount);
        Assert.Equal(125, inventory.GetSlot(1).Amount);
        Assert.True(inventory.GetSlot(2).IsEmpty);
        Assert.Equal(325, inventory.GetAmount(IronOre));
    }

    [Fact]
    public void Add_ToStackWith199_UsesNextSlotForOverflow()
    {
        var inventory = new SlotInventory(2);
        inventory.Add(IronOre, 199);

        var result = inventory.Add(IronOre, 3);

        Assert.True(result.Succeeded);
        Assert.Equal(200, inventory.GetSlot(0).Amount);
        Assert.Equal(2, inventory.GetSlot(1).Amount);
    }

    [Fact]
    public void Add_WhenInventoryCannotFitWholeReward_IsAtomic()
    {
        var inventory = new SlotInventory(2);
        inventory.Add(IronOre, 199);
        inventory.Add(WaterIce, 200);

        var result = inventory.Add(IronOre, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryFailure.CapacityExceeded, result.Failure);
        Assert.Equal(199, inventory.GetSlot(0).Amount);
        Assert.Equal(200, inventory.GetSlot(1).Amount);
        Assert.Equal(399, inventory.TotalItemCount);
    }

    [Fact]
    public void Add_LargerThanOneStack_SplitsAtMaximumStackSize()
    {
        var inventory = new SlotInventory(3);

        var result = inventory.Add(IronOre, 450);

        Assert.True(result.Succeeded);
        Assert.Equal([200, 200, 50], inventory.Slots.Select(slot => slot.Amount));
        Assert.All(inventory.Slots, slot => Assert.InRange(slot.Amount, 0, 200));
    }

    [Fact]
    public void Remove_ConsumesLaterStacksFirstAndClearsEmptySlots()
    {
        var inventory = new SlotInventory(3);
        inventory.Add(IronOre, 450);

        var result = inventory.Remove(IronOre, 75);

        Assert.True(result.Succeeded);
        Assert.Equal(200, inventory.GetSlot(0).Amount);
        Assert.Equal(175, inventory.GetSlot(1).Amount);
        Assert.True(inventory.GetSlot(2).IsEmpty);
        Assert.Equal(375, inventory.GetAmount(IronOre));
    }

    [Fact]
    public void Remove_TooMany_IsAtomic()
    {
        var inventory = new SlotInventory(2);
        inventory.Add(IronOre, 250);

        var result = inventory.Remove(IronOre, 251);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryFailure.InsufficientItems, result.Failure);
        Assert.Equal(250, inventory.GetAmount(IronOre));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_InvalidAmount_ReturnsControlledFailure(int amount)
    {
        var inventory = new SlotInventory(1);

        var result = inventory.Add(IronOre, amount);

        Assert.Equal(InventoryFailure.InvalidAmount, result.Failure);
        Assert.True(inventory.GetSlot(0).IsEmpty);
    }
}

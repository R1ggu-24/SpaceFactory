using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using InventoryModel = SpaceFactory.Core.Inventory.Inventory;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryTests
{
    private static readonly ItemId IronOre = new("iron_ore");

    [Fact]
    public void Add_StoresItems()
    {
        var inventory = new InventoryModel(10);

        var result = inventory.Add(IronOre, 4);

        Assert.True(result.Succeeded);
        Assert.Equal(4, inventory.GetAmount(IronOre));
    }

    [Fact]
    public void Remove_RemovesItems()
    {
        var inventory = new InventoryModel(10);
        inventory.Add(IronOre, 4);

        var result = inventory.Remove(IronOre, 3);

        Assert.True(result.Succeeded);
        Assert.Equal(1, inventory.GetAmount(IronOre));
    }

    [Fact]
    public void Remove_TooManyItems_ReturnsControlledFailure()
    {
        var inventory = new InventoryModel(10);
        inventory.Add(IronOre, 2);

        var result = inventory.Remove(IronOre, 3);

        Assert.Equal(InventoryFailure.InsufficientItems, result.Failure);
        Assert.Equal(2, inventory.GetAmount(IronOre));
    }

    [Fact]
    public void Add_OverCapacity_ReturnsControlledFailure()
    {
        var result = new InventoryModel(2).Add(IronOre, 3);

        Assert.Equal(InventoryFailure.CapacityExceeded, result.Failure);
    }

    [Fact]
    public void ItemId_Empty_Throws() => Assert.Throws<ArgumentException>(() => new ItemId(" "));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_InvalidAmount_ReturnsControlledFailure(int amount)
    {
        var result = new InventoryModel(10).Add(IronOre, amount);

        Assert.Equal(InventoryFailure.InvalidAmount, result.Failure);
    }
}

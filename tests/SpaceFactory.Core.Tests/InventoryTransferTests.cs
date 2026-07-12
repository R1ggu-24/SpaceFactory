using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryTransferTests
{
    private static readonly ItemId IronOre = new("iron_ore");
    private static readonly ItemId WaterIce = new("water_ice");

    [Fact]
    public void Transfer_ToEmptySlot_MovesWholeStackWithoutLoss()
    {
        var astronaut = CreateInventoryWith(IronOre, 137);
        var ship = new SlotInventory(InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 12);

        Assert.True(result.Succeeded);
        Assert.Equal(137, result.MovedAmount);
        Assert.True(astronaut.GetSlot(0).IsEmpty);
        Assert.Equal(IronOre, ship.GetSlot(12).ItemId);
        Assert.Equal(137, ship.GetSlot(12).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_FromShipToAstronaut_UsesSameCentralLogic()
    {
        var astronaut = new SlotInventory(InventoryConfiguration.AstronautSlotCount);
        var ship = CreateInventoryWith(WaterIce, 84, InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(ship, 0, astronaut, 19);

        Assert.True(result.Succeeded);
        Assert.True(ship.GetSlot(0).IsEmpty);
        Assert.Equal(WaterIce, astronaut.GetSlot(19).ItemId);
        Assert.Equal(84, astronaut.GetSlot(19).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_ToSameResource_MergesStacks()
    {
        var astronaut = CreateInventoryWith(IronOre, 70);
        var ship = CreateInventoryWith(IronOre, 100, InventoryConfiguration.ShipSlotCount);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(70, result.MovedAmount);
        Assert.Equal(0, result.RemainingAmount);
        Assert.True(astronaut.GetSlot(0).IsEmpty);
        Assert.Equal(170, ship.GetSlot(0).Amount);
    }

    [Fact]
    public void Transfer_ToStackWith199_LeavesOverflowInSource()
    {
        var astronaut = CreateInventoryWith(IronOre, 50);
        var ship = CreateInventoryWith(IronOre, 199, InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.RequestedAmount);
        Assert.Equal(1, result.MovedAmount);
        Assert.Equal(49, result.RemainingAmount);
        Assert.Equal(49, astronaut.GetSlot(0).Amount);
        Assert.Equal(200, ship.GetSlot(0).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_ToFullSameResourceStack_IsRejectedWithoutMutation()
    {
        var astronaut = CreateInventoryWith(IronOre, 25);
        var ship = CreateInventoryWith(IronOre, 200, InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.TargetStackFull, result.Failure);
        Assert.Equal(25, astronaut.GetSlot(0).Amount);
        Assert.Equal(200, ship.GetSlot(0).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_OntoDifferentResource_SwapsWholeStacks()
    {
        var astronaut = CreateInventoryWith(IronOre, 200);
        var ship = CreateInventoryWith(WaterIce, 173, InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.True(result.Succeeded);
        Assert.True(result.Swapped);
        Assert.Equal(WaterIce, astronaut.GetSlot(0).ItemId);
        Assert.Equal(173, astronaut.GetSlot(0).Amount);
        Assert.Equal(IronOre, ship.GetSlot(0).ItemId);
        Assert.Equal(200, ship.GetSlot(0).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_PartialStackOntoDifferentResource_IsRejectedWithoutLoss()
    {
        var astronaut = CreateInventoryWith(IronOre, 100);
        var ship = CreateInventoryWith(WaterIce, 80, InventoryConfiguration.ShipSlotCount);
        var totalBefore = Total(astronaut, ship);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0, 40);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.IncompatibleStacks, result.Failure);
        Assert.Equal(IronOre, astronaut.GetSlot(0).ItemId);
        Assert.Equal(100, astronaut.GetSlot(0).Amount);
        Assert.Equal(WaterIce, ship.GetSlot(0).ItemId);
        Assert.Equal(80, ship.GetSlot(0).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
    }

    [Fact]
    public void Transfer_PartOfStackToEmptySlot_LeavesRemainderInSource()
    {
        var astronaut = CreateInventoryWith(IronOre, 100);
        var ship = new SlotInventory(InventoryConfiguration.ShipSlotCount);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0, 35);

        Assert.True(result.Succeeded);
        Assert.Equal(65, astronaut.GetSlot(0).Amount);
        Assert.Equal(35, ship.GetSlot(0).Amount);
        Assert.Equal(35, result.MovedAmount);
    }

    [Fact]
    public void Transfer_WithinSameInventory_PreservesSlotIndicesAndTotals()
    {
        var inventory = CreateInventoryWith(IronOre, 250);
        var slotReferences = inventory.Slots.ToArray();

        var result = InventoryTransfer.Transfer(inventory, 1, inventory, 2);

        Assert.True(result.Succeeded);
        Assert.True(inventory.GetSlot(1).IsEmpty);
        Assert.Equal(50, inventory.GetSlot(2).Amount);
        Assert.Equal(250, inventory.TotalItemCount);
        Assert.All(slotReferences, (slot, index) => Assert.Same(slot, inventory.GetSlot(index)));
    }

    [Fact]
    public void Transfer_FromEmptySlot_ReturnsControlledFailure()
    {
        var astronaut = new SlotInventory(20);
        var ship = new SlotInventory(50);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.SourceEmpty, result.Failure);
    }

    [Fact]
    public void Transfer_MoreThanSourceContains_IsRejectedWithoutMutation()
    {
        var astronaut = CreateInventoryWith(IronOre, 12);
        var ship = new SlotInventory(50);

        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0, 13);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.InsufficientItems, result.Failure);
        Assert.Equal(12, astronaut.GetSlot(0).Amount);
        Assert.True(ship.GetSlot(0).IsEmpty);
    }

    [Fact]
    public void Transfer_IntoCompletelyOccupiedInventory_CanStillSwapWithoutLoss()
    {
        var astronaut = CreateInventoryWith(IronOre, 75);
        var ship = new SlotInventory(InventoryConfiguration.ShipSlotCount);
        for (var index = 0; index < ship.SlotCount; index++)
        {
            var resource = index == 0 ? WaterIce : new ItemId($"resource_{index}");
            Assert.True(ship.Add(resource, 200).Succeeded);
        }

        var totalBefore = Total(astronaut, ship);
        var result = InventoryTransfer.Transfer(astronaut, 0, ship, 0);

        Assert.True(result.Succeeded);
        Assert.True(result.Swapped);
        Assert.Equal(WaterIce, astronaut.GetSlot(0).ItemId);
        Assert.Equal(200, astronaut.GetSlot(0).Amount);
        Assert.Equal(IronOre, ship.GetSlot(0).ItemId);
        Assert.Equal(75, ship.GetSlot(0).Amount);
        Assert.Equal(totalBefore, Total(astronaut, ship));
        Assert.All(astronaut.Slots.Concat(ship.Slots), slot => Assert.InRange(slot.Amount, 0, 200));
    }

    private static SlotInventory CreateInventoryWith(
        ItemId itemId,
        int amount,
        int slotCount = InventoryConfiguration.AstronautSlotCount)
    {
        var inventory = new SlotInventory(slotCount);
        Assert.True(inventory.Add(itemId, amount).Succeeded);
        return inventory;
    }

    private static int Total(params SlotInventory[] inventories) =>
        inventories.Sum(inventory => inventory.TotalItemCount);
}

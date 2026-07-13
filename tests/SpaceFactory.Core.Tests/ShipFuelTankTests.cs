using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Core.Tests;

public sealed class ShipFuelTankTests
{
    private static readonly ItemId OtherItem = new("other_item");

    [Fact]
    public void Configuration_FullTankFundsExactlyThirtyMinutesOfBoost()
    {
        Assert.Equal(1_800, ShipFuelConfiguration.TankCapacity);
        Assert.Equal(1, ShipFuelConfiguration.ConsumptionPerBoostSecond);
        Assert.Equal(1_800, ShipFuelConfiguration.MaximumBoostDurationSeconds);
        Assert.Equal(0.000_001, ShipFuelConfiguration.MinimumFuelToActivateBoost);
        Assert.Equal(100, ShipFuelConfiguration.FuelPerContainer);

        var tank = new ShipFuelTank();

        Assert.Equal(1_800, tank.RemainingBoostSeconds);
        Assert.Equal(1, tank.FillRatio);
    }

    [Fact]
    public void ConsumeBoostFuel_FullTankAllowsExactlyEighteenHundredSeconds()
    {
        var tank = new ShipFuelTank();

        for (var second = 0; second < 1_800; second++)
        {
            var result = tank.ConsumeBoostFuel(1, isBoostActuallyActive: true);
            Assert.True(result.BoostAllowed);
            Assert.Equal(1, result.ConsumedFuel);
        }

        var denied = tank.ConsumeBoostFuel(1, isBoostActuallyActive: true);

        Assert.Equal(0, tank.CurrentFuel);
        Assert.False(tank.CanActivateBoost);
        Assert.False(denied.BoostAllowed);
        Assert.Equal(BoostFuelConsumptionFailure.TankBelowMinimum, denied.Failure);
    }

    [Fact]
    public void ConsumeBoostFuel_InactiveOrBlockedBoostConsumesNothing()
    {
        var tank = new ShipFuelTank(50);

        var result = tank.ConsumeBoostFuel(10, isBoostActuallyActive: false);

        Assert.False(result.BoostAllowed);
        Assert.Equal(BoostFuelConsumptionFailure.BoostNotActive, result.Failure);
        Assert.Equal(0, result.ConsumedFuel);
        Assert.Equal(50, tank.CurrentFuel);
    }

    [Fact]
    public void ConsumeBoostFuel_FinalPartialFrameConsumesRemainderAndStopsBoost()
    {
        var tank = new ShipFuelTank(2);

        var result = tank.ConsumeBoostFuel(3, isBoostActuallyActive: true);

        Assert.True(result.BoostAllowed);
        Assert.Equal(BoostFuelConsumptionFailure.None, result.Failure);
        Assert.Equal(3, result.RequestedFuel);
        Assert.Equal(2, result.ConsumedFuel);
        Assert.Equal(0, tank.CurrentFuel);
        Assert.False(tank.CanActivateBoost);
    }

    [Fact]
    public void ConsumeBoostFuel_SixtyFpsFramesUseTheWholeEighteenHundredSecondTank()
    {
        var tank = new ShipFuelTank();
        var consumed = 0.0;

        while (tank.CanActivateBoost)
        {
            var result = tank.ConsumeBoostFuel(1.0 / 60.0, isBoostActuallyActive: true);
            Assert.True(result.Succeeded);
            consumed += result.ConsumedFuel;
        }

        Assert.Equal(ShipFuelConfiguration.TankCapacity, consumed, 6);
        Assert.Equal(0, tank.CurrentFuel);
    }

    [Fact]
    public void TryAddFuel_OverCapacityOrInvalidAmount_IsAtomic()
    {
        var tank = new ShipFuelTank(1_750);

        Assert.False(tank.TryAddFuel(100));
        Assert.False(tank.TryAddFuel(0));
        Assert.False(tank.TryAddFuel(double.NaN));
        Assert.Equal(1_750, tank.CurrentFuel);

        Assert.True(tank.TryAddFuel(50));
        Assert.Equal(1_800, tank.CurrentFuel);
        Assert.Equal(0, tank.RemainingCapacity);
    }

    [Fact]
    public void TransferFilledContainers_FillsTankAndReturnsEveryEmptyContainer()
    {
        var tank = new ShipFuelTank(0);
        var inventory = new SlotInventory(2);
        inventory.Add(ProductionItemIds.FuelContainer, 20);

        var result = ShipRefuelService.TransferFilledContainers(tank, inventory);

        Assert.True(result.Succeeded);
        Assert.Equal(18, result.TransferredContainerCount);
        Assert.Equal(1_800, result.TransferredFuel);
        Assert.Equal(1_800, tank.CurrentFuel);
        Assert.Equal(2, inventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(18, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(20, inventory.TotalItemCount);
    }

    [Fact]
    public void TransferFilledContainers_ExcessFuelStaysInContainer()
    {
        var tank = new ShipFuelTank(1_750);
        var inventory = new SlotInventory(1);
        inventory.Add(ProductionItemIds.FuelContainer, 1);

        var result = ShipRefuelService.TransferFilledContainers(tank, inventory);

        Assert.False(result.Succeeded);
        Assert.Equal(ShipRefuelFailure.TankCannotFitFullContainer, result.Failure);
        Assert.Equal(1_750, tank.CurrentFuel);
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
    }

    [Fact]
    public void TransferFilledContainers_NoSpaceForReturnedContainerChangesNothing()
    {
        var tank = new ShipFuelTank(1_700);
        var inventory = new SlotInventory(2);
        inventory.Add(ProductionItemIds.FuelContainer, 2);
        inventory.Add(OtherItem, 200);

        var result = ShipRefuelService.TransferFilledContainers(tank, inventory);

        Assert.False(result.Succeeded);
        Assert.Equal(ShipRefuelFailure.NoSpaceForReturnedContainers, result.Failure);
        Assert.Equal(1_700, tank.CurrentFuel);
        Assert.Equal(2, inventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(202, inventory.TotalItemCount);
    }

    [Fact]
    public void TransferFilledContainers_RespectsExplicitContainerLimit()
    {
        var tank = new ShipFuelTank(0);
        var inventory = new SlotInventory(2);
        inventory.Add(ProductionItemIds.FuelContainer, 5);

        var result = ShipRefuelService.TransferFilledContainers(tank, inventory, maximumContainerCount: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.TransferredContainerCount);
        Assert.Equal(200, tank.CurrentFuel);
        Assert.Equal(3, inventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(2, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(5, inventory.TotalItemCount);
    }
}

using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Ships;
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
        Assert.Equal(ShipFuelType.Standard, ShipFuelConfiguration.NewGameFuelType);
        Assert.Equal(1.7, ShipFuelConfiguration.HighPerformanceBoostSpeedFactor);
        if (ShipFuelConfiguration.IncludeHighPerformanceTestTankInNewGame)
        {
            Assert.Equal(18, ShipFuelConfiguration.HighPerformanceTestContainerCount);
            Assert.Equal(
                ProductionItemIds.HighPerformanceFuelContainer,
                ShipFuelConfiguration.HighPerformanceTestCargo.ItemId);
        }

        var tank = new ShipFuelTank();

        Assert.Equal(1_800, tank.RemainingBoostSeconds);
        Assert.Equal(1, tank.FillRatio);
        Assert.Equal(ShipFuelType.Standard, tank.CurrentFuelType);
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
    public void TryAddFuel_RejectsMixingUntilTankIsEmpty()
    {
        var tank = new ShipFuelTank(200, ShipFuelType.Standard);

        Assert.False(tank.TryAddFuel(100, ShipFuelType.HighPerformance));
        Assert.Equal(200, tank.CurrentFuel);
        Assert.Equal(ShipFuelType.Standard, tank.CurrentFuelType);

        tank.RestoreFuel(0, ShipFuelType.Standard);

        Assert.True(tank.TryAddFuel(100, ShipFuelType.HighPerformance));
        Assert.Equal(100, tank.CurrentFuel);
        Assert.Equal(ShipFuelType.HighPerformance, tank.CurrentFuelType);
    }

    [Fact]
    public void FuelType_SelectsItsCentralBoostSpeed()
    {
        var standard = new ShipFuelTank(100, ShipFuelType.Standard);
        var highPerformance = new ShipFuelTank(100, ShipFuelType.HighPerformance);

        Assert.Equal(ShipFlightConfiguration.StandardBoostFlightSpeed, standard.BoostFlightSpeed);
        Assert.Equal(
            ShipFlightConfiguration.HighPerformanceBoostFlightSpeed,
            highPerformance.BoostFlightSpeed);
        Assert.Equal(standard.BoostFlightSpeed * 1.7, highPerformance.BoostFlightSpeed, 8);
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
        Assert.Equal(ShipFuelType.HighPerformance, tank.CurrentFuelType);
        Assert.Equal(ShipFuelType.HighPerformance, result.FuelType);
    }

    [Fact]
    public void TransferStandardFuelContainers_FillsOnlyAStandardTank()
    {
        var tank = new ShipFuelTank(0, ShipFuelType.Standard);
        var inventory = new SlotInventory(2);
        inventory.Add(ProductionItemIds.StandardFuelContainer, 3);

        var result = ShipRefuelService.TransferFilledContainers(
            tank,
            inventory,
            ShipFuelType.Standard);

        Assert.True(result.Succeeded);
        Assert.Equal(ShipFuelType.Standard, result.FuelType);
        Assert.Equal(300, tank.CurrentFuel);
        Assert.Equal(ShipFuelType.Standard, tank.CurrentFuelType);
        Assert.Equal(0, inventory.GetAmount(ProductionItemIds.StandardFuelContainer));
        Assert.Equal(3, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
    }

    [Fact]
    public void TransferDifferentFuelIntoNonEmptyTank_IsRejectedAtomically()
    {
        var tank = new ShipFuelTank(100, ShipFuelType.Standard);
        var inventory = new SlotInventory(2);
        inventory.Add(ProductionItemIds.HighPerformanceFuelContainer, 1);

        var result = ShipRefuelService.TransferFilledContainers(
            tank,
            inventory,
            ShipFuelType.HighPerformance);

        Assert.False(result.Succeeded);
        Assert.Equal(ShipRefuelFailure.TankContainsDifferentFuel, result.Failure);
        Assert.Equal(100, tank.CurrentFuel);
        Assert.Equal(ShipFuelType.Standard, tank.CurrentFuelType);
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.HighPerformanceFuelContainer));
        Assert.Equal(0, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
    }

    [Fact]
    public void TransferFilledContainers_ExcessFuelStaysInContainer()
    {
        var tank = new ShipFuelTank(1_750, ShipFuelType.HighPerformance);
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
        var tank = new ShipFuelTank(1_700, ShipFuelType.HighPerformance);
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

    [Fact]
    public void SelectedFuelSlot_FillsTankAndLeavesEmptyContainerInSameSlot()
    {
        var tank = new ShipFuelTank(0, ShipFuelType.Standard);
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(1, ProductionItemIds.StandardFuelContainer, 1).Succeeded);

        var result = ShipRefuelService.TransferFilledContainerFromSlot(tank, inventory, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(100, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, inventory.GetSlot(1).ItemId);
        Assert.Equal(1, inventory.TotalItemCount);
    }

    [Fact]
    public void SelectedEmptySlot_DrainsTankAndLeavesMatchingFilledContainerInSameSlot()
    {
        var tank = new ShipFuelTank(250, ShipFuelType.HighPerformance);
        var inventory = new SlotInventory(2);
        Assert.True(inventory.AddToSlot(1, ProductionItemIds.EmptyFuelContainer, 1).Succeeded);

        var result = ShipRefuelService.TransferTankToContainerInSlot(tank, inventory, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(150, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.HighPerformanceFuelContainer, inventory.GetSlot(1).ItemId);
        Assert.Equal(1, inventory.TotalItemCount);
    }

    [Fact]
    public void SelectedStackedFuelSlot_RelocatesRemainderAndExchangesExactlyOneContainer()
    {
        var tank = new ShipFuelTank(0, ShipFuelType.Standard);
        var inventory = new SlotInventory(3);
        Assert.True(inventory.AddToSlot(1, ProductionItemIds.StandardFuelContainer, 3).Succeeded);

        var result = ShipRefuelService.TransferFilledContainerFromSlot(tank, inventory, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(100, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, inventory.GetSlot(1).ItemId);
        Assert.Equal(1, inventory.GetSlot(1).Amount);
        Assert.Equal(2, inventory.GetAmount(ProductionItemIds.StandardFuelContainer));
        Assert.Equal(3, inventory.TotalItemCount);
    }

    [Fact]
    public void SelectedStackedEmptySlot_RelocatesRemainderAndDrainsExactlyOneContainer()
    {
        var tank = new ShipFuelTank(250, ShipFuelType.HighPerformance);
        var inventory = new SlotInventory(3);
        Assert.True(inventory.AddToSlot(1, ProductionItemIds.EmptyFuelContainer, 3).Succeeded);

        var result = ShipRefuelService.TransferTankToContainerInSlot(tank, inventory, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(150, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.HighPerformanceFuelContainer, inventory.GetSlot(1).ItemId);
        Assert.Equal(1, inventory.GetSlot(1).Amount);
        Assert.Equal(2, inventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(3, inventory.TotalItemCount);
    }

    [Fact]
    public void SelectedStackedContainerWithoutRelocationSpace_RollsBackExactly()
    {
        var tank = new ShipFuelTank(0, ShipFuelType.Standard);
        var inventory = new SlotInventory(1);
        Assert.True(inventory.AddToSlot(0, ProductionItemIds.StandardFuelContainer, 3).Succeeded);

        var result = ShipRefuelService.TransferFilledContainerFromSlot(tank, inventory, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(ShipRefuelFailure.NoSpaceForReturnedContainers, result.Failure);
        Assert.Equal(0, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.StandardFuelContainer, inventory.GetSlot(0).ItemId);
        Assert.Equal(3, inventory.GetSlot(0).Amount);
    }

    [Fact]
    public void DrainWithWrongContainer_IsRejectedWithoutLoss()
    {
        var tank = new ShipFuelTank(250, ShipFuelType.Standard);
        var inventory = new SlotInventory(1);
        Assert.True(inventory.AddToSlot(0, ProductionItemIds.IronOre, 1).Succeeded);

        var result = ShipRefuelService.TransferTankToContainerInSlot(tank, inventory, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(ShipRefuelFailure.SelectedSlotDoesNotContainEmptyContainer, result.Failure);
        Assert.Equal(250, tank.CurrentFuel);
        Assert.Equal(ProductionItemIds.IronOre, inventory.GetSlot(0).ItemId);
    }
}

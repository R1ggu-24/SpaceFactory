using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class GeneratorFuelTankTransferTests
{
    [Fact]
    public void Fill_ExchangesOneFilledContainerForEmptyAndStoresOneDiscreteUnit()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);

        var result = GeneratorFuelTankTransfer.FillFromInput(generator);

        Assert.True(result.Succeeded);
        Assert.Equal(ProductionConfiguration.FuelGeneratorSecondsPerContainer, result.TransferredFuelSeconds);
        Assert.Equal(
            ProductionConfiguration.FuelGeneratorSecondsPerContainer,
            generator.GeneratorFuelSecondsRemaining);
        Assert.Equal(0, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(1, generator.InputInventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void Fill_AtConfiguredCapacityLeavesAllInventoriesUntouched()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);
        for (var index = 0; index < ProductionConfiguration.FuelGeneratorTankContainerCapacity; index++)
        {
            Assert.True(GeneratorFuelTankTransfer.FillFromInput(generator).Succeeded);
            Assert.True(generator.InputInventory.Remove(ProductionItemIds.EmptyFuelContainer, 1).Succeeded);
            Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);
        }

        var inputBefore = generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer);
        var result = GeneratorFuelTankTransfer.FillFromInput(generator);

        Assert.False(result.Succeeded);
        Assert.Equal(GeneratorFuelTankTransferFailure.TankFull, result.Failure);
        Assert.Equal(inputBefore, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
        Assert.Equal(generator.GeneratorFuelTankCapacitySeconds, generator.GeneratorFuelSecondsRemaining);
    }

    [Fact]
    public void Drain_ExchangesInputEmptyContainerForFilledContainerInSameSlot()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);
        Assert.True(GeneratorFuelTankTransfer.FillFromInput(generator).Succeeded);

        var result = GeneratorFuelTankTransfer.DrainToInputContainer(generator);

        Assert.True(result.Succeeded);
        Assert.Equal(0, generator.GeneratorFuelSecondsRemaining);
        Assert.Equal(0, generator.InputInventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(1, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void Drain_WithLessThanOneContainerWorthNeverCreatesPartialContainer()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);
        Assert.True(GeneratorFuelTankTransfer.FillFromInput(generator).Succeeded);
        generator.ConsumeGeneratedEnergy(ProductionConfiguration.FuelGeneratorPowerKilowatts);
        var secondsBefore = generator.GeneratorFuelSecondsRemaining;

        var result = GeneratorFuelTankTransfer.DrainToInputContainer(generator);

        Assert.False(result.Succeeded);
        Assert.Equal(GeneratorFuelTankTransferFailure.TankCannotFillContainer, result.Failure);
        Assert.Equal(secondsBefore, generator.GeneratorFuelSecondsRemaining);
        Assert.Equal(1, generator.InputInventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Transfer_WithWrongItemInDedicatedSlotFailsWithoutMutation(bool fill)
    {
        var generator = CreateGenerator();
        if (!fill)
        {
            Assert.True(generator.InputInventory.AddToSlot(
                0,
                ProductionItemIds.FuelContainer,
                1).Succeeded);
            Assert.True(GeneratorFuelTankTransfer.FillFromInput(generator).Succeeded);
            Assert.True(generator.InputInventory.RemoveFromSlot(
                0,
                ProductionItemIds.EmptyFuelContainer,
                1).Succeeded);
        }

        Assert.True(generator.InputInventory.AddToSlot(0, ProductionItemIds.IronOre, 17).Succeeded);

        var fuelBefore = generator.GeneratorFuelSecondsRemaining;
        var result = fill
            ? GeneratorFuelTankTransfer.FillFromInput(generator)
            : GeneratorFuelTankTransfer.DrainToInputContainer(generator);

        Assert.False(result.Succeeded);
        Assert.Equal(GeneratorFuelTankTransferFailure.WrongContent, result.Failure);
        Assert.Equal(fuelBefore, generator.GeneratorFuelSecondsRemaining);
        Assert.Equal(ProductionItemIds.IronOre, generator.InputInventory.GetSlot(0).ItemId);
        Assert.Equal(17, generator.InputInventory.GetSlot(0).Amount);
    }

    [Fact]
    public void Fill_WithStackedContainerRelocatesRemainderAndKeepsReplacementInSameSlot()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.AddToSlot(
            0,
            ProductionItemIds.FuelContainer,
            3).Succeeded);

        var result = GeneratorFuelTankTransfer.FillFromInput(generator);

        Assert.True(result.Succeeded);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, generator.InputInventory.GetSlot(0).ItemId);
        Assert.Equal(1, generator.InputInventory.GetSlot(0).Amount);
        Assert.Equal(2, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void Fill_WithStackedContainerAndNoRelocationSpaceRollsBackExactly()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.AddToSlot(
            0,
            ProductionItemIds.FuelContainer,
            3).Succeeded);
        Assert.True(generator.InputInventory.AddToSlot(1, ProductionItemIds.Carbon, 200).Succeeded);
        Assert.True(generator.InputInventory.AddToSlot(2, ProductionItemIds.IronOre, 200).Succeeded);

        var result = GeneratorFuelTankTransfer.FillFromInput(generator);

        Assert.False(result.Succeeded);
        Assert.Equal(GeneratorFuelTankTransferFailure.TransferSlotBlocked, result.Failure);
        Assert.Equal(ProductionItemIds.FuelContainer, generator.InputInventory.GetSlot(0).ItemId);
        Assert.Equal(3, generator.InputInventory.GetSlot(0).Amount);
        Assert.Equal(0, generator.GeneratorFuelSecondsRemaining);
    }

    [Fact]
    public void ManuallyFilledTank_CanGenerateWhileNormalReturnedContainerOutputIsFull()
    {
        var generator = CreateGenerator();
        Assert.True(generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1).Succeeded);
        Assert.True(GeneratorFuelTankTransfer.FillFromInput(generator).Succeeded);
        Assert.True(generator.OutputInventory.Add(ProductionItemIds.Carbon, 200).Succeeded);
        Assert.True(generator.OutputInventory.Add(ProductionItemIds.IronOre, 200).Succeeded);
        Assert.True(generator.OutputInventory.Add(ProductionItemIds.WaterIce, 200).Succeeded);
        generator.SetEnabled(true);

        Assert.Equal(
            ProductionConfiguration.FuelGeneratorPowerKilowatts,
            generator.GetAvailableGenerationKilowatts(1));
        generator.ConsumeGeneratedEnergy(ProductionConfiguration.FuelGeneratorPowerKilowatts);
        generator.SetGeneratorOperatingStatus(ProductionConfiguration.FuelGeneratorPowerKilowatts);

        Assert.Equal(
            ProductionConfiguration.FuelGeneratorSecondsPerContainer - 1,
            generator.GeneratorFuelSecondsRemaining);
        Assert.Equal(MachineOperationStatus.Producing, generator.Status);
    }

    [Fact]
    public void NuclearReactor_DoesNotExposeReversibleFuelTankTransactions()
    {
        var reactor = new MachineState(
            new MachineInstanceId("reactor"),
            DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.NuclearReactor),
            constructionCompleted: true);
        Assert.True(reactor.InputInventory.Add(ProductionItemIds.NuclearFuelCell, 1).Succeeded);

        var result = GeneratorFuelTankTransfer.FillFromInput(reactor);

        Assert.False(result.Succeeded);
        Assert.Equal(GeneratorFuelTankTransferFailure.UnsupportedMachine, result.Failure);
        Assert.Equal(1, reactor.InputInventory.GetAmount(ProductionItemIds.NuclearFuelCell));
    }

    private static MachineState CreateGenerator() => new(
        new MachineInstanceId("fuel-generator"),
        DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.FuelGenerator),
        constructionCompleted: true);
}

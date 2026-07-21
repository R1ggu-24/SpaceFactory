using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class ExtractionMachineStateTests
{
    [Fact]
    public void AutomaticMiner_RequiresMatchingSourceAndGridPower()
    {
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.AutomaticMiner);
        var recipe = DefaultRecipeCatalog.Instance.ForMachine(definition.Id)
            .First(candidate => candidate.SourceResourceId == ProductionItemIds.IronOre);
        var miner = new MachineState(new MachineInstanceId("auto"), definition, constructionCompleted: true);
        miner.SelectRecipe(recipe);
        miner.SetEnabled(true);

        var missingSource = miner.TickProduction(recipe, 1, recipe.RequiredPowerKilowatts * 10);
        miner.BindExtractionSource(new ExtractionSourceBinding(
            "source:v2:test",
            ProductionItemIds.IronOre,
            ResourcePurity.Pure,
            12));
        var missingLogistics = miner.TickProduction(recipe, 1, recipe.RequiredPowerKilowatts * 10);
        miner.SetOutputConnectionAvailable(true);
        var missingPower = miner.TickProduction(recipe, 1, 0);
        var producing = miner.TickProduction(recipe, 1, recipe.RequiredPowerKilowatts * 10);

        Assert.Equal(MachineOperationStatus.MissingResourceSource, missingSource.Status);
        Assert.Equal(MachineOperationStatus.WaitingForLogistics, missingLogistics.Status);
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, missingPower.Status);
        Assert.Equal(MachineOperationStatus.Producing, producing.Status);
    }

    [Fact]
    public void MobileMiner_UsesInternalBatteryAndNeverRequestsGridPower()
    {
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.MobileMiner);
        var recipe = DefaultRecipeCatalog.Instance.ForMachine(definition.Id)
            .First(candidate => candidate.SourceResourceId == ProductionItemIds.CopperOre);
        var miner = new MachineState(new MachineInstanceId("mobile"), definition, constructionCompleted: true);
        miner.BindExtractionSource(new ExtractionSourceBinding(
            "source:v2:copper",
            ProductionItemIds.CopperOre,
            ResourcePurity.Normal,
            12));
        miner.SelectRecipe(recipe);
        miner.SetEnabled(true);
        var initialEnergy = miner.InternalEnergyKilowattSeconds;

        var result = miner.TickProduction(recipe, 1, allocatedPowerKilowatts: 0);

        Assert.Equal(0, miner.GetRequestedPowerKilowatts(recipe));
        Assert.Equal(0, result.EnergyConsumedKilowattSeconds);
        Assert.True(miner.InternalEnergyKilowattSeconds < initialEnergy);
    }

    [Fact]
    public void ExtractionBindingAndStoredEnergy_RoundTripInSnapshot()
    {
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.MobileMiner);
        var miner = new MachineState(new MachineInstanceId("snapshot"), definition, constructionCompleted: true);
        miner.BindExtractionSource(new ExtractionSourceBinding(
            "source:v2:snapshot",
            new ItemId("nickel_ore"),
            ResourcePurity.Impure,
            8));

        var restored = MachineState.Restore(miner.CreateSnapshot(), definition);

        Assert.Equal(miner.ExtractionSource, restored.ExtractionSource);
        Assert.Equal(miner.InternalEnergyKilowattSeconds, restored.InternalEnergyKilowattSeconds);
    }
}

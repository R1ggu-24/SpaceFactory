using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class SpecializedStorageTests
{
    [Fact]
    public void LiquidAndGasTanks_RejectWrongPhases()
    {
        var machines = DefaultMachineCatalog.Instance;

        Assert.True(MachineInventoryAcceptanceRules.CanStore(
            machines.Get(MachineDefinitionIds.LiquidTank),
            ProductionItemIds.Water));
        Assert.False(MachineInventoryAcceptanceRules.CanStore(
            machines.Get(MachineDefinitionIds.LiquidTank),
            ProductionItemIds.Hydrogen));
        Assert.True(MachineInventoryAcceptanceRules.CanStore(
            machines.Get(MachineDefinitionIds.GasTank),
            ProductionItemIds.Hydrogen));
        Assert.False(MachineInventoryAcceptanceRules.CanStore(
            machines.Get(MachineDefinitionIds.GasTank),
            ProductionItemIds.IronPlate));
    }

    [Fact]
    public void NuclearWasteStorage_AcceptsOnlyRadioactiveSolids()
    {
        var storage = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.NuclearWasteStorage);

        Assert.True(MachineInventoryAcceptanceRules.CanStore(storage, ProductionItemIds.RadioactiveWaste));
        Assert.True(MachineInventoryAcceptanceRules.CanStore(storage, ProductionItemIds.SpentFuelCell));
        Assert.False(MachineInventoryAcceptanceRules.CanStore(storage, ProductionItemIds.ChemicalWaste));
        Assert.False(MachineInventoryAcceptanceRules.CanStore(storage, ProductionItemIds.IronPlate));
    }
}

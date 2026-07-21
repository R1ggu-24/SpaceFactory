using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class BatteryBankPowerTests
{
    private const string CometId = "battery-comet";

    [Fact]
    public void SurplusGeneration_ChargesAtConfiguredLimitAndAppliesEfficiencyExactlyOnce()
    {
        var network = new LocalCometPowerNetwork(CometId);
        var reactor = BuiltMachine(MachineDefinitionIds.NuclearReactor, "reactor", enabled: true);
        reactor.InputInventory.Add(ProductionItemIds.NuclearFuelCell, 1);
        var battery = BuiltMachine(MachineDefinitionIds.BatteryBank, "battery", enabled: true);
        network.AddMachine(reactor);
        network.AddMachine(battery);

        var result = network.Tick(1, DefaultRecipeCatalog.Instance);

        var expectedInput = MachineEnergyConfiguration.BatteryBankMaximumChargeKilowatts;
        var expectedStored = expectedInput * battery.Definition.EfficiencyMultiplier;
        Assert.Equal(expectedStored, battery.StoredGridEnergyKilowattSeconds, 6);
        Assert.Equal(expectedInput, result.ConsumedEnergyKilowattSeconds, 6);
        Assert.Equal(expectedInput, result.SourceAllocations!.Sum(source => source.SuppliedKilowatts), 6);
        var storage = Assert.Single(result.StorageAllocations!);
        Assert.Equal(expectedInput, storage.ChargeInputKilowatts, 6);
        Assert.Equal(expectedStored, storage.StoredKilowatts, 6);
        Assert.Equal(0, storage.DischargedKilowatts);
        Assert.Equal(expectedInput - expectedStored, result.ConsumedEnergyKilowattSeconds - expectedStored, 6);
    }

    [Fact]
    public void GenerationDeficit_DischargesAtConfiguredLimitWithoutCreatingEnergy()
    {
        var recipe = new RecipeDefinition(
            new RecipeId("battery_test_load"),
            "Battery test load",
            MachineDefinitionIds.Crusher,
            "Test",
            [new ItemAmount(ProductionItemIds.IronOre, 1)],
            [new ItemAmount(ProductionItemIds.CrushedIronOre, 1)],
            1,
            60);
        var recipes = new RecipeCatalog([recipe]);
        var network = new LocalCometPowerNetwork(CometId);
        var battery = BuiltMachine(MachineDefinitionIds.BatteryBank, "battery", enabled: true);
        battery.StoreGridEnergy(MachineEnergyConfiguration.BatteryBankCapacityKilowattSeconds /
                                battery.Definition.EfficiencyMultiplier);
        var first = ReadyConsumer("load-a", recipe);
        var second = ReadyConsumer("load-b", recipe);
        network.AddMachine(battery);
        network.AddMachine(first);
        network.AddMachine(second);
        var energyBefore = battery.StoredGridEnergyKilowattSeconds;

        var result = network.Tick(1, recipes);

        var expectedDischarge = MachineEnergyConfiguration.BatteryBankMaximumDischargeKilowatts;
        Assert.Equal(expectedDischarge, energyBefore - battery.StoredGridEnergyKilowattSeconds, 6);
        Assert.Equal(expectedDischarge, result.ConsumedEnergyKilowattSeconds, 6);
        var batterySource = Assert.Single(result.SourceAllocations!, source => source.IsStorage);
        Assert.Equal(expectedDischarge, batterySource.SuppliedKilowatts, 6);
        Assert.Equal(expectedDischarge, Assert.Single(result.StorageAllocations!).DischargedKilowatts, 6);
        Assert.Equal(1, first.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(1, second.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
    }

    [Fact]
    public void ConnectedSimulation_ChargesOnlyBatteryInTheGeneratorsComponent()
    {
        var connections = new MachineConnectionNetwork();
        var generator = BuiltMachine(MachineDefinitionIds.BasicGenerator, "generator", enabled: true);
        var connectedBattery = BuiltMachine(MachineDefinitionIds.BatteryBank, "battery-connected", enabled: true);
        var isolatedBattery = BuiltMachine(MachineDefinitionIds.BatteryBank, "battery-isolated", enabled: true);
        Assert.True(connections.RegisterMachine(generator));
        Assert.True(connections.RegisterMachine(connectedBattery));
        Assert.True(connections.RegisterMachine(isolatedBattery));
        Assert.True(connections.TryConnect(
            new MachineConnectionId("generator-battery"),
            ConnectionTypeIds.PowerCable,
            Endpoint(generator),
            Endpoint(connectedBattery)).Succeeded);

        var results = new ConnectedPowerGridSimulation(connections).Tick(CometId, 1);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            ProductionConfiguration.BasicGeneratorPowerKilowatts * connectedBattery.Definition.EfficiencyMultiplier,
            connectedBattery.StoredGridEnergyKilowattSeconds,
            6);
        Assert.Equal(0, isolatedBattery.StoredGridEnergyKilowattSeconds);
        var chargingGrid = Assert.Single(results, result => result.Dispatch.StorageAllocations!
            .Any(storage => storage.MachineId == connectedBattery.InstanceId));
        Assert.Equal(ProductionConfiguration.BasicGeneratorPowerKilowatts, chargingGrid.Metrics.ActualProductionKilowatts, 6);
        Assert.Equal(ProductionConfiguration.BasicGeneratorPowerKilowatts, chargingGrid.Metrics.ActualConsumptionKilowatts, 6);
        Assert.Equal(0, chargingGrid.Metrics.ReserveKilowatts, 6);
    }

    [Fact]
    public void StoredEnergyWithoutGenerationOrDemand_RemainsUnchangedAndCannotSelfCharge()
    {
        var network = new LocalCometPowerNetwork(CometId);
        var battery = BuiltMachine(MachineDefinitionIds.BatteryBank, "battery", enabled: true);
        battery.StoreGridEnergy(500);
        network.AddMachine(battery);
        var before = battery.StoredGridEnergyKilowattSeconds;

        var result = network.Tick(5, DefaultRecipeCatalog.Instance);

        Assert.Equal(before, battery.StoredGridEnergyKilowattSeconds);
        Assert.Equal(0, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(0, result.SourceAllocations!.Sum(source => source.SuppliedKilowatts));
        var storage = Assert.Single(result.StorageAllocations!);
        Assert.Equal(0, storage.ChargeInputKilowatts);
        Assert.Equal(0, storage.DischargedKilowatts);
    }

    private static MachineState BuiltMachine(
        MachineDefinitionId definitionId,
        string instanceId,
        bool enabled)
    {
        var state = new MachineState(
            new MachineInstanceId(instanceId),
            DefaultMachineCatalog.Instance.Get(definitionId),
            new MachinePlacement(CometId, 0, 0, 0),
            constructionCompleted: true);
        state.SetEnabled(enabled);
        return state;
    }

    private static MachineState ReadyConsumer(string instanceId, RecipeDefinition recipe)
    {
        var state = BuiltMachine(recipe.MachineId, instanceId, enabled: true);
        Assert.True(state.SelectRecipe(recipe));
        state.InputInventory.Add(ProductionItemIds.IronOre, 1);
        return state;
    }

    private static MachineConnectionEndpoint Endpoint(MachineState machine) =>
        new(machine.InstanceId, MachinePortIds.Power);
}

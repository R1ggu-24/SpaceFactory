using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Tests;

public sealed class PowerAndResearchTests
{
    [Fact]
    public void BasicGenerator_PowersSeveralSimpleMachinesOnSameComet()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.BasicGenerator, "generator");
        var first = ReadyCrusher("crusher-1");
        var second = ReadyCrusher("crusher-2");
        network.AddMachine(generator);
        network.AddMachine(first);
        network.AddMachine(second);

        var result = network.Tick(2, DefaultRecipeCatalog.Instance);

        Assert.Equal(12, result.AvailablePowerKilowatts);
        Assert.Equal(10, result.RequestedPowerKilowatts);
        Assert.Equal(20, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(3, first.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(3, second.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(MachineOperationStatus.Producing, generator.Status);
    }

    [Fact]
    public void InsufficientNetworkPower_PausesWholeRecipeWithoutConsumingInput()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.BasicGenerator, "generator");
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var electrolyzer = ReadyMachine("electrolyzer", recipe);
        electrolyzer.InputInventory.Add(ProductionItemIds.WaterContainer, 1);
        electrolyzer.InputInventory.Add(ProductionItemIds.EmptyGasContainer, 2);
        network.AddMachine(generator);
        network.AddMachine(electrolyzer);

        var result = network.Tick(1, DefaultRecipeCatalog.Instance);

        Assert.Equal(12, result.AvailablePowerKilowatts);
        Assert.Equal(15, result.RequestedPowerKilowatts);
        Assert.Equal(0, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, electrolyzer.Status);
        Assert.Equal(3, electrolyzer.InputInventory.TotalItemCount);
    }

    [Fact]
    public void FuelGenerator_ConsumesFuelOnlyForDeliveredEnergyAndReturnsContainer()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.FuelGenerator, "fuel-generator");
        generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1);
        var crusher = ReadyCrusher("crusher");
        network.AddMachine(generator);
        network.AddMachine(crusher);

        var result = network.Tick(2, DefaultRecipeCatalog.Instance);

        Assert.Equal(10, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(0, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(1, generator.OutputInventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(
            ProductionConfiguration.FuelGeneratorSecondsPerContainer - 10.0 / ProductionConfiguration.FuelGeneratorPowerKilowatts,
            generator.GeneratorFuelSecondsRemaining,
            6);
    }

    [Fact]
    public void FuelGenerator_WithNoDemand_DoesNotBurnOrUnpackFuel()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.FuelGenerator, "fuel-generator");
        generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1);
        network.AddMachine(generator);

        var result = network.Tick(10, DefaultRecipeCatalog.Instance);

        Assert.Equal(0, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(1, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(0, generator.OutputInventory.TotalItemCount);
        Assert.Equal(0, generator.GeneratorFuelSecondsRemaining);
        Assert.True(generator.IsEnabled);
        Assert.Equal(MachineOperationStatus.Ready, generator.Status);
    }

    [Fact]
    public void FuelGenerator_RemainsEnabledWithoutFuelAndRestartsWhenFuelArrives()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.FuelGenerator, "fuel-generator");
        var crusher = ReadyCrusher("crusher");
        network.AddMachine(generator);
        network.AddMachine(crusher);

        network.Tick(1, DefaultRecipeCatalog.Instance);
        Assert.True(generator.IsEnabled);
        Assert.Equal(MachineOperationStatus.WaitingForMaterial, generator.Status);
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, crusher.Status);

        generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1);
        network.Tick(2, DefaultRecipeCatalog.Instance);

        Assert.True(generator.IsEnabled);
        Assert.Equal(3, crusher.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
    }

    [Fact]
    public void FuelGenerator_WaitsWhenReturnedContainerOutputIsFull()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.FuelGenerator, "fuel-generator");
        generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1);
        generator.OutputInventory.Add(ProductionItemIds.Carbon, 200);
        generator.OutputInventory.Add(ProductionItemIds.WaterIce, 200);
        generator.OutputInventory.Add(ProductionItemIds.IronOre, 200);
        var crusher = ReadyCrusher("crusher");
        network.AddMachine(generator);
        network.AddMachine(crusher);

        network.Tick(1, DefaultRecipeCatalog.Instance);

        Assert.Equal(MachineOperationStatus.OutputFull, generator.Status);
        Assert.Equal(1, generator.InputInventory.GetAmount(ProductionItemIds.FuelContainer));
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, crusher.Status);
    }

    [Fact]
    public void PowerNetwork_RejectsMachinePlacedOnAnotherComet()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var machine = new MachineState(
            new MachineInstanceId("crusher"),
            DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Crusher),
            new MachinePlacement("comet-b", 1, 2, 0),
            constructionCompleted: true);

        Assert.Throws<ArgumentException>(() => network.AddMachine(machine));
    }

    [Fact]
    public void PowerNetwork_AllocatesAndChargesExternalResearchDemand()
    {
        var network = new LocalCometPowerNetwork("comet-a");
        var generator = BuiltGenerator(MachineDefinitionIds.FuelGenerator, "fuel-generator");
        generator.InputInventory.Add(ProductionItemIds.FuelContainer, 1);
        network.AddMachine(generator);

        var result = network.Tick(5, DefaultRecipeCatalog.Instance, externalRequestedPowerKilowatts: 14);

        Assert.Equal(14, result.ExternalAllocatedKilowatts);
        Assert.Equal(70, result.ConsumedEnergyKilowattSeconds);
        Assert.Equal(1, generator.OutputInventory.GetAmount(ProductionItemIds.EmptyFuelContainer));
        Assert.Equal(
            ProductionConfiguration.FuelGeneratorSecondsPerContainer - (70.0 / 60.0),
            generator.GeneratorFuelSecondsRemaining,
            6);
    }

    [Fact]
    public void ResearchStart_IsAtomicWhenMaterialsAreMissing()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.AdvancedMetallurgy);
        var inventory = new SpaceFactory.Core.Inventory.SlotInventory(4);
        inventory.Add(ProductionItemIds.IronIngot, 20);
        var state = new ResearchState();

        var result = state.TryStart(definition, inventory);

        Assert.Equal(ResearchStartFailure.MissingMaterials, result.Failure);
        Assert.Equal(20, inventory.GetAmount(ProductionItemIds.IronIngot));
        Assert.Null(state.ActiveResearchId);
    }

    [Fact]
    public void Research_RequiresPrerequisiteAndThenConsumesRequirementsOnce()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.FuelProduction);
        var inventory = ResearchInventory(definition);
        var blocked = new ResearchState();

        Assert.Equal(ResearchStartFailure.MissingPrerequisite, blocked.TryStart(definition, inventory).Failure);
        Assert.Equal(definition.MaterialCosts.Sum(cost => cost.Amount), inventory.TotalItemCount);

        var state = new ResearchState(
            [DefaultResearchIds.HydrogenTechnology, DefaultResearchIds.AdvancedMetallurgy]);
        Assert.True(state.TryStart(definition, inventory).Succeeded);
        Assert.Equal(0, inventory.TotalItemCount);
        Assert.Equal(ResearchStartFailure.AlreadyActive, state.TryStart(definition, inventory).Failure);
    }

    [Fact]
    public void Research_PausesWithoutPowerAndPersistsCompletion()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.AdvancedMetallurgy);
        var inventory = ResearchInventory(definition);
        var state = new ResearchState();
        state.TryStart(definition, inventory);

        var waiting = state.Tick(definition, 10, definition.RequiredPowerKilowatts - 1);
        var completed = state.Tick(definition, definition.DurationSeconds, definition.RequiredPowerKilowatts);

        Assert.Equal(ResearchStatus.WaitingForEnergy, waiting.Status);
        Assert.Equal(0, waiting.EnergyConsumedKilowattSeconds);
        Assert.True(completed.Completed);
        Assert.True(state.IsCompleted(definition.Id));
        Assert.Null(state.ActiveResearchId);
        Assert.Equal(ResearchStartFailure.AlreadyCompleted, state.TryStart(definition, ResearchInventory(definition)).Failure);
    }

    [Fact]
    public void ResearchSnapshot_RestoresActiveProgressAndCompletedTechnologies()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.FuelProduction);
        var state = new ResearchState(
            [DefaultResearchIds.HydrogenTechnology, DefaultResearchIds.AdvancedMetallurgy]);
        state.TryStart(definition, ResearchInventory(definition));
        state.Tick(definition, 5, definition.RequiredPowerKilowatts);

        var restored = ResearchState.Restore(state.CreateSnapshot());

        Assert.Equal(definition.Id, restored.ActiveResearchId);
        Assert.Equal(5, restored.ProgressSeconds);
        Assert.True(restored.IsCompleted(DefaultResearchIds.HydrogenTechnology));
        Assert.True(restored.IsCompleted(DefaultResearchIds.AdvancedMetallurgy));
    }

    private static MachineState BuiltGenerator(MachineDefinitionId definitionId, string instanceId)
    {
        var generator = new MachineState(
            new MachineInstanceId(instanceId),
            DefaultMachineCatalog.Instance.Get(definitionId),
            constructionCompleted: true);
        generator.SetEnabled(true);
        return generator;
    }

    private static MachineState ReadyCrusher(string instanceId)
    {
        var machine = ReadyMachine(instanceId, DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.CrushIronOre));
        machine.InputInventory.Add(ProductionItemIds.IronOre, 2);
        return machine;
    }

    private static MachineState ReadyMachine(string instanceId, RecipeDefinition recipe)
    {
        var machine = new MachineState(
            new MachineInstanceId(instanceId),
            DefaultMachineCatalog.Instance.Get(recipe.MachineId),
            constructionCompleted: true);
        machine.SelectRecipe(recipe);
        machine.SetEnabled(true);
        return machine;
    }

    private static SpaceFactory.Core.Inventory.SlotInventory ResearchInventory(ResearchDefinition definition)
    {
        var inventory = new SpaceFactory.Core.Inventory.SlotInventory(10);
        foreach (var cost in definition.MaterialCosts)
        {
            inventory.Add(cost.ItemId, cost.Amount);
        }

        return inventory;
    }
}

using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class MachineProductionTests
{
    [Fact]
    public void Construction_ReservesStateUntilConfiguredDurationCompletes()
    {
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Crusher);
        var machine = new MachineState(new MachineInstanceId("crusher-1"), definition);

        machine.AdvanceConstruction(definition.ConstructionDurationSeconds - 0.1);
        Assert.False(machine.IsConstructionComplete);
        Assert.Equal(MachineOperationStatus.UnderConstruction, machine.Status);

        machine.AdvanceConstruction(0.1);
        Assert.True(machine.IsConstructionComplete);
        Assert.Equal(1, machine.ConstructionProgress);
        Assert.Equal(MachineOperationStatus.Disabled, machine.Status);
    }

    [Fact]
    public void SelectedRecipe_DoesNotProduceUntilPlayerEnablesMachine()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.CrushIronOre);
        var machine = BuiltMachine(recipe);
        machine.InputInventory.Add(ProductionItemIds.IronOre, 2);

        var result = machine.TickProduction(recipe, 10, recipe.RequiredPowerKilowatts);

        Assert.Equal(MachineOperationStatus.Disabled, result.Status);
        Assert.Equal(2, machine.InputInventory.GetAmount(ProductionItemIds.IronOre));
        Assert.Equal(0, machine.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void MissingMaterial_ConsumesNeitherPowerNorPartialIngredients()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeElectronicComponent);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.CopperCable, 2);
        machine.InputInventory.Add(ProductionItemIds.SilicatePowder, 2);

        var result = machine.TickProduction(recipe, 5, recipe.RequiredPowerKilowatts);

        Assert.Equal(MachineOperationStatus.WaitingForMaterial, result.Status);
        Assert.Equal(0, result.EnergyConsumedKilowattSeconds);
        Assert.Equal(2, machine.InputInventory.GetAmount(ProductionItemIds.CopperCable));
        Assert.Equal(2, machine.InputInventory.GetAmount(ProductionItemIds.SilicatePowder));
    }

    [Fact]
    public void MissingPower_DoesNotStartOrConsumeBatch()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 1);

        var result = machine.TickProduction(recipe, 2, recipe.RequiredPowerKilowatts - 0.1);

        Assert.Equal(MachineOperationStatus.WaitingForEnergy, result.Status);
        Assert.False(machine.IsBatchInProgress);
        Assert.Equal(1, machine.InputInventory.GetAmount(ProductionItemIds.IronIngot));
    }

    [Fact]
    public void ActiveBatch_PausesWithoutPowerAndResumesFromSavedProgress()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 1);

        var first = machine.TickProduction(recipe, 0.75, recipe.RequiredPowerKilowatts);
        var progress = machine.ProductionProgressSeconds;
        var paused = machine.TickProduction(recipe, 1, 0);
        Assert.Equal(progress, machine.ProductionProgressSeconds, 6);
        var resumed = machine.TickProduction(recipe, recipe.DurationSeconds, recipe.RequiredPowerKilowatts);

        Assert.Equal(MachineOperationStatus.Producing, first.Status);
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, paused.Status);
        Assert.Equal(1, resumed.CompletedCycles);
        Assert.Equal(2, machine.OutputInventory.GetAmount(ProductionItemIds.IronPlate));
    }

    [Fact]
    public void FullOutput_PreventsBatchStartWithoutRemovingInputs()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 1);
        FillEveryOutputSlot(machine);

        var result = machine.TickProduction(recipe, 2, recipe.RequiredPowerKilowatts);

        Assert.Equal(MachineOperationStatus.OutputFull, result.Status);
        Assert.False(machine.IsBatchInProgress);
        Assert.Equal(1, machine.InputInventory.GetAmount(ProductionItemIds.IronIngot));
    }

    [Fact]
    public void OutputFilledDuringBatch_PreservesFinishedBatchUntilSpaceReturns()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 1);
        machine.TickProduction(recipe, 0.5, recipe.RequiredPowerKilowatts);
        FillEveryOutputSlot(machine);

        var blocked = machine.TickProduction(recipe, recipe.DurationSeconds, recipe.RequiredPowerKilowatts);

        Assert.Equal(MachineOperationStatus.OutputFull, blocked.Status);
        Assert.True(machine.IsBatchInProgress);
        Assert.Equal(recipe.DurationSeconds, machine.ProductionProgressSeconds);

        machine.OutputInventory.Remove(ProductionItemIds.Carbon, 200);
        var completed = machine.TickProduction(recipe, 0, 0);

        Assert.Equal(1, completed.CompletedCycles);
        Assert.False(machine.IsBatchInProgress);
        Assert.Equal(2, machine.OutputInventory.GetAmount(ProductionItemIds.IronPlate));
    }

    [Fact]
    public void ContainerRecipe_ProducesTwoGasesAndReturnsExactlyOneWaterContainer()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.WaterContainer, 1);
        machine.InputInventory.Add(ProductionItemIds.EmptyGasContainer, 2);

        var result = machine.TickProduction(recipe, recipe.DurationSeconds, recipe.RequiredPowerKilowatts);

        Assert.Equal(1, result.CompletedCycles);
        Assert.Equal(0, machine.InputInventory.TotalItemCount);
        Assert.Equal(1, machine.OutputInventory.GetAmount(ProductionItemIds.HydrogenContainer));
        Assert.Equal(1, machine.OutputInventory.GetAmount(ProductionItemIds.OxygenContainer));
        Assert.Equal(1, machine.OutputInventory.GetAmount(ProductionItemIds.EmptyWaterContainer));
        Assert.Equal(3, machine.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void LargeTick_CompletesSeveralCyclesWithoutExceedingStacksOrDuplicatingItems()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(recipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 5);

        var result = machine.TickProduction(recipe, recipe.DurationSeconds * 5, recipe.RequiredPowerKilowatts);

        Assert.Equal(5, result.CompletedCycles);
        Assert.Equal(10, machine.OutputInventory.GetAmount(ProductionItemIds.IronPlate));
        Assert.Equal(0, machine.InputInventory.TotalItemCount);
        Assert.Equal(recipe.RequiredPowerKilowatts * recipe.DurationSeconds * 5, result.EnergyConsumedKilowattSeconds);
    }

    [Fact]
    public void RecipeCannotChangeMidBatchOrToDifferentMachineType()
    {
        var activeRecipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var machine = BuiltMachine(activeRecipe, enabled: true);
        machine.InputInventory.Add(ProductionItemIds.IronIngot, 1);
        machine.TickProduction(activeRecipe, 0.5, activeRecipe.RequiredPowerKilowatts);

        Assert.False(machine.SelectRecipe(DefaultRecipeCatalog.Instance.Get(new RecipeId("make_iron_rods"))));
        Assert.False(machine.SelectRecipe(DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.CrushIronOre)));
        Assert.Equal(activeRecipe.Id, machine.SelectedRecipeId);
    }

    [Fact]
    public void Snapshot_RestoresPlacementInventoriesSwitchAndMidBatchProgress()
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.MakeIronPlate);
        var definition = DefaultMachineCatalog.Instance.Get(recipe.MachineId);
        var original = new MachineState(
            new MachineInstanceId("constructor-persisted"),
            definition,
            new MachinePlacement("comet-42", 12.5, -3.25, 0.75),
            constructionCompleted: true);
        original.SelectRecipe(recipe);
        original.SetEnabled(true);
        original.InputInventory.Add(ProductionItemIds.IronIngot, 2);
        original.TickProduction(recipe, 0.5, recipe.RequiredPowerKilowatts);

        var restored = MachineState.Restore(original.CreateSnapshot(), definition);

        Assert.Equal(original.InstanceId, restored.InstanceId);
        Assert.Equal(original.Placement, restored.Placement);
        Assert.Equal(original.SelectedRecipeId, restored.SelectedRecipeId);
        Assert.Equal(original.IsEnabled, restored.IsEnabled);
        Assert.Equal(original.ProductionProgressSeconds, restored.ProductionProgressSeconds);
        Assert.True(restored.IsBatchInProgress);
        Assert.Equal(1, restored.InputInventory.GetAmount(ProductionItemIds.IronIngot));

        var completed = restored.TickProduction(recipe, recipe.DurationSeconds, recipe.RequiredPowerKilowatts);
        Assert.Equal(1, completed.CompletedCycles);
        Assert.Equal(2, restored.OutputInventory.GetAmount(ProductionItemIds.IronPlate));
    }

    [Fact]
    public void WorkbenchInventory_EnforcesToolStackLimitAndSplitsLegacySnapshots()
    {
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Workbench);
        var machine = new MachineState(
            new MachineInstanceId("workbench-tool-stack"),
            definition,
            constructionCompleted: true);

        Assert.Equal(1, machine.OutputInventory.GetMaximumStackSize(
            ProductionItemIds.MachineDismantlingTool));
        Assert.True(machine.OutputInventory.Add(ProductionItemIds.MachineDismantlingTool, 2).Succeeded);
        Assert.Equal(2, machine.OutputInventory.UsedSlotCount);
        Assert.All(machine.OutputInventory.Slots.Where(slot => !slot.IsEmpty), slot => Assert.Equal(1, slot.Amount));

        var legacySnapshot = machine.CreateSnapshot() with
        {
            OutputSlots =
            [
                new MachineInventorySlotSnapshot(
                    0,
                    ProductionItemIds.MachineDismantlingTool,
                    2),
            ],
        };
        var restored = MachineState.Restore(legacySnapshot, definition);

        Assert.Equal(2, restored.OutputInventory.GetAmount(ProductionItemIds.MachineDismantlingTool));
        Assert.Equal(2, restored.OutputInventory.UsedSlotCount);
        Assert.All(restored.OutputInventory.Slots.Where(slot => !slot.IsEmpty), slot => Assert.Equal(1, slot.Amount));
    }

    private static MachineState BuiltMachine(RecipeDefinition recipe, bool enabled = false)
    {
        var machine = new MachineState(
            new MachineInstanceId($"{recipe.Id}-machine"),
            DefaultMachineCatalog.Instance.Get(recipe.MachineId),
            constructionCompleted: true);
        Assert.True(machine.SelectRecipe(recipe));
        machine.SetEnabled(enabled);
        return machine;
    }

    private static void FillEveryOutputSlot(MachineState machine)
    {
        var fillerItems = new[]
        {
            ProductionItemIds.Carbon,
            ProductionItemIds.CopperOre,
            ProductionItemIds.NickelOre,
            ProductionItemIds.WaterIce,
        };
        Assert.True(machine.OutputInventory.SlotCount <= fillerItems.Length);
        for (var index = 0; index < machine.OutputInventory.SlotCount; index++)
        {
            Assert.True(machine.OutputInventory.Add(fillerItems[index], 200).Succeeded);
        }
    }
}

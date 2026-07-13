using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Tests;

public sealed class ProductionCatalogTests
{
    [Fact]
    public void DefaultMachineCatalog_ContainsEveryRequestedMachineAndCategory()
    {
        var catalog = DefaultMachineCatalog.Instance;

        Assert.Equal(12, catalog.All.Count);
        Assert.All(
            new[]
            {
                MachineDefinitionIds.Crusher,
                MachineDefinitionIds.Smelter,
                MachineDefinitionIds.Foundry,
                MachineDefinitionIds.Refinery,
                MachineDefinitionIds.WaterProcessor,
                MachineDefinitionIds.Electrolyzer,
                MachineDefinitionIds.Constructor,
                MachineDefinitionIds.Fabricator,
                MachineDefinitionIds.BasicGenerator,
                MachineDefinitionIds.FuelGenerator,
                MachineDefinitionIds.StorageContainer,
                MachineDefinitionIds.ResearchStation,
            },
            id => Assert.Equal(id, catalog.Get(id).Id));
        Assert.Equal(
            Enum.GetValues<MachineCategory>().Order(),
            catalog.All.Select(machine => machine.Category).Distinct().Order());
        Assert.Equal(
            ProductionConfiguration.StorageContainerSlotCount,
            catalog.Get(MachineDefinitionIds.StorageContainer).InputSlotCount);
    }

    [Fact]
    public void DefaultRecipeCatalog_ReferencesKnownMachinesItemsAndResearch()
    {
        var recipes = DefaultRecipeCatalog.Instance.All;
        var items = DefaultProductionItemCatalog.Instance;
        var machines = DefaultMachineCatalog.Instance;
        var research = DefaultResearchCatalog.Instance;

        Assert.True(recipes.Count >= 45);
        foreach (var recipe in recipes)
        {
            Assert.NotNull(machines.Get(recipe.MachineId));
            Assert.All(
                recipe.Inputs.Concat(recipe.Outputs).Concat(recipe.ReturnedContainers),
                item => Assert.True(items.TryGet(item.ItemId, out _), $"Unknown item {item.ItemId} in {recipe.Id}"));
            if (recipe.UnlockRequirement is { } unlock)
            {
                Assert.NotNull(research.Get(unlock));
            }
        }
    }

    [Fact]
    public void DefaultRecipes_IncludeCompleteFluidFuelAndContainerChain()
    {
        var recipes = DefaultRecipeCatalog.Instance;

        Assert.Contains(recipes.Get(DefaultRecipeIds.MeltWaterIce).Outputs, output => output.ItemId == ProductionItemIds.Water);
        Assert.Contains(recipes.Get(DefaultRecipeIds.FillWaterContainer).Outputs, output => output.ItemId == ProductionItemIds.WaterContainer);
        Assert.Equal(
            new[] { ProductionItemIds.HydrogenContainer, ProductionItemIds.OxygenContainer }.OrderBy(id => id.Value),
            recipes.Get(DefaultRecipeIds.ElectrolyzeWaterContainer).Outputs.Select(output => output.ItemId).OrderBy(id => id.Value));
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.ElectrolyzeWaterContainer).ReturnedContainers,
            item => item.ItemId == ProductionItemIds.EmptyWaterContainer);
        Assert.Contains(recipes.Get(DefaultRecipeIds.FillFuelContainer).Outputs, output => output.ItemId == ProductionItemIds.FuelContainer);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.FillFuelContainer).ReturnedContainers,
            item => item.ItemId == ProductionItemIds.EmptyGasContainer);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.PackageFuel).Inputs,
            input => input.ItemId == ProductionItemIds.Fuel);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.PackageFuel).Outputs,
            output => output.ItemId == ProductionItemIds.FuelContainer);
    }

    [Fact]
    public void CrushedOreRecipe_IsFasterAndMoreEfficientThanRawOre()
    {
        var raw = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.SmeltIronOre);
        var crushed = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.SmeltCrushedIronOre);

        Assert.True(crushed.DurationSeconds < raw.DurationSeconds);
        Assert.True(crushed.Outputs.Single().Amount > raw.Outputs.Single().Amount);
    }

    [Fact]
    public void FabricatorRecipes_SupportThreeDifferentInputsWithoutSpecialLogic()
    {
        var recipes = DefaultRecipeCatalog.Instance.ForMachine(MachineDefinitionIds.Fabricator);

        Assert.NotEmpty(recipes);
        Assert.All(recipes, recipe => Assert.Equal(3, recipe.Inputs.Count));
        Assert.Equal(ProductionConfiguration.FabricatorInputSlotCount, DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Fabricator).InputSlotCount);
    }

    [Fact]
    public void ContainerCatalog_DescribesSubstanceCapacityAndReusableEmptyContainer()
    {
        var catalog = DefaultProductionItemCatalog.Instance;
        var fuel = catalog.Get(ProductionItemIds.FuelContainer).Container;
        var emptyFuel = catalog.Get(ProductionItemIds.EmptyFuelContainer).Container;

        Assert.NotNull(fuel);
        Assert.Equal(ProductionItemIds.Fuel, fuel.ContainedSubstanceId);
        Assert.Equal(100, fuel.CurrentAmount);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, fuel.EmptyContainerId);
        Assert.NotNull(emptyFuel);
        Assert.True(emptyFuel.IsEmpty);
    }

    [Fact]
    public void FirstBasicGeneratorState_GrantsExactlyOneFreeGenerator()
    {
        var state = new FirstBasicGeneratorState();
        var basic = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.BasicGenerator);
        var crusher = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Crusher);

        Assert.Empty(state.GetEffectiveBuildCosts(basic));
        Assert.NotEmpty(state.GetEffectiveBuildCosts(crusher));
        Assert.True(state.TryConsumeFreeBuild(basic));
        Assert.False(state.TryConsumeFreeBuild(basic));
        Assert.NotEmpty(state.GetEffectiveBuildCosts(basic));
        Assert.True(state.FreeGeneratorAlreadyBuilt);
    }

    [Fact]
    public void FirstBasicGeneratorState_CanRestorePersistedClaim()
    {
        var state = new FirstBasicGeneratorState(freeGeneratorAlreadyBuilt: true);

        Assert.False(state.IsFreeBuildAvailable(DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.BasicGenerator)));
    }

    [Fact]
    public void DefaultResearchCatalog_ContainsRequestedTreeWithValidPrerequisites()
    {
        var catalog = DefaultResearchCatalog.Instance;

        Assert.Equal(6, catalog.All.Count);
        Assert.Contains(DefaultResearchIds.HydrogenTechnology, catalog.Get(DefaultResearchIds.FuelProduction).Prerequisites);
        Assert.Contains(DefaultResearchIds.FuelProduction, catalog.Get(DefaultResearchIds.ImprovedEnergySupply).Prerequisites);
        Assert.Contains(DefaultResearchIds.AdvancedElectronics, catalog.Get(DefaultResearchIds.SpaceshipComponents).Prerequisites);
    }

    [Fact]
    public void ResearchUnlockMetadata_MatchesEveryMachineAndRecipeRequirement()
    {
        var research = DefaultResearchCatalog.Instance;

        foreach (var machine in DefaultMachineCatalog.Instance.All.Where(machine => machine.UnlockRequirement is not null))
        {
            Assert.Contains(machine.Id, research.Get(machine.UnlockRequirement!.Value).UnlockedMachines);
        }

        foreach (var recipe in DefaultRecipeCatalog.Instance.All.Where(recipe => recipe.UnlockRequirement is not null))
        {
            Assert.Contains(recipe.Id, research.Get(recipe.UnlockRequirement!.Value).UnlockedRecipes);
        }
    }
}

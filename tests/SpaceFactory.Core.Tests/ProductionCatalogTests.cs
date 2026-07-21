using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Tests;

public sealed class ProductionCatalogTests
{
    [Fact]
    public void DefaultProductionItemCatalog_ContainsEveryDeclaredStableItemId()
    {
        var declaredIds = typeof(ProductionItemIds)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(SpaceFactory.Core.Items.ItemId))
            .Select(field => (SpaceFactory.Core.Items.ItemId)field.GetValue(null)!)
            .ToArray();
        var catalog = DefaultProductionItemCatalog.Instance;

        Assert.True(declaredIds.Length >= 100);
        Assert.Equal(declaredIds.Length, declaredIds.Distinct().Count());
        Assert.Equal(declaredIds.Length, catalog.All.Count);
        Assert.All(declaredIds, id => Assert.True(catalog.TryGet(id, out _), $"Missing item definition: {id}"));
    }

    [Fact]
    public void DefaultMachineCatalog_ContainsEveryRequestedMachineAndCategory()
    {
        var catalog = DefaultMachineCatalog.Instance;

        Assert.True(catalog.All.Count >= 28);
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
                MachineDefinitionIds.PowerPole,
                MachineDefinitionIds.MobileMiner,
                MachineDefinitionIds.AutomaticMiner,
                MachineDefinitionIds.ChemicalPlant,
                MachineDefinitionIds.Assembler,
                MachineDefinitionIds.AdvancedFabricator,
                MachineDefinitionIds.PrecisionManufacturer,
                MachineDefinitionIds.LiquidTank,
                MachineDefinitionIds.GasTank,
                MachineDefinitionIds.PumpStation,
                MachineDefinitionIds.BatteryBank,
                MachineDefinitionIds.UraniumProcessor,
                MachineDefinitionIds.FuelCellFabricator,
                MachineDefinitionIds.NuclearReactor,
                MachineDefinitionIds.WasteProcessor,
                MachineDefinitionIds.NuclearWasteStorage,
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

        Assert.True(recipes.Count >= 120);
        Assert.True(items.All.Count >= 100);
        foreach (var recipe in recipes)
        {
            Assert.NotNull(machines.Get(recipe.MachineId));
            Assert.All(
                recipe.Inputs.Concat(recipe.Outputs).Concat(recipe.ReturnedContainers),
                item => Assert.True(items.TryGet(item.ItemId, out _), $"Unknown item {item.ItemId} in {recipe.Id}"));
            if (recipe.SourceResourceId is { } sourceResourceId)
            {
                Assert.True(items.TryGet(sourceResourceId, out _), $"Unknown source {sourceResourceId} in {recipe.Id}");
            }
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
        var standardFuel = recipes.Get(DefaultRecipeIds.RefineStandardFuel);
        Assert.Contains(
            standardFuel.Inputs,
            input => input.ItemId == ProductionItemIds.ProcessedCarbon);
        Assert.Contains(
            standardFuel.Inputs,
            input => input.ItemId == ProductionItemIds.Hydrogen);
        Assert.Contains(
            standardFuel.Outputs,
            output => output.ItemId == ProductionItemIds.StandardFuel);
        Assert.Equal(DefaultResearchIds.HydrogenTechnology, standardFuel.UnlockRequirement);
        Assert.Equal(MachineDefinitionIds.Electrolyzer, standardFuel.MachineId);
        Assert.Equal(TechnologyTier.Tier3, standardFuel.TechnologyTier);
        Assert.Equal(
            DefaultResearchIds.HydrogenTechnology,
            recipes.Get(DefaultRecipeIds.MakeEmptyFuelContainer).UnlockRequirement);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.PackageStandardFuel).Outputs,
            output => output.ItemId == ProductionItemIds.StandardFuelContainer);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.FillFuelContainer).Outputs,
            output => output.ItemId == ProductionItemIds.HighPerformanceFuelContainer);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.FillFuelContainer).ReturnedContainers,
            item => item.ItemId == ProductionItemIds.EmptyGasContainer);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.PackageFuel).Inputs,
            input => input.ItemId == ProductionItemIds.HighPerformanceFuel);
        Assert.Contains(
            recipes.Get(DefaultRecipeIds.PackageFuel).Outputs,
            output => output.ItemId == ProductionItemIds.HighPerformanceFuelContainer);
    }

    [Fact]
    public void CrushedOreRecipes_KeepTheirTimingAndRawOreTakesExactlyFiftyPercentLonger()
    {
        var pairs = new[]
        {
            (Raw: DefaultRecipeIds.SmeltIronOre, Crushed: DefaultRecipeIds.SmeltCrushedIronOre, PriorCrushedDuration: 2.8),
            (Raw: new RecipeId("smelt_copper_ore"), Crushed: new RecipeId("smelt_crushed_copper_ore"), PriorCrushedDuration: 2.8),
            (Raw: new RecipeId("smelt_nickel_ore"), Crushed: new RecipeId("smelt_crushed_nickel_ore"), PriorCrushedDuration: 3.0),
            (Raw: new RecipeId("smelt_titanium_ore"), Crushed: new RecipeId("smelt_crushed_titanium_ore"), PriorCrushedDuration: 4.0),
        };

        foreach (var pair in pairs)
        {
            var raw = DefaultRecipeCatalog.Instance.Get(pair.Raw);
            var crushed = DefaultRecipeCatalog.Instance.Get(pair.Crushed);

            Assert.Equal(pair.PriorCrushedDuration, crushed.DurationSeconds, 10);
            Assert.Equal(
                crushed.DurationSeconds * SmeltingConfiguration.RawOreDurationMultiplier,
                raw.DurationSeconds,
                10);
            Assert.True(crushed.Outputs.Single().Amount > raw.Outputs.Single().Amount);
        }
    }

    [Fact]
    public void SmeltingConfiguration_RejectsInvalidCrushedOreDurations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SmeltingConfiguration.GetRawOreDuration(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SmeltingConfiguration.GetRawOreDuration(double.PositiveInfinity));
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
        var standardFuel = catalog.Get(ProductionItemIds.StandardFuelContainer).Container;
        var highPerformanceFuel = catalog.Get(ProductionItemIds.HighPerformanceFuelContainer).Container;
        var emptyFuel = catalog.Get(ProductionItemIds.EmptyFuelContainer).Container;

        Assert.NotNull(standardFuel);
        Assert.Equal(ProductionItemIds.StandardFuel, standardFuel.ContainedSubstanceId);
        Assert.Equal(100, standardFuel.CurrentAmount);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, standardFuel.EmptyContainerId);
        Assert.NotNull(highPerformanceFuel);
        Assert.Equal(ProductionItemIds.HighPerformanceFuel, highPerformanceFuel.ContainedSubstanceId);
        Assert.Equal(100, highPerformanceFuel.CurrentAmount);
        Assert.Equal(ProductionItemIds.EmptyFuelContainer, highPerformanceFuel.EmptyContainerId);
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

        Assert.True(catalog.All.Count >= 17);
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

    [Fact]
    public void ExtendedItems_DescribePhasesHazardsAndNonStackableToolsCentrally()
    {
        var items = DefaultProductionItemCatalog.Instance;
        var sulfuricAcid = items.Get(ProductionItemIds.SulfuricAcid);
        var chemicalWaste = items.Get(ProductionItemIds.ChemicalWaste);
        var uranium = items.Get(ProductionItemIds.UraniumOre);
        var spentFuel = items.Get(ProductionItemIds.SpentFuelCell);

        Assert.Equal(ProductionItemPhase.Liquid, sulfuricAcid.Phase);
        Assert.Equal(ItemHazardKind.Chemical, sulfuricAcid.HazardKind);
        Assert.Equal(ItemContainmentRequirement.ChemicalResistant, chemicalWaste.ContainmentRequirement);
        Assert.Equal(ItemHazardKind.Radioactive, uranium.HazardKind);
        Assert.True(spentFuel.HazardStrength > uranium.HazardStrength);
        Assert.Equal(ItemContainmentRequirement.RadiationShielding, spentFuel.ContainmentRequirement);
        Assert.Equal(1, items.Get(ProductionItemIds.UpgradedMiningTool).MaximumStackSize);
        Assert.Equal(1, items.Get(ProductionItemIds.HighPerformanceMiningTool).MaximumStackSize);
    }

    [Fact]
    public void ExtractionRecipes_AreSourceBoundAndOnlyMobileVariantsNeedNoGridPower()
    {
        var catalog = DefaultRecipeCatalog.Instance;
        var mobile = catalog.ForMachine(MachineDefinitionIds.MobileMiner);
        var automatic = catalog.ForMachine(MachineDefinitionIds.AutomaticMiner);

        var rawResourceIds = DefaultProductionItemCatalog.Instance.All
            .Where(item => item.Category == ProductionItemCategory.RawMaterial)
            .Select(item => item.Id)
            .OrderBy(id => id.Value)
            .ToArray();

        Assert.Equal(26, rawResourceIds.Length);
        Assert.Equal(rawResourceIds.Length, mobile.Count);
        Assert.Equal(mobile.Count, automatic.Count);
        Assert.All(mobile, recipe =>
        {
            Assert.True(recipe.IsExtractionRecipe);
            Assert.Empty(recipe.Inputs);
            Assert.NotNull(recipe.SourceResourceId);
            Assert.Equal(0, recipe.RequiredPowerKilowatts);
            Assert.Contains("infinite-source", recipe.Tags);
        });
        Assert.All(automatic, recipe =>
        {
            Assert.True(recipe.IsExtractionRecipe);
            Assert.Empty(recipe.Inputs);
            Assert.True(recipe.RequiredPowerKilowatts > 0);
        });
        Assert.Equal(
            mobile.Select(recipe => recipe.SourceResourceId).OrderBy(id => id!.Value.Value),
            automatic.Select(recipe => recipe.SourceResourceId).OrderBy(id => id!.Value.Value));
        Assert.Equal(
            rawResourceIds,
            mobile.Select(recipe => recipe.SourceResourceId!.Value).OrderBy(id => id.Value));
        Assert.Contains(mobile, recipe => recipe.SourceResourceId == ProductionItemIds.UraniumOre);
    }

    [Fact]
    public void ResearchUnlocks_ReferenceExistingRecipesAndAlternativeGroups()
    {
        var recipes = DefaultRecipeCatalog.Instance;

        foreach (var research in DefaultResearchCatalog.Instance.All)
        {
            Assert.All(
                research.UnlockedRecipes,
                recipeId => Assert.True(recipes.TryGet(recipeId, out _),
                    $"Unknown recipe {recipeId} in research {research.Id}"));
            Assert.All(
                research.UnlockedAlternativeRecipeGroups,
                group => Assert.Contains(recipes.All,
                    recipe => string.Equals(recipe.AlternativeGroup, group.Value, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void ProgressionGraph_IsReachableFromRawResourcesWithoutMachineOrResearchCycles()
    {
        var items = DefaultProductionItemCatalog.Instance;
        var machines = DefaultMachineCatalog.Instance;
        var recipes = DefaultRecipeCatalog.Instance;
        var research = DefaultResearchCatalog.Instance;
        var availableItems = items.All
            .Where(item => item.Category == ProductionItemCategory.RawMaterial)
            .Select(item => item.Id)
            .Append(ProductionItemIds.MiningTool)
            .ToHashSet();
        var builtMachines = new HashSet<MachineDefinitionId>();
        var completedResearch = new HashSet<ResearchId>();

        for (var pass = 0; pass < 256; pass++)
        {
            var changed = false;
            foreach (var machine in machines.All)
            {
                var directUnlockReady = machine.UnlockRequirement is null ||
                                        completedResearch.Contains(machine.UnlockRequirement.Value);
                var listedUnlocks = research.All
                    .Where(definition => definition.UnlockedMachines.Contains(machine.Id))
                    .ToArray();
                var listedUnlockReady = listedUnlocks.Length == 0 ||
                                        listedUnlocks.Any(definition => completedResearch.Contains(definition.Id));
                if (directUnlockReady && listedUnlockReady &&
                    machine.BuildCosts.All(cost => availableItems.Contains(cost.ItemId)))
                {
                    changed |= builtMachines.Add(machine.Id);
                }
            }

            foreach (var recipe in recipes.All.Where(recipe => builtMachines.Contains(recipe.MachineId)))
            {
                var directUnlockReady = recipe.UnlockRequirement is null ||
                                        completedResearch.Contains(recipe.UnlockRequirement.Value);
                var listedUnlocks = research.All
                    .Where(definition => definition.UnlockedRecipes.Contains(recipe.Id))
                    .ToArray();
                var listedUnlockReady = listedUnlocks.Length == 0 ||
                                        listedUnlocks.Any(definition => completedResearch.Contains(definition.Id));
                var isAdvancedRecipe = recipe.Tags.Contains("alternative", StringComparer.Ordinal) ||
                                       recipe.Tags.Contains("endgame", StringComparer.Ordinal);
                var highestCompletedTier = completedResearch.Count == 0
                    ? 0
                    : completedResearch.Select(research.Get).Max(definition => (int)definition.Tier);
                var normalTierReady = (int)recipe.TechnologyTier <= Math.Min(
                    (int)TechnologyTier.Tier10,
                    highestCompletedTier + 1);
                var advancedUnlockReady = !isAdvancedRecipe
                    ? normalTierReady
                    : completedResearch
                        .Select(research.Get)
                        .Any(definition => definition.Tier >= recipe.TechnologyTier ||
                            recipe.AlternativeGroup is { } group &&
                            definition.UnlockedAlternativeRecipeGroups.Any(unlocked =>
                                unlocked.Value == group));
                if (!directUnlockReady || !listedUnlockReady || !advancedUnlockReady ||
                    recipe.Inputs.Any(input => !availableItems.Contains(input.ItemId)))
                {
                    continue;
                }

                foreach (var output in recipe.CombinedOutputs)
                {
                    changed |= availableItems.Add(output.ItemId);
                }
            }

            foreach (var generator in machines.All.Where(machine =>
                         builtMachines.Contains(machine.Id) &&
                         machine.GeneratorFuelItemId is not null &&
                         machine.GeneratorReturnedContainerItemId is not null &&
                         availableItems.Contains(machine.GeneratorFuelItemId.Value)))
            {
                changed |= availableItems.Add(generator.GeneratorReturnedContainerItemId!.Value);
            }

            if (builtMachines.Contains(MachineDefinitionIds.ResearchStation))
            {
                foreach (var definition in research.TopologicalOrder)
                {
                    if (definition.Prerequisites.All(completedResearch.Contains) &&
                        definition.MaterialCosts.All(cost => availableItems.Contains(cost.ItemId)) &&
                        definition.RequiredDiscoveries.All(availableItems.Contains))
                    {
                        changed |= completedResearch.Add(definition.Id);
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        var unreachableResearch = research.All
            .Select(definition => definition.Id)
            .Where(id => !completedResearch.Contains(id))
            .OrderBy(id => id.Value)
            .ToArray();
        var unreachableMachines = machines.All
            .Select(machine => machine.Id)
            .Where(id => !builtMachines.Contains(id))
            .OrderBy(id => id.Value)
            .ToArray();
        Assert.True(unreachableResearch.Length == 0,
            $"Unreachable research: {string.Join(", ", unreachableResearch)}");
        Assert.True(unreachableMachines.Length == 0,
            $"Unreachable machines: {string.Join(", ", unreachableMachines)}");
        Assert.Contains(ProductionItemIds.DeepSpaceControlCore, availableItems);
        Assert.Contains(ProductionItemIds.QuantumNavigationModule, availableItems);
    }

    [Fact]
    public void AlternativeRecipes_HaveStableGroupsAcrossSeveralProgressionTiers()
    {
        var alternatives = DefaultRecipeCatalog.Instance.All
            .Where(recipe => recipe.AlternativeGroup is not null)
            .GroupBy(recipe => recipe.AlternativeGroup, StringComparer.Ordinal)
            .ToArray();

        Assert.True(alternatives.Length >= 10);
        Assert.All(alternatives, group => Assert.True(group.Count() >= 2, group.Key));
        Assert.Contains(alternatives, group => group.Key == "precision_component");
        Assert.Contains(alternatives.SelectMany(group => group), recipe => recipe.TechnologyTier == TechnologyTier.Tier10);
    }

    [Fact]
    public void NuclearChain_ProducesFuelReturnsSpentCellsAndRequiresWasteStabilization()
    {
        var recipes = DefaultRecipeCatalog.Instance;
        var process = recipes.Get(DefaultRecipeIds.ProcessUraniumOre);
        var enrich = recipes.Get(DefaultRecipeIds.EnrichUranium);
        var fuelCell = recipes.Get(DefaultRecipeIds.MakeNuclearFuelCell);
        var reprocess = recipes.Get(DefaultRecipeIds.ReprocessSpentFuelCell);
        var stabilize = recipes.Get(DefaultRecipeIds.StabilizeRadioactiveWaste);
        var contain = recipes.Get(DefaultRecipeIds.MakeShieldedWasteContainer);
        var reactor = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.NuclearReactor);

        Assert.Contains(process.Outputs, item => item.ItemId == ProductionItemIds.UraniumConcentrate);
        Assert.Contains(enrich.Inputs, item => item.ItemId == ProductionItemIds.UraniumConcentrate);
        Assert.Contains(enrich.Outputs, item => item.ItemId == ProductionItemIds.EnrichedUranium);
        Assert.Contains(enrich.Outputs, item => item.ItemId == ProductionItemIds.RadioactiveWaste);
        Assert.Contains(fuelCell.Outputs, item => item.ItemId == ProductionItemIds.NuclearFuelCell);
        Assert.Contains(reprocess.Inputs, item => item.ItemId == ProductionItemIds.SpentFuelCell);
        Assert.Contains(stabilize.Outputs, item => item.ItemId == ProductionItemIds.StabilizedWaste);
        Assert.Contains(contain.Outputs, item => item.ItemId == ProductionItemIds.ShieldedWasteContainer);
        Assert.Equal(ProductionItemIds.NuclearFuelCell, reactor.GeneratorFuelItemId);
        Assert.Equal(ProductionItemIds.SpentFuelCell, reactor.GeneratorReturnedContainerItemId);
        Assert.Equal(ProductionConfiguration.NuclearReactorPowerKilowatts, reactor.GeneratedPowerKilowatts);
        Assert.True(reactor.RadiationShielding >= 0.9);
    }

    [Fact]
    public void NewMachines_ExposeCentralProgressionAndPlacementProfiles()
    {
        var machines = DefaultMachineCatalog.Instance;
        var mobile = machines.Get(MachineDefinitionIds.MobileMiner);
        var automatic = machines.Get(MachineDefinitionIds.AutomaticMiner);
        var uraniumProcessor = machines.Get(MachineDefinitionIds.UraniumProcessor);

        Assert.Equal(MachineArchetype.Extractor, mobile.Archetype);
        Assert.Equal(MachinePlacementRequirement.ResourceDeposit, mobile.PlacementRequirement);
        Assert.Equal(MachinePlacementRequirement.ResourceDeposit, automatic.PlacementRequirement);
        Assert.True(automatic.SpeedMultiplier > mobile.SpeedMultiplier);
        Assert.Equal(TechnologyTier.Tier7, uraniumProcessor.TechnologyTier);
        Assert.True(uraniumProcessor.RadiationShielding > 0);
    }
}

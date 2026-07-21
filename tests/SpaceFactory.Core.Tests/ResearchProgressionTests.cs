using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Tests;

public sealed class ResearchProgressionTests
{
    [Fact]
    public void DefaultResearchTree_IsAStableSeventeenTechnologyDagAcrossAllTiers()
    {
        var catalog = DefaultResearchCatalog.Instance;
        var order = catalog.TopologicalOrder;
        var indices = order
            .Select((definition, index) => (definition.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index);

        Assert.Equal(17, catalog.All.Count);
        Assert.Equal(Enum.GetValues<ResearchCategory>().Order(),
            catalog.All.Select(definition => definition.Category).Distinct().Order());
        Assert.Equal(Enum.GetValues<SpaceFactory.Core.Research.TechnologyTier>().Order(),
            catalog.All.Select(definition => definition.Tier).Distinct().Order());
        Assert.Equal(catalog.All.Count, order.Select(definition => definition.Id).Distinct().Count());

        foreach (var definition in order)
        {
            Assert.All(definition.Prerequisites, prerequisite =>
            {
                Assert.True(indices[prerequisite] < indices[definition.Id]);
                Assert.True(catalog.Get(prerequisite).Tier <= definition.Tier);
            });

            if (definition.Tier != SpaceFactory.Core.Research.TechnologyTier.Tier1)
            {
                Assert.NotEmpty(definition.Prerequisites);
            }
        }
    }

    [Fact]
    public void ResearchCatalog_RejectsCyclesWithDeterministicPath()
    {
        var alpha = new ResearchId("alpha");
        var beta = new ResearchId("beta");
        var definitions = new[]
        {
            Definition(alpha, [beta]),
            Definition(beta, [alpha]),
        };

        var error = Assert.Throws<ArgumentException>(() => new ResearchCatalog(definitions));

        Assert.Contains("alpha -> beta -> alpha", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NuclearResearch_MissingDiscoveryDoesNotConsumeMaterials()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.NuclearProcessing);
        var inventory = InventoryFor(definition);
        var amountBefore = inventory.TotalItemCount;
        var state = new ResearchState(definition.Prerequisites);

        var blocked = state.TryStart(definition, inventory);

        Assert.False(blocked.Succeeded);
        Assert.Equal(ResearchStartFailure.MissingDiscovery, blocked.Failure);
        Assert.Equal(amountBefore, inventory.TotalItemCount);
        Assert.Null(state.ActiveResearchId);

        Assert.True(state.DiscoverResource(ProductionItemIds.UraniumOre));
        Assert.False(state.DiscoverResource(ProductionItemIds.UraniumOre));
        Assert.True(state.TryStart(definition, inventory).Succeeded);
        Assert.Equal(0, inventory.TotalItemCount);
    }

    [Fact]
    public void ResearchSnapshot_RoundTripsDiscoveriesAndAcceptsLegacySnapshot()
    {
        var state = new ResearchState();
        state.DiscoverResource(ProductionItemIds.UraniumOre);
        state.DiscoverResource(ProductionItemIds.IronOre);

        var snapshot = state.CreateSnapshot();
        var restored = ResearchState.Restore(snapshot);
        var legacy = ResearchState.Restore(new ResearchStateSnapshot(
            [],
            null,
            0,
            true,
            ResearchStatus.Idle));

        Assert.Equal(
            new[] { ProductionItemIds.IronOre, ProductionItemIds.UraniumOre },
            snapshot.DiscoveredResources!);
        Assert.Contains(ProductionItemIds.UraniumOre, restored.DiscoveredResources);
        Assert.Contains(ProductionItemIds.IronOre, restored.DiscoveredResources);
        Assert.Empty(legacy.DiscoveredResources);
    }

    [Fact]
    public void ExistingResearchIdsRemainStableAndNuclearPathRequiresUranium()
    {
        Assert.Equal("advanced_metallurgy", DefaultResearchIds.AdvancedMetallurgy.Value);
        Assert.Equal("hydrogen_technology", DefaultResearchIds.HydrogenTechnology.Value);
        Assert.Equal("fuel_production", DefaultResearchIds.FuelProduction.Value);
        Assert.Equal("improved_energy_supply", DefaultResearchIds.ImprovedEnergySupply.Value);
        Assert.Equal("advanced_electronics", DefaultResearchIds.AdvancedElectronics.Value);
        Assert.Equal("spaceship_components", DefaultResearchIds.SpaceshipComponents.Value);

        var catalog = DefaultResearchCatalog.Instance;
        Assert.Contains(ProductionItemIds.UraniumOre,
            catalog.Get(DefaultResearchIds.NuclearProcessing).RequiredDiscoveries);
        Assert.Contains(ProductionItemIds.UraniumOre,
            catalog.Get(DefaultResearchIds.NuclearPower).RequiredDiscoveries);
        Assert.Contains(DefaultResearchIds.NuclearProcessing,
            catalog.Get(DefaultResearchIds.NuclearPower).Prerequisites);
        Assert.Contains(DefaultResearchIds.NuclearPower,
            catalog.Get(DefaultResearchIds.RadioactiveWasteManagement).Prerequisites);
        Assert.Contains(DefaultResearchIds.RadioactiveWasteManagement,
            catalog.Get(DefaultResearchIds.SpaceSystems).Prerequisites);
    }

    [Fact]
    public void StandardFuelTechnology_IsAnEarlySelfContainedTierThreeBranch()
    {
        var definition = DefaultResearchCatalog.Instance.Get(DefaultResearchIds.HydrogenTechnology);

        Assert.Equal(TechnologyTier.Tier3, definition.Tier);
        Assert.Equal([DefaultResearchIds.AdvancedMetallurgy], definition.Prerequisites);
        Assert.DoesNotContain(definition.MaterialCosts,
            cost => cost.ItemId == ProductionItemIds.ChemistryResearchPack);
        Assert.Contains(MachineDefinitionIds.Electrolyzer, definition.UnlockedMachines);
        Assert.Contains(DefaultRecipeIds.MakeEmptyFuelContainer, definition.UnlockedRecipes);
        Assert.Contains(DefaultRecipeIds.RefineStandardFuel, definition.UnlockedRecipes);
        Assert.Contains(DefaultRecipeIds.PackageStandardFuel, definition.UnlockedRecipes);
        Assert.Contains(DefaultRecipeIds.FillStandardFuelContainer, definition.UnlockedRecipes);
    }

    [Fact]
    public void LateTechnologiesUseResearchPacksAndExposeAlternativeRecipeMetadata()
    {
        var catalog = DefaultResearchCatalog.Instance;
        var nuclearPower = catalog.Get(DefaultResearchIds.NuclearPower);
        var deepSpace = catalog.Get(DefaultResearchIds.DeepSpaceOptimization);

        Assert.Contains(nuclearPower.MaterialCosts,
            cost => cost.ItemId == ProductionItemIds.NuclearResearchPack);
        Assert.Contains(deepSpace.MaterialCosts,
            cost => cost.ItemId == ProductionItemIds.SpaceResearchPack);
        Assert.NotEmpty(deepSpace.UnlockedAlternativeRecipeGroups);
        Assert.Contains("optimization", deepSpace.Tags);
    }

    private static ResearchDefinition Definition(
        ResearchId id,
        IEnumerable<ResearchId> prerequisites) => new(
        id,
        id.Value,
        $"Test technology {id.Value}",
        [new ItemAmount(ProductionItemIds.IronOre, 1)],
        1,
        1,
        prerequisites);

    private static SlotInventory InventoryFor(ResearchDefinition definition)
    {
        var inventory = new SlotInventory(12);
        foreach (var cost in definition.MaterialCosts)
        {
            Assert.True(inventory.Add(cost.ItemId, cost.Amount).Succeeded);
        }

        return inventory;
    }
}

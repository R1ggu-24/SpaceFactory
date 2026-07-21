using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryDeletionRulesTests
{
    [Fact]
    public void RareResourceStack_RequiresConfirmation()
    {
        var resource = new ResourceDefinition(
            new ItemId("rare-test-ore"),
            "Seltenes Testerz",
            ResourceRarity.Rare,
            1,
            "#8899AA",
            ResourceVisualStyle.Vein,
            1,
            1,
            2,
            1,
            "dust",
            "res://rare-test.png",
            200,
            ["test"],
            [AsteroidSize.Large],
            0.02,
            0.04);

        Assert.True(InventoryDeletionRules.RequiresConfirmation(200, resource, null));
    }

    [Fact]
    public void CommonOrdinaryStack_DoesNotRequireConfirmation()
    {
        var product = new ProductionItemDefinition(
            new ItemId("ordinary-test-part"),
            "Normales Testteil",
            ProductionItemCategory.Intermediate,
            "test-part");

        Assert.False(InventoryDeletionRules.RequiresConfirmation(200, null, product));
    }

    [Theory]
    [InlineData(ProductionItemCategory.Alloy)]
    [InlineData(ProductionItemCategory.Component)]
    public void ValuableManufacturedItem_RequiresConfirmation(ProductionItemCategory category)
    {
        var product = new ProductionItemDefinition(
            new ItemId($"valuable-{category}"),
            "Wertvolles Produkt",
            category,
            "valuable");

        Assert.True(InventoryDeletionRules.RequiresConfirmation(200, resource: null, product));
    }

    [Fact]
    public void ToolHazardAndFilledContainer_RequireConfirmation()
    {
        var catalog = DefaultProductionItemCatalog.Instance;

        Assert.True(InventoryDeletionRules.RequiresConfirmation(
            1,
            null,
            catalog.Get(ProductionItemIds.MiningTool)));
        Assert.True(InventoryDeletionRules.RequiresConfirmation(
            200,
            null,
            catalog.Get(ProductionItemIds.RadioactiveWaste)));
        Assert.True(InventoryDeletionRules.RequiresConfirmation(
            200,
            null,
            catalog.Get(ProductionItemIds.StandardFuelContainer)));
    }
}

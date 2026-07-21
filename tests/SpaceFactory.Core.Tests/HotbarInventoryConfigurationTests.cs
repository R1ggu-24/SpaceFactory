using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class HotbarInventoryConfigurationTests
{
    [Fact]
    public void PlayerInventoryConfiguration_UsesExpandedBackpackHotbarAndShipSizes()
    {
        Assert.Equal(24, InventoryConfiguration.AstronautSlotCount);
        Assert.Equal(6, InventoryConfiguration.HotbarSlotCount);
        Assert.Equal(4, InventoryConfiguration.ToolSlotCount);
        Assert.Equal(56, InventoryConfiguration.ShipSlotCount);
        Assert.Equal(200, InventoryConfiguration.MaximumStackSize);
    }

    [Fact]
    public void ToolInventory_AcceptsOnlyCataloguedToolItems()
    {
        var tools = ToolInventoryState.Create();

        var toolResult = tools.Inventory.Add(ProductionItemIds.MiningTool, 1);
        var materialResult = tools.Inventory.Add(ProductionItemIds.IronPlate, 1);

        Assert.True(toolResult.Succeeded);
        Assert.False(materialResult.Succeeded);
        Assert.Equal(InventoryFailure.ItemNotAccepted, materialResult.Failure);
        Assert.Equal(1, tools.Inventory.GetAmount(ProductionItemIds.MiningTool));
        Assert.Equal(0, tools.Inventory.GetAmount(ProductionItemIds.IronPlate));
    }

    [Fact]
    public void ToolInventoryTransfer_RejectsMaterialsWithoutChangingEitherInventory()
    {
        var backpack = new SlotInventory(2, itemStackSizeResolver: ResolveMaximumStackSize);
        var tools = ToolInventoryState.Create();
        Assert.True(backpack.Add(ProductionItemIds.IronPlate, 12).Succeeded);

        var result = InventoryTransfer.Transfer(backpack, 0, tools.Inventory, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.ItemNotAccepted, result.Failure);
        Assert.Equal(12, backpack.GetAmount(ProductionItemIds.IronPlate));
        Assert.Equal(0, tools.Inventory.TotalItemCount);
    }

    [Fact]
    public void ToolSelection_CyclesAndSkipsEmptySlotsInBothDirections()
    {
        var tools = ToolInventoryState.Create();
        Assert.True(tools.Inventory.AddToSlot(1, ProductionItemIds.MiningTool, 1).Succeeded);
        Assert.True(tools.Inventory.AddToSlot(3, ProductionItemIds.MachineDismantlingTool, 1).Succeeded);

        Assert.True(tools.SelectNextTool());
        Assert.Equal(1, tools.SelectedSlotIndex);
        Assert.True(tools.SelectNextTool());
        Assert.Equal(3, tools.SelectedSlotIndex);
        Assert.True(tools.SelectNextTool());
        Assert.Equal(1, tools.SelectedSlotIndex);
        Assert.True(tools.SelectPreviousTool());
        Assert.Equal(3, tools.SelectedSlotIndex);
    }

    [Fact]
    public void HandMode_EquipsOnlyTheSelectedToolAndCanBeDeactivated()
    {
        var tools = ToolInventoryState.Create();
        Assert.True(tools.Inventory.AddToSlot(2, ProductionItemIds.MachineDismantlingTool, 1).Succeeded);
        tools.SelectSlot(2);

        Assert.Null(tools.EquippedToolId);
        Assert.True(tools.ActivateHandMode());
        Assert.Equal(ProductionItemIds.MachineDismantlingTool, tools.EquippedToolId);
        Assert.False(tools.ActivateHandMode());
        Assert.True(tools.DeactivateHandMode());
        Assert.Null(tools.EquippedToolId);
    }

    [Fact]
    public void MiningToolDefinition_IsAUniqueNonStackableItem()
    {
        var definition = DefaultProductionItemCatalog.Instance.Get(ProductionItemIds.MiningTool);

        Assert.Equal("Abbauwerkzeug", definition.DisplayName);
        Assert.Equal(ProductionItemCategory.Tool, definition.Category);
        Assert.Equal(1, definition.MaximumStackSize);
        Assert.Equal("res://assets/sprites/tools/mining_tool.png", definition.IconKey);
    }

    [Fact]
    public void ItemAwareSlotInventory_DoesNotStackMiningToolsAboveOne()
    {
        var inventory = new SlotInventory(1, itemStackSizeResolver: ResolveMaximumStackSize);

        Assert.True(inventory.Add(ProductionItemIds.MiningTool, 1).Succeeded);
        Assert.False(inventory.Add(ProductionItemIds.MiningTool, 1).Succeeded);
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.MiningTool));
        Assert.Equal(1, inventory.GetMaximumStackSize(ProductionItemIds.MiningTool));
        Assert.True(inventory.IsFull);
    }

    [Fact]
    public void HotbarState_TracksSlotIndexAndDerivesActiveItemAfterTransfers()
    {
        var backpack = new SlotInventory(InventoryConfiguration.AstronautSlotCount,
            itemStackSizeResolver: ResolveMaximumStackSize);
        var hotbarInventory = new SlotInventory(InventoryConfiguration.HotbarSlotCount,
            itemStackSizeResolver: ResolveMaximumStackSize);
        Assert.True(hotbarInventory.Add(ProductionItemIds.MiningTool, 1).Succeeded);
        var hotbar = new HotbarState(hotbarInventory);

        Assert.Equal(ProductionItemIds.MiningTool, hotbar.ActiveItemId);
        Assert.True(InventoryTransfer.Transfer(hotbarInventory, 0, backpack, 7).Succeeded);
        Assert.Null(hotbar.ActiveItemId);
        Assert.Equal(1, backpack.GetAmount(ProductionItemIds.MiningTool));

        Assert.True(InventoryTransfer.Transfer(backpack, 7, hotbarInventory, 4).Succeeded);
        Assert.True(hotbar.SelectSlot(4));
        Assert.Equal(ProductionItemIds.MiningTool, hotbar.ActiveItemId);
        Assert.Equal(1, backpack.GetAmount(ProductionItemIds.MiningTool) +
                        hotbarInventory.GetAmount(ProductionItemIds.MiningTool));
    }

    [Fact]
    public void InventoryTransfer_RejectsMergingTwoNonStackableToolsWithoutLoss()
    {
        var source = new SlotInventory(1, itemStackSizeResolver: ResolveMaximumStackSize);
        var target = new SlotInventory(1, itemStackSizeResolver: ResolveMaximumStackSize);
        Assert.True(source.Add(ProductionItemIds.MiningTool, 1).Succeeded);
        Assert.True(target.Add(ProductionItemIds.MiningTool, 1).Succeeded);

        var result = InventoryTransfer.Transfer(source, 0, target, 0);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryTransferFailure.TargetStackFull, result.Failure);
        Assert.Equal(1, source.GetAmount(ProductionItemIds.MiningTool));
        Assert.Equal(1, target.GetAmount(ProductionItemIds.MiningTool));
    }

    private static int ResolveMaximumStackSize(ItemId itemId) =>
        DefaultProductionItemCatalog.Instance.TryGet(itemId, out var definition) && definition is not null
            ? definition.MaximumStackSize
            : InventoryConfiguration.MaximumStackSize;
}

using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class MachineOutputInventoryRulesTests
{
    [Fact]
    public void MixedRecipe_StoresWasteOnlyInReservedTailSlot()
    {
        var inventory = CreateOutputInventory(4);
        ItemAmount[] outputs =
        [
            new(ProductionItemIds.BatteryCell, 2),
            new(ProductionItemIds.ChemicalWaste, 1),
        ];

        Assert.True(MachineOutputInventoryRules.CanStoreAll(inventory, outputs));
        Assert.True(MachineOutputInventoryRules.TryAddAll(inventory, outputs));
        Assert.Equal(ProductionItemIds.BatteryCell, inventory.GetSlot(0).ItemId);
        Assert.Equal(ProductionItemIds.ChemicalWaste, inventory.GetSlot(3).ItemId);
        Assert.True(inventory.GetSlot(1).IsEmpty);
        Assert.True(inventory.GetSlot(2).IsEmpty);
    }

    [Fact]
    public void ReservedWasteSlot_CannotHideAFullNormalOutputArea()
    {
        var inventory = CreateOutputInventory(4);
        Assert.True(inventory.AddToSlot(0, ProductionItemIds.IronPlate, 200).Succeeded);
        Assert.True(inventory.AddToSlot(1, ProductionItemIds.CopperWire, 200).Succeeded);
        Assert.True(inventory.AddToSlot(2, ProductionItemIds.Screws, 200).Succeeded);
        ItemAmount[] outputs =
        [
            new(ProductionItemIds.BatteryCell, 1),
            new(ProductionItemIds.ChemicalWaste, 1),
        ];

        Assert.False(MachineOutputInventoryRules.CanStoreAll(inventory, outputs));
        Assert.False(MachineOutputInventoryRules.TryAddAll(inventory, outputs));
        Assert.True(inventory.GetSlot(3).IsEmpty);
    }

    [Fact]
    public void SingleWasteOutput_ReportsAndUsesOnlyReservedTailCapacity()
    {
        var inventory = CreateOutputInventory(4);
        Assert.True(inventory.AddToSlot(3, ProductionItemIds.SpentFuelCell, 199).Succeeded);

        Assert.Equal(
            1,
            MachineOutputInventoryRules.GetAvailableCapacity(
                inventory,
                ProductionItemIds.SpentFuelCell));
        Assert.True(MachineOutputInventoryRules.TryAddAll(
            inventory,
            [new ItemAmount(ProductionItemIds.SpentFuelCell, 1)]));
        Assert.Equal(200, inventory.GetSlot(3).Amount);
        Assert.All(inventory.Slots.Take(3), slot => Assert.True(slot.IsEmpty));
        Assert.False(MachineOutputInventoryRules.CanStoreAll(
            inventory,
            [new ItemAmount(ProductionItemIds.SpentFuelCell, 1)]));
    }

    private static SlotInventory CreateOutputInventory(int slots) => new(
        slots,
        InventoryConfiguration.MaximumStackSize,
        itemId => DefaultProductionItemCatalog.Instance.Get(itemId).MaximumStackSize);
}

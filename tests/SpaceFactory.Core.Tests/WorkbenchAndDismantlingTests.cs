using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Tests;

public sealed class WorkbenchAndDismantlingTests
{
    [Fact]
    public void DismantlingProgress_RequiresContinuousHoldAndResetsForChangedTarget()
    {
        var progress = new DismantlingProgressState();

        progress.Begin("machine:a", 2);
        Assert.False(progress.Advance(0.75));
        Assert.Equal(0.375, progress.Progress, precision: 6);

        progress.Begin("machine:b", 1);
        Assert.Equal("machine:b", progress.TargetId);
        Assert.Equal(0, progress.Progress);
        Assert.False(progress.Advance(0.9));
        Assert.True(progress.Advance(0.1));

        progress.Cancel();
        Assert.False(progress.IsActive);
        Assert.Equal(0, progress.Progress);
    }

    [Fact]
    public void Workbench_ProducesEveryMiningToolAndTheCheaperMobileMinerKit()
    {
        var recipes = DefaultRecipeCatalog.Instance;
        var workbench = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Workbench);
        var mobileMiner = recipes.Get(DefaultRecipeIds.MakeMobileMiner);

        Assert.True(workbench.IsDirectBuildMenuEntry);
        Assert.Null(workbench.PlacementItemId);
        Assert.Equal(MachineKind.Production, workbench.Kind);
        Assert.All(
            new[]
            {
                DefaultRecipeIds.MakeMiningTool,
                DefaultRecipeIds.MakeMachineDismantlingTool,
                DefaultRecipeIds.MakeImprovedMiningTool,
                DefaultRecipeIds.MakeHighPerformanceMiningTool,
                DefaultRecipeIds.MakeMobileMiner,
            },
            recipeId => Assert.Equal(MachineDefinitionIds.Workbench, recipes.Get(recipeId).MachineId));

        Assert.Equal(ProductionItemIds.MobileMinerKit, Assert.Single(mobileMiner.Outputs).ItemId);
        Assert.Equal(9, mobileMiner.Inputs.Sum(input => input.Amount));
        Assert.DoesNotContain(mobileMiner.Inputs, input =>
            input.ItemId == ProductionItemIds.ReinforcedPlate ||
            input.ItemId == ProductionItemIds.SmallElectricMotor ||
            input.ItemId == ProductionItemIds.MobileBatteryPack);
    }

    [Fact]
    public void MobileMiner_IsHiddenFromDirectBuildMenuAndResolvedByItsPhysicalItem()
    {
        var catalog = DefaultMachineCatalog.Instance;
        var mobileMiner = catalog.Get(MachineDefinitionIds.MobileMiner);

        Assert.False(mobileMiner.IsDirectBuildMenuEntry);
        Assert.Equal(ProductionItemIds.MobileMinerKit, mobileMiner.PlacementItemId);
        var placementCost = Assert.Single(mobileMiner.BuildCosts);
        Assert.Equal(ProductionItemIds.MobileMinerKit, placementCost.ItemId);
        Assert.Equal(1, placementCost.Amount);
        Assert.DoesNotContain(catalog.DirectBuildMenuEntries, definition =>
            definition.Id == MachineDefinitionIds.MobileMiner);
        Assert.True(catalog.TryGetByPlacementItem(ProductionItemIds.MobileMinerKit, out var resolved));
        Assert.Equal(MachineDefinitionIds.MobileMiner, resolved!.Id);
    }

    [Fact]
    public void AutomaticMinerKit_UsesTheSamePhysicalHotbarPlacementPath()
    {
        var catalog = DefaultMachineCatalog.Instance;
        var automaticMiner = catalog.Get(MachineDefinitionIds.AutomaticMiner);

        Assert.False(automaticMiner.IsDirectBuildMenuEntry);
        Assert.Equal(ProductionItemIds.AutomaticMinerKit, automaticMiner.PlacementItemId);
        var placementCost = Assert.Single(automaticMiner.BuildCosts);
        Assert.Equal(new ItemAmount(ProductionItemIds.AutomaticMinerKit, 1), placementCost);
        Assert.True(catalog.TryGetByPlacementItem(ProductionItemIds.AutomaticMinerKit, out var resolved));
        Assert.Equal(MachineDefinitionIds.AutomaticMiner, resolved!.Id);
    }

    [Fact]
    public void MachineDismantlingTool_IsNonStackableAndCannotMineOre()
    {
        var definition = DefaultProductionItemCatalog.Instance.Get(
            ProductionItemIds.MachineDismantlingTool);

        Assert.Equal(ProductionItemCategory.Tool, definition.Category);
        Assert.Equal(1, definition.MaximumStackSize);
        Assert.True(DismantlingRules.IsDismantlingTool(definition.Id));
        Assert.False(MiningToolRules.TryGetTier(definition.Id, out _));
        Assert.False(DismantlingRules.IsDismantlingTool(ProductionItemIds.MiningTool));
    }

    [Fact]
    public void DismantlingMachine_StoresRecoveryOnlyAfterSuccessfulRemoval()
    {
        var inventory = CreateInventory(6);
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Workbench);
        var removalCalls = 0;

        var rejected = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MiningTool,
            inventory,
            definition,
            () =>
            {
                removalCalls++;
                return true;
            });

        Assert.Equal(DismantlingFailure.WrongTool, rejected.Failure);
        Assert.Equal(0, removalCalls);
        Assert.Equal(0, inventory.TotalItemCount);

        var succeeded = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            definition,
            () =>
            {
                removalCalls++;
                return true;
            },
            additionalRecovery:
            [
                new ItemAmount(ProductionItemIds.IronPlate, 2),
                new ItemAmount(ProductionItemIds.PowerCable, 1),
            ]);

        Assert.True(succeeded.Succeeded);
        Assert.Equal(1, removalCalls);
        Assert.Equal(definition.BuildCosts.Sum(cost => cost.Amount) + 3, inventory.TotalItemCount);
        Assert.All(definition.BuildCosts, cost =>
            Assert.Equal(
                cost.Amount + (cost.ItemId == ProductionItemIds.IronPlate ? 2 : 0),
                inventory.GetAmount(cost.ItemId)));
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.PowerCable));
    }

    [Fact]
    public void DismantlingPhysicalMachine_ReturnsItsKitAndNeverRawBuildCosts()
    {
        var inventory = CreateInventory(1);
        var mobileMiner = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.MobileMiner);

        var result = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            mobileMiner,
            () => true);

        Assert.True(result.Succeeded);
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.MobileMinerKit));
        Assert.Equal(1, inventory.TotalItemCount);
    }

    [Fact]
    public void DismantlingFreeStarterGenerator_DoesNotCreateUnpaidMaterials()
    {
        var inventory = CreateInventory(6);
        var generator = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.BasicGenerator);
        var removed = false;

        var result = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            generator,
            () =>
            {
                removed = true;
                return true;
            },
            constructionCostsPaid: false);

        Assert.True(result.Succeeded);
        Assert.True(removed);
        Assert.Empty(result.RecoveredItems);
        Assert.Equal(0, inventory.TotalItemCount);
    }

    [Fact]
    public void DismantlingConnection_ReturnsItsConsumedItemAndFullInventoryPreventsRemoval()
    {
        var inventory = CreateInventory(1);
        Assert.True(inventory.Add(ProductionItemIds.IronOre, InventoryConfiguration.MaximumStackSize).Succeeded);
        var removalCalls = 0;

        var full = DismantlingRules.TryDismantleConnection(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            ConnectionKind.PowerCable,
            () =>
            {
                removalCalls++;
                return true;
            });

        Assert.Equal(DismantlingFailure.InventoryFull, full.Failure);
        Assert.Equal(0, removalCalls);

        Assert.True(inventory.Remove(
            ProductionItemIds.IronOre,
            InventoryConfiguration.MaximumStackSize).Succeeded);
        var recovered = DismantlingRules.TryDismantleConnection(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            ConnectionKind.PowerCable,
            () =>
            {
                removalCalls++;
                return true;
            });

        Assert.True(recovered.Succeeded);
        Assert.Equal(1, removalCalls);
        Assert.Equal(1, inventory.GetAmount(ProductionItemIds.PowerCable));
        Assert.Equal(2, DefaultConnectionTypeCatalog.Instance
            .ForBuildItem(ProductionItemIds.TransportPipe)
            .Count);
    }

    [Fact]
    public void DismantlingRemovalFailure_IsAtomicAndDoesNotGrantARefund()
    {
        var inventory = CreateInventory(6);
        var definition = DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Workbench);
        var removalCalls = 0;

        var result = DismantlingRules.TryDismantleMachine(
            ProductionItemIds.MachineDismantlingTool,
            inventory,
            definition,
            () =>
            {
                removalCalls++;
                return false;
            },
            [new ItemAmount(ProductionItemIds.PowerCable, 2)]);

        Assert.Equal(DismantlingFailure.RemovalRejected, result.Failure);
        Assert.Equal(1, removalCalls);
        Assert.Equal(0, inventory.TotalItemCount);
        Assert.Empty(result.RecoveredItems);
    }

    [Fact]
    public void PlaceableHotbarItems_ResolveToOneCentralMachineOrConnectionDefinition()
    {
        var machines = DefaultMachineCatalog.Instance;
        var connections = DefaultConnectionTypeCatalog.Instance;

        Assert.True(machines.TryGetByPlacementItem(ProductionItemIds.MobileMinerKit, out var miner));
        Assert.Equal(MachineDefinitionIds.MobileMiner, miner!.Id);
        Assert.Equal(MachinePlacementRequirement.ResourceDeposit, miner.PlacementRequirement);
        Assert.False(miner.IsDirectBuildMenuEntry);

        var powerCable = Assert.Single(connections.ForBuildItem(ProductionItemIds.PowerCable));
        var conveyor = Assert.Single(connections.ForBuildItem(ProductionItemIds.ConveyorBelt));
        Assert.Equal(ConnectionKind.PowerCable, powerCable.Kind);
        Assert.Equal(ConnectionKind.ConveyorBelt, conveyor.Kind);
        Assert.Equal(
            new[] { ConnectionKind.LiquidPipe, ConnectionKind.GasPipe },
            connections.ForBuildItem(ProductionItemIds.TransportPipe).Select(item => item.Kind));
    }

    [Fact]
    public void StarterEquipment_DefinesBothDistinctToolsExactlyOnce()
    {
        Assert.Equal(2, StarterEquipmentConfiguration.RequiredStarterTools.Count);
        Assert.Equal(
            2,
            StarterEquipmentConfiguration.RequiredStarterTools
                .Select(item => item.ItemId)
                .Distinct()
                .Count());
        Assert.Contains(
            StarterEquipmentConfiguration.RequiredStarterTools,
            item => item == StarterEquipmentConfiguration.StartingMiningTool);
        Assert.Contains(
            StarterEquipmentConfiguration.RequiredStarterTools,
            item => item == StarterEquipmentConfiguration.StartingMachineDismantlingTool);
    }

    private static SlotInventory CreateInventory(int slotCount) => new(
        slotCount,
        InventoryConfiguration.MaximumStackSize,
        itemId => DefaultProductionItemCatalog.Instance.Get(itemId).MaximumStackSize);
}

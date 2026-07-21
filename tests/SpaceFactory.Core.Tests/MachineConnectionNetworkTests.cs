using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Core.Tests;

public sealed class MachineConnectionNetworkTests
{
    [Fact]
    public void PowerCable_CreatesARealNetworkWithoutPoweringDisconnectedMachines()
    {
        var network = new MachineConnectionNetwork();
        var generator = PlacedMachine(MachineDefinitionIds.BasicGenerator, "generator");
        generator.SetEnabled(true);
        var connectedCrusher = ReadyCrusher("connected-crusher");
        var isolatedCrusher = ReadyCrusher("isolated-crusher");
        Register(network, generator, connectedCrusher, isolatedCrusher);

        var connection = network.TryConnect(
            new MachineConnectionId("cable-1"),
            ConnectionTypeIds.PowerCable,
            Endpoint(generator, MachinePortIds.Power),
            Endpoint(connectedCrusher, MachinePortIds.Power));

        Assert.True(connection.Succeeded);
        var components = network.GetPowerComponents(CometId);
        Assert.Equal(2, components.Count);
        Assert.Contains(components, component => component.MachineIds.Count == 2);

        network.TickPower(CometId, 2, DefaultRecipeCatalog.Instance);

        Assert.Equal(3, connectedCrusher.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(0, isolatedCrusher.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(MachineOperationStatus.WaitingForEnergy, isolatedCrusher.Status);
    }

    [Fact]
    public void ConveyorBelt_TransfersOnlySolidItemsAtConfiguredRate()
    {
        var network = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.Crusher, "crusher");
        var target = PlacedMachine(MachineDefinitionIds.Smelter, "smelter");
        source.OutputInventory.Add(ProductionItemIds.CrushedIronOre, 20);
        Register(network, source, target);
        Assert.True(network.TryConnect(
            new MachineConnectionId("belt-1"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(source, MachinePortIds.SolidOutput),
            Endpoint(target, MachinePortIds.SolidInput)).Succeeded);

        var result = Assert.Single(network.TickDirectedTransfers(CometId, 0.5));

        Assert.True(result.TransferResult.Succeeded);
        Assert.Equal(4, result.TransferResult.TransferredAmount);
        Assert.Equal(16, source.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(4, target.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(20, source.OutputInventory.TotalItemCount + target.InputInventory.TotalItemCount);
    }

    [Fact]
    public void CoarseOfflineTransferStep_MatchesTenOnlineSecondsWithoutStoredBurst()
    {
        static (MachineConnectionNetwork Network, MachineState Source, MachineState Target) CreateLine(
            string suffix)
        {
            var network = new MachineConnectionNetwork();
            var source = PlacedMachine(MachineDefinitionIds.Crusher, $"offline-source-{suffix}");
            var target = PlacedMachine(MachineDefinitionIds.Smelter, $"offline-target-{suffix}");
            Assert.True(source.OutputInventory.Add(ProductionItemIds.CrushedIronOre, 160).Succeeded);
            Register(network, source, target);
            Assert.True(network.TryConnect(
                new MachineConnectionId($"offline-belt-{suffix}"),
                ConnectionTypeIds.ConveyorBelt,
                Endpoint(source, MachinePortIds.SolidOutput),
                Endpoint(target, MachinePortIds.SolidInput)).Succeeded);
            return (network, source, target);
        }

        var online = CreateLine("online");
        var offline = CreateLine("coarse");
        for (var second = 0; second < 10; second++)
        {
            online.Network.TickDirectedTransfers(CometId, 1);
        }

        offline.Network.TickDirectedTransfers(CometId, 10);

        Assert.Equal(80, online.Target.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(
            online.Target.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre),
            offline.Target.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(160, offline.Source.OutputInventory.TotalItemCount + offline.Target.InputInventory.TotalItemCount);
    }

    [Fact]
    public void LiquidPipe_TransfersLiquidButNeverAContainerOrSolid()
    {
        var network = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.WaterProcessor, "water-processor");
        var target = PlacedMachine(MachineDefinitionIds.Electrolyzer, "electrolyzer");
        source.OutputInventory.Add(ProductionItemIds.WaterContainer, 5);
        source.OutputInventory.Add(ProductionItemIds.Water, 30);
        Register(network, source, target);
        Assert.True(network.TryConnect(
            new MachineConnectionId("liquid-1"),
            ConnectionTypeIds.LiquidPipe,
            Endpoint(source, MachinePortIds.LiquidOutput),
            Endpoint(target, MachinePortIds.LiquidInput)).Succeeded);

        var result = Assert.Single(network.TickDirectedTransfers(CometId, 0.5));

        Assert.Equal(ProductionItemIds.Water, result.TransferResult.ItemId);
        Assert.Equal(10, result.TransferResult.TransferredAmount);
        Assert.Equal(5, source.OutputInventory.GetAmount(ProductionItemIds.WaterContainer));
        Assert.Equal(20, source.OutputInventory.GetAmount(ProductionItemIds.Water));
        Assert.Equal(10, target.InputInventory.GetAmount(ProductionItemIds.Water));
    }

    [Fact]
    public void GasPipe_TransfersGasSeparatelyFromLiquid()
    {
        var network = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.Electrolyzer, "electrolyzer");
        var target = PlacedMachine(MachineDefinitionIds.Refinery, "refinery");
        source.OutputInventory.Add(ProductionItemIds.Water, 10);
        source.OutputInventory.Add(ProductionItemIds.Hydrogen, 30);
        Register(network, source, target);
        Assert.True(network.TryConnect(
            new MachineConnectionId("gas-1"),
            ConnectionTypeIds.GasPipe,
            Endpoint(source, MachinePortIds.GasOutput),
            Endpoint(target, MachinePortIds.GasInput)).Succeeded);

        var result = Assert.Single(network.TickDirectedTransfers(CometId, 0.5));

        Assert.Equal(ProductionItemIds.Hydrogen, result.TransferResult.ItemId);
        Assert.Equal(10, result.TransferResult.TransferredAmount);
        Assert.Equal(10, source.OutputInventory.GetAmount(ProductionItemIds.Water));
        Assert.Equal(20, source.OutputInventory.GetAmount(ProductionItemIds.Hydrogen));
        Assert.Equal(10, target.InputInventory.GetAmount(ProductionItemIds.Hydrogen));
    }

    [Fact]
    public void ConnectionValidation_RejectsWrongMediumAndWrongDirection()
    {
        var network = new MachineConnectionNetwork();
        var crusher = PlacedMachine(MachineDefinitionIds.Crusher, "crusher");
        var smelter = PlacedMachine(MachineDefinitionIds.Smelter, "smelter");
        var waterProcessor = PlacedMachine(MachineDefinitionIds.WaterProcessor, "water-processor");
        var electrolyzer = PlacedMachine(MachineDefinitionIds.Electrolyzer, "electrolyzer");
        var refinery = PlacedMachine(MachineDefinitionIds.Refinery, "refinery");
        Register(network, crusher, smelter, waterProcessor, electrolyzer, refinery);

        var wrongMedium = network.TryConnect(
            new MachineConnectionId("wrong-medium"),
            ConnectionTypeIds.LiquidPipe,
            Endpoint(electrolyzer, MachinePortIds.GasOutput),
            Endpoint(refinery, MachinePortIds.GasInput));
        var wrongDirection = network.TryConnect(
            new MachineConnectionId("wrong-direction"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(smelter, MachinePortIds.SolidInput),
            Endpoint(crusher, MachinePortIds.SolidOutput));
        var incompatibleLiquid = network.TryConnect(
            new MachineConnectionId("wrong-liquid"),
            ConnectionTypeIds.LiquidPipe,
            Endpoint(waterProcessor, MachinePortIds.LiquidOutput),
            Endpoint(refinery, MachinePortIds.LiquidInput));

        Assert.Equal(MachineConnectionFailure.PortMediumMismatch, wrongMedium.Failure);
        Assert.Equal(MachineConnectionFailure.DirectionMismatch, wrongDirection.Failure);
        Assert.Equal(MachineConnectionFailure.ItemCompatibilityMismatch, incompatibleLiquid.Failure);
        Assert.Empty(network.Connections);
    }

    [Fact]
    public void ValidateConnection_UsesCommitRulesWithoutMutatingTopology()
    {
        var network = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.Crusher, "preview-source");
        var target = PlacedMachine(MachineDefinitionIds.Smelter, "preview-target");
        Register(network, source, target);
        var sourceEndpoint = Endpoint(source, MachinePortIds.SolidOutput);
        var targetEndpoint = Endpoint(target, MachinePortIds.SolidInput);

        var preview = network.ValidateConnection(
            ConnectionTypeIds.ConveyorBelt,
            sourceEndpoint,
            targetEndpoint);

        Assert.Equal(MachineConnectionFailure.None, preview);
        Assert.Empty(network.Connections);
        Assert.True(network.TryConnect(
            new MachineConnectionId("preview-commit"),
            ConnectionTypeIds.ConveyorBelt,
            sourceEndpoint,
            targetEndpoint).Succeeded);
        Assert.Equal(
            MachineConnectionFailure.DuplicateEndpoints,
            network.ValidateConnection(ConnectionTypeIds.ConveyorBelt, sourceEndpoint, targetEndpoint));
        Assert.Single(network.Connections);
    }

    [Fact]
    public void TransferAtCapacity_MovesOnlyWhatFitsWithoutLossOrDuplication()
    {
        var network = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.Crusher, "crusher");
        var target = PlacedMachine(MachineDefinitionIds.Smelter, "smelter");
        source.OutputInventory.Add(ProductionItemIds.CrushedIronOre, 10);
        target.InputInventory.Add(ProductionItemIds.CrushedIronOre, 199);
        target.InputInventory.Add(ProductionItemIds.CopperOre, 200);
        target.InputInventory.Add(ProductionItemIds.NickelOre, 200);
        target.InputInventory.Add(ProductionItemIds.TitaniumOre, 200);
        Register(network, source, target);
        Assert.True(network.TryConnect(
            new MachineConnectionId("capacity-belt"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(source, MachinePortIds.SolidOutput),
            Endpoint(target, MachinePortIds.SolidInput)).Succeeded);
        var totalBefore = source.OutputInventory.TotalItemCount + target.InputInventory.TotalItemCount;

        var first = Assert.Single(network.TickDirectedTransfers(CometId, 1));
        var second = Assert.Single(network.TickDirectedTransfers(CometId, 1));

        Assert.True(first.TransferResult.Succeeded);
        Assert.Equal(1, first.TransferResult.TransferredAmount);
        Assert.Equal(TransportTransferFailure.TargetFull, second.TransferResult.Failure);
        Assert.Equal(9, source.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(200, target.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(totalBefore, source.OutputInventory.TotalItemCount + target.InputInventory.TotalItemCount);
    }

    [Fact]
    public void Connections_HaveStableSnapshotsAndCanBeRestored()
    {
        var original = new MachineConnectionNetwork();
        var source = PlacedMachine(MachineDefinitionIds.Crusher, "crusher");
        var target = PlacedMachine(MachineDefinitionIds.Smelter, "smelter");
        Register(original, source, target);
        Assert.True(original.TryConnect(
            new MachineConnectionId("persisted-belt"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(source, MachinePortIds.SolidOutput),
            Endpoint(target, MachinePortIds.SolidInput)).Succeeded);
        var snapshot = Assert.Single(original.CreateSnapshots());

        var restored = new MachineConnectionNetwork();
        Register(restored, source, target);
        var restoreResult = restored.TryRestore(snapshot);

        Assert.True(restoreResult.Succeeded);
        Assert.Equal(new MachineConnectionId("persisted-belt"), snapshot.ConnectionId);
        Assert.Equal(source.InstanceId, snapshot.SourceMachineId);
        Assert.Equal(target.InstanceId, snapshot.TargetMachineId);
        Assert.Equal(ConnectionKind.ConveyorBelt, snapshot.Kind);
        Assert.Equal(snapshot, Assert.Single(restored.CreateSnapshots()));
    }

    [Fact]
    public void DirectedTick_ForOneCometDoesNotAdvanceAnotherCometsConnections()
    {
        var network = new MachineConnectionNetwork();
        var firstSource = PlacedMachine(MachineDefinitionIds.Crusher, "source-a", "comet-a");
        var firstTarget = PlacedMachine(MachineDefinitionIds.Smelter, "target-a", "comet-a");
        var secondSource = PlacedMachine(MachineDefinitionIds.Crusher, "source-b", "comet-b");
        var secondTarget = PlacedMachine(MachineDefinitionIds.Smelter, "target-b", "comet-b");
        firstSource.OutputInventory.Add(ProductionItemIds.CrushedIronOre, 20);
        secondSource.OutputInventory.Add(ProductionItemIds.CrushedIronOre, 20);
        Register(network, firstSource, firstTarget, secondSource, secondTarget);
        Assert.True(network.TryConnect(
            new MachineConnectionId("belt-a"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(firstSource, MachinePortIds.SolidOutput),
            Endpoint(firstTarget, MachinePortIds.SolidInput)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("belt-b"),
            ConnectionTypeIds.ConveyorBelt,
            Endpoint(secondSource, MachinePortIds.SolidOutput),
            Endpoint(secondTarget, MachinePortIds.SolidInput)).Succeeded);

        var results = network.TickDirectedTransfers("comet-a", 1);

        Assert.Single(results);
        Assert.Equal(12, firstSource.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(8, firstTarget.InputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(20, secondSource.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(0, secondTarget.InputInventory.TotalItemCount);
    }

    [Fact]
    public void ConnectionCatalog_UsesOnePipeItemForSeparateLiquidAndGasTypes()
    {
        var types = DefaultConnectionTypeCatalog.Instance;

        Assert.Equal(ProductionItemIds.TransportPipe, types.Get(ConnectionTypeIds.LiquidPipe).RequiredBuildItemId);
        Assert.Equal(ProductionItemIds.TransportPipe, types.Get(ConnectionTypeIds.GasPipe).RequiredBuildItemId);
        Assert.Equal(TransportMedium.Liquid, types.Get(ConnectionTypeIds.LiquidPipe).Medium);
        Assert.Equal(TransportMedium.Gas, types.Get(ConnectionTypeIds.GasPipe).Medium);
        Assert.Equal(3, LogisticsConfiguration.StartingPowerCableCount);
        Assert.Equal(3, LogisticsConfiguration.StartingConveyorBeltCount);
        Assert.Equal(3, LogisticsConfiguration.StartingTransportPipeCount);
    }

    [Fact]
    public void PowerPole_ExposesExactlySixOneCablePorts()
    {
        var ports = DefaultMachinePortCatalog.Instance.ForMachine(MachineDefinitionIds.PowerPole)
            .Where(port => port.Medium == TransportMedium.Power)
            .ToArray();

        Assert.Equal(PowerGridConfiguration.PowerPolePortCount, ports.Length);
        Assert.All(ports, port =>
        {
            Assert.Equal(PowerGridConfiguration.MaximumCablesPerPort, port.MaximumConnections);
            Assert.Equal(MachinePortDirection.Bidirectional, port.Direction);
        });
    }

    [Fact]
    public void ConnectedPowerGrid_TripsOnlyTheOverloadedNetwork()
    {
        var network = new MachineConnectionNetwork();
        var stableGenerator = PlacedMachine(MachineDefinitionIds.BasicGenerator, "stable-generator");
        stableGenerator.SetEnabled(true);
        var stableCrusher = ReadyCrusher("stable-crusher");
        var overloadedGenerator = PlacedMachine(MachineDefinitionIds.BasicGenerator, "overload-generator");
        overloadedGenerator.SetEnabled(true);
        var electrolyzerRecipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var overloadedMachine = ReadyMachine("overload-electrolyzer", electrolyzerRecipe);
        overloadedMachine.InputInventory.Add(ProductionItemIds.WaterContainer, 1);
        overloadedMachine.InputInventory.Add(ProductionItemIds.EmptyGasContainer, 2);
        Register(network, stableGenerator, stableCrusher, overloadedGenerator, overloadedMachine);
        Assert.True(network.TryConnect(
            new MachineConnectionId("stable-cable"),
            ConnectionTypeIds.PowerCable,
            Endpoint(stableGenerator, MachinePortIds.Power),
            Endpoint(stableCrusher, MachinePortIds.Power)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("overload-cable"),
            ConnectionTypeIds.PowerCable,
            Endpoint(overloadedGenerator, MachinePortIds.Power),
            Endpoint(overloadedMachine, MachinePortIds.Power)).Succeeded);
        var simulation = new ConnectedPowerGridSimulation(network);

        simulation.Tick(CometId, PowerGridConfiguration.OverloadToleranceSeconds + 0.1);
        var secondTick = simulation.Tick(CometId, 1);

        Assert.Equal(3, stableCrusher.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(0, overloadedMachine.OutputInventory.TotalItemCount);
        Assert.Contains(secondTick, result => result.Metrics.Status == PowerGridStatus.BreakerTripped);
        Assert.Contains(secondTick, result => result.Metrics.Status is PowerGridStatus.Online or PowerGridStatus.Idle);
    }

    [Fact]
    public void ShipConnectors_CanFeedSeparateNetworksFromOneFuelTank()
    {
        var network = new MachineConnectionNetwork();
        var first = ReadyCrusher("ship-crusher-a");
        var second = ReadyCrusher("ship-crusher-b");
        Register(network, first, second);
        var tank = new ShipFuelTank(initialFuel: 1);
        var ship = new ShipPowerNode(new MachineInstanceId("player_ship"), CometId, tank);
        Assert.True(network.RegisterPowerNode(ship));
        Assert.True(network.TryConnect(
            new MachineConnectionId("ship-cable-a"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(ship.NodeId, MachinePortIds.ShipPowerA),
            Endpoint(first, MachinePortIds.Power)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("ship-cable-b"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(ship.NodeId, MachinePortIds.ShipPowerB),
            Endpoint(second, MachinePortIds.Power)).Succeeded);
        var simulation = new ConnectedPowerGridSimulation(network);

        simulation.Tick(CometId, 2);

        Assert.Equal(1 - (20 * PowerGridConfiguration.ShipFuelPerKilowattSecond), tank.CurrentFuel, 6);
        Assert.Equal(3, first.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
        Assert.Equal(3, second.OutputInventory.GetAmount(ProductionItemIds.CrushedIronOre));
    }

    [Fact]
    public void ShipConnectors_InOneNetwork_AddTheirCapacity()
    {
        var network = new MachineConnectionNetwork();
        var pole = PlacedMachine(MachineDefinitionIds.PowerPole, "shared-pole");
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.ElectrolyzeWaterContainer);
        var first = ReadyMachine("shared-consumer-a", recipe);
        var second = ReadyMachine("shared-consumer-b", recipe);
        foreach (var consumer in new[] { first, second })
        {
            consumer.InputInventory.Add(ProductionItemIds.WaterContainer, 1);
            consumer.InputInventory.Add(ProductionItemIds.EmptyGasContainer, 2);
        }

        Register(network, pole, first, second);
        var tank = new ShipFuelTank(initialFuel: 1);
        var ship = new ShipPowerNode(new MachineInstanceId("player_ship"), CometId, tank);
        Assert.True(network.RegisterPowerNode(ship));
        Assert.True(network.TryConnect(
            new MachineConnectionId("shared-ship-a"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(ship.NodeId, MachinePortIds.ShipPowerA),
            Endpoint(pole, MachinePortIds.Power1)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("shared-ship-b"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(ship.NodeId, MachinePortIds.ShipPowerB),
            Endpoint(pole, MachinePortIds.Power2)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("shared-consumer-a-cable"),
            ConnectionTypeIds.PowerCable,
            Endpoint(first, MachinePortIds.Power),
            Endpoint(pole, MachinePortIds.Power3)).Succeeded);
        Assert.True(network.TryConnect(
            new MachineConnectionId("shared-consumer-b-cable"),
            ConnectionTypeIds.PowerCable,
            Endpoint(second, MachinePortIds.Power),
            Endpoint(pole, MachinePortIds.Power4)).Succeeded);
        var simulation = new ConnectedPowerGridSimulation(network);

        var result = Assert.Single(simulation.Tick(CometId, 1));

        Assert.Equal(80, result.Metrics.MaximumCapacityKilowatts);
        var expectedDemand = recipe.RequiredPowerKilowatts * 2;
        Assert.Equal(expectedDemand, result.Metrics.ActualProductionKilowatts);
        Assert.Equal(expectedDemand, result.Metrics.ActualConsumptionKilowatts);
        Assert.Equal(2, result.Metrics.ActiveSourceCount);
        Assert.Equal(
            1 - (expectedDemand * PowerGridConfiguration.ShipFuelPerKilowattSecond),
            tank.CurrentFuel,
            6);
    }

    [Fact]
    public void DisabledShipConnector_DeliversNoPowerAndConsumesNoFuel()
    {
        var network = new MachineConnectionNetwork();
        var crusher = ReadyCrusher("disabled-ship-consumer");
        Register(network, crusher);
        var tank = new ShipFuelTank(initialFuel: 1);
        var ship = new ShipPowerNode(
            new MachineInstanceId("player_ship"),
            CometId,
            tank,
            connectorAEnabled: false);
        Assert.True(network.RegisterPowerNode(ship));
        Assert.True(network.TryConnect(
            new MachineConnectionId("disabled-ship-cable"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(ship.NodeId, MachinePortIds.ShipPowerA),
            Endpoint(crusher, MachinePortIds.Power)).Succeeded);

        var result = new ConnectedPowerGridSimulation(network).Tick(CometId, 1)
            .Single(component => component.Topology.Endpoints.Contains(
                Endpoint(crusher, MachinePortIds.Power)));

        Assert.Equal(0, result.Metrics.MaximumCapacityKilowatts);
        Assert.Equal(0, result.Metrics.ActualConsumptionKilowatts);
        Assert.Equal(1, tank.CurrentFuel);
        Assert.Equal(0, crusher.OutputInventory.TotalItemCount);
    }

    [Fact]
    public void ShipConnector_WithNoDemandOrEmptyTank_DoesNotConsumeOrProduce()
    {
        var noDemandNetwork = new MachineConnectionNetwork();
        var idlePole = PlacedMachine(MachineDefinitionIds.PowerPole, "idle-pole");
        Register(noDemandNetwork, idlePole);
        var fullTank = new ShipFuelTank(initialFuel: 1);
        var idleShip = new ShipPowerNode(new MachineInstanceId("idle-ship"), CometId, fullTank);
        Assert.True(noDemandNetwork.RegisterPowerNode(idleShip));
        Assert.True(noDemandNetwork.TryConnect(
            new MachineConnectionId("idle-cable"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(idleShip.NodeId, MachinePortIds.ShipPowerA),
            Endpoint(idlePole, MachinePortIds.Power1)).Succeeded);

        var idleResult = new ConnectedPowerGridSimulation(noDemandNetwork).Tick(CometId, 1)
            .Single(component => component.Topology.Endpoints.Contains(
                new MachineConnectionEndpoint(idleShip.NodeId, MachinePortIds.ShipPowerA)));
        Assert.Equal(0, idleResult.Metrics.ActualProductionKilowatts);
        Assert.Equal(1, fullTank.CurrentFuel);

        var emptyNetwork = new MachineConnectionNetwork();
        var crusher = ReadyCrusher("empty-tank-consumer");
        Register(emptyNetwork, crusher);
        var emptyTank = new ShipFuelTank(initialFuel: 0);
        var emptyShip = new ShipPowerNode(new MachineInstanceId("empty-ship"), CometId, emptyTank);
        Assert.True(emptyNetwork.RegisterPowerNode(emptyShip));
        Assert.True(emptyNetwork.TryConnect(
            new MachineConnectionId("empty-cable"),
            ConnectionTypeIds.PowerCable,
            new MachineConnectionEndpoint(emptyShip.NodeId, MachinePortIds.ShipPowerA),
            Endpoint(crusher, MachinePortIds.Power)).Succeeded);

        var emptyResult = new ConnectedPowerGridSimulation(emptyNetwork).Tick(CometId, 1)
            .Single(component => component.Topology.Endpoints.Contains(
                Endpoint(crusher, MachinePortIds.Power)));
        Assert.Equal(PowerGridStatus.NoCapacity, emptyResult.Metrics.Status);
        Assert.Equal(0, emptyResult.Metrics.ActualProductionKilowatts);
        Assert.Equal(0, emptyTank.CurrentFuel);
    }

    [Fact]
    public void ControlSnapshots_PruneTopologyThatNoLongerExistsAndRestoreReplacesState()
    {
        var network = new MachineConnectionNetwork();
        var first = PlacedMachine(MachineDefinitionIds.BasicGenerator, "snapshot-generator-a");
        var second = PlacedMachine(MachineDefinitionIds.BasicGenerator, "snapshot-generator-b");
        first.SetEnabled(true);
        second.SetEnabled(true);
        Register(network, first, second);
        var simulation = new ConnectedPowerGridSimulation(network);
        var initial = simulation.Tick(CometId, 1);
        Assert.Equal(2, initial.Count);
        Assert.True(simulation.SetNetworkEnabled(initial[0].NetworkId, false));
        Assert.Equal(2, simulation.CreateControlSnapshots().Count);

        Assert.True(network.TryConnect(
            new MachineConnectionId("snapshot-merge"),
            ConnectionTypeIds.PowerCable,
            Endpoint(first, MachinePortIds.Power),
            Endpoint(second, MachinePortIds.Power)).Succeeded);
        simulation.InvalidateTopology();
        var merged = Assert.Single(simulation.Tick(CometId, 1));
        var snapshots = simulation.CreateControlSnapshots();

        Assert.Single(snapshots);
        Assert.Equal(merged.NetworkId, snapshots[0].NetworkId);
        simulation.RestoreControlSnapshots([
            new PowerNetworkControlSnapshot(merged.NetworkId, false, false, 0),
        ]);
        Assert.Single(simulation.NetworkStates);
        Assert.False(simulation.GetNetworkState(merged.NetworkId)!.IsEnabled);
    }

    private const string CometId = "comet-logistics";

    private static MachineState PlacedMachine(MachineDefinitionId definitionId, string instanceId) => new(
        new MachineInstanceId(instanceId),
        DefaultMachineCatalog.Instance.Get(definitionId),
        new MachinePlacement(CometId, 0, 0, 0),
        constructionCompleted: true);

    private static MachineState PlacedMachine(
        MachineDefinitionId definitionId,
        string instanceId,
        string cometId) => new(
        new MachineInstanceId(instanceId),
        DefaultMachineCatalog.Instance.Get(definitionId),
        new MachinePlacement(cometId, 0, 0, 0),
        constructionCompleted: true);

    private static MachineState ReadyCrusher(string instanceId)
    {
        var recipe = DefaultRecipeCatalog.Instance.Get(DefaultRecipeIds.CrushIronOre);
        var machine = ReadyMachine(instanceId, recipe);
        machine.InputInventory.Add(ProductionItemIds.IronOre, 2);
        return machine;
    }

    private static MachineState ReadyMachine(string instanceId, RecipeDefinition recipe)
    {
        var machine = PlacedMachine(recipe.MachineId, instanceId);
        Assert.True(machine.SelectRecipe(recipe));
        machine.SetEnabled(true);
        return machine;
    }

    private static MachineConnectionEndpoint Endpoint(MachineState machine, MachinePortId portId) =>
        new(machine.InstanceId, portId);

    private static void Register(MachineConnectionNetwork network, params MachineState[] machines)
    {
        foreach (var machine in machines)
        {
            Assert.True(network.RegisterMachine(machine));
        }
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Infrastructure.Persistence;

/// <summary>
/// Pure JSON conversion seam for factory saves. Core snapshot records are deliberately mapped
/// to primitive DTO fields instead of being serialized directly.
/// </summary>
public static class FactoryStateJsonCodec
{
    private const int SchemaVersion1 = 1;
    private const int SchemaVersion2 = 2;
    private const int SchemaVersion3 = 3;
    private const int SchemaVersion4 = 4;
    private const string PlayerShipMachineId = "player_ship";
    private const double ValidationEpsilon = 0.000_001;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Serialize(FactoryStateData state)
    {
        Validate(state);
        return JsonSerializer.Serialize(ToDocument(state), JsonOptions);
    }

    public static bool TryDeserialize(
        string json,
        out FactoryStateData state,
        out string error)
    {
        state = FactoryStateData.CreateDefault();
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The factory save is empty.";
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize<FactoryStateDocument>(json, JsonOptions)
                           ?? throw new InvalidDataException("The factory save has no root object.");
            state = FromDocument(document);
            Validate(state);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Headless-test seam covering two independently switched ship power ports, separate power-grid
    /// protection states, connections, exact player-inventory slot roundtrips and all v1-v3
    /// migrations. It throws when an invariant is broken.
    /// </summary>
    public static void RunSchemaV4SmokeTest()
    {
        var sourceMachine = new MachineState(
            new MachineInstanceId("crusher-smoke"),
            DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Crusher),
            new MachinePlacement("comet-smoke", -40, 0, 0),
            constructionCompleted: true).CreateSnapshot();
        var targetMachine = new MachineState(
            new MachineInstanceId("smelter-smoke"),
            DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.Smelter),
            new MachinePlacement("comet-smoke", 40, 0, 0),
            constructionCompleted: true).CreateSnapshot();
        var powerPole = new MachineState(
            new MachineInstanceId("power-pole-smoke"),
            DefaultMachineCatalog.Instance.Get(MachineDefinitionIds.PowerPole),
            new MachinePlacement("comet-smoke", 0, 80, 0.25),
            constructionCompleted: true).CreateSnapshot();
        var expectedBelt = new MachineConnectionSnapshot(
            new MachineConnectionId("belt-smoke"),
            sourceMachine.InstanceId,
            MachinePortIds.SolidOutput,
            targetMachine.InstanceId,
            MachinePortIds.SolidInput,
            ConnectionKind.ConveyorBelt);
        var expectedShipCableA = new MachineConnectionSnapshot(
            new MachineConnectionId("ship-cable-a-smoke"),
            new MachineInstanceId(PlayerShipMachineId),
            MachinePortIds.ShipPowerA,
            powerPole.InstanceId,
            MachinePortIds.Power1,
            ConnectionKind.PowerCable);
        var expectedShipCableB = new MachineConnectionSnapshot(
            new MachineConnectionId("ship-cable-b-smoke"),
            new MachineInstanceId(PlayerShipMachineId),
            MachinePortIds.ShipPowerB,
            sourceMachine.InstanceId,
            MachinePortIds.Power,
            ConnectionKind.PowerCable);
        var expectedShipDocking = new ShipDockingStateData(
            true,
            "comet-smoke",
            0,
            0,
            180,
            -12,
            0.4,
            0.75,
            2700,
            2500,
            0.6);
        var expected = FactoryStateData.CreateDefault() with
        {
            Machines = [sourceMachine, targetMachine, powerPole],
            Connections = [expectedBelt, expectedShipCableA, expectedShipCableB],
            AstronautInventory =
            [
                new InventorySlotState(0, "iron_ore", 200),
                new InventorySlotState(7, ProductionItemIds.WaterContainer.Value, 3),
            ],
            ShipInventory =
            [
                new InventorySlotState(12, ProductionItemIds.FuelContainer.Value, 9),
                new InventorySlotState(49, ProductionItemIds.EmptyFuelContainer.Value, 2),
            ],
            ActiveResearchStationId = "research-station-smoke",
            PowerNetworkControls =
            [
                new PowerNetworkControlState("grid-ship-a-disabled-smoke", false, false, 0),
                new PowerNetworkControlState("grid-ship-b-tripped-smoke", true, true, 0),
            ],
            ShipPower = new ShipPowerState(false, true),
            ShipDocking = expectedShipDocking,
        };
        var json = Serialize(expected);
        if (!TryDeserialize(json, out var restored, out var roundtripError) ||
            restored.Connections.Count != 3 ||
            !expected.Connections.SequenceEqual(restored.Connections) ||
            !expected.AstronautInventory.SequenceEqual(restored.AstronautInventory) ||
            !expected.ShipInventory.SequenceEqual(restored.ShipInventory) ||
            expected.ActiveResearchStationId != restored.ActiveResearchStationId ||
            !expected.PowerNetworkControls.SequenceEqual(restored.PowerNetworkControls) ||
            expected.ShipPower != restored.ShipPower ||
            expected.ShipDocking != restored.ShipDocking)
        {
            throw new InvalidOperationException(
                $"Factory schema v4 roundtrip failed: {roundtripError}");
        }

        var restoredInventory = new SlotInventory(InventoryConfiguration.AstronautSlotCount);
        InventoryStatePersistence.Restore(restoredInventory, restored.AstronautInventory);
        if (restoredInventory.GetSlot(0).Amount != 200 ||
            restoredInventory.GetSlot(7).ItemId != ProductionItemIds.WaterContainer ||
            restoredInventory.GetSlot(7).Amount != 3)
        {
            throw new InvalidOperationException("Factory inventory slot restoration changed slot positions.");
        }

        var versionThreeDocument = JsonNode.Parse(json)?.AsObject()
                                   ?? throw new InvalidOperationException(
                                       "Could not create the v3 migration fixture.");
        versionThreeDocument["version"] = SchemaVersion3;
        versionThreeDocument.Remove("powerNetworkControls");
        versionThreeDocument.Remove("shipPower");
        versionThreeDocument.Remove("shipDocking");
        versionThreeDocument["connections"] = new JsonArray(
            versionThreeDocument["connections"]!.AsArray()[0]!.DeepClone());
        if (!TryDeserialize(versionThreeDocument.ToJsonString(), out var migratedV3, out var migrationV3Error) ||
            migratedV3.Version != FactoryStateData.CurrentVersion ||
            !new[] { expectedBelt }.SequenceEqual(migratedV3.Connections) ||
            migratedV3.PowerNetworkControls.Count != 0 ||
            migratedV3.ShipPower != ShipPowerState.Default ||
            migratedV3.ShipDocking != ShipDockingStateData.Detached)
        {
            throw new InvalidOperationException(
                $"Factory schema v3 migration failed: {migrationV3Error}");
        }

        var versionTwoDocument = JsonNode.Parse(versionThreeDocument.ToJsonString())?.AsObject()
                                 ?? throw new InvalidOperationException("Could not create the v2 migration fixture.");
        versionTwoDocument["version"] = SchemaVersion2;
        versionTwoDocument.Remove("connections");
        if (!TryDeserialize(versionTwoDocument.ToJsonString(), out var migratedV2, out var migrationV2Error) ||
            migratedV2.Version != FactoryStateData.CurrentVersion || migratedV2.Connections.Count != 0 ||
            !expected.AstronautInventory.SequenceEqual(migratedV2.AstronautInventory) ||
            !expected.ShipInventory.SequenceEqual(migratedV2.ShipInventory) ||
            migratedV2.PowerNetworkControls.Count != 0 ||
            migratedV2.ShipPower != ShipPowerState.Default ||
            migratedV2.ShipDocking != ShipDockingStateData.Detached)
        {
            throw new InvalidOperationException(
                $"Factory schema v2 migration failed: {migrationV2Error}");
        }

        var legacyDocument = JsonNode.Parse(versionTwoDocument.ToJsonString())?.AsObject()
                             ?? throw new InvalidOperationException("Could not create the v1 migration fixture.");
        legacyDocument["version"] = SchemaVersion1;
        legacyDocument.Remove("astronautInventory");
        legacyDocument.Remove("shipInventory");
        legacyDocument.Remove("activeResearchStationId");
        if (!TryDeserialize(legacyDocument.ToJsonString(), out var migratedV1, out var migrationV1Error) ||
            migratedV1.Version != FactoryStateData.CurrentVersion || migratedV1.Connections.Count != 0 ||
            migratedV1.AstronautInventory.Count != 0 || migratedV1.ShipInventory.Count != 0 ||
            migratedV1.ActiveResearchStationId is not null)
        {
            throw new InvalidOperationException(
                $"Factory schema v1 migration failed: {migrationV1Error}");
        }

        var corruptDocument = JsonNode.Parse(json)?.AsObject()
                              ?? throw new InvalidOperationException("Could not create the corruption fixture.");
        corruptDocument["astronautInventory"]![0]!["amount"] =
            InventoryConfiguration.MaximumStackSize + 1;
        if (TryDeserialize(corruptDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("An overfilled persisted inventory slot was accepted.");
        }

        var corruptConnectionDocument = JsonNode.Parse(json)?.AsObject()
                                        ?? throw new InvalidOperationException(
                                            "Could not create the connection corruption fixture.");
        corruptConnectionDocument["connections"]![0]!["targetMachineId"] = "missing-machine";
        if (TryDeserialize(corruptConnectionDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("A dangling persisted machine connection was accepted.");
        }

        var duplicateControlDocument = JsonNode.Parse(json)?.AsObject()
                                       ?? throw new InvalidOperationException(
                                           "Could not create the power-control corruption fixture.");
        duplicateControlDocument["powerNetworkControls"]!.AsArray().Add(
            duplicateControlDocument["powerNetworkControls"]![0]!.DeepClone());
        if (TryDeserialize(duplicateControlDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("A duplicated persisted power-network control was accepted.");
        }

        var invalidDockingDocument = JsonNode.Parse(json)?.AsObject()
                                     ?? throw new InvalidOperationException(
                                         "Could not create the docking corruption fixture.");
        invalidDockingDocument["shipDocking"]!["landingLegProgress"] = 1.5;
        if (TryDeserialize(invalidDockingDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("An invalid persisted landing-leg progress was accepted.");
        }

        var detachedCableDocument = JsonNode.Parse(json)?.AsObject()
                                    ?? throw new InvalidOperationException(
                                        "Could not create the detached ship-cable fixture.");
        detachedCableDocument["shipDocking"]!["isAttached"] = false;
        detachedCableDocument["shipDocking"]!["cometId"] = null;
        if (TryDeserialize(detachedCableDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("A ship cable was accepted while the ship was detached.");
        }

        var occupiedShipPortDocument = JsonNode.Parse(json)?.AsObject()
                                       ?? throw new InvalidOperationException(
                                           "Could not create the occupied ship-port fixture.");
        occupiedShipPortDocument["connections"]!.AsArray().Add(new JsonObject
        {
            ["connectionId"] = "duplicate-ship-port-smoke",
            ["sourceMachineId"] = PlayerShipMachineId,
            ["sourcePortId"] = MachinePortIds.ShipPowerA.Value,
            ["targetMachineId"] = targetMachine.InstanceId.Value,
            ["targetPortId"] = MachinePortIds.Power.Value,
            ["kind"] = ConnectionKind.PowerCable.ToString(),
        });
        if (TryDeserialize(occupiedShipPortDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("A second cable on one ship power port was accepted.");
        }
    }

    private static FactoryStateDocument ToDocument(FactoryStateData state) => new()
    {
        Version = state.Version,
        Machines = state.Machines.Select(ToDto).ToList(),
        Connections = state.Connections
            .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList(),
        Research = ToDto(state.Research),
        FirstBasicGeneratorBuilt = state.FirstBasicGeneratorBuilt,
        ShipFuel = state.ShipFuel,
        AstronautInventory = state.AstronautInventory.Select(ToDto).ToList(),
        ShipInventory = state.ShipInventory.Select(ToDto).ToList(),
        ActiveResearchStationId = state.ActiveResearchStationId,
        PowerNetworkControls = state.PowerNetworkControls
            .OrderBy(control => control.NetworkId, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList(),
        ShipPower = ToDto(state.ShipPower),
        ShipDocking = ToDto(state.ShipDocking),
        LastSimulatedUtcByComet = state.LastSimulatedUtcByComet
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CometSimulationTimestampDto
            {
                CometId = pair.Key,
                LastSimulatedUtc = pair.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            })
            .ToList(),
    };

    private static MachineStateDto ToDto(MachineStateSnapshot machine) => new()
    {
        InstanceId = machine.InstanceId.Value,
        DefinitionId = machine.DefinitionId.Value,
        Placement = machine.Placement is null
            ? null
            : new MachinePlacementDto
            {
                CometId = machine.Placement.CometId,
                RelativePositionX = machine.Placement.RelativePositionX,
                RelativePositionY = machine.Placement.RelativePositionY,
                RelativeRotationRadians = machine.Placement.RelativeRotationRadians,
            },
        SelectedRecipeId = machine.SelectedRecipeId?.Value,
        IsEnabled = machine.IsEnabled,
        Status = machine.Status.ToString(),
        ConstructionProgressSeconds = machine.ConstructionProgressSeconds,
        ProductionProgressSeconds = machine.ProductionProgressSeconds,
        IsBatchInProgress = machine.IsBatchInProgress,
        GeneratorFuelSecondsRemaining = machine.GeneratorFuelSecondsRemaining,
        InputSlots = machine.InputSlots.Select(ToDto).ToList(),
        OutputSlots = machine.OutputSlots.Select(ToDto).ToList(),
    };

    private static MachineInventorySlotDto ToDto(MachineInventorySlotSnapshot slot) => new()
    {
        Index = slot.Index,
        ItemId = slot.ItemId.Value,
        Amount = slot.Amount,
    };

    private static MachineConnectionDto ToDto(MachineConnectionSnapshot connection) => new()
    {
        ConnectionId = connection.ConnectionId.Value,
        SourceMachineId = connection.SourceMachineId.Value,
        SourcePortId = connection.SourcePortId.Value,
        TargetMachineId = connection.TargetMachineId.Value,
        TargetPortId = connection.TargetPortId.Value,
        Kind = connection.Kind.ToString(),
    };

    private static ResearchStateDto ToDto(ResearchStateSnapshot research) => new()
    {
        CompletedResearch = research.CompletedResearch.Select(id => id.Value).ToList(),
        ActiveResearchId = research.ActiveResearchId?.Value,
        ProgressSeconds = research.ProgressSeconds,
        IsEnabled = research.IsEnabled,
        Status = research.Status.ToString(),
    };

    private static InventorySlotDto ToDto(InventorySlotState slot) => new()
    {
        Index = slot.Index,
        ItemId = slot.ItemId,
        Amount = slot.Amount,
    };

    private static PowerNetworkControlDto ToDto(PowerNetworkControlState control) => new()
    {
        NetworkId = control.NetworkId,
        IsEnabled = control.IsEnabled,
        BreakerTripped = control.BreakerTripped,
        OverloadElapsedSeconds = control.OverloadElapsedSeconds,
    };

    private static ShipPowerDto ToDto(ShipPowerState shipPower) => new()
    {
        ConnectorAEnabled = shipPower.ConnectorAEnabled,
        ConnectorBEnabled = shipPower.ConnectorBEnabled,
    };

    private static ShipDockingDto ToDto(ShipDockingStateData docking) => new()
    {
        IsAttached = docking.IsAttached,
        CometId = docking.CometId,
        SectorX = docking.SectorX,
        SectorY = docking.SectorY,
        RelativePositionX = docking.RelativePositionX,
        RelativePositionY = docking.RelativePositionY,
        RelativeRotationRadians = docking.RelativeRotationRadians,
        LandingLegProgress = docking.LandingLegProgress,
        GlobalPositionX = docking.GlobalPositionX,
        GlobalPositionY = docking.GlobalPositionY,
        GlobalRotationRadians = docking.GlobalRotationRadians,
    };

    private static FactoryStateData FromDocument(FactoryStateDocument document)
    {
        if (document.Version is null ||
            document.Version is not (SchemaVersion1 or SchemaVersion2 or SchemaVersion3 or SchemaVersion4))
        {
            throw new InvalidDataException(
                $"Factory save version {document.Version?.ToString() ?? "<missing>"} is unsupported; " +
                $"expected {SchemaVersion1}, {SchemaVersion2}, {SchemaVersion3} or {SchemaVersion4}.");
        }

        if (document.Machines is null || document.Research is null ||
            document.FirstBasicGeneratorBuilt is null || document.ShipFuel is null ||
            document.LastSimulatedUtcByComet is null)
        {
            throw new InvalidDataException("The factory save is missing required root fields.");
        }

        var hasPlayerInventories = document.Version >= SchemaVersion2;
        var hasConnections = document.Version >= SchemaVersion3;
        var hasPowerGridState = document.Version >= SchemaVersion4;
        if (hasPlayerInventories && (document.AstronautInventory is null || document.ShipInventory is null))
        {
            throw new InvalidDataException("The factory save is missing player inventory fields.");
        }

        if (hasConnections && document.Connections is null)
        {
            throw new InvalidDataException("The factory save is missing machine connections.");
        }

        if (hasPowerGridState &&
            (document.PowerNetworkControls is null || document.ShipPower is null || document.ShipDocking is null))
        {
            throw new InvalidDataException("The factory save is missing power-grid or ship state.");
        }

        var timestamps = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var timestamp in document.LastSimulatedUtcByComet)
        {
            if (timestamp is null || string.IsNullOrWhiteSpace(timestamp.CometId) ||
                string.IsNullOrWhiteSpace(timestamp.LastSimulatedUtc) ||
                !DateTimeOffset.TryParseExact(
                    timestamp.LastSimulatedUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedUtc) ||
                !timestamps.TryAdd(timestamp.CometId, parsedUtc.ToUniversalTime()))
            {
                throw new InvalidDataException("A comet simulation timestamp is invalid or duplicated.");
            }
        }

        return new FactoryStateData(
            FactoryStateData.CurrentVersion,
            document.Machines.Select(FromDto).ToArray(),
            hasConnections ? document.Connections!.Select(FromDto).ToArray() : [],
            FromDto(document.Research),
            document.FirstBasicGeneratorBuilt.Value,
            document.ShipFuel.Value,
            timestamps,
            hasPlayerInventories ? document.AstronautInventory!.Select(FromDto).ToArray() : [],
            hasPlayerInventories ? document.ShipInventory!.Select(FromDto).ToArray() : [],
            hasPlayerInventories ? document.ActiveResearchStationId : null,
            hasPowerGridState ? document.PowerNetworkControls!.Select(FromDto).ToArray() : [],
            hasPowerGridState ? FromDto(document.ShipPower) : ShipPowerState.Default,
            hasPowerGridState ? FromDto(document.ShipDocking) : ShipDockingStateData.Detached);
    }

    private static MachineStateSnapshot FromDto(MachineStateDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.InstanceId) || string.IsNullOrWhiteSpace(dto.DefinitionId) ||
            dto.IsEnabled is null || string.IsNullOrWhiteSpace(dto.Status) ||
            dto.ConstructionProgressSeconds is null || dto.ProductionProgressSeconds is null ||
            dto.IsBatchInProgress is null || dto.GeneratorFuelSecondsRemaining is null ||
            dto.InputSlots is null || dto.OutputSlots is null ||
            !TryParseDefinedEnum(dto.Status, out MachineOperationStatus status))
        {
            throw new InvalidDataException("A persisted machine is missing required fields.");
        }

        MachinePlacement? placement = null;
        if (dto.Placement is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Placement.CometId) ||
                dto.Placement.RelativePositionX is null || dto.Placement.RelativePositionY is null ||
                dto.Placement.RelativeRotationRadians is null)
            {
                throw new InvalidDataException("A persisted machine placement is incomplete.");
            }

            placement = new MachinePlacement(
                dto.Placement.CometId,
                dto.Placement.RelativePositionX.Value,
                dto.Placement.RelativePositionY.Value,
                dto.Placement.RelativeRotationRadians.Value);
        }

        return new MachineStateSnapshot(
            new MachineInstanceId(dto.InstanceId),
            new MachineDefinitionId(dto.DefinitionId),
            placement,
            dto.SelectedRecipeId is null ? null : new RecipeId(dto.SelectedRecipeId),
            dto.IsEnabled.Value,
            status,
            dto.ConstructionProgressSeconds.Value,
            dto.ProductionProgressSeconds.Value,
            dto.IsBatchInProgress.Value,
            dto.GeneratorFuelSecondsRemaining.Value,
            dto.InputSlots.Select(FromDto).ToArray(),
            dto.OutputSlots.Select(FromDto).ToArray());
    }

    private static MachineInventorySlotSnapshot FromDto(MachineInventorySlotDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Index is null || string.IsNullOrWhiteSpace(dto.ItemId) || dto.Amount is null)
        {
            throw new InvalidDataException("A persisted machine inventory slot is incomplete.");
        }

        return new MachineInventorySlotSnapshot(dto.Index.Value, new ItemId(dto.ItemId), dto.Amount.Value);
    }

    private static MachineConnectionSnapshot FromDto(MachineConnectionDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.ConnectionId) ||
            string.IsNullOrWhiteSpace(dto.SourceMachineId) ||
            string.IsNullOrWhiteSpace(dto.SourcePortId) ||
            string.IsNullOrWhiteSpace(dto.TargetMachineId) ||
            string.IsNullOrWhiteSpace(dto.TargetPortId) ||
            string.IsNullOrWhiteSpace(dto.Kind) ||
            !TryParseDefinedEnum(dto.Kind, out ConnectionKind kind))
        {
            throw new InvalidDataException("A persisted machine connection is incomplete or invalid.");
        }

        return new MachineConnectionSnapshot(
            new MachineConnectionId(dto.ConnectionId),
            new MachineInstanceId(dto.SourceMachineId),
            new MachinePortId(dto.SourcePortId),
            new MachineInstanceId(dto.TargetMachineId),
            new MachinePortId(dto.TargetPortId),
            kind);
    }

    private static PowerNetworkControlState FromDto(PowerNetworkControlDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.NetworkId) || dto.IsEnabled is null ||
            dto.BreakerTripped is null || dto.OverloadElapsedSeconds is null)
        {
            throw new InvalidDataException("A persisted power-network control is incomplete.");
        }

        return new PowerNetworkControlState(
            dto.NetworkId,
            dto.IsEnabled.Value,
            dto.BreakerTripped.Value,
            dto.OverloadElapsedSeconds.Value);
    }

    private static ShipPowerState FromDto(ShipPowerDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.ConnectorAEnabled is null || dto.ConnectorBEnabled is null)
        {
            throw new InvalidDataException("The persisted ship power state is incomplete.");
        }

        return new ShipPowerState(dto.ConnectorAEnabled.Value, dto.ConnectorBEnabled.Value);
    }

    private static ShipDockingStateData FromDto(ShipDockingDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.IsAttached is null || dto.SectorX is null || dto.SectorY is null ||
            dto.RelativePositionX is null || dto.RelativePositionY is null ||
            dto.RelativeRotationRadians is null || dto.LandingLegProgress is null ||
            dto.GlobalPositionX is null || dto.GlobalPositionY is null ||
            dto.GlobalRotationRadians is null)
        {
            throw new InvalidDataException("The persisted ship docking state is incomplete.");
        }

        return new ShipDockingStateData(
            dto.IsAttached.Value,
            dto.CometId,
            dto.SectorX.Value,
            dto.SectorY.Value,
            dto.RelativePositionX.Value,
            dto.RelativePositionY.Value,
            dto.RelativeRotationRadians.Value,
            dto.LandingLegProgress.Value,
            dto.GlobalPositionX.Value,
            dto.GlobalPositionY.Value,
            dto.GlobalRotationRadians.Value);
    }

    private static ResearchStateSnapshot FromDto(ResearchStateDto dto)
    {
        if (dto.CompletedResearch is null || dto.ProgressSeconds is null || dto.IsEnabled is null ||
            string.IsNullOrWhiteSpace(dto.Status) ||
            !TryParseDefinedEnum(dto.Status, out ResearchStatus status))
        {
            throw new InvalidDataException("The persisted research state is missing required fields.");
        }

        return new ResearchStateSnapshot(
            dto.CompletedResearch.Select(id => new ResearchId(id)).ToArray(),
            dto.ActiveResearchId is null ? null : new ResearchId(dto.ActiveResearchId),
            dto.ProgressSeconds.Value,
            dto.IsEnabled.Value,
            status);
    }

    private static InventorySlotState FromDto(InventorySlotDto? dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Index is null || string.IsNullOrWhiteSpace(dto.ItemId) || dto.Amount is null)
        {
            throw new InvalidDataException("A persisted player inventory slot is incomplete.");
        }

        return new InventorySlotState(dto.Index.Value, dto.ItemId, dto.Amount.Value);
    }

    private static void Validate(FactoryStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Version != FactoryStateData.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Factory save version {state.Version} is unsupported; expected {FactoryStateData.CurrentVersion}.");
        }

        if (state.Machines is null || state.Connections is null || state.Research is null ||
            state.LastSimulatedUtcByComet is null ||
            state.AstronautInventory is null || state.ShipInventory is null ||
            state.PowerNetworkControls is null || state.ShipPower is null || state.ShipDocking is null ||
            !double.IsFinite(state.ShipFuel) || state.ShipFuel < 0 ||
            state.ShipFuel > ShipFuelConfiguration.TankCapacity)
        {
            throw new InvalidDataException("The factory save contains invalid root state.");
        }

        ValidatePlayerInventory(
            state.AstronautInventory,
            InventoryConfiguration.AstronautSlotCount,
            "astronaut");
        ValidatePlayerInventory(
            state.ShipInventory,
            InventoryConfiguration.ShipSlotCount,
            "ship");
        if (state.ActiveResearchStationId is not null &&
            string.IsNullOrWhiteSpace(state.ActiveResearchStationId))
        {
            throw new InvalidDataException("The active research station ID is invalid.");
        }

        var machineIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var machine in state.Machines)
        {
            if (machine is null || !machineIds.Add(machine.InstanceId.Value))
            {
                throw new InvalidDataException("Persisted machine instance IDs must be present and unique.");
            }

            Validate(machine);
        }

        ValidatePowerNetworkControls(state.PowerNetworkControls);
        Validate(state.ShipDocking);
        ValidateConnections(state.Connections, state.Machines, state.ShipDocking);

        Validate(state.Research);
        var cometIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (cometId, _) in state.LastSimulatedUtcByComet)
        {
            if (string.IsNullOrWhiteSpace(cometId) || !cometIds.Add(cometId))
            {
                throw new InvalidDataException("Persisted comet simulation IDs must be present and unique.");
            }
        }
    }

    private static void Validate(MachineStateSnapshot machine)
    {
        if (string.IsNullOrWhiteSpace(machine.InstanceId.Value) ||
            string.IsNullOrWhiteSpace(machine.DefinitionId.Value) ||
            !Enum.IsDefined(machine.Status) ||
            !double.IsFinite(machine.ConstructionProgressSeconds) || machine.ConstructionProgressSeconds < 0 ||
            !double.IsFinite(machine.ProductionProgressSeconds) || machine.ProductionProgressSeconds < 0 ||
            !double.IsFinite(machine.GeneratorFuelSecondsRemaining) || machine.GeneratorFuelSecondsRemaining < 0 ||
            machine.InputSlots is null || machine.OutputSlots is null ||
            (machine.IsBatchInProgress && machine.SelectedRecipeId is null) ||
            (machine.SelectedRecipeId is { } recipeId && string.IsNullOrWhiteSpace(recipeId.Value)))
        {
            throw new InvalidDataException($"Machine '{machine.InstanceId}' contains invalid persisted state.");
        }

        machine.Placement?.Validate();
        ValidateSlots(machine.InputSlots, machine.InstanceId);
        ValidateSlots(machine.OutputSlots, machine.InstanceId);
    }

    private static void ValidateSlots(
        IReadOnlyList<MachineInventorySlotSnapshot> slots,
        MachineInstanceId machineId)
    {
        var indices = new HashSet<int>();
        foreach (var slot in slots)
        {
            if (slot is null || !indices.Add(slot.Index) || slot.Index < 0 ||
                string.IsNullOrWhiteSpace(slot.ItemId.Value) || slot.Amount <= 0 ||
                slot.Amount > InventoryConfiguration.MaximumStackSize)
            {
                throw new InvalidDataException($"Machine '{machineId}' contains an invalid inventory slot.");
            }
        }
    }

    private static void ValidateConnections(
        IReadOnlyList<MachineConnectionSnapshot> connections,
        IReadOnlyList<MachineStateSnapshot> machines,
        ShipDockingStateData shipDocking)
    {
        var machinesById = machines.ToDictionary(machine => machine.InstanceId.Value, StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var endpointConnectionCounts = new Dictionary<(string MachineId, string PortId), int>();
        var endpointPairs = new HashSet<(
            ConnectionKind Kind,
            string SourceMachineId,
            string SourcePortId,
            string TargetMachineId,
            string TargetPortId)>();

        foreach (var connection in connections)
        {
            if (connection is null ||
                string.IsNullOrWhiteSpace(connection.ConnectionId.Value) ||
                string.IsNullOrWhiteSpace(connection.SourceMachineId.Value) ||
                string.IsNullOrWhiteSpace(connection.SourcePortId.Value) ||
                string.IsNullOrWhiteSpace(connection.TargetMachineId.Value) ||
                string.IsNullOrWhiteSpace(connection.TargetPortId.Value) ||
                !Enum.IsDefined(connection.Kind) ||
                !connectionIds.Add(connection.ConnectionId.Value) ||
                connection.SourceMachineId == connection.TargetMachineId)
            {
                throw new InvalidDataException("The factory save contains an invalid machine connection.");
            }

            if (!DefaultConnectionTypeCatalog.Instance.TryGet(connection.Kind, out var connectionType) ||
                connectionType is null)
            {
                throw new InvalidDataException(
                    $"Machine connection '{connection.ConnectionId}' has an unknown connection type.");
            }

            var source = ResolvePersistedEndpoint(
                connection.SourceMachineId,
                connection.SourcePortId,
                machinesById,
                shipDocking);
            var target = ResolvePersistedEndpoint(
                connection.TargetMachineId,
                connection.TargetPortId,
                machinesById,
                shipDocking);
            if (!string.Equals(source.CometId, target.CometId, StringComparison.Ordinal) ||
                source.Medium != connectionType.Medium || target.Medium != connectionType.Medium ||
                (connectionType.IsDirectional && (!source.CanSend || !target.CanReceive)) ||
                (!connectionType.IsDirectional &&
                 !((source.CanSend && target.CanReceive) || (target.CanSend && source.CanReceive))) ||
                (connectionType.Medium != TransportMedium.Power &&
                 !HaveCompatiblePersistedItems(source.AllowedItemIds, target.AllowedItemIds)))
            {
                throw new InvalidDataException(
                    $"Machine connection '{connection.ConnectionId}' has missing or incompatible endpoints.");
            }

            ValidateEndpointCapacity(
                connection.ConnectionId,
                connection.SourceMachineId,
                connection.SourcePortId,
                source.MaximumConnections,
                endpointConnectionCounts);
            ValidateEndpointCapacity(
                connection.ConnectionId,
                connection.TargetMachineId,
                connection.TargetPortId,
                target.MaximumConnections,
                endpointConnectionCounts);

            var sourceMachineId = connection.SourceMachineId.Value;
            var sourcePortId = connection.SourcePortId.Value;
            var targetMachineId = connection.TargetMachineId.Value;
            var targetPortId = connection.TargetPortId.Value;
            if (!connectionType.IsDirectional &&
                CompareEndpoints(sourceMachineId, sourcePortId, targetMachineId, targetPortId) > 0)
            {
                (sourceMachineId, targetMachineId) = (targetMachineId, sourceMachineId);
                (sourcePortId, targetPortId) = (targetPortId, sourcePortId);
            }

            if (!endpointPairs.Add((
                    connection.Kind,
                    sourceMachineId,
                    sourcePortId,
                    targetMachineId,
                    targetPortId)))
            {
                throw new InvalidDataException(
                    $"Machine connection '{connection.ConnectionId}' duplicates an existing endpoint pair.");
            }
        }
    }

    private static PersistedEndpointInfo ResolvePersistedEndpoint(
        MachineInstanceId machineId,
        MachinePortId portId,
        IReadOnlyDictionary<string, MachineStateSnapshot> machinesById,
        ShipDockingStateData shipDocking)
    {
        if (string.Equals(machineId.Value, PlayerShipMachineId, StringComparison.Ordinal))
        {
            if (!shipDocking.IsAttached || string.IsNullOrWhiteSpace(shipDocking.CometId) ||
                (portId != MachinePortIds.ShipPowerA && portId != MachinePortIds.ShipPowerB))
            {
                throw new InvalidDataException(
                    $"Ship power endpoint '{machineId}/{portId}' is unavailable or invalid.");
            }

            return new PersistedEndpointInfo(
                shipDocking.CometId,
                TransportMedium.Power,
                true,
                true,
                1,
                null);
        }

        if (!machinesById.TryGetValue(machineId.Value, out var machine) || machine.Placement is null ||
            !DefaultMachinePortCatalog.Instance.TryGet(machine.DefinitionId, portId, out var port) ||
            port is null)
        {
            throw new InvalidDataException($"Machine endpoint '{machineId}/{portId}' does not exist.");
        }

        return new PersistedEndpointInfo(
            machine.Placement.CometId,
            port.Medium,
            port.CanSend,
            port.CanReceive,
            port.MaximumConnections,
            port.AllowedItemIds);
    }

    private static void ValidateEndpointCapacity(
        MachineConnectionId connectionId,
        MachineInstanceId machineId,
        MachinePortId portId,
        int maximumConnections,
        IDictionary<(string MachineId, string PortId), int> endpointConnectionCounts)
    {
        var key = (machineId.Value, portId.Value);
        endpointConnectionCounts.TryGetValue(key, out var count);
        count++;
        if (count > maximumConnections)
        {
            throw new InvalidDataException(
                $"Machine connection '{connectionId}' exceeds endpoint capacity at '{machineId}/{portId}'.");
        }

        endpointConnectionCounts[key] = count;
    }

    private static bool HaveCompatiblePersistedItems(
        IReadOnlyCollection<ItemId>? sourceItems,
        IReadOnlyCollection<ItemId>? targetItems)
    {
        if (sourceItems is null || targetItems is null)
        {
            return true;
        }

        return sourceItems.Any(targetItems.Contains);
    }

    private static void ValidatePowerNetworkControls(
        IReadOnlyList<PowerNetworkControlState> controls)
    {
        var networkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in controls)
        {
            if (control is null || string.IsNullOrWhiteSpace(control.NetworkId) ||
                !networkIds.Add(control.NetworkId) ||
                !double.IsFinite(control.OverloadElapsedSeconds) ||
                control.OverloadElapsedSeconds < 0 ||
                control.OverloadElapsedSeconds >=
                SpaceFactory.Core.Power.PowerGridConfiguration.OverloadToleranceSeconds ||
                (control.BreakerTripped && control.OverloadElapsedSeconds > ValidationEpsilon))
            {
                throw new InvalidDataException(
                    "Persisted power-network controls must be finite and uniquely identified.");
            }
        }
    }

    private static void Validate(ShipDockingStateData docking)
    {
        if (!double.IsFinite(docking.RelativePositionX) ||
            !double.IsFinite(docking.RelativePositionY) ||
            !double.IsFinite(docking.RelativeRotationRadians) ||
            !double.IsFinite(docking.LandingLegProgress) ||
            docking.LandingLegProgress < 0 || docking.LandingLegProgress > 1 ||
            !double.IsFinite(docking.GlobalPositionX) ||
            !double.IsFinite(docking.GlobalPositionY) ||
            !double.IsFinite(docking.GlobalRotationRadians) ||
            (docking.IsAttached && string.IsNullOrWhiteSpace(docking.CometId)) ||
            (!docking.IsAttached && docking.CometId is not null))
        {
            throw new InvalidDataException("The persisted ship docking state is invalid.");
        }
    }

    private readonly record struct PersistedEndpointInfo(
        string CometId,
        TransportMedium Medium,
        bool CanSend,
        bool CanReceive,
        int MaximumConnections,
        IReadOnlyCollection<ItemId>? AllowedItemIds);

    private static int CompareEndpoints(
        string firstMachineId,
        string firstPortId,
        string secondMachineId,
        string secondPortId)
    {
        var machineComparison = string.Compare(firstMachineId, secondMachineId, StringComparison.Ordinal);
        return machineComparison != 0
            ? machineComparison
            : string.Compare(firstPortId, secondPortId, StringComparison.Ordinal);
    }

    private static void ValidatePlayerInventory(
        IReadOnlyList<InventorySlotState> slots,
        int slotCount,
        string inventoryName)
    {
        var indices = new HashSet<int>();
        foreach (var slot in slots)
        {
            if (slot is null || slot.Index < 0 || slot.Index >= slotCount ||
                !indices.Add(slot.Index) || string.IsNullOrWhiteSpace(slot.ItemId) ||
                slot.Amount <= 0 || slot.Amount > InventoryConfiguration.MaximumStackSize)
            {
                throw new InvalidDataException(
                    $"The persisted {inventoryName} inventory contains an invalid slot.");
            }
        }
    }

    private static void Validate(ResearchStateSnapshot research)
    {
        if (research.CompletedResearch is null || !Enum.IsDefined(research.Status) ||
            !double.IsFinite(research.ProgressSeconds) || research.ProgressSeconds < 0 ||
            (research.ActiveResearchId is null && research.ProgressSeconds > 0))
        {
            throw new InvalidDataException("The persisted research state is invalid.");
        }

        var completedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var researchId in research.CompletedResearch)
        {
            if (string.IsNullOrWhiteSpace(researchId.Value) || !completedIds.Add(researchId.Value))
            {
                throw new InvalidDataException("Completed research IDs must be present and unique.");
            }
        }

        if (research.ActiveResearchId is { } active &&
            (string.IsNullOrWhiteSpace(active.Value) || completedIds.Contains(active.Value)))
        {
            throw new InvalidDataException("The active research ID is invalid.");
        }

        _ = ResearchState.Restore(research);
    }

    private static bool TryParseDefinedEnum<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum =>
        Enum.TryParse(text, ignoreCase: false, out value) && Enum.IsDefined(value);

    private sealed class FactoryStateDocument
    {
        public int? Version { get; set; }

        public List<MachineStateDto>? Machines { get; set; }

        public List<MachineConnectionDto>? Connections { get; set; }

        public ResearchStateDto? Research { get; set; }

        public bool? FirstBasicGeneratorBuilt { get; set; }

        public double? ShipFuel { get; set; }

        public List<InventorySlotDto>? AstronautInventory { get; set; }

        public List<InventorySlotDto>? ShipInventory { get; set; }

        public string? ActiveResearchStationId { get; set; }

        public List<PowerNetworkControlDto>? PowerNetworkControls { get; set; }

        public ShipPowerDto? ShipPower { get; set; }

        public ShipDockingDto? ShipDocking { get; set; }

        public List<CometSimulationTimestampDto>? LastSimulatedUtcByComet { get; set; }
    }

    private sealed class MachineStateDto
    {
        public string? InstanceId { get; set; }

        public string? DefinitionId { get; set; }

        public MachinePlacementDto? Placement { get; set; }

        public string? SelectedRecipeId { get; set; }

        public bool? IsEnabled { get; set; }

        public string? Status { get; set; }

        public double? ConstructionProgressSeconds { get; set; }

        public double? ProductionProgressSeconds { get; set; }

        public bool? IsBatchInProgress { get; set; }

        public double? GeneratorFuelSecondsRemaining { get; set; }

        public List<MachineInventorySlotDto>? InputSlots { get; set; }

        public List<MachineInventorySlotDto>? OutputSlots { get; set; }
    }

    private sealed class MachinePlacementDto
    {
        public string? CometId { get; set; }

        public double? RelativePositionX { get; set; }

        public double? RelativePositionY { get; set; }

        public double? RelativeRotationRadians { get; set; }
    }

    private sealed class MachineConnectionDto
    {
        public string? ConnectionId { get; set; }

        public string? SourceMachineId { get; set; }

        public string? SourcePortId { get; set; }

        public string? TargetMachineId { get; set; }

        public string? TargetPortId { get; set; }

        public string? Kind { get; set; }
    }

    private sealed class PowerNetworkControlDto
    {
        public string? NetworkId { get; set; }

        public bool? IsEnabled { get; set; }

        public bool? BreakerTripped { get; set; }

        public double? OverloadElapsedSeconds { get; set; }
    }

    private sealed class ShipPowerDto
    {
        public bool? ConnectorAEnabled { get; set; }

        public bool? ConnectorBEnabled { get; set; }
    }

    private sealed class ShipDockingDto
    {
        public bool? IsAttached { get; set; }

        public string? CometId { get; set; }

        public int? SectorX { get; set; }

        public int? SectorY { get; set; }

        public double? RelativePositionX { get; set; }

        public double? RelativePositionY { get; set; }

        public double? RelativeRotationRadians { get; set; }

        public double? LandingLegProgress { get; set; }

        public double? GlobalPositionX { get; set; }

        public double? GlobalPositionY { get; set; }

        public double? GlobalRotationRadians { get; set; }
    }

    private sealed class MachineInventorySlotDto
    {
        public int? Index { get; set; }

        public string? ItemId { get; set; }

        public int? Amount { get; set; }
    }

    private sealed class InventorySlotDto
    {
        public int? Index { get; set; }

        public string? ItemId { get; set; }

        public int? Amount { get; set; }
    }

    private sealed class ResearchStateDto
    {
        public List<string>? CompletedResearch { get; set; }

        public string? ActiveResearchId { get; set; }

        public double? ProgressSeconds { get; set; }

        public bool? IsEnabled { get; set; }

        public string? Status { get; set; }
    }

    private sealed class CometSimulationTimestampDto
    {
        public string? CometId { get; set; }

        public string? LastSimulatedUtc { get; set; }
    }
}

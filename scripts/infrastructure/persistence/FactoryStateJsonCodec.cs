using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
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
    private const int LegacyVersion = 1;

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
    /// Headless-test seam covering exact player-inventory slot roundtrips and the v1-to-v2
    /// migration. It throws when either invariant is broken.
    /// </summary>
    public static void RunSchemaV2SmokeTest()
    {
        var expected = FactoryStateData.CreateDefault() with
        {
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
        };
        var json = Serialize(expected);
        if (!TryDeserialize(json, out var restored, out var roundtripError) ||
            !expected.AstronautInventory.SequenceEqual(restored.AstronautInventory) ||
            !expected.ShipInventory.SequenceEqual(restored.ShipInventory) ||
            expected.ActiveResearchStationId != restored.ActiveResearchStationId)
        {
            throw new InvalidOperationException(
                $"Factory schema v2 roundtrip failed: {roundtripError}");
        }

        var restoredInventory = new SlotInventory(InventoryConfiguration.AstronautSlotCount);
        InventoryStatePersistence.Restore(restoredInventory, restored.AstronautInventory);
        if (restoredInventory.GetSlot(0).Amount != 200 ||
            restoredInventory.GetSlot(7).ItemId != ProductionItemIds.WaterContainer ||
            restoredInventory.GetSlot(7).Amount != 3)
        {
            throw new InvalidOperationException("Factory inventory slot restoration changed slot positions.");
        }

        var legacyDocument = JsonNode.Parse(json)?.AsObject()
                             ?? throw new InvalidOperationException("Could not create the v1 migration fixture.");
        legacyDocument["version"] = LegacyVersion;
        legacyDocument.Remove("astronautInventory");
        legacyDocument.Remove("shipInventory");
        legacyDocument.Remove("activeResearchStationId");
        if (!TryDeserialize(legacyDocument.ToJsonString(), out var migrated, out var migrationError) ||
            migrated.Version != FactoryStateData.CurrentVersion ||
            migrated.AstronautInventory.Count != 0 || migrated.ShipInventory.Count != 0 ||
            migrated.ActiveResearchStationId is not null)
        {
            throw new InvalidOperationException(
                $"Factory schema v1 migration failed: {migrationError}");
        }

        var corruptDocument = JsonNode.Parse(json)?.AsObject()
                              ?? throw new InvalidOperationException("Could not create the corruption fixture.");
        corruptDocument["astronautInventory"]![0]!["amount"] =
            InventoryConfiguration.MaximumStackSize + 1;
        if (TryDeserialize(corruptDocument.ToJsonString(), out _, out _))
        {
            throw new InvalidOperationException("An overfilled persisted inventory slot was accepted.");
        }
    }

    private static FactoryStateDocument ToDocument(FactoryStateData state) => new()
    {
        Version = state.Version,
        Machines = state.Machines.Select(ToDto).ToList(),
        Research = ToDto(state.Research),
        FirstBasicGeneratorBuilt = state.FirstBasicGeneratorBuilt,
        ShipFuel = state.ShipFuel,
        AstronautInventory = state.AstronautInventory.Select(ToDto).ToList(),
        ShipInventory = state.ShipInventory.Select(ToDto).ToList(),
        ActiveResearchStationId = state.ActiveResearchStationId,
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

    private static FactoryStateData FromDocument(FactoryStateDocument document)
    {
        if (document.Version is null ||
            document.Version is not (LegacyVersion or FactoryStateData.CurrentVersion))
        {
            throw new InvalidDataException(
                $"Factory save version {document.Version?.ToString() ?? "<missing>"} is unsupported; " +
                $"expected {LegacyVersion} or {FactoryStateData.CurrentVersion}.");
        }

        if (document.Machines is null || document.Research is null ||
            document.FirstBasicGeneratorBuilt is null || document.ShipFuel is null ||
            document.LastSimulatedUtcByComet is null)
        {
            throw new InvalidDataException("The factory save is missing required root fields.");
        }

        var isLegacy = document.Version == LegacyVersion;
        if (!isLegacy && (document.AstronautInventory is null || document.ShipInventory is null))
        {
            throw new InvalidDataException("The factory save is missing player inventory fields.");
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
            FromDto(document.Research),
            document.FirstBasicGeneratorBuilt.Value,
            document.ShipFuel.Value,
            timestamps,
            isLegacy ? [] : document.AstronautInventory!.Select(FromDto).ToArray(),
            isLegacy ? [] : document.ShipInventory!.Select(FromDto).ToArray(),
            isLegacy ? null : document.ActiveResearchStationId);
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

        if (state.Machines is null || state.Research is null || state.LastSimulatedUtcByComet is null ||
            state.AstronautInventory is null || state.ShipInventory is null ||
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

        public ResearchStateDto? Research { get; set; }

        public bool? FirstBasicGeneratorBuilt { get; set; }

        public double? ShipFuel { get; set; }

        public List<InventorySlotDto>? AstronautInventory { get; set; }

        public List<InventorySlotDto>? ShipInventory { get; set; }

        public string? ActiveResearchStationId { get; set; }

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

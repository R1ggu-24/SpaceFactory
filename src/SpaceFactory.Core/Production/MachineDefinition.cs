using SpaceFactory.Core.Items;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Production;

public enum MachineCategory
{
    Processing,
    Manufacturing,
    Energy,
    Storage,
    Research,
}

public enum MachineKind
{
    Production,
    Generator,
    Storage,
    Research,
    Infrastructure,
}

public enum MachineArchetype
{
    GeneralProcessing,
    Extractor,
    ChemicalProcessing,
    Assembly,
    PrecisionManufacturing,
    SolidStorage,
    LiquidStorage,
    GasStorage,
    FluidTransport,
    PowerGeneration,
    EnergyStorage,
    Research,
    Infrastructure,
    NuclearProcessing,
    WasteProcessing,
}

public enum MachinePlacementRequirement
{
    AnyBuildableComet,
    LargeOrHugeComet,
    ResourceDeposit,
}

public sealed class MachineDefinition
{
    public MachineDefinition(
        MachineDefinitionId id,
        string displayName,
        string description,
        MachineCategory category,
        MachineKind kind,
        IEnumerable<ItemAmount> buildCosts,
        int inputSlotCount,
        int outputSlotCount,
        double constructionDurationSeconds,
        double generatedPowerKilowatts = 0,
        ItemId? generatorFuelItemId = null,
        ItemId? generatorReturnedContainerItemId = null,
        double generatorFuelSecondsPerItem = 0,
        ResearchId? unlockRequirement = null,
        MachineArchetype archetype = MachineArchetype.GeneralProcessing,
        MachinePlacementRequirement placementRequirement = MachinePlacementRequirement.LargeOrHugeComet,
        TechnologyTier technologyTier = TechnologyTier.Tier1,
        double speedMultiplier = 1,
        double efficiencyMultiplier = 1,
        double radiationShielding = 0,
        bool isDirectBuildMenuEntry = true,
        ItemId? placementItemId = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        Kind = kind;
        BuildCosts = ProductionInventoryRules.Group(buildCosts);
        InputSlotCount = inputSlotCount;
        OutputSlotCount = outputSlotCount;
        ConstructionDurationSeconds = constructionDurationSeconds;
        GeneratedPowerKilowatts = generatedPowerKilowatts;
        GeneratorFuelItemId = generatorFuelItemId;
        GeneratorReturnedContainerItemId = generatorReturnedContainerItemId;
        GeneratorFuelSecondsPerItem = generatorFuelSecondsPerItem;
        UnlockRequirement = unlockRequirement;
        Archetype = archetype;
        PlacementRequirement = placementRequirement;
        TechnologyTier = technologyTier;
        SpeedMultiplier = speedMultiplier;
        EfficiencyMultiplier = efficiencyMultiplier;
        RadiationShielding = radiationShielding;
        IsDirectBuildMenuEntry = isDirectBuildMenuEntry;
        PlacementItemId = placementItemId;
        Validate();
    }

    public MachineDefinitionId Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public MachineCategory Category { get; }

    public MachineKind Kind { get; }

    public IReadOnlyList<ItemAmount> BuildCosts { get; }

    public int InputSlotCount { get; }

    public int OutputSlotCount { get; }

    public double ConstructionDurationSeconds { get; }

    public double GeneratedPowerKilowatts { get; }

    public ItemId? GeneratorFuelItemId { get; }

    public ItemId? GeneratorReturnedContainerItemId { get; }

    public double GeneratorFuelSecondsPerItem { get; }

    public ResearchId? UnlockRequirement { get; }

    public MachineArchetype Archetype { get; }

    public MachinePlacementRequirement PlacementRequirement { get; }

    public TechnologyTier TechnologyTier { get; }

    public double SpeedMultiplier { get; }

    public double EfficiencyMultiplier { get; }

    public double RadiationShielding { get; }

    /// <summary>
    /// Whether the definition is offered as a directly paid entry in the build menu. Machines
    /// represented by a physical inventory item keep their definition in the same catalog but
    /// opt out here, so UI and placement code do not need independent exclusion lists.
    /// </summary>
    public bool IsDirectBuildMenuEntry { get; }

    /// <summary>
    /// Optional physical item consumed to place this machine and returned when it is dismantled.
    /// </summary>
    public ItemId? PlacementItemId { get; }

    public bool IsFuelledGenerator => GeneratorFuelItemId is not null;

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description) ||
            InputSlotCount <= 0 || OutputSlotCount <= 0 || ConstructionDurationSeconds <= 0 ||
            !Enum.IsDefined(Archetype) || !Enum.IsDefined(PlacementRequirement) ||
            !Enum.IsDefined(TechnologyTier) ||
            !double.IsFinite(SpeedMultiplier) || SpeedMultiplier <= 0 ||
            !double.IsFinite(EfficiencyMultiplier) || EfficiencyMultiplier <= 0 ||
            !double.IsFinite(RadiationShielding) || RadiationShielding < 0 || RadiationShielding > 1)
        {
            throw new ArgumentException($"Machine definition '{Id}' is invalid.");
        }

        if (!IsDirectBuildMenuEntry && PlacementItemId is null)
        {
            throw new ArgumentException(
                $"Non-menu machine '{Id}' needs a physical placement item.");
        }

        if (Kind == MachineKind.Generator)
        {
            if (Category != MachineCategory.Energy || GeneratedPowerKilowatts <= 0)
            {
                throw new ArgumentException($"Generator '{Id}' needs a positive power output.");
            }

            if (GeneratorFuelItemId is not null &&
                (GeneratorReturnedContainerItemId is null || GeneratorFuelSecondsPerItem <= 0))
            {
                throw new ArgumentException($"Fuelled generator '{Id}' needs fuel duration and a returned container.");
            }
        }
        else if (GeneratedPowerKilowatts != 0 || GeneratorFuelItemId is not null ||
                 GeneratorReturnedContainerItemId is not null || GeneratorFuelSecondsPerItem != 0)
        {
            throw new ArgumentException($"Non-generator '{Id}' cannot define generator values.");
        }
    }
}

public static class MachineDefinitionIds
{
    public static readonly MachineDefinitionId Crusher = new("crusher");
    public static readonly MachineDefinitionId Smelter = new("smelter");
    public static readonly MachineDefinitionId Foundry = new("foundry");
    public static readonly MachineDefinitionId Refinery = new("refinery");
    public static readonly MachineDefinitionId WaterProcessor = new("water_processor");
    public static readonly MachineDefinitionId Electrolyzer = new("electrolyzer");
    public static readonly MachineDefinitionId Constructor = new("constructor");
    public static readonly MachineDefinitionId Fabricator = new("fabricator");
    public static readonly MachineDefinitionId BasicGenerator = new("basic_generator");
    public static readonly MachineDefinitionId FuelGenerator = new("fuel_generator");
    public static readonly MachineDefinitionId StorageContainer = new("storage_container");
    public static readonly MachineDefinitionId ResearchStation = new("research_station");
    public static readonly MachineDefinitionId PowerPole = new("power_pole");
    public static readonly MachineDefinitionId Workbench = new("workbench");
    public static readonly MachineDefinitionId MobileMiner = new("mobile_miner");
    public static readonly MachineDefinitionId AutomaticMiner = new("automatic_miner");
    public static readonly MachineDefinitionId ChemicalPlant = new("chemical_plant");
    public static readonly MachineDefinitionId Assembler = new("assembler");
    public static readonly MachineDefinitionId AdvancedFabricator = new("advanced_fabricator");
    public static readonly MachineDefinitionId PrecisionManufacturer = new("precision_manufacturer");
    public static readonly MachineDefinitionId LiquidTank = new("liquid_tank");
    public static readonly MachineDefinitionId GasTank = new("gas_tank");
    public static readonly MachineDefinitionId PumpStation = new("pump_station");
    public static readonly MachineDefinitionId BatteryBank = new("battery_bank");
    public static readonly MachineDefinitionId UraniumProcessor = new("uranium_processor");
    public static readonly MachineDefinitionId FuelCellFabricator = new("fuel_cell_fabricator");
    public static readonly MachineDefinitionId NuclearReactor = new("nuclear_reactor");
    public static readonly MachineDefinitionId WasteProcessor = new("waste_processor");
    public static readonly MachineDefinitionId NuclearWasteStorage = new("nuclear_waste_storage");
}

public static class ProductionConfiguration
{
    public const int StandardInputSlotCount = 4;
    public const int StandardOutputSlotCount = 4;
    public const int FabricatorInputSlotCount = 6;
    public const int StorageContainerSlotCount = 30;
    public const double BasicGeneratorPowerKilowatts = 12;
    public const double FuelGeneratorPowerKilowatts = 60;
    public const double FuelGeneratorSecondsPerContainer = 120;
    public const int FuelGeneratorTankContainerCapacity = 6;
    public const int FuelGeneratorTankTransferSlotIndex = 0;
    public const int MobileMinerOutputSlotCount = 6;
    public const int AutomaticMinerOutputSlotCount = 8;
    public const int FluidTankSlotCount = 40;
    public const int NuclearWasteStorageSlotCount = 50;
    public const double NuclearReactorPowerKilowatts = 1200;
    public const double NuclearFuelCellSeconds = 300;
}

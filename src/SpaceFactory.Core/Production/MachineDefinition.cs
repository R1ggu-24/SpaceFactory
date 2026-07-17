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
        ResearchId? unlockRequirement = null)
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

    public bool IsFuelledGenerator => GeneratorFuelItemId is not null;

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description) ||
            InputSlotCount <= 0 || OutputSlotCount <= 0 || ConstructionDurationSeconds <= 0)
        {
            throw new ArgumentException($"Machine definition '{Id}' is invalid.");
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
}

using Godot;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Presentation-only category keys. Gameplay definitions can map their own
/// machine category to this small, stable UI vocabulary.
/// </summary>
public enum BuildMenuCategory
{
    Processing,
    Manufacturing,
    Energy,
    Logistics,
    Storage,
    Research,
}

public enum MachineGlyph
{
    Crusher,
    Smelter,
    Foundry,
    Constructor,
    Fabricator,
    WaterProcessor,
    Electrolyzer,
    Refinery,
    BasicGenerator,
    FuelGenerator,
    PowerPole,
    PowerCable,
    ConveyorBelt,
    LiquidPipe,
    GasPipe,
    Storage,
    Research,
}

public enum MachineUiStatus
{
    UnderConstruction,
    SwitchedOff,
    Ready,
    Producing,
    WaitingForMaterials,
    WaitingForEnergy,
    OutputFull,
    Blocked,
}

public sealed record BuildCostViewModel(
    string DisplayName,
    int RequiredAmount,
    int AvailableAmount,
    Color Accent)
{
    public int MissingAmount => Math.Max(0, RequiredAmount - AvailableAmount);

    public bool IsAvailable => MissingAmount == 0;
}

public sealed record BuildMachineViewModel(
    string MachineId,
    string DisplayName,
    string Description,
    string FunctionSummary,
    BuildMenuCategory Category,
    MachineGlyph Glyph,
    IReadOnlyList<BuildCostViewModel> Costs,
    bool IsUnlocked,
    string UnlockMessage = "");

public sealed record MachineMaterialViewModel(
    string DisplayName,
    int RequiredAmount,
    int AvailableAmount,
    Color Accent)
{
    public bool IsAvailable => AvailableAmount >= RequiredAmount;
}

public sealed record MachineOutputViewModel(
    string DisplayName,
    int ProducedAmount,
    int StoredAmount,
    int Capacity,
    Color Accent)
{
    public bool IsFull => Capacity > 0 && StoredAmount >= Capacity;
}

public sealed record MachineRecipeViewModel(
    string RecipeId,
    string DisplayName,
    IReadOnlyList<MachineMaterialViewModel> Inputs,
    IReadOnlyList<MachineOutputViewModel> Outputs,
    float DurationSeconds,
    bool IsUnlocked = true,
    string UnlockMessage = "");

/// <summary>
/// Immutable snapshot consumed by <see cref="MachinePanelController"/>.
/// Updating the panel with a new snapshot does not mutate gameplay state.
/// User requests are exposed through events instead.
/// </summary>
public sealed record MachinePanelViewModel(
    string MachineId,
    string DisplayName,
    MachineGlyph Glyph,
    IReadOnlyList<MachineRecipeViewModel> Recipes,
    string? SelectedRecipeId,
    MachineUiStatus Status,
    string StatusDetail,
    bool IsActive,
    float ProductionProgress,
    float RequiredPower,
    float AvailablePower);

public static class BuildMenuCategoryPresentation
{
    public static readonly IReadOnlyList<BuildMenuCategory> OrderedCategories =
    [
        BuildMenuCategory.Processing,
        BuildMenuCategory.Manufacturing,
        BuildMenuCategory.Energy,
        BuildMenuCategory.Logistics,
        BuildMenuCategory.Storage,
        BuildMenuCategory.Research,
    ];

    public static string GetDisplayName(BuildMenuCategory category) => category switch
    {
        BuildMenuCategory.Processing => "VERARBEITUNG",
        BuildMenuCategory.Manufacturing => "HERSTELLUNG",
        BuildMenuCategory.Energy => "ENERGIE",
        BuildMenuCategory.Logistics => "TRANSPORT",
        BuildMenuCategory.Storage => "LAGERUNG",
        BuildMenuCategory.Research => "FORSCHUNG",
        _ => category.ToString().ToUpperInvariant(),
    };
}

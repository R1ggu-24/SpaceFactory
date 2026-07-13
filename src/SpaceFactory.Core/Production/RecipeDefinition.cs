using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Production;

public sealed class RecipeDefinition
{
    public RecipeDefinition(
        RecipeId id,
        string displayName,
        MachineDefinitionId machineId,
        string category,
        IEnumerable<ItemAmount> inputs,
        IEnumerable<ItemAmount> outputs,
        double durationSeconds,
        double requiredPowerKilowatts,
        IEnumerable<ItemAmount>? returnedContainers = null,
        ResearchId? unlockRequirement = null)
    {
        Id = id;
        DisplayName = displayName;
        MachineId = machineId;
        Category = category;
        Inputs = ProductionInventoryRules.Group(inputs);
        Outputs = ProductionInventoryRules.Group(outputs);
        ReturnedContainers = ProductionInventoryRules.Group(returnedContainers ?? []);
        DurationSeconds = durationSeconds;
        RequiredPowerKilowatts = requiredPowerKilowatts;
        UnlockRequirement = unlockRequirement;
        CombinedOutputs = ProductionInventoryRules.Group(Outputs.Concat(ReturnedContainers));
        Validate();
    }

    public RecipeId Id { get; }

    public string DisplayName { get; }

    public MachineDefinitionId MachineId { get; }

    public string Category { get; }

    public IReadOnlyList<ItemAmount> Inputs { get; }

    public IReadOnlyList<ItemAmount> Outputs { get; }

    public IReadOnlyList<ItemAmount> ReturnedContainers { get; }

    public IReadOnlyList<ItemAmount> CombinedOutputs { get; }

    public double DurationSeconds { get; }

    public double RequiredPowerKilowatts { get; }

    public ResearchId? UnlockRequirement { get; }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Category) ||
            Inputs.Count == 0 || Outputs.Count == 0 || DurationSeconds <= 0 || RequiredPowerKilowatts <= 0)
        {
            throw new ArgumentException($"Recipe definition '{Id}' is invalid.");
        }
    }
}

public sealed class RecipeCatalog
{
    private readonly IReadOnlyDictionary<RecipeId, RecipeDefinition> _definitions;

    public RecipeCatalog(IEnumerable<RecipeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        _definitions = materialized.ToDictionary(definition => definition.Id);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Recipe IDs must be unique.", nameof(definitions));
        }
    }

    public IReadOnlyCollection<RecipeDefinition> All => _definitions.Values.ToArray();

    public RecipeDefinition Get(RecipeId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown recipe '{id}'.");

    public bool TryGet(RecipeId id, out RecipeDefinition? definition) => _definitions.TryGetValue(id, out definition);

    public IReadOnlyList<RecipeDefinition> ForMachine(MachineDefinitionId machineId) => _definitions.Values
        .Where(definition => definition.MachineId == machineId)
        .OrderBy(definition => definition.DisplayName, StringComparer.Ordinal)
        .ToArray();
}

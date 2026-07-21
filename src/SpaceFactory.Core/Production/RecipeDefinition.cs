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
        ResearchId? unlockRequirement = null,
        TechnologyTier technologyTier = TechnologyTier.Tier1,
        string? alternativeGroup = null,
        IEnumerable<string>? tags = null,
        SpaceFactory.Core.Items.ItemId? sourceResourceId = null)
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
        TechnologyTier = technologyTier;
        AlternativeGroup = string.IsNullOrWhiteSpace(alternativeGroup) ? null : alternativeGroup.Trim();
        Tags = (tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        SourceResourceId = sourceResourceId;
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

    public TechnologyTier TechnologyTier { get; }

    public string? AlternativeGroup { get; }

    public IReadOnlyList<string> Tags { get; }

    public SpaceFactory.Core.Items.ItemId? SourceResourceId { get; }

    public bool IsExtractionRecipe => SourceResourceId is not null;

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Category) ||
            Outputs.Count == 0 || DurationSeconds <= 0 || RequiredPowerKilowatts < 0 ||
            !Enum.IsDefined(TechnologyTier) ||
            (SourceResourceId is null && Inputs.Count == 0) ||
            (SourceResourceId is not null && Inputs.Count != 0) ||
            (RequiredPowerKilowatts == 0 && SourceResourceId is null))
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

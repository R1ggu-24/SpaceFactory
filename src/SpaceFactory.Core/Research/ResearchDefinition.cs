using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Research;

public sealed class ResearchDefinition
{
    public ResearchDefinition(
        ResearchId id,
        string displayName,
        string description,
        IEnumerable<ItemAmount> materialCosts,
        double durationSeconds,
        double requiredPowerKilowatts,
        IEnumerable<ResearchId>? prerequisites = null,
        IEnumerable<MachineDefinitionId>? unlockedMachines = null,
        IEnumerable<RecipeId>? unlockedRecipes = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        MaterialCosts = ProductionInventoryRules.Group(materialCosts);
        DurationSeconds = durationSeconds;
        RequiredPowerKilowatts = requiredPowerKilowatts;
        Prerequisites = (prerequisites ?? []).Distinct().ToArray();
        UnlockedMachines = (unlockedMachines ?? []).Distinct().ToArray();
        UnlockedRecipes = (unlockedRecipes ?? []).Distinct().ToArray();
        Validate();
    }

    public ResearchId Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public IReadOnlyList<ItemAmount> MaterialCosts { get; }

    public double DurationSeconds { get; }

    public double RequiredPowerKilowatts { get; }

    public IReadOnlyList<ResearchId> Prerequisites { get; }

    public IReadOnlyList<MachineDefinitionId> UnlockedMachines { get; }

    public IReadOnlyList<RecipeId> UnlockedRecipes { get; }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description) ||
            MaterialCosts.Count == 0 || DurationSeconds <= 0 || RequiredPowerKilowatts <= 0 ||
            Prerequisites.Contains(Id))
        {
            throw new ArgumentException($"Research definition '{Id}' is invalid.");
        }
    }
}

public sealed class ResearchCatalog
{
    private readonly IReadOnlyDictionary<ResearchId, ResearchDefinition> _definitions;

    public ResearchCatalog(IEnumerable<ResearchDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        _definitions = materialized.ToDictionary(definition => definition.Id);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Research IDs must be unique.", nameof(definitions));
        }

        foreach (var prerequisite in materialized.SelectMany(definition => definition.Prerequisites))
        {
            if (!_definitions.ContainsKey(prerequisite))
            {
                throw new ArgumentException($"Unknown research prerequisite '{prerequisite}'.", nameof(definitions));
            }
        }
    }

    public IReadOnlyCollection<ResearchDefinition> All => _definitions.Values.ToArray();

    public ResearchDefinition Get(ResearchId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown research '{id}'.");
}

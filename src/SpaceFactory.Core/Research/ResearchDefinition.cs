using SpaceFactory.Core.Production;
using SpaceFactory.Core.Items;

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
        IEnumerable<RecipeId>? unlockedRecipes = null,
        ResearchCategory category = ResearchCategory.Fundamentals,
        TechnologyTier tier = TechnologyTier.Tier1,
        IEnumerable<ItemId>? requiredDiscoveries = null,
        IEnumerable<AlternativeRecipeGroupId>? unlockedAlternativeRecipeGroups = null,
        IEnumerable<string>? tags = null)
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
        Category = category;
        Tier = tier;
        RequiredDiscoveries = (requiredDiscoveries ?? []).Distinct().ToArray();
        UnlockedAlternativeRecipeGroups = (unlockedAlternativeRecipeGroups ?? []).Distinct().ToArray();
        Tags = (tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    public ResearchCategory Category { get; }

    public TechnologyTier Tier { get; }

    public IReadOnlyList<ItemId> RequiredDiscoveries { get; }

    public IReadOnlyList<AlternativeRecipeGroupId> UnlockedAlternativeRecipeGroups { get; }

    public IReadOnlyList<string> Tags { get; }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description) ||
            MaterialCosts.Count == 0 || DurationSeconds <= 0 || RequiredPowerKilowatts <= 0 ||
            Prerequisites.Contains(Id) || !Enum.IsDefined(Category) || !Enum.IsDefined(Tier) ||
            RequiredDiscoveries.Any(itemId => string.IsNullOrWhiteSpace(itemId.Value)) ||
            Tags.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Research definition '{Id}' is invalid.");
        }
    }
}

public sealed class ResearchCatalog
{
    private readonly IReadOnlyDictionary<ResearchId, ResearchDefinition> _definitions;
    private readonly IReadOnlyList<ResearchDefinition> _topologicalOrder;

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

        _topologicalOrder = CreateTopologicalOrder(_definitions, nameof(definitions));
    }

    public IReadOnlyCollection<ResearchDefinition> All => _topologicalOrder;

    public IReadOnlyList<ResearchDefinition> TopologicalOrder => _topologicalOrder;

    public ResearchDefinition Get(ResearchId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown research '{id}'.");

    private static IReadOnlyList<ResearchDefinition> CreateTopologicalOrder(
        IReadOnlyDictionary<ResearchId, ResearchDefinition> definitions,
        string parameterName)
    {
        var visitStates = new Dictionary<ResearchId, VisitState>();
        var path = new List<ResearchId>();
        var result = new List<ResearchDefinition>(definitions.Count);

        foreach (var id in definitions.Keys.OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            Visit(id);
        }

        return result;

        void Visit(ResearchId id)
        {
            if (visitStates.TryGetValue(id, out var state))
            {
                if (state == VisitState.Visited)
                {
                    return;
                }

                if (state == VisitState.Visiting)
                {
                    var cycleStart = path.IndexOf(id);
                    var cycle = path.Skip(Math.Max(0, cycleStart)).Append(id);
                    throw new ArgumentException(
                        $"Research prerequisite graph contains a cycle: {string.Join(" -> ", cycle)}.",
                        parameterName);
                }
            }

            visitStates[id] = VisitState.Visiting;
            path.Add(id);
            foreach (var prerequisite in definitions[id].Prerequisites
                         .OrderBy(prerequisite => prerequisite.Value, StringComparer.Ordinal))
            {
                Visit(prerequisite);
            }

            path.RemoveAt(path.Count - 1);
            visitStates[id] = VisitState.Visited;
            result.Add(definitions[id]);
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }
}

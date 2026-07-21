using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Research;

public enum ResearchStatus
{
    Idle,
    Disabled,
    WaitingForEnergy,
    Researching,
    Completed,
}

public enum ResearchStartFailure
{
    None,
    AlreadyActive,
    AlreadyCompleted,
    MissingPrerequisite,
    MissingDiscovery,
    MissingMaterials,
}

public readonly record struct ResearchStartResult(bool Succeeded, ResearchStartFailure Failure)
{
    public static ResearchStartResult Success() => new(true, ResearchStartFailure.None);

    public static ResearchStartResult Failed(ResearchStartFailure failure) => new(false, failure);
}

public readonly record struct ResearchTickResult(
    bool Completed,
    double EnergyConsumedKilowattSeconds,
    ResearchStatus Status);

public sealed record ResearchStateSnapshot(
    IReadOnlyList<ResearchId> CompletedResearch,
    ResearchId? ActiveResearchId,
    double ProgressSeconds,
    bool IsEnabled,
    ResearchStatus Status,
    IReadOnlyList<ItemId>? DiscoveredResources = null);

public sealed class ResearchState
{
    private const double Epsilon = 0.000_001;
    private readonly HashSet<ResearchId> _completedResearch = [];
    private readonly HashSet<ItemId> _discoveredResources = [];

    public ResearchState(
        IEnumerable<ResearchId>? completedResearch = null,
        IEnumerable<ItemId>? discoveredResources = null)
    {
        if (completedResearch is not null)
        {
            _completedResearch.UnionWith(completedResearch);
        }

        if (discoveredResources is not null)
        {
            _discoveredResources.UnionWith(discoveredResources);
        }
    }

    public IReadOnlySet<ResearchId> CompletedResearch => _completedResearch;

    public IReadOnlySet<ItemId> DiscoveredResources => _discoveredResources;

    public ResearchId? ActiveResearchId { get; private set; }

    public double ProgressSeconds { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public ResearchStatus Status { get; private set; } = ResearchStatus.Idle;

    public bool IsCompleted(ResearchId id) => _completedResearch.Contains(id);

    public bool DiscoverResource(ItemId itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId.Value))
        {
            throw new ArgumentException("A discovered resource ID cannot be empty.", nameof(itemId));
        }

        return _discoveredResources.Add(itemId);
    }

    public ResearchStateSnapshot CreateSnapshot() => new(
        _completedResearch.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
        ActiveResearchId,
        ProgressSeconds,
        IsEnabled,
        Status,
        _discoveredResources.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());

    public static ResearchState Restore(ResearchStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var discoveries = snapshot.DiscoveredResources ?? [];
        if (snapshot.CompletedResearch is null ||
            !double.IsFinite(snapshot.ProgressSeconds) || snapshot.ProgressSeconds < 0 ||
            discoveries.Any(itemId => string.IsNullOrWhiteSpace(itemId.Value)) ||
            discoveries.Distinct().Count() != discoveries.Count ||
            (snapshot.ActiveResearchId is null && snapshot.ProgressSeconds > Epsilon))
        {
            throw new ArgumentException("The persisted research state is invalid.", nameof(snapshot));
        }

        return new ResearchState(snapshot.CompletedResearch, discoveries)
        {
            ActiveResearchId = snapshot.ActiveResearchId,
            ProgressSeconds = snapshot.ProgressSeconds,
            IsEnabled = snapshot.IsEnabled,
            Status = snapshot.Status,
        };
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (ActiveResearchId is not null)
        {
            Status = enabled ? ResearchStatus.WaitingForEnergy : ResearchStatus.Disabled;
        }
    }

    public ResearchStartResult TryStart(ResearchDefinition definition, SlotInventory materialInventory)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(materialInventory);
        if (ActiveResearchId is not null)
        {
            return ResearchStartResult.Failed(ResearchStartFailure.AlreadyActive);
        }

        if (_completedResearch.Contains(definition.Id))
        {
            return ResearchStartResult.Failed(ResearchStartFailure.AlreadyCompleted);
        }

        if (definition.Prerequisites.Any(prerequisite => !_completedResearch.Contains(prerequisite)))
        {
            return ResearchStartResult.Failed(ResearchStartFailure.MissingPrerequisite);
        }

        if (definition.RequiredDiscoveries.Any(discovery => !_discoveredResources.Contains(discovery)))
        {
            return ResearchStartResult.Failed(ResearchStartFailure.MissingDiscovery);
        }

        if (!ProductionInventoryRules.TryRemoveAll(materialInventory, definition.MaterialCosts))
        {
            return ResearchStartResult.Failed(ResearchStartFailure.MissingMaterials);
        }

        ActiveResearchId = definition.Id;
        ProgressSeconds = 0;
        Status = IsEnabled ? ResearchStatus.WaitingForEnergy : ResearchStatus.Disabled;
        return ResearchStartResult.Success();
    }

    public ResearchTickResult Tick(
        ResearchDefinition definition,
        double deltaSeconds,
        double allocatedPowerKilowatts)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (ActiveResearchId != definition.Id)
        {
            throw new InvalidOperationException($"Research '{definition.Id}' is not active.");
        }

        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 ||
            !double.IsFinite(allocatedPowerKilowatts) || allocatedPowerKilowatts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (!IsEnabled)
        {
            Status = ResearchStatus.Disabled;
            return new ResearchTickResult(false, 0, Status);
        }

        if (allocatedPowerKilowatts + Epsilon < definition.RequiredPowerKilowatts)
        {
            Status = ResearchStatus.WaitingForEnergy;
            return new ResearchTickResult(false, 0, Status);
        }

        var advancedSeconds = Math.Min(deltaSeconds, definition.DurationSeconds - ProgressSeconds);
        ProgressSeconds += advancedSeconds;
        var energy = advancedSeconds * definition.RequiredPowerKilowatts;
        if (ProgressSeconds + Epsilon < definition.DurationSeconds)
        {
            Status = ResearchStatus.Researching;
            return new ResearchTickResult(false, energy, Status);
        }

        _completedResearch.Add(definition.Id);
        ActiveResearchId = null;
        ProgressSeconds = 0;
        Status = ResearchStatus.Completed;
        return new ResearchTickResult(true, energy, Status);
    }
}

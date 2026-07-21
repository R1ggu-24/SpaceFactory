using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Construction;

public enum DismantlingFailure
{
    None,
    WrongTool,
    InventoryFull,
    RemovalRejected,
}

public static class DismantlingConfiguration
{
    public const double InteractionRangeWorldUnits = 210;
    public const double ConnectionSelectionRadiusWorldUnits = 22;
    public const double MachineDismantlingDurationSeconds = 1.8;
    public const double ConnectionDismantlingDurationSeconds = 0.9;
}

/// <summary>
/// Small deterministic hold-progress state used by every dismantlable world object.
/// Presentation owns target selection while this state owns the timing and cancellation
/// semantics, so a released button or changed target can never commit a partial removal.
/// </summary>
public sealed class DismantlingProgressState
{
    private string? _targetId;
    private double _durationSeconds;
    private double _elapsedSeconds;

    public string? TargetId => _targetId;

    public bool IsActive => _targetId is not null;

    public double Progress => !IsActive || _durationSeconds <= 0
        ? 0
        : Math.Clamp(_elapsedSeconds / _durationSeconds, 0, 1);

    public void Begin(string targetId, double durationSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (string.Equals(_targetId, targetId, StringComparison.Ordinal) &&
            Math.Abs(_durationSeconds - durationSeconds) <= double.Epsilon)
        {
            return;
        }

        _targetId = targetId;
        _durationSeconds = durationSeconds;
        _elapsedSeconds = 0;
    }

    public bool Advance(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (!IsActive)
        {
            return false;
        }

        _elapsedSeconds = Math.Min(_durationSeconds, _elapsedSeconds + deltaSeconds);
        return _elapsedSeconds >= _durationSeconds;
    }

    public void Cancel()
    {
        _targetId = null;
        _durationSeconds = 0;
        _elapsedSeconds = 0;
    }
}

public readonly record struct DismantlingResult(
    DismantlingFailure Failure,
    IReadOnlyList<ItemAmount> RecoveredItems)
{
    public bool Succeeded => Failure == DismantlingFailure.None;

    public static DismantlingResult Success(IReadOnlyList<ItemAmount> recoveredItems) =>
        new(DismantlingFailure.None, recoveredItems);

    public static DismantlingResult Failed(DismantlingFailure failure) =>
        new(failure, []);
}

/// <summary>
/// Single source of truth for machine/connection dismantling. The removal callback is executed
/// only after inventory capacity was validated; recovered items are inserted only after removal
/// succeeded. This prevents both item loss and duplication in presentation-layer removal flows.
/// </summary>
public static class DismantlingRules
{
    public static bool IsDismantlingTool(ItemId itemId) =>
        itemId == ProductionItemIds.MachineDismantlingTool;

    public static IReadOnlyList<ItemAmount> GetMachineRecovery(
        MachineDefinition definition,
        bool constructionCostsPaid = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!constructionCostsPaid)
        {
            return [];
        }

        return definition.PlacementItemId is { } placementItemId
            ? [new ItemAmount(placementItemId, 1)]
            : definition.BuildCosts.ToArray();
    }

    public static IReadOnlyList<ItemAmount> GetConnectionRecovery(
        ConnectionKind connectionKind,
        ConnectionTypeCatalog? connectionTypes = null)
    {
        var definition = (connectionTypes ?? DefaultConnectionTypeCatalog.Instance).Get(connectionKind);
        return [new ItemAmount(definition.RequiredBuildItemId, 1)];
    }

    public static DismantlingResult TryDismantleMachine(
        ItemId activeToolItemId,
        SlotInventory targetInventory,
        MachineDefinition definition,
        Func<bool> removeObject,
        IEnumerable<ItemAmount>? additionalRecovery = null,
        bool constructionCostsPaid = true) =>
        TryDismantle(
            activeToolItemId,
            targetInventory,
            GetMachineRecovery(definition, constructionCostsPaid).Concat(additionalRecovery ?? []),
            removeObject);

    public static DismantlingResult TryDismantleConnection(
        ItemId activeToolItemId,
        SlotInventory targetInventory,
        ConnectionKind connectionKind,
        Func<bool> removeObject,
        ConnectionTypeCatalog? connectionTypes = null) =>
        TryDismantle(
            activeToolItemId,
            targetInventory,
            GetConnectionRecovery(connectionKind, connectionTypes),
            removeObject);

    /// <summary>
    /// Atomically applies an arbitrary, precomputed recovery bundle. Callers can combine machine
    /// construction costs, input/output contents and all connected cable/belt/pipe items. The
    /// callback must remove the world objects and connections together and return false if it did
    /// not commit the removal.
    /// </summary>
    public static DismantlingResult TryDismantle(
        ItemId activeToolItemId,
        SlotInventory targetInventory,
        IEnumerable<ItemAmount> recovery,
        Func<bool> removeObject)
    {
        ArgumentNullException.ThrowIfNull(targetInventory);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(removeObject);

        var groupedRecovery = ProductionInventoryRules.Group(recovery);

        if (!IsDismantlingTool(activeToolItemId))
        {
            return DismantlingResult.Failed(DismantlingFailure.WrongTool);
        }

        if (!ProductionInventoryRules.CanStoreAll(targetInventory, groupedRecovery))
        {
            return DismantlingResult.Failed(DismantlingFailure.InventoryFull);
        }

        if (!removeObject())
        {
            return DismantlingResult.Failed(DismantlingFailure.RemovalRejected);
        }

        if (!ProductionInventoryRules.TryAddAll(targetInventory, groupedRecovery))
        {
            throw new InvalidOperationException(
                "A prevalidated dismantling recovery could not be stored.");
        }

        return DismantlingResult.Success(groupedRecovery);
    }
}

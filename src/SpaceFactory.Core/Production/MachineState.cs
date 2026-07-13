using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public enum MachineOperationStatus
{
    UnderConstruction,
    Disabled,
    NoRecipeSelected,
    Ready,
    Producing,
    WaitingForMaterial,
    WaitingForEnergy,
    OutputFull,
}

public sealed record MachinePlacement(
    string CometId,
    double RelativePositionX,
    double RelativePositionY,
    double RelativeRotationRadians)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CometId) || !double.IsFinite(RelativePositionX) ||
            !double.IsFinite(RelativePositionY) || !double.IsFinite(RelativeRotationRadians))
        {
            throw new ArgumentException("The machine placement is invalid.");
        }
    }
}

public readonly record struct MachineTickResult(
    int CompletedCycles,
    double EnergyConsumedKilowattSeconds,
    MachineOperationStatus Status);

public sealed record MachineInventorySlotSnapshot(int Index, ItemId ItemId, int Amount);

public sealed record MachineStateSnapshot(
    MachineInstanceId InstanceId,
    MachineDefinitionId DefinitionId,
    MachinePlacement? Placement,
    RecipeId? SelectedRecipeId,
    bool IsEnabled,
    MachineOperationStatus Status,
    double ConstructionProgressSeconds,
    double ProductionProgressSeconds,
    bool IsBatchInProgress,
    double GeneratorFuelSecondsRemaining,
    IReadOnlyList<MachineInventorySlotSnapshot> InputSlots,
    IReadOnlyList<MachineInventorySlotSnapshot> OutputSlots);

public sealed class MachineState
{
    private const double Epsilon = 0.000_001;
    private bool _batchInProgress;

    public MachineState(
        MachineInstanceId instanceId,
        MachineDefinition definition,
        MachinePlacement? placement = null,
        bool constructionCompleted = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        placement?.Validate();
        InstanceId = instanceId;
        Definition = definition;
        Placement = placement;
        InputInventory = new SlotInventory(definition.InputSlotCount);
        OutputInventory = new SlotInventory(definition.OutputSlotCount);
        ConstructionProgressSeconds = constructionCompleted ? definition.ConstructionDurationSeconds : 0;
        Status = constructionCompleted ? MachineOperationStatus.Disabled : MachineOperationStatus.UnderConstruction;
    }

    public MachineInstanceId InstanceId { get; }

    public MachineDefinition Definition { get; }

    public MachinePlacement? Placement { get; }

    public SlotInventory InputInventory { get; }

    public SlotInventory OutputInventory { get; }

    public RecipeId? SelectedRecipeId { get; private set; }

    public bool IsEnabled { get; private set; }

    public MachineOperationStatus Status { get; private set; }

    public double ConstructionProgressSeconds { get; private set; }

    public double ProductionProgressSeconds { get; private set; }

    public double GeneratorFuelSecondsRemaining { get; private set; }

    public bool IsBatchInProgress => _batchInProgress;

    public bool IsConstructionComplete =>
        ConstructionProgressSeconds + Epsilon >= Definition.ConstructionDurationSeconds;

    public double ConstructionProgress => Math.Clamp(
        ConstructionProgressSeconds / Definition.ConstructionDurationSeconds,
        0,
        1);

    public MachineStateSnapshot CreateSnapshot() => new(
        InstanceId,
        Definition.Id,
        Placement,
        SelectedRecipeId,
        IsEnabled,
        Status,
        ConstructionProgressSeconds,
        ProductionProgressSeconds,
        _batchInProgress,
        GeneratorFuelSecondsRemaining,
        SnapshotInventory(InputInventory),
        SnapshotInventory(OutputInventory));

    public static MachineState Restore(MachineStateSnapshot snapshot, MachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definition);
        if (snapshot.DefinitionId != definition.Id ||
            !double.IsFinite(snapshot.ConstructionProgressSeconds) || snapshot.ConstructionProgressSeconds < 0 ||
            snapshot.ConstructionProgressSeconds > definition.ConstructionDurationSeconds ||
            !double.IsFinite(snapshot.ProductionProgressSeconds) || snapshot.ProductionProgressSeconds < 0 ||
            !double.IsFinite(snapshot.GeneratorFuelSecondsRemaining) || snapshot.GeneratorFuelSecondsRemaining < 0 ||
            (snapshot.IsBatchInProgress && snapshot.SelectedRecipeId is null))
        {
            throw new ArgumentException("The persisted machine state is invalid.", nameof(snapshot));
        }

        var state = new MachineState(snapshot.InstanceId, definition, snapshot.Placement)
        {
            SelectedRecipeId = snapshot.SelectedRecipeId,
            IsEnabled = snapshot.IsEnabled,
            Status = snapshot.Status,
            ConstructionProgressSeconds = snapshot.ConstructionProgressSeconds,
            ProductionProgressSeconds = snapshot.ProductionProgressSeconds,
            _batchInProgress = snapshot.IsBatchInProgress,
            GeneratorFuelSecondsRemaining = snapshot.GeneratorFuelSecondsRemaining,
        };
        RestoreInventory(state.InputInventory, snapshot.InputSlots);
        RestoreInventory(state.OutputInventory, snapshot.OutputSlots);
        return state;
    }

    public bool SelectRecipe(RecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.MachineId != Definition.Id || _batchInProgress)
        {
            return false;
        }

        SelectedRecipeId = recipe.Id;
        RefreshIdleStatus();
        return true;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        RefreshIdleStatus();
    }

    public void AdvanceConstruction(double deltaSeconds)
    {
        ValidateDelta(deltaSeconds);
        if (IsConstructionComplete)
        {
            return;
        }

        ConstructionProgressSeconds = Math.Min(
            Definition.ConstructionDurationSeconds,
            ConstructionProgressSeconds + deltaSeconds);
        Status = IsConstructionComplete
            ? (IsEnabled ? MachineOperationStatus.Ready : MachineOperationStatus.Disabled)
            : MachineOperationStatus.UnderConstruction;
    }

    public double GetRequestedPowerKilowatts(RecipeDefinition recipe)
    {
        EnsureSelectedRecipe(recipe);
        if (!IsConstructionComplete || !IsEnabled)
        {
            return 0;
        }

        if (_batchInProgress)
        {
            if (ProductionProgressSeconds + Epsilon >= recipe.DurationSeconds)
            {
                return 0;
            }

            return recipe.RequiredPowerKilowatts;
        }

        if (!ProductionInventoryRules.ContainsAll(InputInventory, recipe.Inputs) ||
            !ProductionInventoryRules.CanStoreAll(OutputInventory, recipe.CombinedOutputs))
        {
            return 0;
        }

        return recipe.RequiredPowerKilowatts;
    }

    public MachineTickResult TickProduction(
        RecipeDefinition recipe,
        double deltaSeconds,
        double allocatedPowerKilowatts)
    {
        EnsureSelectedRecipe(recipe);
        ValidateDelta(deltaSeconds);
        if (!double.IsFinite(allocatedPowerKilowatts) || allocatedPowerKilowatts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedPowerKilowatts));
        }

        if (!IsConstructionComplete)
        {
            Status = MachineOperationStatus.UnderConstruction;
            return new MachineTickResult(0, 0, Status);
        }

        if (!IsEnabled)
        {
            Status = MachineOperationStatus.Disabled;
            return new MachineTickResult(0, 0, Status);
        }

        var remainingSeconds = deltaSeconds;
        var completedCycles = 0;
        var consumedEnergy = 0.0;

        while (true)
        {
            if (_batchInProgress && ProductionProgressSeconds + Epsilon >= recipe.DurationSeconds)
            {
                if (!TryCompleteBatch(recipe))
                {
                    Status = MachineOperationStatus.OutputFull;
                    break;
                }

                completedCycles++;
            }

            if (!_batchInProgress)
            {
                if (!ProductionInventoryRules.ContainsAll(InputInventory, recipe.Inputs))
                {
                    Status = MachineOperationStatus.WaitingForMaterial;
                    break;
                }

                if (!ProductionInventoryRules.CanStoreAll(OutputInventory, recipe.CombinedOutputs))
                {
                    Status = MachineOperationStatus.OutputFull;
                    break;
                }

                if (remainingSeconds <= Epsilon)
                {
                    Status = MachineOperationStatus.Ready;
                    break;
                }

                if (allocatedPowerKilowatts + Epsilon < recipe.RequiredPowerKilowatts)
                {
                    Status = MachineOperationStatus.WaitingForEnergy;
                    break;
                }

                if (!ProductionInventoryRules.TryRemoveAll(InputInventory, recipe.Inputs))
                {
                    throw new InvalidOperationException("A production batch lost its prevalidated inputs.");
                }

                _batchInProgress = true;
                ProductionProgressSeconds = 0;
            }

            if (allocatedPowerKilowatts + Epsilon < recipe.RequiredPowerKilowatts)
            {
                Status = MachineOperationStatus.WaitingForEnergy;
                break;
            }

            var advancedSeconds = Math.Min(
                remainingSeconds,
                recipe.DurationSeconds - ProductionProgressSeconds);
            ProductionProgressSeconds += advancedSeconds;
            remainingSeconds -= advancedSeconds;
            consumedEnergy += advancedSeconds * recipe.RequiredPowerKilowatts;
            Status = MachineOperationStatus.Producing;

            if (remainingSeconds <= Epsilon && ProductionProgressSeconds + Epsilon < recipe.DurationSeconds)
            {
                break;
            }
        }

        return new MachineTickResult(completedCycles, consumedEnergy, Status);
    }

    public double GetAvailableGenerationKilowatts(double deltaSeconds)
    {
        ValidateDelta(deltaSeconds);
        if (Definition.Kind != MachineKind.Generator || !IsConstructionComplete || !IsEnabled ||
            deltaSeconds <= Epsilon)
        {
            return 0;
        }

        if (!Definition.IsFuelledGenerator)
        {
            return Definition.GeneratedPowerKilowatts;
        }

        var fuelId = Definition.GeneratorFuelItemId!.Value;
        var returnedContainerId = Definition.GeneratorReturnedContainerItemId!.Value;
        var loadableFuelItems = Math.Min(
            InputInventory.GetAmount(fuelId),
            ProductionInventoryRules.GetAvailableCapacity(OutputInventory, returnedContainerId));
        var availableFuelSeconds = GeneratorFuelSecondsRemaining +
                                   loadableFuelItems * Definition.GeneratorFuelSecondsPerItem;
        return Definition.GeneratedPowerKilowatts * Math.Min(1, availableFuelSeconds / deltaSeconds);
    }

    public void ConsumeGeneratedEnergy(double energyKilowattSeconds)
    {
        if (!double.IsFinite(energyKilowattSeconds) || energyKilowattSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyKilowattSeconds));
        }

        if (Definition.Kind != MachineKind.Generator || energyKilowattSeconds <= Epsilon ||
            !Definition.IsFuelledGenerator)
        {
            return;
        }

        var fuelSecondsNeeded = energyKilowattSeconds / Definition.GeneratedPowerKilowatts;
        while (GeneratorFuelSecondsRemaining + Epsilon < fuelSecondsNeeded)
        {
            var fuelId = Definition.GeneratorFuelItemId!.Value;
            var returnedContainerId = Definition.GeneratorReturnedContainerItemId!.Value;
            if (InputInventory.GetAmount(fuelId) == 0 ||
                !ProductionInventoryRules.CanStoreAll(OutputInventory, [new ItemAmount(returnedContainerId, 1)]))
            {
                throw new InvalidOperationException("The generator cannot provide its preallocated energy.");
            }

            if (!InputInventory.Remove(fuelId, 1).Succeeded || !OutputInventory.Add(returnedContainerId, 1).Succeeded)
            {
                throw new InvalidOperationException("A generator fuel container transaction failed.");
            }

            GeneratorFuelSecondsRemaining += Definition.GeneratorFuelSecondsPerItem;
        }

        GeneratorFuelSecondsRemaining = Math.Max(0, GeneratorFuelSecondsRemaining - fuelSecondsNeeded);
    }

    public void SetGeneratorOperatingStatus(double suppliedPowerKilowatts)
    {
        if (Definition.Kind != MachineKind.Generator)
        {
            throw new InvalidOperationException("Only a generator has a generator operating status.");
        }

        if (!IsConstructionComplete)
        {
            Status = MachineOperationStatus.UnderConstruction;
        }
        else if (!IsEnabled)
        {
            Status = MachineOperationStatus.Disabled;
        }
        else if (suppliedPowerKilowatts > Epsilon)
        {
            Status = MachineOperationStatus.Producing;
        }
        else if (Definition.IsFuelledGenerator && GeneratorFuelSecondsRemaining <= Epsilon &&
                 InputInventory.GetAmount(Definition.GeneratorFuelItemId!.Value) == 0)
        {
            Status = MachineOperationStatus.WaitingForMaterial;
        }
        else if (Definition.IsFuelledGenerator && GeneratorFuelSecondsRemaining <= Epsilon &&
                 !ProductionInventoryRules.CanStoreAll(
                     OutputInventory,
                     [new ItemAmount(Definition.GeneratorReturnedContainerItemId!.Value, 1)]))
        {
            Status = MachineOperationStatus.OutputFull;
        }
        else
        {
            Status = MachineOperationStatus.Ready;
        }
    }

    private bool TryCompleteBatch(RecipeDefinition recipe)
    {
        if (!ProductionInventoryRules.TryAddAll(OutputInventory, recipe.CombinedOutputs))
        {
            return false;
        }

        _batchInProgress = false;
        ProductionProgressSeconds = 0;
        return true;
    }

    private void EnsureSelectedRecipe(RecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.MachineId != Definition.Id || SelectedRecipeId != recipe.Id)
        {
            throw new InvalidOperationException($"Recipe '{recipe.Id}' is not selected for machine '{InstanceId}'.");
        }
    }

    private void RefreshIdleStatus()
    {
        if (!IsConstructionComplete)
        {
            Status = MachineOperationStatus.UnderConstruction;
        }
        else if (!IsEnabled)
        {
            Status = MachineOperationStatus.Disabled;
        }
        else if (Definition.Kind == MachineKind.Production && SelectedRecipeId is null)
        {
            Status = MachineOperationStatus.NoRecipeSelected;
        }
        else
        {
            Status = MachineOperationStatus.Ready;
        }
    }

    private static void ValidateDelta(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }
    }

    private static IReadOnlyList<MachineInventorySlotSnapshot> SnapshotInventory(SlotInventory inventory) =>
        inventory.Slots
            .Where(slot => slot.ItemId is not null)
            .Select(slot => new MachineInventorySlotSnapshot(slot.Index, slot.ItemId!.Value, slot.Amount))
            .ToArray();

    private static void RestoreInventory(
        SlotInventory inventory,
        IReadOnlyList<MachineInventorySlotSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Select(snapshot => snapshot.Index).Distinct().Count() != snapshots.Count)
        {
            throw new ArgumentException("Persisted machine inventory slots must be unique.", nameof(snapshots));
        }

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Index < 0 || snapshot.Index >= inventory.SlotCount ||
                snapshot.Amount <= 0 || snapshot.Amount > inventory.MaximumStackSize)
            {
                throw new ArgumentException("A persisted machine inventory slot is invalid.", nameof(snapshots));
            }

            inventory.GetMutableSlot(snapshot.Index).Assign(snapshot.ItemId, snapshot.Amount);
        }
    }
}

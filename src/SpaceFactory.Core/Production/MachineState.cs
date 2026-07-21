using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Resources;

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
    MissingResourceSource,
    WaitingForLogistics,
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

public sealed record ExtractionSourceBinding(
    string SourceId,
    ItemId ResourceId,
    ResourcePurity Purity,
    double BaseExtractionUnitsPerMinute)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceId) || !Enum.IsDefined(Purity) ||
            !double.IsFinite(BaseExtractionUnitsPerMinute) || BaseExtractionUnitsPerMinute <= 0)
        {
            throw new ArgumentException("The extraction source binding is invalid.");
        }
    }
}

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
    IReadOnlyList<MachineInventorySlotSnapshot> OutputSlots,
    ExtractionSourceBinding? ExtractionSource = null,
    double InternalEnergyKilowattSeconds = 0,
    double StoredGridEnergyKilowattSeconds = 0,
    bool ConstructionCostsPaid = true);

public sealed class MachineState
{
    private const double Epsilon = 0.000_001;
    private bool _batchInProgress;

    public MachineState(
        MachineInstanceId instanceId,
        MachineDefinition definition,
        MachinePlacement? placement = null,
        bool constructionCompleted = false,
        bool constructionCostsPaid = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        placement?.Validate();
        InstanceId = instanceId;
        Definition = definition;
        Placement = placement;
        InputInventory = new SlotInventory(
            definition.InputSlotCount,
            itemStackSizeResolver: ResolveItemStackSize);
        OutputInventory = new SlotInventory(
            definition.OutputSlotCount,
            itemStackSizeResolver: ResolveItemStackSize);
        ConstructionProgressSeconds = constructionCompleted ? definition.ConstructionDurationSeconds : 0;
        ConstructionCostsPaid = constructionCostsPaid;
        Status = constructionCompleted ? MachineOperationStatus.Disabled : MachineOperationStatus.UnderConstruction;
        InternalEnergyKilowattSeconds = definition.Id == MachineDefinitionIds.MobileMiner
            ? MachineEnergyConfiguration.MobileMinerInitialEnergyKilowattSeconds
            : 0;
        HasRequiredOutputConnection = definition.Id != MachineDefinitionIds.AutomaticMiner;
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

    public double GeneratorFuelTankCapacitySeconds => Definition.Id == MachineDefinitionIds.FuelGenerator
        ? Definition.GeneratorFuelSecondsPerItem * ProductionConfiguration.FuelGeneratorTankContainerCapacity
        : 0;

    public ExtractionSourceBinding? ExtractionSource { get; private set; }

    public double InternalEnergyKilowattSeconds { get; private set; }

    public double StoredGridEnergyKilowattSeconds { get; private set; }

    /// <summary>
    /// Tracks whether this exact instance consumed its catalog construction costs. The first
    /// basic generator is intentionally free and therefore must not mint materials when removed.
    /// Physical placement kits still count as paid construction costs.
    /// </summary>
    public bool ConstructionCostsPaid { get; private set; }

    public bool HasRequiredOutputConnection { get; private set; }

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
        SnapshotInventory(OutputInventory),
        ExtractionSource,
        InternalEnergyKilowattSeconds,
        StoredGridEnergyKilowattSeconds,
        ConstructionCostsPaid);

    public static MachineState Restore(MachineStateSnapshot snapshot, MachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definition);
        if (snapshot.DefinitionId != definition.Id ||
            !double.IsFinite(snapshot.ConstructionProgressSeconds) || snapshot.ConstructionProgressSeconds < 0 ||
            snapshot.ConstructionProgressSeconds > definition.ConstructionDurationSeconds ||
            !double.IsFinite(snapshot.ProductionProgressSeconds) || snapshot.ProductionProgressSeconds < 0 ||
            !double.IsFinite(snapshot.GeneratorFuelSecondsRemaining) || snapshot.GeneratorFuelSecondsRemaining < 0 ||
            !double.IsFinite(snapshot.InternalEnergyKilowattSeconds) || snapshot.InternalEnergyKilowattSeconds < 0 ||
            !double.IsFinite(snapshot.StoredGridEnergyKilowattSeconds) || snapshot.StoredGridEnergyKilowattSeconds < 0 ||
            snapshot.StoredGridEnergyKilowattSeconds > MachineEnergyConfiguration.BatteryBankCapacityKilowattSeconds ||
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
            ExtractionSource = snapshot.ExtractionSource,
            InternalEnergyKilowattSeconds = snapshot.InternalEnergyKilowattSeconds,
            StoredGridEnergyKilowattSeconds = snapshot.StoredGridEnergyKilowattSeconds,
            ConstructionCostsPaid = snapshot.ConstructionCostsPaid,
        };
        snapshot.ExtractionSource?.Validate();
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

    public void BindExtractionSource(ExtractionSourceBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        if (Definition.Archetype != MachineArchetype.Extractor)
        {
            throw new InvalidOperationException("Only an extractor can bind a resource source.");
        }

        ExtractionSource = source;
        RefreshIdleStatus();
    }

    public void ClearExtractionSource()
    {
        if (_batchInProgress)
        {
            throw new InvalidOperationException("An active extraction batch cannot lose its source.");
        }

        ExtractionSource = null;
        RefreshIdleStatus();
    }

    public void SetOutputConnectionAvailable(bool available)
    {
        HasRequiredOutputConnection = Definition.Id != MachineDefinitionIds.AutomaticMiner || available;
        if (!HasRequiredOutputConnection && IsEnabled)
        {
            Status = MachineOperationStatus.WaitingForLogistics;
        }
        else
        {
            RefreshIdleStatus();
        }
    }

    public double StoreGridEnergy(double requestedEnergyKilowattSeconds)
    {
        ValidateEnergyAmount(requestedEnergyKilowattSeconds);
        EnsureBatteryBank();
        var stored = Math.Min(
            requestedEnergyKilowattSeconds * Definition.EfficiencyMultiplier,
            MachineEnergyConfiguration.BatteryBankCapacityKilowattSeconds - StoredGridEnergyKilowattSeconds);
        StoredGridEnergyKilowattSeconds += stored;
        return stored;
    }

    public double ProvideStoredGridEnergy(double requestedEnergyKilowattSeconds)
    {
        ValidateEnergyAmount(requestedEnergyKilowattSeconds);
        EnsureBatteryBank();
        var provided = Math.Min(requestedEnergyKilowattSeconds, StoredGridEnergyKilowattSeconds);
        StoredGridEnergyKilowattSeconds -= provided;
        return provided;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        RefreshIdleStatus();
    }

    /// <summary>
    /// Commits a prevalidated manual tank transfer. Container exchange and this value are
    /// coordinated by <see cref="GeneratorFuelTankTransfer"/> so gameplay code cannot mutate
    /// the generator reservoir independently of its physical inventories.
    /// </summary>
    internal void SetGeneratorFuelSecondsForTransfer(double fuelSeconds)
    {
        if (Definition.Id != MachineDefinitionIds.FuelGenerator ||
            !double.IsFinite(fuelSeconds) || fuelSeconds < -Epsilon)
        {
            throw new InvalidOperationException("The generator fuel-tank value is invalid.");
        }

        GeneratorFuelSecondsRemaining = Math.Max(0, fuelSeconds);
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

        if (!HasMatchingExtractionSource(recipe) ||
            IsMobileMinerWithoutAvailableEnergy(recipe) ||
            !HasRequiredOutputConnection)
        {
            return 0;
        }

        var requiredPower = GetEffectiveRequiredPowerKilowatts(recipe);

        if (_batchInProgress)
        {
            if (ProductionProgressSeconds + Epsilon >= recipe.DurationSeconds)
            {
                return 0;
            }

            return requiredPower;
        }

        if (!ProductionInventoryRules.ContainsAll(InputInventory, recipe.Inputs) ||
            !MachineOutputInventoryRules.CanStoreAll(OutputInventory, recipe.CombinedOutputs))
        {
            return 0;
        }

        return requiredPower;
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


        if (!HasMatchingExtractionSource(recipe))
        {
            Status = MachineOperationStatus.MissingResourceSource;
            return new MachineTickResult(0, 0, Status);
        }


        if (!HasRequiredOutputConnection)
        {
            Status = MachineOperationStatus.WaitingForLogistics;
            return new MachineTickResult(0, 0, Status);
        }

        var requiredPower = GetEffectiveRequiredPowerKilowatts(recipe);
        var workRate = GetProductionWorkRate(recipe);

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

                if (!MachineOutputInventoryRules.CanStoreAll(OutputInventory, recipe.CombinedOutputs))
                {
                    Status = MachineOperationStatus.OutputFull;
                    break;
                }

                if (remainingSeconds <= Epsilon)
                {
                    Status = MachineOperationStatus.Ready;
                    break;
                }

                if (Definition.Id == MachineDefinitionIds.MobileMiner &&
                    !EnsureMobileMinerEnergyAvailable())
                {
                    Status = MachineOperationStatus.WaitingForEnergy;
                    break;
                }

                if (allocatedPowerKilowatts + Epsilon < requiredPower)
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

            if (Definition.Id == MachineDefinitionIds.MobileMiner &&
                !EnsureMobileMinerEnergyAvailable())
            {
                Status = MachineOperationStatus.WaitingForEnergy;
                break;
            }

            if (allocatedPowerKilowatts + Epsilon < requiredPower)
            {
                Status = MachineOperationStatus.WaitingForEnergy;
                break;
            }

            var realSecondsAvailable = remainingSeconds;
            if (Definition.Id == MachineDefinitionIds.MobileMiner)
            {
                realSecondsAvailable = Math.Min(
                    realSecondsAvailable,
                    InternalEnergyKilowattSeconds /
                    MachineEnergyConfiguration.MobileMinerInternalPowerKilowatts);
            }

            var workSeconds = Math.Min(
                realSecondsAvailable * workRate,
                recipe.DurationSeconds - ProductionProgressSeconds);
            var realSecondsAdvanced = workSeconds / workRate;
            ProductionProgressSeconds += workSeconds;
            remainingSeconds -= realSecondsAdvanced;
            if (Definition.Id == MachineDefinitionIds.MobileMiner)
            {
                InternalEnergyKilowattSeconds = Math.Max(
                    0,
                    InternalEnergyKilowattSeconds -
                    (realSecondsAdvanced * MachineEnergyConfiguration.MobileMinerInternalPowerKilowatts));
            }
            else
            {
                consumedEnergy += realSecondsAdvanced * requiredPower;
            }
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
        var returnContainerCapacity = MachineOutputInventoryRules.GetAvailableCapacity(
            OutputInventory,
            returnedContainerId);
        if (Definition.Id != MachineDefinitionIds.FuelGenerator && returnContainerCapacity <= 0)
        {
            return 0;
        }

        var loadableFuelItems = Math.Min(InputInventory.GetAmount(fuelId), returnContainerCapacity);
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

        if (Definition.Id != MachineDefinitionIds.FuelGenerator &&
            !MachineOutputInventoryRules.CanStoreAll(
                OutputInventory,
                [new ItemAmount(Definition.GeneratorReturnedContainerItemId!.Value, 1)]))
        {
            throw new InvalidOperationException("The generator output is full.");
        }

        var fuelSecondsNeeded = energyKilowattSeconds / Definition.GeneratedPowerKilowatts;
        while (GeneratorFuelSecondsRemaining + Epsilon < fuelSecondsNeeded)
        {
            var fuelId = Definition.GeneratorFuelItemId!.Value;
            var returnedContainerId = Definition.GeneratorReturnedContainerItemId!.Value;
            if (InputInventory.GetAmount(fuelId) == 0 ||
                !MachineOutputInventoryRules.CanStoreAll(OutputInventory, [new ItemAmount(returnedContainerId, 1)]))
            {
                throw new InvalidOperationException("The generator cannot provide its preallocated energy.");
            }

            if (!InputInventory.Remove(fuelId, 1).Succeeded)
            {
                throw new InvalidOperationException("A generator fuel container transaction failed.");
            }

            if (!MachineOutputInventoryRules.TryAddAll(
                    OutputInventory,
                    [new ItemAmount(returnedContainerId, 1)]))
            {
                if (!InputInventory.Add(fuelId, 1).Succeeded)
                {
                    throw new InvalidOperationException(
                        "A failed generator output transaction could not restore its fuel item.");
                }

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
        else if (Definition.IsFuelledGenerator &&
                 (Definition.Id != MachineDefinitionIds.FuelGenerator ||
                  GeneratorFuelSecondsRemaining <= Epsilon) &&
                 !MachineOutputInventoryRules.CanStoreAll(
                     OutputInventory,
                     [new ItemAmount(Definition.GeneratorReturnedContainerItemId!.Value, 1)]))
        {
            Status = MachineOperationStatus.OutputFull;
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
        else
        {
            Status = MachineOperationStatus.Ready;
        }
    }

    private bool TryCompleteBatch(RecipeDefinition recipe)
    {
        if (!MachineOutputInventoryRules.TryAddAll(OutputInventory, recipe.CombinedOutputs))
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

    private bool HasMatchingExtractionSource(RecipeDefinition recipe) =>
        !recipe.IsExtractionRecipe ||
        ExtractionSource is not null && recipe.SourceResourceId == ExtractionSource.ResourceId;

    private bool IsMobileMinerWithoutAvailableEnergy(RecipeDefinition recipe) =>
        recipe.IsExtractionRecipe && Definition.Id == MachineDefinitionIds.MobileMiner &&
        InternalEnergyKilowattSeconds <= Epsilon &&
        InputInventory.GetAmount(ProductionItemIds.MobileBatteryPack) == 0;

    private bool EnsureMobileMinerEnergyAvailable()
    {
        if (InternalEnergyKilowattSeconds > Epsilon)
        {
            return true;
        }

        if (!InputInventory.Remove(ProductionItemIds.MobileBatteryPack, 1).Succeeded)
        {
            return false;
        }

        InternalEnergyKilowattSeconds = MachineEnergyConfiguration.MobileBatteryPackEnergyKilowattSeconds;
        return true;
    }

    private double GetProductionWorkRate(RecipeDefinition recipe)
    {
        if (!recipe.IsExtractionRecipe || ExtractionSource is null)
        {
            return Definition.SpeedMultiplier;
        }

        var unitsPerCycle = recipe.Outputs
            .Where(output => output.ItemId == ExtractionSource.ResourceId)
            .Sum(output => output.Amount);
        if (unitsPerCycle <= 0)
        {
            throw new InvalidOperationException("An extraction recipe must output its bound source resource.");
        }

        var desiredUnitsPerSecond = MiningConfiguration.GetExtractionUnitsPerMinute(
                                        ExtractionSource.BaseExtractionUnitsPerMinute,
                                        ExtractionSource.Purity) *
                                    Definition.SpeedMultiplier / 60;
        return desiredUnitsPerSecond * recipe.DurationSeconds / unitsPerCycle;
    }

    private double GetEffectiveRequiredPowerKilowatts(RecipeDefinition recipe)
    {
        if (Definition.Id == MachineDefinitionIds.MobileMiner)
        {
            return 0;
        }

        var purityEfficiency = recipe.IsExtractionRecipe && ExtractionSource is not null
            ? MiningConfiguration.GetEnergyEfficiencyMultiplier(ExtractionSource.Purity)
            : 1;
        return recipe.RequiredPowerKilowatts /
               (Definition.EfficiencyMultiplier * purityEfficiency);
    }

    private void EnsureBatteryBank()
    {
        if (Definition.Id != MachineDefinitionIds.BatteryBank)
        {
            throw new InvalidOperationException("Only a battery bank stores grid energy.");
        }
    }

    private static void ValidateEnergyAmount(double energyKilowattSeconds)
    {
        if (!double.IsFinite(energyKilowattSeconds) || energyKilowattSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyKilowattSeconds));
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

        var occupiedIndices = snapshots.Select(snapshot => snapshot.Index).ToHashSet();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Index < 0 || snapshot.Index >= inventory.SlotCount ||
                snapshot.Amount <= 0 || snapshot.Amount > inventory.MaximumStackSize)
            {
                throw new ArgumentException("A persisted machine inventory slot is invalid.", nameof(snapshots));
            }

            var maximum = inventory.GetMaximumStackSize(snapshot.ItemId);
            var firstAmount = Math.Min(snapshot.Amount, maximum);
            inventory.GetMutableSlot(snapshot.Index).Assign(snapshot.ItemId, firstAmount);
            var remaining = snapshot.Amount - firstAmount;
            while (remaining > 0)
            {
                var overflowSlot = Enumerable.Range(0, inventory.SlotCount)
                    .FirstOrDefault(index => !occupiedIndices.Contains(index) && inventory.GetSlot(index).IsEmpty, -1);
                if (overflowSlot < 0)
                {
                    throw new ArgumentException(
                        "A legacy machine inventory stack cannot be split without losing items.",
                        nameof(snapshots));
                }

                var amount = Math.Min(remaining, maximum);
                inventory.GetMutableSlot(overflowSlot).Assign(snapshot.ItemId, amount);
                occupiedIndices.Add(overflowSlot);
                remaining -= amount;
            }
        }
    }

    private static int ResolveItemStackSize(ItemId itemId) =>
        DefaultProductionItemCatalog.Instance.TryGet(itemId, out var definition) && definition is not null
            ? definition.MaximumStackSize
            : InventoryConfiguration.MaximumStackSize;
}

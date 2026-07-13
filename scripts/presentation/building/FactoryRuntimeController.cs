using Godot;
using SpaceFactory.Application.Factory;
using SpaceFactory.Core.Construction;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;
using SpaceFactory.Infrastructure.Persistence;
using SpaceFactory.Presentation.InventoryUI;
using SpaceFactory.Presentation.World;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Owns the live, chunk-aware presentation of the Godot-independent factory
/// state. Only loaded comet machines receive nodes and full simulation ticks.
/// </summary>
public partial class FactoryRuntimeController : Node2D
{
    public const double SimulationIntervalSeconds = 0.1;
    public const double AutosaveIntervalSeconds = 1.0;
    public const double MaximumOfflineSimulationSeconds = 60 * 60;
    public const double OfflineSimulationStepSeconds = 10;
    private static bool _persistenceSmokeCompleted;

    private readonly MachineCatalog _machineCatalog = DefaultMachineCatalog.Instance;
    private readonly RecipeCatalog _recipeCatalog = DefaultRecipeCatalog.Instance;
    private readonly ResearchCatalog _researchCatalog = DefaultResearchCatalog.Instance;
    private readonly Dictionary<MachineInstanceId, MachineState> _machines = [];
    private readonly Dictionary<string, List<MachineState>> _machinesByComet = new(StringComparer.Ordinal);
    private readonly Dictionary<MachineInstanceId, MachineView> _machineViews = [];
    private readonly Dictionary<string, AsteroidView> _loadedComets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastSimulatedUtc = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PowerNetworkTickResult> _lastPowerResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LocalCometPowerNetwork> _powerNetworks = new(StringComparer.Ordinal);
    private readonly Dictionary<MachineInstanceId, ResearchId> _pendingResearch = [];

    private SlotInventory _astronautInventory = null!;
    private SlotInventory? _shipInventory;
    private IReadOnlyList<InventorySlotState> _pendingShipInventory = [];
    private IReadOnlyList<InventorySlotState> _lastAstronautInventory = [];
    private IReadOnlyList<InventorySlotState> _lastShipInventory = [];
    private ItemPresentationCatalog _itemPresentation = null!;
    private IFactoryStateStore _stateStore = null!;
    private Func<double> _shipFuelProvider = null!;
    private Action<double> _restoreShipFuel = null!;
    private Action<string> _showMessage = null!;
    private ResearchState _research = new();
    private FirstBasicGeneratorState _firstBasicGenerator = new();
    private MachinePlacementPreview _placementPreview = null!;
    private MachineInstanceId? _activeResearchStation;
    private MachineState? _openMachine;
    private double _simulationElapsed;
    private double _autosaveElapsed;
    private bool _initialized;
    private bool _dirty;

    public bool IsPlacementActive => _placementPreview?.IsActive == true;

    public MachinePlacementFailureReason PlacementFailure =>
        _placementPreview?.CurrentFailure ?? MachinePlacementFailureReason.PlacementNotActive;

    public IReadOnlyCollection<MachineState> Machines => _machines.Values.ToArray();

    public ResearchState Research => _research;

#if DEBUG
    /// <summary>
    /// Exposes the persisted research owner to the headless integration smoke without
    /// making research-station rebinding part of the release API.
    /// </summary>
    public string? DebugActiveResearchStationId => _activeResearchStation?.Value;

    public double DebugGetResearchDemandForComet(string cometId) =>
        GetResearchDemandForComet(cometId);
#endif

    public event Action<MachineState>? MachineInteractionRequested;

    public event Action? FactoryStateChanged;

    public event Action? BuildCatalogChanged;

    public override void _Ready()
    {
        if (!_persistenceSmokeCompleted &&
            (OS.HasFeature("headless") ||
             DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase)))
        {
            FactoryStateJsonCodec.RunSchemaV2SmokeTest();
            _persistenceSmokeCompleted = true;
            GD.Print(
                "FACTORY_PERSISTENCE_V2_SMOKE_OK: exact inventory slots, v1 migration, corruption rejection");
        }

        _placementPreview = new MachinePlacementPreview { Name = "MachinePlacementPreview" };
        AddChild(_placementPreview);
        _placementPreview.ConfigureWorldSources(
            () => _loadedComets.Values.Where(GodotObject.IsInstanceValid).ToArray(),
            () => _machineViews.Values.Where(GodotObject.IsInstanceValid).ToArray());
    }

    public override void _Process(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        _simulationElapsed += delta;
        if (_simulationElapsed >= SimulationIntervalSeconds)
        {
            var elapsed = _simulationElapsed;
            _simulationElapsed = 0;
            SimulateLoadedComets(elapsed);
            if (DetectPlayerInventoryChanges())
            {
                MarkDirty();
                BuildCatalogChanged?.Invoke();
            }
        }

        if (!_dirty)
        {
            return;
        }

        _autosaveElapsed += delta;
        if (_autosaveElapsed >= AutosaveIntervalSeconds)
        {
            SaveNow();
        }
    }

    public void Initialize(
        SlotInventory astronautInventory,
        ItemPresentationCatalog itemPresentation,
        IFactoryStateStore stateStore,
        Func<double> shipFuelProvider,
        Action<double> restoreShipFuel,
        Action<string> showMessage)
    {
        ArgumentNullException.ThrowIfNull(astronautInventory);
        ArgumentNullException.ThrowIfNull(itemPresentation);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(shipFuelProvider);
        ArgumentNullException.ThrowIfNull(restoreShipFuel);
        ArgumentNullException.ThrowIfNull(showMessage);
        if (_initialized)
        {
            throw new InvalidOperationException("Factory runtime is already initialized.");
        }

        _astronautInventory = astronautInventory;
        _itemPresentation = itemPresentation;
        _stateStore = stateStore;
        _shipFuelProvider = shipFuelProvider;
        _restoreShipFuel = restoreShipFuel;
        _showMessage = showMessage;
        Restore(stateStore.Load());
        RememberPlayerInventories();
        _initialized = true;
    }

    /// <summary>
    /// Attaches the independently owned ship inventory after the main runtime initialization.
    /// This preserves the existing Initialize API while allowing its persisted v2 state to be
    /// restored before the first autosave.
    /// </summary>
    public void AttachShipInventory(SlotInventory shipInventory)
    {
        ArgumentNullException.ThrowIfNull(shipInventory);
        if (!_initialized)
        {
            throw new InvalidOperationException("Factory runtime must be initialized first.");
        }

        if (_shipInventory is not null && !ReferenceEquals(_shipInventory, shipInventory))
        {
            throw new InvalidOperationException("A different ship inventory is already attached.");
        }

        _shipInventory = shipInventory;
        InventoryStatePersistence.Restore(_shipInventory, _pendingShipInventory);
        _pendingShipInventory = [];
        RememberPlayerInventories();
    }

    public void RegisterSector(SectorView sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        foreach (var comet in sector.Comets)
        {
            RegisterComet(comet);
        }
    }

    public void UnregisterSector(SectorView sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        var persistentStateChanged = false;
        foreach (var comet in sector.Comets)
        {
            if (!_loadedComets.Remove(comet.CometId))
            {
                continue;
            }

            if (GetMachinesOnComet(comet.CometId).Any())
            {
                _lastSimulatedUtc[comet.CometId] = DateTimeOffset.UtcNow;
                persistentStateChanged = true;
            }
            foreach (var pair in _machineViews
                         .Where(pair => pair.Value.HostComet == comet)
                         .ToArray())
            {
                pair.Value.InteractionRequested -= HandleMachineInteractionRequested;
                _machineViews.Remove(pair.Key);
            }

            // The binding is persistent identity, not a loaded-view reference. Removing
            // it here would allow an active project to jump to another research station.
            _lastPowerResults.Remove(comet.CometId);
        }

        if (persistentStateChanged)
        {
            MarkDirty();
        }
    }

    public IReadOnlyList<BuildMachineViewModel> CreateBuildMenuViewModels() =>
        _machineCatalog.All
            .OrderBy(machine => machine.Category)
            .ThenBy(machine => machine.DisplayName, StringComparer.CurrentCulture)
            .Select(CreateBuildMachineViewModel)
            .ToArray();

    public bool StartPlacement(string machineDefinitionId)
    {
        var id = new MachineDefinitionId(machineDefinitionId);
        if (!_machineCatalog.TryGet(id, out var definition) || definition is null)
        {
            return false;
        }

        if (!IsUnlocked(definition.UnlockRequirement))
        {
            _showMessage("Maschine noch nicht erforscht");
            return false;
        }

        _placementPreview.Start(id, HasBuildMaterials);
        return true;
    }

    public void CancelPlacement() => _placementPreview.Cancel();

    public void RotatePlacement(int direction) => _placementPreview.Rotate(direction);

    public bool TryPlaceSelectedMachine()
    {
        _placementPreview.Refresh();
        if (!_placementPreview.TryGetPlacement(out var placement, out var comet, out var failure) ||
            placement is null || comet is null ||
            _placementPreview.SelectedDefinitionId is not { } definitionId)
        {
            _showMessage(MachinePlacementPreview.GetReasonText(failure));
            return false;
        }

        var definition = _machineCatalog.Get(definitionId);
        var costs = _firstBasicGenerator.GetEffectiveBuildCosts(definition);
        if (!ProductionInventoryRules.ContainsAll(_astronautInventory, costs))
        {
            _showMessage("Materialien fehlen");
            return false;
        }

        var state = new MachineState(
            new MachineInstanceId($"machine:{Guid.NewGuid():N}"),
            definition,
            placement);
        if (definition.Kind is MachineKind.Generator or MachineKind.Research or MachineKind.Storage)
        {
            state.SetEnabled(true);
        }

        if (!ProductionInventoryRules.TryRemoveAll(_astronautInventory, costs))
        {
            _showMessage("Materialien konnten nicht abgezogen werden");
            return false;
        }

        try
        {
            _machines.Add(state.InstanceId, state);
            IndexMachine(state);
            CreateMachineView(state, comet);
            _lastSimulatedUtc[comet.CometId] = DateTimeOffset.UtcNow;
            if (_powerNetworks.TryGetValue(comet.CometId, out var existingNetwork))
            {
                existingNetwork.AddMachine(state);
            }
            _firstBasicGenerator.TryConsumeFreeBuild(definition);
        }
        catch
        {
            _machines.Remove(state.InstanceId);
            RemoveIndexedMachine(state);
            if (_powerNetworks.TryGetValue(comet.CometId, out var existingNetwork))
            {
                existingNetwork.RemoveMachine(state.InstanceId);
            }

            if (!ProductionInventoryRules.TryAddAll(_astronautInventory, costs))
            {
                throw new InvalidOperationException("A failed construction could not restore its build costs.");
            }

            throw;
        }

        _placementPreview.Cancel();
        MarkDirty();
        BuildCatalogChanged?.Invoke();
        FactoryStateChanged?.Invoke();
        _showMessage($"{definition.DisplayName} wird gebaut");
        return true;
    }

    public MachineState? FindNearestInteractiveMachine(Vector2 worldPosition)
    {
        MachineView? closest = null;
        var bestDistanceSquared = float.PositiveInfinity;
        foreach (var view in _machineViews.Values)
        {
            if (!GodotObject.IsInstanceValid(view) || !view.IsWithinInteractionRange(worldPosition))
            {
                continue;
            }

            var distance = view.GlobalPosition.DistanceSquaredTo(worldPosition);
            if (distance >= bestDistanceSquared)
            {
                continue;
            }

            closest = view;
            bestDistanceSquared = distance;
        }

        return closest is null ? null : _machines[closest.InstanceId];
    }

    public float GetMachineDistanceSquared(MachineState state, Vector2 worldPosition) =>
        _machineViews.TryGetValue(state.InstanceId, out var view) && GodotObject.IsInstanceValid(view)
            ? view.GlobalPosition.DistanceSquaredTo(worldPosition)
            : float.PositiveInfinity;

    public MachinePanelViewModel CreateMachinePanelViewModel(MachineState state)
    {
        _openMachine = state;
        return state.Definition.Kind switch
        {
            MachineKind.Research => CreateResearchPanelViewModel(state),
            MachineKind.Storage => CreateStoragePanelViewModel(state),
            MachineKind.Generator => CreateGeneratorPanelViewModel(state),
            _ => CreateProductionPanelViewModel(state),
        };
    }

    public MachinePanelViewModel? RefreshOpenMachineViewModel() =>
        _openMachine is null ? null : CreateMachinePanelViewModel(_openMachine);

    public void CloseMachine() => _openMachine = null;

    public void SelectMachineRecipe(string recipeId)
    {
        if (_openMachine is null)
        {
            return;
        }

        if (_openMachine.Definition.Kind == MachineKind.Research)
        {
            const string prefix = "research:";
            if (!recipeId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }

            var researchId = new ResearchId(recipeId[prefix.Length..]);
            _ = _researchCatalog.Get(researchId);
            _pendingResearch[_openMachine.InstanceId] = researchId;
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!_recipeCatalog.TryGet(new RecipeId(recipeId), out var recipe) || recipe is null ||
            recipe.MachineId != _openMachine.Definition.Id || !IsUnlocked(recipe.UnlockRequirement))
        {
            return;
        }

        if (!_openMachine.SelectRecipe(recipe))
        {
            _showMessage("Rezept kann während eines Zyklus nicht gewechselt werden");
            return;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void SetOpenMachineActive(bool active)
    {
        if (_openMachine is null)
        {
            return;
        }

        if (_openMachine.Definition.Kind == MachineKind.Research)
        {
            SetResearchActive(_openMachine, active);
            return;
        }

        _openMachine.SetEnabled(active);
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    public void LoadOpenMachineInputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        MachineInventoryTransferResult result;
        switch (_openMachine.Definition.Kind)
        {
            case MachineKind.Production when _openMachine.SelectedRecipeId is { } recipeId:
                result = MachineInventoryTransfer.LoadRecipeInputs(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    _recipeCatalog.Get(recipeId));
                break;
            case MachineKind.Generator when _openMachine.Definition.IsFuelledGenerator:
                result = MachineInventoryTransfer.TransferExact(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    [new ItemAmount(_openMachine.Definition.GeneratorFuelItemId!.Value, 1)]);
                break;
            case MachineKind.Storage:
                result = MachineInventoryTransfer.TransferAsMuchAsPossible(
                    _astronautInventory,
                    _openMachine.InputInventory);
                break;
            case MachineKind.Research when TryGetSelectedResearch(_openMachine, out var research):
                result = MachineInventoryTransfer.TransferExact(
                    _astronautInventory,
                    _openMachine.InputInventory,
                    research.MaterialCosts);
                break;
            default:
                _showMessage("Zuerst ein Rezept auswählen");
                return;
        }

        ShowTransferResult(result, "Material geladen");
    }

    public void CollectOpenMachineOutputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        var source = _openMachine.Definition.Kind == MachineKind.Storage
            ? _openMachine.InputInventory
            : _openMachine.OutputInventory;
        var result = _openMachine.Definition.Kind == MachineKind.Storage
            ? MachineInventoryTransfer.TransferAsMuchAsPossible(source, _astronautInventory)
            : MachineInventoryTransfer.TransferAll(source, _astronautInventory);
        ShowTransferResult(result, "Ausgabe ins Inventar übernommen");
    }

    public void ReturnOpenMachineInputs()
    {
        if (_openMachine is null)
        {
            return;
        }

        var result = MachineInventoryTransfer.TransferAll(
            _openMachine.InputInventory,
            _astronautInventory);
        ShowTransferResult(result, "Eingabematerial ins Inventar zurückgenommen");
    }

    public void MarkFuelChanged() => MarkDirty();

    public void MarkInventoryChanged()
    {
        MarkDirty();
        BuildCatalogChanged?.Invoke();
    }

    public void SaveNow()
    {
        if (!_initialized)
        {
            return;
        }

        var saved = _stateStore.Save(new FactoryStateData(
            FactoryStateData.CurrentVersion,
            _machines.Values
                .OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal)
                .Select(machine => machine.CreateSnapshot())
                .ToArray(),
            _research.CreateSnapshot(),
            _firstBasicGenerator.FreeGeneratorAlreadyBuilt,
            _shipFuelProvider(),
            new Dictionary<string, DateTimeOffset>(_lastSimulatedUtc, StringComparer.Ordinal),
            InventoryStatePersistence.Capture(_astronautInventory),
            CaptureShipInventory(),
            _activeResearchStation?.Value));
        _autosaveElapsed = 0;
        if (saved)
        {
            _dirty = false;
            RememberPlayerInventories();
        }
    }

    private void Restore(FactoryStateData saved)
    {
        _research = RestoreResearchSafely(saved.Research);
        _firstBasicGenerator = new FirstBasicGeneratorState(saved.FirstBasicGeneratorBuilt);
        _restoreShipFuel(saved.ShipFuel);
        InventoryStatePersistence.Restore(_astronautInventory, saved.AstronautInventory);
        _pendingShipInventory = saved.ShipInventory.ToArray();
        foreach (var (cometId, timestamp) in saved.LastSimulatedUtcByComet)
        {
            _lastSimulatedUtc[cometId] = timestamp;
        }

        foreach (var snapshot in saved.Machines)
        {
            if (!_machineCatalog.TryGet(snapshot.DefinitionId, out var definition) || definition is null ||
                snapshot.Placement is null)
            {
                continue;
            }

            try
            {
                if (snapshot.SelectedRecipeId is { } recipeId &&
                    (!_recipeCatalog.TryGet(recipeId, out var recipe) || recipe is null ||
                     recipe.MachineId != definition.Id))
                {
                    throw new InvalidDataException(
                        $"Selected recipe '{recipeId}' does not belong to machine '{definition.Id}'.");
                }

                var state = MachineState.Restore(snapshot, definition);
                _machines[state.InstanceId] = state;
                IndexMachine(state);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                GD.PushWarning(
                    $"Persisted machine '{snapshot.InstanceId}' was ignored: {exception.Message}");
            }
        }

        var occupiedCometIds = _machinesByComet.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var cometId in _lastSimulatedUtc.Keys
                     .Where(cometId => !occupiedCometIds.Contains(cometId))
                     .ToArray())
        {
            _lastSimulatedUtc.Remove(cometId);
        }

        // An active project may remain paused while its exact station is unavailable. It
        // must never be reassigned merely because another station happens to be loaded.
        _activeResearchStation = _research.ActiveResearchId is not null &&
                                 saved.ActiveResearchStationId is { } stationId
            ? new MachineInstanceId(stationId)
            : null;
    }

    private ResearchState RestoreResearchSafely(ResearchStateSnapshot snapshot)
    {
        try
        {
            var knownResearch = _researchCatalog.All
                .ToDictionary(definition => definition.Id);
            if (snapshot.CompletedResearch.Any(id => !knownResearch.ContainsKey(id)) ||
                snapshot.ActiveResearchId is { } activeId &&
                (!knownResearch.TryGetValue(activeId, out var activeDefinition) ||
                 snapshot.ProgressSeconds > activeDefinition.DurationSeconds))
            {
                throw new InvalidDataException("The research snapshot references unknown catalog data.");
            }

            return ResearchState.Restore(snapshot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            GD.PushWarning($"Persisted research state was reset: {exception.Message}");
            return new ResearchState();
        }
    }

    private IReadOnlyList<InventorySlotState> CaptureShipInventory() =>
        _shipInventory is null
            ? _pendingShipInventory.ToArray()
            : InventoryStatePersistence.Capture(_shipInventory);

    private bool DetectPlayerInventoryChanges()
    {
        var astronaut = InventoryStatePersistence.Capture(_astronautInventory);
        var ship = CaptureShipInventory();
        var changed = !_lastAstronautInventory.SequenceEqual(astronaut) ||
                      !_lastShipInventory.SequenceEqual(ship);
        _lastAstronautInventory = astronaut;
        _lastShipInventory = ship;
        return changed;
    }

    private void RememberPlayerInventories()
    {
        _lastAstronautInventory = InventoryStatePersistence.Capture(_astronautInventory);
        _lastShipInventory = CaptureShipInventory();
    }

    private void RegisterComet(AsteroidView comet)
    {
        if (_loadedComets.TryGetValue(comet.CometId, out var existing) && existing == comet)
        {
            return;
        }

        _loadedComets[comet.CometId] = comet;
        var machines = GetMachinesOnComet(comet.CometId).ToArray();
        if (machines.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastSimulatedUtc.TryGetValue(comet.CometId, out var lastSimulated))
        {
            var elapsed = Math.Clamp(
                (now - lastSimulated).TotalSeconds,
                0,
                MaximumOfflineSimulationSeconds);
            if (elapsed >= SimulationIntervalSeconds)
            {
                if (SimulateOfflineComet(comet.CometId, elapsed))
                {
                    MarkDirty();
                }
            }
        }

        _lastSimulatedUtc[comet.CometId] = now;
        foreach (var state in machines)
        {
            CreateMachineView(state, comet);
        }
    }

    private void CreateMachineView(MachineState state, AsteroidView comet)
    {
        if (_machineViews.TryGetValue(state.InstanceId, out var existing) &&
            GodotObject.IsInstanceValid(existing))
        {
            existing.Refresh(state);
            return;
        }

        var view = new MachineView();
        view.Configure(state, comet);
        view.InteractionRequested += HandleMachineInteractionRequested;
        _machineViews[state.InstanceId] = view;
    }

    private void HandleMachineInteractionRequested(MachineView view)
    {
        if (_machines.TryGetValue(view.InstanceId, out var state))
        {
            MachineInteractionRequested?.Invoke(state);
        }
    }

    private void SimulateLoadedComets(double deltaSeconds)
    {
        var changed = false;
        foreach (var cometId in _loadedComets.Keys.ToArray())
        {
            if (!GetMachinesOnComet(cometId).Any())
            {
                continue;
            }

            changed |= SimulateComet(cometId, deltaSeconds);
            _lastSimulatedUtc[cometId] = DateTimeOffset.UtcNow;
        }

        if (!changed)
        {
            return;
        }

        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    private bool SimulateComet(string cometId, double deltaSeconds)
    {
        var states = GetMachinesOnComet(cometId).ToArray();
        if (states.Length == 0)
        {
            return false;
        }

        var machineFingerprints = states.ToDictionary(
            state => state.InstanceId,
            CreateSimulationFingerprint);
        var researchFingerprint = CreateResearchFingerprint();
        var changed = false;
        foreach (var state in states)
        {
            if (!state.IsConstructionComplete)
            {
                state.AdvanceConstruction(deltaSeconds);
                changed = true;
            }
        }

        if (!_powerNetworks.TryGetValue(cometId, out var network))
        {
            network = new LocalCometPowerNetwork(cometId);
            foreach (var state in states.OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal))
            {
                network.AddMachine(state);
            }

            _powerNetworks[cometId] = network;
        }

        var researchDemand = GetResearchDemandForComet(cometId);
        var result = network.Tick(deltaSeconds, _recipeCatalog, researchDemand);
        _lastPowerResults[cometId] = result;
        changed |= result.ConsumedEnergyKilowattSeconds > 0;
        if (researchDemand > 0 && _research.ActiveResearchId is { } activeResearchId)
        {
            var researchTick = _research.Tick(
                _researchCatalog.Get(activeResearchId),
                deltaSeconds,
                result.ExternalAllocatedKilowatts);
            changed |= researchTick.EnergyConsumedKilowattSeconds > 0 || researchTick.Completed;
            if (researchTick.Completed)
            {
                _activeResearchStation = null;
                BuildCatalogChanged?.Invoke();
                _showMessage("Forschung abgeschlossen");
            }
        }

        foreach (var state in states)
        {
            if (_machineViews.TryGetValue(state.InstanceId, out var view) &&
                GodotObject.IsInstanceValid(view))
            {
                view.Refresh(state);
            }
        }

        changed |= states.Any(state => machineFingerprints[state.InstanceId] != CreateSimulationFingerprint(state));
        changed |= researchFingerprint != CreateResearchFingerprint();
        return changed;
    }

    private bool SimulateOfflineComet(string cometId, double elapsedSeconds)
    {
        var remaining = elapsedSeconds;
        var changed = false;
        while (remaining >= SimulationIntervalSeconds)
        {
            var step = Math.Min(OfflineSimulationStepSeconds, remaining);
            changed |= SimulateComet(cometId, step);
            remaining -= step;
        }

        return changed;
    }

    private double GetResearchDemandForComet(string cometId)
    {
        if (_research.ActiveResearchId is not { } researchId || !_research.IsEnabled)
        {
            return 0;
        }

        if (_activeResearchStation is not { } stationId ||
            !_machines.TryGetValue(stationId, out var station) ||
            station.Definition.Kind != MachineKind.Research ||
            !string.Equals(station.Placement?.CometId, cometId, StringComparison.Ordinal) ||
            !station.IsConstructionComplete)
        {
            return 0;
        }

        return _researchCatalog.Get(researchId).RequiredPowerKilowatts;
    }

    private IReadOnlyList<MachineState> GetMachinesOnComet(string cometId) =>
        _machinesByComet.TryGetValue(cometId, out var machines)
            ? machines
            : Array.Empty<MachineState>();

    private void IndexMachine(MachineState machine)
    {
        var cometId = machine.Placement?.CometId ??
                      throw new InvalidOperationException("A placed machine needs a comet ID.");
        if (!_machinesByComet.TryGetValue(cometId, out var machines))
        {
            machines = [];
            _machinesByComet.Add(cometId, machines);
        }

        machines.Add(machine);
    }

    private void RemoveIndexedMachine(MachineState machine)
    {
        var cometId = machine.Placement?.CometId;
        if (cometId is null || !_machinesByComet.TryGetValue(cometId, out var machines))
        {
            return;
        }

        machines.Remove(machine);
        if (machines.Count == 0)
        {
            _machinesByComet.Remove(cometId);
        }
    }

    private BuildMachineViewModel CreateBuildMachineViewModel(MachineDefinition definition)
    {
        var costs = _firstBasicGenerator.GetEffectiveBuildCosts(definition)
            .Select(cost =>
            {
                var item = _itemPresentation.GetOrCreateFallback(
                    cost.ItemId,
                    _astronautInventory.MaximumStackSize);
                return new BuildCostViewModel(
                    item.DisplayName,
                    cost.Amount,
                    _astronautInventory.GetAmount(cost.ItemId),
                    item.Color);
            })
            .ToArray();
        return new BuildMachineViewModel(
            definition.Id.Value,
            definition.DisplayName,
            definition.Description,
            GetFunctionSummary(definition),
            MapCategory(definition.Category),
            MachinePresentationCatalog.Instance.Get(definition.Id).Glyph,
            costs,
            IsUnlocked(definition.UnlockRequirement),
            definition.UnlockRequirement is { } researchId
                ? $"Forschung erforderlich: {_researchCatalog.Get(researchId).DisplayName}"
                : string.Empty);
    }

    private MachinePanelViewModel CreateProductionPanelViewModel(MachineState state)
    {
        var recipes = _recipeCatalog.ForMachine(state.Definition.Id)
            .Select(recipe => CreateRecipeViewModel(state, recipe))
            .ToArray();
        var selected = state.SelectedRecipeId is { } selectedId
            ? _recipeCatalog.Get(selectedId)
            : null;
        return CreateBasePanel(
            state,
            recipes,
            selected?.Id.Value,
            selected is null ? 0 : (float)(state.ProductionProgressSeconds / selected.DurationSeconds),
            selected is null ? 0 : (float)selected.RequiredPowerKilowatts);
    }

    private MachinePanelViewModel CreateGeneratorPanelViewModel(MachineState state)
    {
        var recipes = state.Definition.IsFuelledGenerator
            ? new[]
            {
                new MachineRecipeViewModel(
                    "generator_fuel",
                    "Treibstoff in Strom umwandeln",
                    [CreateMaterial(state.Definition.GeneratorFuelItemId!.Value, 1, state.InputInventory)],
                    [CreateOutput(state.Definition.GeneratorReturnedContainerItemId!.Value, 1, state.OutputInventory)],
                    (float)state.Definition.GeneratorFuelSecondsPerItem),
            }
            : Array.Empty<MachineRecipeViewModel>();
        return CreateBasePanel(
            state,
            recipes,
            recipes.FirstOrDefault()?.RecipeId,
            0,
            0);
    }

    private MachinePanelViewModel CreateStoragePanelViewModel(MachineState state)
    {
        var stored = state.InputInventory.Slots
            .Where(slot => !slot.IsEmpty)
            .GroupBy(slot => slot.ItemId!.Value)
            .Select(group => CreateMaterial(group.Key, 0, state.InputInventory))
            .ToArray();
        var recipe = new MachineRecipeViewModel(
            "storage_contents",
            $"Lagerinhalt ({state.InputInventory.UsedSlotCount}/{state.InputInventory.SlotCount} Slots)",
            stored,
            [],
            0);
        return CreateBasePanel(state, [recipe], recipe.RecipeId, 0, 0);
    }

    private MachinePanelViewModel CreateResearchPanelViewModel(MachineState state)
    {
        var ownsActiveResearch = _research.ActiveResearchId is not null &&
                                 _activeResearchStation == state.InstanceId;
        var selectedResearchId = ownsActiveResearch
            ? _research.ActiveResearchId
            : (_pendingResearch.TryGetValue(state.InstanceId, out var pending) ? pending : null);
        var recipes = _researchCatalog.All
            .OrderBy(research => research.DisplayName, StringComparer.CurrentCulture)
            .Select(research =>
            {
                var completed = _research.IsCompleted(research.Id);
                var prerequisiteReady = research.Prerequisites.All(_research.IsCompleted);
                return new MachineRecipeViewModel(
                    $"research:{research.Id.Value}",
                    completed ? $"{research.DisplayName} · abgeschlossen" : research.DisplayName,
                    research.MaterialCosts.Select(cost => CreateMaterial(
                        cost.ItemId,
                        cost.Amount,
                        state.InputInventory)).ToArray(),
                    [new MachineOutputViewModel("Technologie", 1, completed ? 1 : 0, 1, new Color(0.72f, 0.48f, 1))],
                    (float)research.DurationSeconds,
                    !completed && prerequisiteReady,
                    completed ? "Bereits abgeschlossen" : "Vorherige Forschung erforderlich");
            })
            .ToArray();
        var active = ownsActiveResearch && _research.ActiveResearchId is { } activeId
            ? _researchCatalog.Get(activeId)
            : null;
        var needsStationBinding = _research.ActiveResearchId is not null && _activeResearchStation is null;
        var activeElsewhere = _research.ActiveResearchId is not null &&
                              _activeResearchStation is not null &&
                              !ownsActiveResearch;
        var progress = active is null ? 0 : (float)(_research.ProgressSeconds / active.DurationSeconds);
        var requiredPower = active is null ? 0 : (float)active.RequiredPowerKilowatts;
        var baseModel = CreateBasePanel(
            state,
            recipes,
            selectedResearchId is null ? null : $"research:{selectedResearchId.Value.Value}",
            progress,
            requiredPower);
        var status = !state.IsConstructionComplete
            ? MachineUiStatus.UnderConstruction
            : activeElsewhere
                ? MachineUiStatus.Blocked
                : active is null
                    ? MachineUiStatus.Ready
                    : _research.Status switch
                    {
                        ResearchStatus.Researching => MachineUiStatus.Producing,
                        ResearchStatus.WaitingForEnergy => MachineUiStatus.WaitingForEnergy,
                        ResearchStatus.Disabled => MachineUiStatus.SwitchedOff,
                        _ => MachineUiStatus.Ready,
                    };
        return baseModel with
        {
            Status = status,
            StatusDetail = !state.IsConstructionComplete
                ? "Die Forschungsstation wird noch aufgebaut."
                : needsStationBinding
                    ? "Laufendes Forschungsprojekt mit dem Startschalter an diese Station binden."
                : activeElsewhere
                    ? "Ein Forschungsprojekt ist bereits an eine andere Station gebunden."
                    : active is null
                        ? "Forschung auswählen, Materialien laden und starten."
                        : "Forschung läuft im lokalen Stromnetz.",
            IsActive = state.IsConstructionComplete && ownsActiveResearch && _research.IsEnabled,
        };
    }

    private MachinePanelViewModel CreateBasePanel(
        MachineState state,
        IReadOnlyList<MachineRecipeViewModel> recipes,
        string? selectedRecipeId,
        float progress,
        float requiredPower)
    {
        var availablePower = state.Placement is { } placement &&
                             _lastPowerResults.TryGetValue(placement.CometId, out var network)
            ? (float)network.AvailablePowerKilowatts
            : 0;
        return new MachinePanelViewModel(
            state.InstanceId.Value,
            state.Definition.DisplayName,
            MachinePresentationCatalog.Instance.Get(state.Definition.Id).Glyph,
            recipes,
            selectedRecipeId,
            MapStatus(state.Status),
            GetStatusDetail(state.Status),
            state.IsEnabled,
            Mathf.Clamp(progress, 0, 1),
            requiredPower,
            availablePower);
    }

    private MachineRecipeViewModel CreateRecipeViewModel(MachineState state, RecipeDefinition recipe) =>
        new(
            recipe.Id.Value,
            recipe.DisplayName,
            recipe.Inputs.Select(input => CreateMaterial(
                input.ItemId,
                input.Amount,
                state.InputInventory)).ToArray(),
            recipe.CombinedOutputs.Select(output => CreateOutput(
                output.ItemId,
                output.Amount,
                state.OutputInventory)).ToArray(),
            (float)recipe.DurationSeconds,
            IsUnlocked(recipe.UnlockRequirement),
            recipe.UnlockRequirement is { } researchId
                ? $"Forschung erforderlich: {_researchCatalog.Get(researchId).DisplayName}"
                : string.Empty);

    private MachineMaterialViewModel CreateMaterial(
        SpaceFactory.Core.Items.ItemId itemId,
        int required,
        SlotInventory inventory)
    {
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.MaximumStackSize);
        return new MachineMaterialViewModel(
            item.DisplayName,
            required,
            inventory.GetAmount(itemId),
            item.Color);
    }

    private MachineOutputViewModel CreateOutput(
        SpaceFactory.Core.Items.ItemId itemId,
        int produced,
        SlotInventory inventory)
    {
        var item = _itemPresentation.GetOrCreateFallback(itemId, inventory.MaximumStackSize);
        var stored = inventory.GetAmount(itemId);
        var capacity = stored + ProductionInventoryRules.GetAvailableCapacity(inventory, itemId);
        return new MachineOutputViewModel(item.DisplayName, produced, stored, capacity, item.Color);
    }

    private void SetResearchActive(MachineState station, bool active)
    {
        if (!station.IsConstructionComplete)
        {
            _showMessage("Forschungsstation noch im Bau");
            return;
        }

        if (_research.ActiveResearchId is not null)
        {
            if (_activeResearchStation is null)
            {
                if (!active)
                {
                    return;
                }

                _activeResearchStation = station.InstanceId;
                _research.SetEnabled(true);
                MarkDirty();
                FactoryStateChanged?.Invoke();
                _showMessage("Forschung an diese Station gebunden");
                return;
            }

            if (_activeResearchStation != station.InstanceId)
            {
                _showMessage("Forschung läuft bereits in einer anderen Station");
                return;
            }

            _research.SetEnabled(active);
            MarkDirty();
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!active)
        {
            _research.SetEnabled(false);
            MarkDirty();
            FactoryStateChanged?.Invoke();
            return;
        }

        if (!TryGetSelectedResearch(station, out var selected))
        {
            _showMessage("Zuerst eine Forschung auswählen");
            return;
        }

        var result = _research.TryStart(selected, station.InputInventory);
        if (!result.Succeeded)
        {
            _showMessage(result.Failure switch
            {
                ResearchStartFailure.MissingMaterials => "Forschungsmaterialien fehlen",
                ResearchStartFailure.MissingPrerequisite => "Vorherige Forschung fehlt",
                ResearchStartFailure.AlreadyCompleted => "Forschung bereits abgeschlossen",
                _ => "Forschung kann nicht gestartet werden",
            });
            return;
        }

        _activeResearchStation = station.InstanceId;
        MarkDirty();
        FactoryStateChanged?.Invoke();
    }

    private bool TryGetSelectedResearch(MachineState station, out ResearchDefinition definition)
    {
        var selected = (_activeResearchStation == station.InstanceId
                            ? _research.ActiveResearchId
                            : null) ??
                       (_pendingResearch.TryGetValue(station.InstanceId, out var pending) ? pending : null);
        if (selected is null)
        {
            definition = null!;
            return false;
        }

        definition = _researchCatalog.Get(selected.Value);
        return true;
    }

    private bool HasBuildMaterials(MachineDefinitionId definitionId)
    {
        var definition = _machineCatalog.Get(definitionId);
        return IsUnlocked(definition.UnlockRequirement) &&
               ProductionInventoryRules.ContainsAll(
                   _astronautInventory,
                   _firstBasicGenerator.GetEffectiveBuildCosts(definition));
    }

    private bool IsUnlocked(ResearchId? requirement) =>
        requirement is null || _research.IsCompleted(requirement.Value);

    private void ShowTransferResult(MachineInventoryTransferResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            MarkDirty();
            BuildCatalogChanged?.Invoke();
            FactoryStateChanged?.Invoke();
            _showMessage(successMessage);
            return;
        }

        _showMessage(result.Failure switch
        {
            MachineInventoryTransferFailure.MissingSourceItems => "Benötigte Materialien fehlen",
            MachineInventoryTransferFailure.TargetFull => "Zielinventar voll",
            MachineInventoryTransferFailure.NothingToTransfer => "Nichts zu übertragen",
            _ => "Übertragung nicht möglich",
        });
    }

    private void MarkDirty()
    {
        _dirty = true;
        _autosaveElapsed = Math.Min(_autosaveElapsed, AutosaveIntervalSeconds);
    }

    private static BuildMenuCategory MapCategory(MachineCategory category) => category switch
    {
        MachineCategory.Processing => BuildMenuCategory.Processing,
        MachineCategory.Manufacturing => BuildMenuCategory.Manufacturing,
        MachineCategory.Energy => BuildMenuCategory.Energy,
        MachineCategory.Storage => BuildMenuCategory.Storage,
        MachineCategory.Research => BuildMenuCategory.Research,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    private static MachineUiStatus MapStatus(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => MachineUiStatus.UnderConstruction,
        MachineOperationStatus.Disabled => MachineUiStatus.SwitchedOff,
        MachineOperationStatus.Ready or MachineOperationStatus.NoRecipeSelected => MachineUiStatus.Ready,
        MachineOperationStatus.Producing => MachineUiStatus.Producing,
        MachineOperationStatus.WaitingForMaterial => MachineUiStatus.WaitingForMaterials,
        MachineOperationStatus.WaitingForEnergy => MachineUiStatus.WaitingForEnergy,
        MachineOperationStatus.OutputFull => MachineUiStatus.OutputFull,
        _ => MachineUiStatus.Blocked,
    };

    private static string GetStatusDetail(MachineOperationStatus status) => status switch
    {
        MachineOperationStatus.UnderConstruction => "Die Maschine wird aufgebaut.",
        MachineOperationStatus.Disabled => "Startschalter ist ausgeschaltet.",
        MachineOperationStatus.NoRecipeSelected => "Wähle zuerst ein Rezept.",
        MachineOperationStatus.Ready => "Bereit für den nächsten Zyklus.",
        MachineOperationStatus.Producing => "Produktionszyklus läuft.",
        MachineOperationStatus.WaitingForMaterial => "Eingabematerialien fehlen.",
        MachineOperationStatus.WaitingForEnergy => "Das lokale Stromnetz liefert zu wenig Leistung.",
        MachineOperationStatus.OutputFull => "Der Ausgabespeicher ist voll.",
        _ => "Maschine ist blockiert.",
    };

    private static string GetFunctionSummary(MachineDefinition definition) => definition.Kind switch
    {
        MachineKind.Generator => $"Erzeugt {definition.GeneratedPowerKilowatts:0} kW im lokalen Netz",
        MachineKind.Storage => $"Lagert bis zu {ProductionConfiguration.StorageContainerSlotCount} Stapel",
        MachineKind.Research => "Schaltet Maschinen und Rezepte frei",
        _ => "Verarbeitet Materialien nach auswählbaren Rezepten",
    };

    private static MachineSimulationFingerprint CreateSimulationFingerprint(MachineState state) => new(
        state.Status,
        state.ConstructionProgressSeconds,
        state.ProductionProgressSeconds,
        state.GeneratorFuelSecondsRemaining,
        state.InputInventory.TotalItemCount,
        state.OutputInventory.TotalItemCount);

    private ResearchSimulationFingerprint CreateResearchFingerprint() => new(
        _research.ActiveResearchId,
        _research.ProgressSeconds,
        _research.IsEnabled,
        _research.Status,
        _research.CompletedResearch.Count);

    private readonly record struct MachineSimulationFingerprint(
        MachineOperationStatus Status,
        double ConstructionProgress,
        double ProductionProgress,
        double GeneratorFuel,
        int InputItems,
        int OutputItems);

    private readonly record struct ResearchSimulationFingerprint(
        ResearchId? ActiveResearch,
        double Progress,
        bool Enabled,
        ResearchStatus Status,
        int CompletedCount);
}

using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Power;

public sealed record MachinePowerAllocation(
    MachineInstanceId MachineId,
    double RequestedKilowatts,
    double AllocatedKilowatts,
    MachineTickResult TickResult);

public sealed record PowerSourceAllocation(
    string SourceId,
    double AvailableKilowatts,
    double SuppliedKilowatts,
    double ConsumedFuel,
    bool IsExternal,
    bool IsStorage = false);

public sealed record PowerStorageAllocation(
    MachineInstanceId MachineId,
    double ChargeInputKilowatts,
    double StoredKilowatts,
    double DischargedKilowatts,
    double StoredEnergyKilowattSeconds);

public sealed record PowerNetworkForecast(
    double MaximumCapacityKilowatts,
    double RequestedPowerKilowatts,
    int ActiveSourceCount,
    double StorageChargeCapacityKilowatts = 0,
    double StorageDischargeCapacityKilowatts = 0);

public sealed record PowerNetworkTickResult(
    double AvailablePowerKilowatts,
    double RequestedPowerKilowatts,
    double ConsumedEnergyKilowattSeconds,
    IReadOnlyList<MachinePowerAllocation> MachineAllocations,
    double ExternalAllocatedKilowatts = 0,
    IReadOnlyList<PowerSourceAllocation>? SourceAllocations = null,
    double ConsumedFuel = 0,
    IReadOnlyList<PowerStorageAllocation>? StorageAllocations = null);

public sealed class LocalCometPowerNetwork
{
    private const double Epsilon = 0.000_001;
    private readonly List<MachineState> _machines = [];

    public LocalCometPowerNetwork(string cometId)
    {
        if (string.IsNullOrWhiteSpace(cometId))
        {
            throw new ArgumentException("A power network needs a comet ID.", nameof(cometId));
        }

        CometId = cometId;
    }

    public string CometId { get; }

    public IReadOnlyList<MachineState> Machines => _machines;

    public bool AddMachine(MachineState machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (machine.Placement is not null && !string.Equals(machine.Placement.CometId, CometId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A machine can only join the power network of its own comet.", nameof(machine));
        }

        if (_machines.Any(existing => existing.InstanceId == machine.InstanceId))
        {
            return false;
        }

        _machines.Add(machine);
        return true;
    }

    public bool RemoveMachine(MachineInstanceId machineId) =>
        _machines.RemoveAll(machine => machine.InstanceId == machineId) > 0;

    public PowerNetworkForecast Forecast(
        double deltaSeconds,
        RecipeCatalog recipes,
        double externalRequestedPowerKilowatts = 0,
        IReadOnlyCollection<IPowerGridExternalSource>? externalSources = null)
    {
        ValidateTick(deltaSeconds, externalRequestedPowerKilowatts, recipes);
        var generators = GetGeneratorCapacities(deltaSeconds);
        var externalCapacities = GetExternalCapacities(deltaSeconds, externalSources);
        var storage = GetStorageCapacities(deltaSeconds);
        var consumers = GetConsumerDemands(recipes);
        return new PowerNetworkForecast(
            generators.Sum(generator => generator.AvailableKilowatts) +
            externalCapacities.Sum(source => source.AvailableKilowatts) +
            storage.Sum(battery => battery.DischargeCapacityKilowatts),
            consumers.Sum(consumer => consumer.RequestedKilowatts) + externalRequestedPowerKilowatts,
            generators.Count(generator => generator.AvailableKilowatts > Epsilon) +
            externalCapacities.Count(source => source.AvailableKilowatts > Epsilon) +
            storage.Count(battery => battery.DischargeCapacityKilowatts > Epsilon),
            storage.Sum(battery => battery.ChargeInputCapacityKilowatts),
            storage.Sum(battery => battery.DischargeCapacityKilowatts));
    }

    public PowerNetworkTickResult Tick(
        double deltaSeconds,
        RecipeCatalog recipes,
        double externalRequestedPowerKilowatts = 0) =>
        Tick(
            deltaSeconds,
            recipes,
            externalRequestedPowerKilowatts,
            Array.Empty<IPowerGridExternalSource>(),
            allowPowerDelivery: true);

    public PowerNetworkTickResult Tick(
        double deltaSeconds,
        RecipeCatalog recipes,
        double externalRequestedPowerKilowatts,
        IReadOnlyCollection<IPowerGridExternalSource> externalSources,
        bool allowPowerDelivery)
    {
        ArgumentNullException.ThrowIfNull(externalSources);
        ValidateTick(deltaSeconds, externalRequestedPowerKilowatts, recipes);

        var generators = GetGeneratorCapacities(deltaSeconds);
        var externalCapacities = GetExternalCapacities(deltaSeconds, externalSources);
        var storage = GetStorageCapacities(deltaSeconds);
        var primaryAvailableEnergy = (
            generators.Sum(generator => generator.AvailableKilowatts) +
            externalCapacities.Sum(source => source.AvailableKilowatts)) * deltaSeconds;
        var storageAvailableEnergy = storage.Sum(battery => battery.DischargeCapacityKilowatts) * deltaSeconds;
        var availablePower = (primaryAvailableEnergy + storageAvailableEnergy) / deltaSeconds;
        var consumers = GetConsumerDemands(recipes);
        var requestedPower = consumers.Sum(consumer => consumer.RequestedKilowatts) +
                             externalRequestedPowerKilowatts;
        var remainingPower = allowPowerDelivery ? availablePower : 0;
        var allocations = new List<MachinePowerAllocation>(consumers.Length);
        var loadEnergy = 0.0;

        foreach (var consumer in consumers)
        {
            var allocated = consumer.RequestedKilowatts > Epsilon &&
                            remainingPower + Epsilon >= consumer.RequestedKilowatts
                ? consumer.RequestedKilowatts
                : 0;
            remainingPower -= allocated;
            var tick = consumer.Machine.TickProduction(consumer.Recipe, deltaSeconds, allocated);
            loadEnergy += tick.EnergyConsumedKilowattSeconds;
            allocations.Add(new MachinePowerAllocation(
                consumer.Machine.InstanceId,
                consumer.RequestedKilowatts,
                allocated,
                tick));
        }

        var externalAllocated = externalRequestedPowerKilowatts > Epsilon &&
                                remainingPower + Epsilon >= externalRequestedPowerKilowatts
            ? externalRequestedPowerKilowatts
            : 0;
        remainingPower -= externalAllocated;
        loadEnergy += externalAllocated * deltaSeconds;

        var storageDischargeRequired = allowPowerDelivery
            ? Math.Max(0, loadEnergy - primaryAvailableEnergy)
            : 0;
        var storageChargeInput = allowPowerDelivery && storageDischargeRequired <= Epsilon
            ? Math.Min(
                Math.Max(0, primaryAvailableEnergy - loadEnergy),
                storage.Sum(battery => battery.ChargeInputCapacityKilowatts) * deltaSeconds)
            : 0;
        var primaryEnergyToProvide = allowPowerDelivery
            ? Math.Min(primaryAvailableEnergy, loadEnergy) + storageChargeInput
            : 0;

        var sourceAllocations = new List<PowerSourceAllocation>(
            generators.Length + externalCapacities.Length + storage.Length);
        var primaryEnergyStillToProvide = primaryEnergyToProvide;
        foreach (var generator in generators)
        {
            var availableEnergy = generator.AvailableKilowatts * deltaSeconds;
            var providedEnergy = Math.Min(availableEnergy, primaryEnergyStillToProvide);
            generator.Machine.ConsumeGeneratedEnergy(providedEnergy);
            generator.Machine.SetGeneratorOperatingStatus(providedEnergy / deltaSeconds);
            primaryEnergyStillToProvide -= providedEnergy;
            sourceAllocations.Add(new PowerSourceAllocation(
                generator.Machine.InstanceId.Value,
                generator.AvailableKilowatts,
                providedEnergy / deltaSeconds,
                0,
                IsExternal: false));
        }

        foreach (var external in externalCapacities)
        {
            var availableEnergy = external.AvailableKilowatts * deltaSeconds;
            var requestedEnergy = Math.Min(availableEnergy, primaryEnergyStillToProvide);
            var delivery = external.Source.DeliverEnergy(requestedEnergy);
            if (!delivery.FullyDelivered)
            {
                throw new InvalidOperationException(
                    $"External power source '{external.Source.SourceId}' did not deliver its reserved energy.");
            }

            primaryEnergyStillToProvide -= delivery.DeliveredEnergyKilowattSeconds;
            sourceAllocations.Add(new PowerSourceAllocation(
                external.Source.SourceId,
                external.AvailableKilowatts,
                delivery.DeliveredEnergyKilowattSeconds / deltaSeconds,
                delivery.ConsumedFuel,
                IsExternal: true));
        }

        if (primaryEnergyStillToProvide > Epsilon)
        {
            throw new InvalidOperationException("The power network reserved primary energy that its sources could not provide.");
        }

        var dischargedByMachine = new Dictionary<MachineInstanceId, double>();
        var dischargeStillRequired = storageDischargeRequired;
        foreach (var battery in storage)
        {
            var requested = Math.Min(
                battery.DischargeCapacityKilowatts * deltaSeconds,
                dischargeStillRequired);
            var provided = allowPowerDelivery
                ? battery.Machine.ProvideStoredGridEnergy(requested)
                : 0;
            dischargedByMachine[battery.Machine.InstanceId] = provided;
            dischargeStillRequired -= provided;
            sourceAllocations.Add(new PowerSourceAllocation(
                battery.Machine.InstanceId.Value,
                battery.DischargeCapacityKilowatts,
                provided / deltaSeconds,
                0,
                IsExternal: false,
                IsStorage: true));
        }

        if (dischargeStillRequired > Epsilon)
        {
            throw new InvalidOperationException("The power network reserved stored energy that its batteries could not provide.");
        }

        var storageAllocations = new List<PowerStorageAllocation>(storage.Length);
        var chargeInputStillAvailable = storageChargeInput;
        foreach (var battery in storage)
        {
            var chargeInput = Math.Min(
                battery.ChargeInputCapacityKilowatts * deltaSeconds,
                chargeInputStillAvailable);
            var storedEnergy = allowPowerDelivery
                ? battery.Machine.StoreGridEnergy(chargeInput)
                : 0;
            chargeInputStillAvailable -= chargeInput;
            storageAllocations.Add(new PowerStorageAllocation(
                battery.Machine.InstanceId,
                chargeInput / deltaSeconds,
                storedEnergy / deltaSeconds,
                dischargedByMachine.GetValueOrDefault(battery.Machine.InstanceId) / deltaSeconds,
                battery.Machine.StoredGridEnergyKilowattSeconds));
        }

        if (chargeInputStillAvailable > Epsilon)
        {
            throw new InvalidOperationException("The power network reserved charging energy that its batteries could not accept.");
        }

        var consumedEnergy = loadEnergy + storageChargeInput;

        return new PowerNetworkTickResult(
            availablePower,
            requestedPower,
            consumedEnergy,
            allocations,
            externalAllocated,
            sourceAllocations,
            sourceAllocations.Sum(source => source.ConsumedFuel),
            storageAllocations);
    }

    private GeneratorCapacity[] GetGeneratorCapacities(double deltaSeconds) => _machines
        .Where(machine => machine.Definition.Kind == MachineKind.Generator)
        .OrderBy(machine => machine.Definition.IsFuelledGenerator)
        .ThenBy(machine => machine.InstanceId.Value, StringComparer.Ordinal)
        .Select(machine => new GeneratorCapacity(machine, machine.GetAvailableGenerationKilowatts(deltaSeconds)))
        .ToArray();

    private BatteryCapacity[] GetStorageCapacities(double deltaSeconds) => _machines
        .Where(machine => machine.Definition.Id == MachineDefinitionIds.BatteryBank &&
                          machine.IsConstructionComplete &&
                          machine.IsEnabled)
        .OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal)
        .Select(machine =>
        {
            var remainingCapacity = Math.Max(
                0,
                MachineEnergyConfiguration.BatteryBankCapacityKilowattSeconds -
                machine.StoredGridEnergyKilowattSeconds);
            var maximumChargeInput = Math.Min(
                MachineEnergyConfiguration.BatteryBankMaximumChargeKilowatts,
                remainingCapacity / machine.Definition.EfficiencyMultiplier / deltaSeconds);
            var maximumDischarge = Math.Min(
                MachineEnergyConfiguration.BatteryBankMaximumDischargeKilowatts,
                machine.StoredGridEnergyKilowattSeconds / deltaSeconds);
            return new BatteryCapacity(machine, maximumChargeInput, maximumDischarge);
        })
        .ToArray();

    private ConsumerDemand[] GetConsumerDemands(RecipeCatalog recipes)
    {
        var consumers = new List<ConsumerDemand>();
        foreach (var machine in _machines
                     .Where(machine => machine.Definition.Kind == MachineKind.Production)
                     .OrderBy(machine => machine.InstanceId.Value, StringComparer.Ordinal))
        {
            if (machine.SelectedRecipeId is not { } recipeId)
            {
                continue;
            }

            var recipe = recipes.Get(recipeId);
            consumers.Add(new ConsumerDemand(machine, recipe, machine.GetRequestedPowerKilowatts(recipe)));
        }

        return consumers.ToArray();
    }

    private static ExternalSourceCapacity[] GetExternalCapacities(
        double deltaSeconds,
        IReadOnlyCollection<IPowerGridExternalSource>? sources)
    {
        if (sources is null || sources.Count == 0)
        {
            return [];
        }

        var remainingEnergyByPool = new Dictionary<string, double>(StringComparer.Ordinal);
        var capacities = new List<ExternalSourceCapacity>(sources.Count);
        foreach (var source in sources.OrderBy(source => source.SourceId, StringComparer.Ordinal))
        {
            if (!source.IsEnabled)
            {
                capacities.Add(new ExternalSourceCapacity(source, 0));
                continue;
            }

            if (!remainingEnergyByPool.TryGetValue(source.ResourcePoolId, out var remainingPoolEnergy))
            {
                remainingPoolEnergy = source.GetAvailableResourceEnergyKilowattSeconds();
            }

            var availableEnergy = Math.Min(
                source.RatedCapacityKilowatts * deltaSeconds,
                remainingPoolEnergy);
            remainingEnergyByPool[source.ResourcePoolId] = Math.Max(0, remainingPoolEnergy - availableEnergy);
            capacities.Add(new ExternalSourceCapacity(source, availableEnergy / deltaSeconds));
        }

        return capacities.ToArray();
    }

    private static void ValidateTick(
        double deltaSeconds,
        double externalRequestedPowerKilowatts,
        RecipeCatalog recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0 ||
            !double.IsFinite(externalRequestedPowerKilowatts) || externalRequestedPowerKilowatts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }
    }

    private sealed record GeneratorCapacity(MachineState Machine, double AvailableKilowatts);

    private sealed record BatteryCapacity(
        MachineState Machine,
        double ChargeInputCapacityKilowatts,
        double DischargeCapacityKilowatts);

    private sealed record ExternalSourceCapacity(IPowerGridExternalSource Source, double AvailableKilowatts);

    private sealed record ConsumerDemand(
        MachineState Machine,
        RecipeDefinition Recipe,
        double RequestedKilowatts);
}

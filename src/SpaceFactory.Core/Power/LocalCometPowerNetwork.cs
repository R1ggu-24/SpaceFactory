using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Power;

public sealed record MachinePowerAllocation(
    MachineInstanceId MachineId,
    double RequestedKilowatts,
    double AllocatedKilowatts,
    MachineTickResult TickResult);

public sealed record PowerNetworkTickResult(
    double AvailablePowerKilowatts,
    double RequestedPowerKilowatts,
    double ConsumedEnergyKilowattSeconds,
    IReadOnlyList<MachinePowerAllocation> MachineAllocations,
    double ExternalAllocatedKilowatts = 0);

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

    public PowerNetworkTickResult Tick(
        double deltaSeconds,
        RecipeCatalog recipes,
        double externalRequestedPowerKilowatts = 0)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0 ||
            !double.IsFinite(externalRequestedPowerKilowatts) || externalRequestedPowerKilowatts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var generators = _machines
            .Where(machine => machine.Definition.Kind == MachineKind.Generator)
            .Select(machine => new GeneratorCapacity(machine, machine.GetAvailableGenerationKilowatts(deltaSeconds)))
            .ToArray();
        var availablePower = generators.Sum(generator => generator.AvailableKilowatts);

        var consumers = new List<ConsumerDemand>();
        foreach (var machine in _machines.Where(machine => machine.Definition.Kind == MachineKind.Production))
        {
            if (machine.SelectedRecipeId is not { } recipeId)
            {
                continue;
            }

            var recipe = recipes.Get(recipeId);
            consumers.Add(new ConsumerDemand(machine, recipe, machine.GetRequestedPowerKilowatts(recipe)));
        }

        var requestedPower = consumers.Sum(consumer => consumer.RequestedKilowatts) +
                             externalRequestedPowerKilowatts;
        var remainingPower = availablePower;
        var allocations = new List<MachinePowerAllocation>(consumers.Count);
        var consumedEnergy = 0.0;

        foreach (var consumer in consumers)
        {
            var allocated = consumer.RequestedKilowatts > Epsilon &&
                            remainingPower + Epsilon >= consumer.RequestedKilowatts
                ? consumer.RequestedKilowatts
                : 0;
            remainingPower -= allocated;
            var tick = consumer.Machine.TickProduction(consumer.Recipe, deltaSeconds, allocated);
            consumedEnergy += tick.EnergyConsumedKilowattSeconds;
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
        consumedEnergy += externalAllocated * deltaSeconds;

        var energyStillToProvide = consumedEnergy;
        foreach (var generator in generators)
        {
            var availableEnergy = generator.AvailableKilowatts * deltaSeconds;
            var providedEnergy = Math.Min(availableEnergy, energyStillToProvide);
            generator.Machine.ConsumeGeneratedEnergy(providedEnergy);
            generator.Machine.SetGeneratorOperatingStatus(providedEnergy / deltaSeconds);
            energyStillToProvide -= providedEnergy;
        }

        if (energyStillToProvide > Epsilon)
        {
            throw new InvalidOperationException("The power network allocated energy that its generators could not provide.");
        }

        return new PowerNetworkTickResult(
            availablePower,
            requestedPower,
            consumedEnergy,
            allocations,
            externalAllocated);
    }

    private sealed record GeneratorCapacity(MachineState Machine, double AvailableKilowatts);

    private sealed record ConsumerDemand(
        MachineState Machine,
        RecipeDefinition Recipe,
        double RequestedKilowatts);
}

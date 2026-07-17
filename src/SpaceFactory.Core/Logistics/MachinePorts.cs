using SpaceFactory.Core.Items;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Logistics;

public enum MachinePortDirection
{
    Input,
    Output,
    Bidirectional,
}

public enum MachinePortInventorySide
{
    None,
    Input,
    Output,
}

public sealed record MachinePortDefinition(
    MachineDefinitionId MachineDefinitionId,
    MachinePortId Id,
    TransportMedium Medium,
    MachinePortDirection Direction,
    MachinePortInventorySide InventorySide,
    int MaximumConnections,
    IReadOnlyCollection<ItemId>? AllowedItemIds = null)
{
    public bool CanSend => Direction is MachinePortDirection.Output or MachinePortDirection.Bidirectional;

    public bool CanReceive => Direction is MachinePortDirection.Input or MachinePortDirection.Bidirectional;

    public bool Accepts(ItemId itemId) => AllowedItemIds is null || AllowedItemIds.Contains(itemId);

    public void Validate()
    {
        if (MaximumConnections <= 0 ||
            (Medium == TransportMedium.Power && InventorySide != MachinePortInventorySide.None) ||
            (Medium != TransportMedium.Power && InventorySide == MachinePortInventorySide.None) ||
            (AllowedItemIds is not null && AllowedItemIds.Count == 0) ||
            (AllowedItemIds?.Distinct().Count() != AllowedItemIds?.Count))
        {
            throw new ArgumentException($"Machine port '{MachineDefinitionId}/{Id}' is invalid.");
        }
    }
}

public sealed class MachinePortCatalog
{
    private readonly IReadOnlyDictionary<(MachineDefinitionId MachineId, MachinePortId PortId), MachinePortDefinition>
        _definitions;

    public MachinePortCatalog(IEnumerable<MachinePortDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        foreach (var definition in materialized)
        {
            definition.Validate();
        }

        _definitions = materialized.ToDictionary(
            definition => (definition.MachineDefinitionId, definition.Id));
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Machine port IDs must be unique per machine definition.", nameof(definitions));
        }
    }

    public IReadOnlyCollection<MachinePortDefinition> All => _definitions.Values.ToArray();

    public IReadOnlyList<MachinePortDefinition> ForMachine(MachineDefinitionId machineId) => _definitions.Values
        .Where(definition => definition.MachineDefinitionId == machineId)
        .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public MachinePortDefinition Get(MachineDefinitionId machineId, MachinePortId portId) =>
        _definitions.TryGetValue((machineId, portId), out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown machine port '{machineId}/{portId}'.");

    public bool TryGet(
        MachineDefinitionId machineId,
        MachinePortId portId,
        out MachinePortDefinition? definition) =>
        _definitions.TryGetValue((machineId, portId), out definition);
}

public static class DefaultMachinePortCatalog
{
    public static MachinePortCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<MachinePortDefinition> CreateDefinitions()
    {
        var items = DefaultProductionItemCatalog.Instance;
        var recipes = DefaultRecipeCatalog.Instance;

        foreach (var machine in DefaultMachineCatalog.Instance.All)
        {
            if (machine.Id == MachineDefinitionIds.PowerPole)
            {
                foreach (var portId in MachinePortIds.PowerPolePorts)
                {
                    yield return new MachinePortDefinition(
                        machine.Id,
                        portId,
                        TransportMedium.Power,
                        MachinePortDirection.Bidirectional,
                        MachinePortInventorySide.None,
                        PowerGridConfiguration.MaximumCablesPerPort);
                }

                continue;
            }

            yield return new MachinePortDefinition(
                machine.Id,
                MachinePortIds.Power,
                TransportMedium.Power,
                MachinePortDirection.Bidirectional,
                MachinePortInventorySide.None,
                LogisticsConfiguration.MaximumPowerConnectionsPerPort);

            if (machine.Kind == MachineKind.Storage)
            {
                foreach (var phase in Enum.GetValues<ProductionItemPhase>())
                {
                    yield return MaterialPort(
                        machine.Id,
                        MachinePortIds.StorageFor(phase),
                        phase,
                        MachinePortDirection.Bidirectional,
                        MachinePortInventorySide.Input,
                        null);
                }

                continue;
            }

            if (machine.Kind == MachineKind.Research)
            {
                yield return MaterialPort(
                    machine.Id,
                    MachinePortIds.SolidInput,
                    ProductionItemPhase.Solid,
                    MachinePortDirection.Input,
                    MachinePortInventorySide.Input,
                    null);
                continue;
            }

            if (machine.Kind == MachineKind.Generator)
            {
                if (machine.GeneratorFuelItemId is { } fuelItemId)
                {
                    yield return MaterialPort(
                        machine.Id,
                        MachinePortIds.InputFor(items.Get(fuelItemId).Phase),
                        items.Get(fuelItemId).Phase,
                        MachinePortDirection.Input,
                        MachinePortInventorySide.Input,
                        [fuelItemId]);
                }

                if (machine.GeneratorReturnedContainerItemId is { } returnedItemId)
                {
                    yield return MaterialPort(
                        machine.Id,
                        MachinePortIds.OutputFor(items.Get(returnedItemId).Phase),
                        items.Get(returnedItemId).Phase,
                        MachinePortDirection.Output,
                        MachinePortInventorySide.Output,
                        [returnedItemId]);
                }

                continue;
            }

            var machineRecipes = recipes.ForMachine(machine.Id);
            var inputItems = machineRecipes
                .SelectMany(recipe => recipe.Inputs)
                .Select(input => input.ItemId)
                .Distinct()
                .ToArray();
            foreach (var group in inputItems.GroupBy(itemId => items.Get(itemId).Phase).OrderBy(group => group.Key))
            {
                yield return MaterialPort(
                    machine.Id,
                    MachinePortIds.InputFor(group.Key),
                    group.Key,
                    MachinePortDirection.Input,
                    MachinePortInventorySide.Input,
                    group);
            }

            var outputItems = machineRecipes
                .SelectMany(recipe => recipe.CombinedOutputs)
                .Select(output => output.ItemId)
                .Distinct()
                .ToArray();
            foreach (var group in outputItems.GroupBy(itemId => items.Get(itemId).Phase).OrderBy(group => group.Key))
            {
                yield return MaterialPort(
                    machine.Id,
                    MachinePortIds.OutputFor(group.Key),
                    group.Key,
                    MachinePortDirection.Output,
                    MachinePortInventorySide.Output,
                    group);
            }
        }
    }

    private static MachinePortDefinition MaterialPort(
        MachineDefinitionId machineId,
        MachinePortId portId,
        ProductionItemPhase phase,
        MachinePortDirection direction,
        MachinePortInventorySide inventorySide,
        IEnumerable<ItemId>? allowedItemIds) =>
        new(
            machineId,
            portId,
            ToMedium(phase),
            direction,
            inventorySide,
            LogisticsConfiguration.MaximumMaterialConnectionsPerPort,
            allowedItemIds?.Distinct().ToArray());

    private static TransportMedium ToMedium(ProductionItemPhase phase) => phase switch
    {
        ProductionItemPhase.Solid => TransportMedium.Solid,
        ProductionItemPhase.Liquid => TransportMedium.Liquid,
        ProductionItemPhase.Gas => TransportMedium.Gas,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };
}

public readonly record struct MachineConnectionEndpoint(
    MachineInstanceId MachineId,
    MachinePortId PortId);

using SpaceFactory.Core.Research;
using SpaceFactory.Core.Power;

namespace SpaceFactory.Core.Production;

public sealed class MachineCatalog
{
    private readonly IReadOnlyDictionary<MachineDefinitionId, MachineDefinition> _definitions;

    public MachineCatalog(IEnumerable<MachineDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        _definitions = materialized.ToDictionary(definition => definition.Id);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Machine definition IDs must be unique.", nameof(definitions));
        }
    }

    public IReadOnlyCollection<MachineDefinition> All => _definitions.Values.ToArray();

    public MachineDefinition Get(MachineDefinitionId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown machine definition '{id}'.");

    public bool TryGet(MachineDefinitionId id, out MachineDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);
}

public static class DefaultMachineCatalog
{
    public static MachineCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<MachineDefinition> CreateDefinitions()
    {
        yield return Production(
            MachineDefinitionIds.Crusher,
            "Zerkleinerer",
            "Zerkleinert Erze und Gestein für die effiziente Weiterverarbeitung.",
            MachineCategory.Processing,
            [(ProductionItemIds.IronOre, 14), (ProductionItemIds.CopperOre, 6), (ProductionItemIds.SilicateRock, 6)]);
        yield return Production(
            MachineDefinitionIds.Smelter,
            "Schmelzer",
            "Schmilzt rohe und zerkleinerte Erze zu Metallbarren.",
            MachineCategory.Processing,
            [(ProductionItemIds.IronOre, 16), (ProductionItemIds.CopperOre, 6), (ProductionItemIds.SilicateRock, 8)]);
        yield return Production(
            MachineDefinitionIds.Foundry,
            "Giesserei",
            "Verbindet Metalle zu widerstandsfähigen Legierungen.",
            MachineCategory.Processing,
            [(ProductionItemIds.IronIngot, 12), (ProductionItemIds.CopperIngot, 8), (ProductionItemIds.IronPlate, 4)],
            unlock: DefaultResearchIds.AdvancedMetallurgy);
        yield return Production(
            MachineDefinitionIds.Refinery,
            "Raffinerie",
            "Reinigt Zwischenprodukte und stellt Treibstoff her.",
            MachineCategory.Processing,
            [(ProductionItemIds.SteelBeam, 10), (ProductionItemIds.CopperCable, 12), (ProductionItemIds.GoldIngot, 3)],
            unlock: DefaultResearchIds.FuelProduction);
        yield return Production(
            MachineDefinitionIds.WaterProcessor,
            "Wasseraufbereiter",
            "Schmilzt Wassereis und füllt gereinigtes Wasser ab.",
            MachineCategory.Processing,
            [(ProductionItemIds.IronPlate, 8), (ProductionItemIds.IronPipe, 6), (ProductionItemIds.CopperWire, 5)]);
        yield return Production(
            MachineDefinitionIds.Electrolyzer,
            "Elektrolyseur",
            "Spaltet Wasser in Wasserstoff und Sauerstoff.",
            MachineCategory.Processing,
            [(ProductionItemIds.SteelPipe, 10), (ProductionItemIds.CopperCable, 10), (ProductionItemIds.GoldIngot, 2)],
            unlock: DefaultResearchIds.HydrogenTechnology);
        yield return Production(
            MachineDefinitionIds.Constructor,
            "Konstruktor",
            "Fertigt einfache Platten, Drähte, Rohre und Behälter.",
            MachineCategory.Manufacturing,
            [(ProductionItemIds.IronIngot, 8), (ProductionItemIds.CopperIngot, 4), (ProductionItemIds.SilicateRock, 6)]);
        yield return new MachineDefinition(
            MachineDefinitionIds.Fabricator,
            "Fabrikator",
            "Fertigt komplexe Bauteile aus bis zu drei unterschiedlichen Zutaten.",
            MachineCategory.Manufacturing,
            MachineKind.Production,
            Costs([(ProductionItemIds.SteelBeam, 12), (ProductionItemIds.CopperCable, 14), (ProductionItemIds.GoldIngot, 4)]),
            ProductionConfiguration.FabricatorInputSlotCount,
            ProductionConfiguration.StandardOutputSlotCount,
            1.8,
            unlockRequirement: DefaultResearchIds.AdvancedElectronics);
        yield return new MachineDefinition(
            MachineDefinitionIds.BasicGenerator,
            "Basisgenerator",
            "Erzeugt eine kleine, verlässliche Startleistung ohne Treibstoff.",
            MachineCategory.Energy,
            MachineKind.Generator,
            Costs([(ProductionItemIds.IronPlate, 10), (ProductionItemIds.CopperWire, 10), (ProductionItemIds.IronRod, 6)]),
            1,
            1,
            1.2,
            generatedPowerKilowatts: ProductionConfiguration.BasicGeneratorPowerKilowatts);
        yield return new MachineDefinition(
            MachineDefinitionIds.FuelGenerator,
            "Treibstoffgenerator",
            "Erzeugt viel Leistung aus gefüllten Treibstoffbehältern.",
            MachineCategory.Energy,
            MachineKind.Generator,
            Costs([(ProductionItemIds.SteelBeam, 16), (ProductionItemIds.CopperCable, 14), (ProductionItemIds.Motor, 4)]),
            3,
            3,
            1.8,
            generatedPowerKilowatts: ProductionConfiguration.FuelGeneratorPowerKilowatts,
            generatorFuelItemId: ProductionItemIds.FuelContainer,
            generatorReturnedContainerItemId: ProductionItemIds.EmptyFuelContainer,
            generatorFuelSecondsPerItem: ProductionConfiguration.FuelGeneratorSecondsPerContainer,
            unlockRequirement: DefaultResearchIds.ImprovedEnergySupply);
        yield return new MachineDefinition(
            MachineDefinitionIds.StorageContainer,
            "Lagercontainer",
            "Lagert Rohstoffe, Zwischenprodukte, Bauteile und Behälter.",
            MachineCategory.Storage,
            MachineKind.Storage,
            Costs([(ProductionItemIds.IronPlate, 12), (ProductionItemIds.IronRod, 6)]),
            ProductionConfiguration.StorageContainerSlotCount,
            1,
            1.2);
        yield return new MachineDefinition(
            MachineDefinitionIds.ResearchStation,
            "Forschungsstation",
            "Verbraucht Materialien und Energie, um neue Technologien freizuschalten.",
            MachineCategory.Research,
            MachineKind.Research,
            Costs([(ProductionItemIds.IronPlate, 12), (ProductionItemIds.CopperCable, 10), (ProductionItemIds.GoldIngot, 2)]),
            6,
            2,
            1.8);
        yield return new MachineDefinition(
            MachineDefinitionIds.PowerPole,
            "Strommast",
            "Verbindet bis zu sechs Stromkabel zu einem gemeinsamen lokalen Netz.",
            MachineCategory.Energy,
            MachineKind.Infrastructure,
            PowerGridConfiguration.PowerPoleBuildCosts,
            1,
            1,
            PowerGridConfiguration.PowerPoleConstructionDurationSeconds);
    }

    private static MachineDefinition Production(
        MachineDefinitionId id,
        string name,
        string description,
        MachineCategory category,
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs,
        ResearchId? unlock = null) =>
        new(
            id,
            name,
            description,
            category,
            MachineKind.Production,
            Costs(costs),
            ProductionConfiguration.StandardInputSlotCount,
            ProductionConfiguration.StandardOutputSlotCount,
            1.5,
            unlockRequirement: unlock);

    private static IReadOnlyList<ItemAmount> Costs(
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs) =>
        costs.Select(cost => new ItemAmount(cost.Id, cost.Amount)).ToArray();

}

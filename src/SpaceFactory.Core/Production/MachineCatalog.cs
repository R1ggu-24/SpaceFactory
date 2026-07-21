using SpaceFactory.Core.Research;
using SpaceFactory.Core.Power;

namespace SpaceFactory.Core.Production;

public sealed class MachineCatalog
{
    private readonly IReadOnlyDictionary<MachineDefinitionId, MachineDefinition> _definitions;
    private readonly IReadOnlyDictionary<SpaceFactory.Core.Items.ItemId, MachineDefinition> _definitionsByPlacementItem;

    public MachineCatalog(IEnumerable<MachineDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        _definitions = materialized.ToDictionary(definition => definition.Id);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Machine definition IDs must be unique.", nameof(definitions));
        }

        var itemPlacedDefinitions = materialized
            .Where(definition => definition.PlacementItemId is not null)
            .ToArray();
        _definitionsByPlacementItem = itemPlacedDefinitions.ToDictionary(
            definition => definition.PlacementItemId!.Value);
        if (_definitionsByPlacementItem.Count != itemPlacedDefinitions.Length)
        {
            throw new ArgumentException(
                "Physical machine placement items must map to exactly one machine definition.",
                nameof(definitions));
        }
    }

    public IReadOnlyCollection<MachineDefinition> All => _definitions.Values.ToArray();

    public IReadOnlyCollection<MachineDefinition> DirectBuildMenuEntries => _definitions.Values
        .Where(definition => definition.IsDirectBuildMenuEntry)
        .ToArray();

    public MachineDefinition Get(MachineDefinitionId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown machine definition '{id}'.");

    public bool TryGet(MachineDefinitionId id, out MachineDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);

    public bool TryGetByPlacementItem(
        SpaceFactory.Core.Items.ItemId itemId,
        out MachineDefinition? definition) =>
        _definitionsByPlacementItem.TryGetValue(itemId, out definition);
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
        yield return new MachineDefinition(
            MachineDefinitionIds.Workbench,
            "Werkbank",
            "Fertigt Abbauwerkzeuge und frühe mobile Bergbauausrüstung.",
            MachineCategory.Manufacturing,
            MachineKind.Production,
            Costs([
                (ProductionItemIds.IronPlate, 6),
                (ProductionItemIds.CopperWire, 4),
                (ProductionItemIds.IronRod, 4),
            ]),
            ProductionConfiguration.StandardInputSlotCount,
            ProductionConfiguration.StandardOutputSlotCount,
            1.2,
            archetype: MachineArchetype.Assembly,
            placementRequirement: MachinePlacementRequirement.LargeOrHugeComet,
            technologyTier: TechnologyTier.Tier1);
        yield return ProfiledProduction(
            MachineDefinitionIds.MobileMiner,
            "Mobiler Miner",
            "Früher, autarker Miner mit internem Ausgabespeicher für eine feste Erzquelle.",
            MachineCategory.Processing,
            [(ProductionItemIds.MobileMinerKit, 1)],
            1,
            ProductionConfiguration.MobileMinerOutputSlotCount,
            1.2,
            MachineArchetype.Extractor,
            MachinePlacementRequirement.ResourceDeposit,
            TechnologyTier.Tier1,
            0.45,
            0.75,
            placementItemId: ProductionItemIds.MobileMinerKit,
            isDirectBuildMenuEntry: false);
        yield return ProfiledProduction(
            MachineDefinitionIds.AutomaticMiner,
            "Automatischer Miner",
            "Stromabhängiger Hochleistungsminer mit direktem Logistikausgang.",
            MachineCategory.Processing,
            [(ProductionItemIds.AutomaticMinerKit, 1)],
            1,
            ProductionConfiguration.AutomaticMinerOutputSlotCount,
            1.8,
            MachineArchetype.Extractor,
            MachinePlacementRequirement.ResourceDeposit,
            TechnologyTier.Tier3,
            1.6,
            1.15,
            placementItemId: ProductionItemIds.AutomaticMinerKit,
            isDirectBuildMenuEntry: false);
        yield return ProfiledProduction(
            MachineDefinitionIds.ChemicalPlant,
            "Chemieanlage",
            "Verarbeitet Flüssigkeiten, Gase und Feststoffe zu chemischen Grundstoffen.",
            MachineCategory.Processing,
            [(ProductionItemIds.SteelPlate, 14), (ProductionItemIds.Pump, 4), (ProductionItemIds.Valve, 6), (ProductionItemIds.CircuitBoard, 4)],
            8,
            6,
            2.4,
            MachineArchetype.ChemicalProcessing,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier4,
            1.0,
            0.92);
        yield return ProfiledProduction(
            MachineDefinitionIds.Assembler,
            "Montagemaschine",
            "Kombiniert Grundbauteile zu frühen Automatisierungsprodukten.",
            MachineCategory.Manufacturing,
            [(ProductionItemIds.IronPlate, 10), (ProductionItemIds.CopperWire, 8), (ProductionItemIds.Screws, 20)],
            6,
            4,
            1.6,
            MachineArchetype.Assembly,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier2,
            1.1,
            1.0);
        yield return ProfiledProduction(
            MachineDefinitionIds.AdvancedFabricator,
            "Erweiterter Fabrikator",
            "Verarbeitet vier Komponenten zu anspruchsvollen Industrie- und Raumschiffsystemen.",
            MachineCategory.Manufacturing,
            [(ProductionItemIds.ReinforcedPlate, 14), (ProductionItemIds.AutomationModule, 6), (ProductionItemIds.CircuitBoard, 8)],
            10,
            6,
            2.6,
            MachineArchetype.Assembly,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier5,
            1.35,
            1.08);
        yield return ProfiledProduction(
            MachineDefinitionIds.PrecisionManufacturer,
            "Präzisionshersteller",
            "Fertigt Chips, Sensoren und hochpräzise Weltraumkomponenten.",
            MachineCategory.Manufacturing,
            [(ProductionItemIds.TitaniumFrame, 8), (ProductionItemIds.Computer, 4), (ProductionItemIds.HighPerformanceMotor, 2)],
            10,
            6,
            3.0,
            MachineArchetype.PrecisionManufacturing,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier6,
            1.5,
            1.18);
        yield return StorageProfile(
            MachineDefinitionIds.LiquidTank,
            "Flüssigkeitstank",
            "Speichert große Mengen flüssiger Produktionsstoffe.",
            [(ProductionItemIds.SteelPlate, 16), (ProductionItemIds.PressureVessel, 2), (ProductionItemIds.Valve, 4)],
            ProductionConfiguration.FluidTankSlotCount,
            MachineArchetype.LiquidStorage,
            TechnologyTier.Tier4);
        yield return StorageProfile(
            MachineDefinitionIds.GasTank,
            "Gastank",
            "Speichert Gase in abgeschirmten Druckbehältern.",
            [(ProductionItemIds.SteelPlate, 18), (ProductionItemIds.PressureVessel, 3), (ProductionItemIds.Valve, 5)],
            ProductionConfiguration.FluidTankSlotCount,
            MachineArchetype.GasStorage,
            TechnologyTier.Tier4);
        yield return StorageProfile(
            MachineDefinitionIds.PumpStation,
            "Pumpstation",
            "Puffert und fördert Flüssigkeiten und Gase zwischen Rohrnetzen.",
            [(ProductionItemIds.Pump, 3), (ProductionItemIds.Valve, 4), (ProductionItemIds.PowerRegulator, 1)],
            8,
            MachineArchetype.FluidTransport,
            TechnologyTier.Tier4);
        yield return new MachineDefinition(
            MachineDefinitionIds.BatteryBank,
            "Batteriebank",
            "Speichert Netzenergie für Lastspitzen und kontrollierte Wiederabgabe.",
            MachineCategory.Energy,
            MachineKind.Infrastructure,
            Costs([(ProductionItemIds.HighPerformanceBattery, 4), (ProductionItemIds.PowerRegulator, 4), (ProductionItemIds.SteelPlate, 8)]),
            1,
            1,
            2.2,
            archetype: MachineArchetype.EnergyStorage,
            technologyTier: TechnologyTier.Tier5,
            efficiencyMultiplier: 0.94);
        yield return ProfiledProduction(
            MachineDefinitionIds.UraniumProcessor,
            "Uran-Aufbereitungsanlage",
            "Reinigt und reichert Uran unter hoher Abschirmung an.",
            MachineCategory.Processing,
            [(ProductionItemIds.TitaniumFrame, 10), (ProductionItemIds.CoolingSystem, 4), (ProductionItemIds.ControlModule, 4)],
            8,
            6,
            3.2,
            MachineArchetype.NuclearProcessing,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier7,
            0.9,
            0.82,
            0.78);
        yield return ProfiledProduction(
            MachineDefinitionIds.FuelCellFabricator,
            "Brennstoffzellen-Fabrikator",
            "Versiegelt angereichertes Uran in kontrollierten Brennstoffzellen.",
            MachineCategory.Manufacturing,
            [(ProductionItemIds.TitaniumFrame, 10), (ProductionItemIds.PrecisionComponent, 8), (ProductionItemIds.FactoryController, 2)],
            8,
            5,
            3.0,
            MachineArchetype.NuclearProcessing,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier8,
            0.9,
            0.9,
            0.9);
        yield return new MachineDefinition(
            MachineDefinitionIds.NuclearReactor,
            "Atomkraftwerk",
            "Erzeugt enorme Leistung und gibt verbrauchte Brennstoffzellen aus.",
            MachineCategory.Energy,
            MachineKind.Generator,
            Costs([(ProductionItemIds.TitaniumFrame, 24), (ProductionItemIds.CoolingSystem, 10), (ProductionItemIds.FactoryController, 4)]),
            4,
            4,
            4.0,
            generatedPowerKilowatts: ProductionConfiguration.NuclearReactorPowerKilowatts,
            generatorFuelItemId: ProductionItemIds.NuclearFuelCell,
            generatorReturnedContainerItemId: ProductionItemIds.SpentFuelCell,
            generatorFuelSecondsPerItem: ProductionConfiguration.NuclearFuelCellSeconds,
            archetype: MachineArchetype.PowerGeneration,
            technologyTier: TechnologyTier.Tier8,
            efficiencyMultiplier: 0.96,
            radiationShielding: 0.95);
        yield return ProfiledProduction(
            MachineDefinitionIds.WasteProcessor,
            "Abfallverarbeitungsanlage",
            "Reduziert und stabilisiert chemischen sowie radioaktiven Abfall.",
            MachineCategory.Processing,
            [(ProductionItemIds.TitaniumFrame, 12), (ProductionItemIds.ChemistryResearchPack, 4), (ProductionItemIds.ControlModule, 4)],
            8,
            6,
            3.0,
            MachineArchetype.WasteProcessing,
            MachinePlacementRequirement.LargeOrHugeComet,
            TechnologyTier.Tier8,
            0.8,
            0.88,
            0.86);
        yield return new MachineDefinition(
            MachineDefinitionIds.NuclearWasteStorage,
            "Atommülllager",
            "Lagert abgeschirmte radioaktive Abfälle langfristig und sicher.",
            MachineCategory.Storage,
            MachineKind.Storage,
            Costs([(ProductionItemIds.TitaniumFrame, 16), (ProductionItemIds.ReinforcedPlate, 24), (ProductionItemIds.ShieldedWasteContainer, 4)]),
            ProductionConfiguration.NuclearWasteStorageSlotCount,
            1,
            3.2,
            archetype: MachineArchetype.SolidStorage,
            technologyTier: TechnologyTier.Tier8,
            radiationShielding: 1.0);
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

    private static MachineDefinition ProfiledProduction(
        MachineDefinitionId id,
        string name,
        string description,
        MachineCategory category,
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs,
        int inputSlots,
        int outputSlots,
        double constructionSeconds,
        MachineArchetype archetype,
        MachinePlacementRequirement placementRequirement,
        TechnologyTier tier,
        double speed,
        double efficiency,
        double shielding = 0,
        SpaceFactory.Core.Items.ItemId? placementItemId = null,
        bool isDirectBuildMenuEntry = true) =>
        new(
            id,
            name,
            description,
            category,
            MachineKind.Production,
            Costs(costs),
            inputSlots,
            outputSlots,
            constructionSeconds,
            archetype: archetype,
            placementRequirement: placementRequirement,
            technologyTier: tier,
            speedMultiplier: speed,
            efficiencyMultiplier: efficiency,
            radiationShielding: shielding,
            isDirectBuildMenuEntry: isDirectBuildMenuEntry,
            placementItemId: placementItemId);

    private static MachineDefinition StorageProfile(
        MachineDefinitionId id,
        string name,
        string description,
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs,
        int slotCount,
        MachineArchetype archetype,
        TechnologyTier tier) =>
        new(
            id,
            name,
            description,
            MachineCategory.Storage,
            MachineKind.Storage,
            Costs(costs),
            slotCount,
            1,
            2.0,
            archetype: archetype,
            technologyTier: tier);

    private static IReadOnlyList<ItemAmount> Costs(
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs) =>
        costs.Select(cost => new ItemAmount(cost.Id, cost.Amount)).ToArray();

}

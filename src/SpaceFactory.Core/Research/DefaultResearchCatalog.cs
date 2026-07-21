using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Research;

public static class DefaultResearchIds
{
    public static readonly ResearchId BasicAutomation = new("basic_automation");
    public static readonly ResearchId MiningAutomation = new("mining_automation");
    public static readonly ResearchId ImprovedMining = new("improved_mining");
    public static readonly ResearchId AdvancedMetallurgy = new("advanced_metallurgy");
    public static readonly ResearchId IndustrialAutomation = new("industrial_automation");
    public static readonly ResearchId AdvancedElectronics = new("advanced_electronics");
    public static readonly ResearchId ChemicalEngineering = new("chemical_engineering");
    public static readonly ResearchId HydrogenTechnology = new("hydrogen_technology");
    public static readonly ResearchId FuelProduction = new("fuel_production");
    public static readonly ResearchId ImprovedEnergySupply = new("improved_energy_supply");
    public static readonly ResearchId PrecisionManufacturing = new("precision_manufacturing");
    public static readonly ResearchId NuclearProcessing = new("nuclear_processing");
    public static readonly ResearchId NuclearPower = new("nuclear_power");
    public static readonly ResearchId RadioactiveWasteManagement = new("radioactive_waste_management");
    public static readonly ResearchId SpaceshipComponents = new("spaceship_components");
    public static readonly ResearchId SpaceSystems = new("space_systems");
    public static readonly ResearchId DeepSpaceOptimization = new("deep_space_optimization");
}

public static class DefaultResearchCatalog
{
    public static ResearchCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<ResearchDefinition> CreateDefinitions()
    {
        yield return D(
            DefaultResearchIds.BasicAutomation,
            "Grundlegende Automatisierung",
            "Etabliert standardisierte Fertigung und die ersten Automatisierungs-Forschungspakete.",
            [(ProductionItemIds.IronPlate, 20), (ProductionItemIds.CopperWire, 16), (ProductionItemIds.Screws, 24)],
            35,
            7,
            ResearchCategory.Fundamentals,
            TechnologyTier.Tier1,
            machines: [M("assembler")],
            recipes: [R("make_automation_research_pack"), R("make_circuit_board")],
            alternativeGroups: ["steel_plate", "reinforced_plate", "rotor"],
            tags: ["milestone", "automation"]);

        yield return D(
            DefaultResearchIds.MiningAutomation,
            "Mobile Rohstoffgewinnung",
            "Schaltet mobile Miner für die erste kontinuierliche Erzförderung frei.",
            [(ProductionItemIds.AutomationResearchPack, 4), (ProductionItemIds.IronPlate, 24), (ProductionItemIds.CopperCable, 8)],
            55,
            9,
            ResearchCategory.Mining,
            TechnologyTier.Tier2,
            prerequisites: [DefaultResearchIds.BasicAutomation],
            machines: [M("mobile_miner")],
            recipes: [R("make_mobile_miner")],
            tags: ["mining", "automation"]);

        yield return D(
            DefaultResearchIds.AdvancedMetallurgy,
            "Erweiterte Metallverarbeitung",
            "Schaltet die Giesserei, Stahl und hochwertige Weltraumlegierungen frei.",
            [(ProductionItemIds.AutomationResearchPack, 5), (ProductionItemIds.IronIngot, 20), (ProductionItemIds.CopperIngot, 12)],
            60,
            11,
            ResearchCategory.Metallurgy,
            TechnologyTier.Tier2,
            prerequisites: [DefaultResearchIds.BasicAutomation],
            machines: [MachineDefinitionIds.Foundry],
            recipes:
            [
                DefaultRecipeIds.MakeSteel,
                R("make_nickel_steel"),
                R("make_titanium_cobalt_alloy"),
                R("make_copper_nickel_alloy"),
            ],
            alternativeGroups: ["nickel_steel"],
            tags: ["metallurgy"]);

        yield return D(
            DefaultResearchIds.ImprovedMining,
            "Verbesserte Abbautechnik",
            "Ermöglicht netzgebundene automatische Miner und ein schnelleres Abbauwerkzeug.",
            [(ProductionItemIds.AutomationResearchPack, 8), (ProductionItemIds.CircuitBoard, 4), (ProductionItemIds.SmallElectricMotor, 4)],
            75,
            13,
            ResearchCategory.Mining,
            TechnologyTier.Tier3,
            prerequisites: [DefaultResearchIds.MiningAutomation, DefaultResearchIds.AdvancedMetallurgy],
            machines: [M("automatic_miner")],
            recipes: [R("make_automatic_miner"), R("make_improved_mining_tool")],
            alternativeGroups: [],
            tags: ["mining", "automation"]);

        yield return D(
            DefaultResearchIds.IndustrialAutomation,
            "Industrielle Automatisierung",
            "Verknüpft automatischen Abbau, Montage und mehrstufige Serienfertigung.",
            [(ProductionItemIds.AutomationResearchPack, 10), (ProductionItemIds.CircuitBoard, 6), (ProductionItemIds.SmallElectricMotor, 6)],
            90,
            15,
            ResearchCategory.Automation,
            TechnologyTier.Tier3,
            prerequisites: [DefaultResearchIds.ImprovedMining, DefaultResearchIds.AdvancedMetallurgy],
            machines: [M("advanced_fabricator")],
            recipes: [R("make_automation_module")],
            tags: ["milestone", "automation"]);

        yield return D(
            DefaultResearchIds.AdvancedElectronics,
            "Fortgeschrittene Elektronik",
            "Schaltet Leiterplatten, Steuerungsmodule und komplexe elektronische Fertigung frei.",
            [(ProductionItemIds.AutomationResearchPack, 12), (ProductionItemIds.CopperCable, 18), (ProductionItemIds.GoldIngot, 6)],
            110,
            18,
            ResearchCategory.Electronics,
            TechnologyTier.Tier4,
            prerequisites: [DefaultResearchIds.IndustrialAutomation],
            machines: [MachineDefinitionIds.Fabricator],
            recipes:
            [
                DefaultRecipeIds.MakeElectronicComponent,
                R("make_battery_cell"),
                R("make_machine_part"),
                R("make_motor"),
                R("make_computer_chip"),
                R("make_conveyor_part"),
                R("make_electronics_research_pack"),
                R("make_control_module"),
            ],
            alternativeGroups: ["circuit_board", "electronic_parts", "battery_cell"],
            tags: ["milestone", "electronics"]);

        yield return D(
            DefaultResearchIds.ChemicalEngineering,
            "Chemieingenieurwesen",
            "Erschliesst kontrollierte Flüssigkeits-, Gas- und Grundstoffchemie.",
            [(ProductionItemIds.ElectronicsResearchPack, 8), (ProductionItemIds.ControlModule, 4), (ProductionItemIds.SteelPipe, 16)],
            130,
            22,
            ResearchCategory.Chemistry,
            TechnologyTier.Tier5,
            prerequisites: [DefaultResearchIds.AdvancedElectronics, DefaultResearchIds.AdvancedMetallurgy],
            machines:
            [
                M("chemical_plant"),
                M("liquid_tank"),
                M("gas_tank"),
                M("pump_station"),
            ],
            recipes: [R("make_chemistry_research_pack")],
            alternativeGroups: ["sulfuric_acid", "plastic", "lubricant", "coolant"],
            tags: ["milestone", "chemistry"]);

        yield return D(
            DefaultResearchIds.HydrogenTechnology,
            "Wasserstofftechnologie",
            "Schaltet früh Elektrolyse, Gasbehälter und einfachen Standardtreibstoff frei.",
            [(ProductionItemIds.AutomationResearchPack, 6), (ProductionItemIds.SteelPipe, 8), (ProductionItemIds.WaterContainer, 4)],
            75,
            12,
            ResearchCategory.Chemistry,
            TechnologyTier.Tier3,
            prerequisites: [DefaultResearchIds.AdvancedMetallurgy],
            machines: [MachineDefinitionIds.Electrolyzer],
            recipes:
            [
                DefaultRecipeIds.MakeEmptyFuelContainer,
                DefaultRecipeIds.ElectrolyzeWater,
                DefaultRecipeIds.ElectrolyzeWaterContainer,
                DefaultRecipeIds.RefineStandardFuel,
                DefaultRecipeIds.PackageStandardFuel,
                DefaultRecipeIds.FillStandardFuelContainer,
            ],
            tags: ["chemistry", "gas"]);

        yield return D(
            DefaultResearchIds.FuelProduction,
            "Treibstoffproduktion",
            "Schaltet die Raffinerie und skalierbare Treibstoffbehälter-Produktion frei.",
            [(ProductionItemIds.ChemistryResearchPack, 10), (ProductionItemIds.ElectronicsResearchPack, 6), (ProductionItemIds.HydrogenContainer, 6)],
            170,
            29,
            ResearchCategory.Chemistry,
            TechnologyTier.Tier6,
            prerequisites: [DefaultResearchIds.HydrogenTechnology, DefaultResearchIds.AdvancedMetallurgy],
            machines: [MachineDefinitionIds.Refinery],
            recipes:
            [
                DefaultRecipeIds.RefineFuel,
                DefaultRecipeIds.PackageFuel,
                DefaultRecipeIds.FillFuelContainer,
            ],
            alternativeGroups: ["fuel"],
            tags: ["chemistry", "fuel"]);

        yield return D(
            DefaultResearchIds.ImprovedEnergySupply,
            "Verbesserte Energieversorgung",
            "Schaltet leistungsfähige Treibstoffgeneratoren und industrielle Stromregelung frei.",
            [(ProductionItemIds.ElectronicsResearchPack, 12), (ProductionItemIds.ControlModule, 8), (ProductionItemIds.FuelContainer, 4)],
            185,
            32,
            ResearchCategory.Energy,
            TechnologyTier.Tier6,
            prerequisites: [DefaultResearchIds.FuelProduction, DefaultResearchIds.AdvancedElectronics],
            machines: [MachineDefinitionIds.FuelGenerator, M("battery_bank")],
            recipes: [R("make_power_regulator"), DefaultRecipeIds.MakeHighPerformanceBattery],
            tags: ["energy"]);

        yield return D(
            DefaultResearchIds.PrecisionManufacturing,
            "Präzisionsfertigung",
            "Etabliert eng tolerierte Bauteile für Reaktoren und Raumschiffsysteme.",
            [(ProductionItemIds.ElectronicsResearchPack, 14), (ProductionItemIds.ControlModule, 8), (ProductionItemIds.MachinePart, 8)],
            195,
            34,
            ResearchCategory.Automation,
            TechnologyTier.Tier6,
            prerequisites: [DefaultResearchIds.IndustrialAutomation, DefaultResearchIds.AdvancedElectronics],
            machines: [M("precision_manufacturer")],
            recipes: [R("make_precision_component")],
            alternativeGroups: ["precision_component"],
            tags: ["automation", "hightech"]);

        yield return D(
            DefaultResearchIds.NuclearProcessing,
            "Nukleare Aufbereitung",
            "Ermöglicht die sichere Aufbereitung, Anreicherung und Brennstoffzellenfertigung.",
            [(ProductionItemIds.ChemistryResearchPack, 16), (ProductionItemIds.ElectronicsResearchPack, 12), (ProductionItemIds.PrecisionComponent, 8)],
            240,
            48,
            ResearchCategory.Nuclear,
            TechnologyTier.Tier7,
            prerequisites: [DefaultResearchIds.ChemicalEngineering, DefaultResearchIds.PrecisionManufacturing],
            machines: [M("uranium_processor"), M("fuel_cell_fabricator")],
            recipes:
            [
                R("process_uranium_ore"),
                R("enrich_uranium"),
                R("make_nuclear_research_pack"),
                R("make_nuclear_fuel_cell"),
                R("make_improved_radiation_suit"),
            ],
            discoveries: [ProductionItemIds.UraniumOre],
            alternativeGroups: ["uranium_concentrate", "enriched_uranium", "nuclear_fuel_cell"],
            tags: ["milestone", "nuclear", "discovery-gated"]);

        yield return D(
            DefaultResearchIds.SpaceshipComponents,
            "Raumschiffbauteile",
            "Schaltet belastbare Präzisionskomponenten für modulare Raumschiffsysteme frei.",
            [(ProductionItemIds.ElectronicsResearchPack, 12), (ProductionItemIds.PrecisionComponent, 10), (ProductionItemIds.TitaniumCobaltAlloy, 8)],
            220,
            40,
            ResearchCategory.SpaceTechnology,
            TechnologyTier.Tier7,
            prerequisites: [DefaultResearchIds.PrecisionManufacturing, DefaultResearchIds.AdvancedElectronics],
            recipes: [R("make_spaceship_part"), R("make_life_support_system"), R("make_space_research_pack")],
            tags: ["space", "hightech"]);

        yield return D(
            DefaultResearchIds.NuclearPower,
            "Nukleare Energieerzeugung",
            "Schaltet Reaktoren und eine extrem leistungsfähige, bedarfsgeregelte Energiequelle frei.",
            [(ProductionItemIds.NuclearResearchPack, 12), (ProductionItemIds.NuclearFuelCell, 2), (ProductionItemIds.PrecisionComponent, 8)],
            300,
            62,
            ResearchCategory.Nuclear,
            TechnologyTier.Tier8,
            prerequisites: [DefaultResearchIds.NuclearProcessing, DefaultResearchIds.ImprovedEnergySupply],
            machines: [M("nuclear_reactor")],
            recipes: [R("make_shielded_waste_container")],
            discoveries: [ProductionItemIds.UraniumOre],
            tags: ["milestone", "nuclear", "energy", "discovery-gated"]);

        yield return D(
            DefaultResearchIds.RadioactiveWasteManagement,
            "Radioaktives Abfallmanagement",
            "Ermöglicht Wiederaufbereitung, Stabilisierung und abgeschirmte Langzeitlagerung.",
            [(ProductionItemIds.NuclearResearchPack, 12), (ProductionItemIds.SpentFuelCell, 2), (ProductionItemIds.ControlModule, 8)],
            280,
            54,
            ResearchCategory.Nuclear,
            TechnologyTier.Tier8,
            prerequisites: [DefaultResearchIds.NuclearPower],
            machines: [M("waste_processor"), M("nuclear_waste_storage")],
            recipes:
            [
                R("reprocess_spent_fuel_cell"),
                R("stabilize_radioactive_waste"),
                R("make_nuclear_radiation_suit"),
            ],
            tags: ["nuclear", "safety", "waste"]);

        yield return D(
            DefaultResearchIds.SpaceSystems,
            "Integrierte Weltraumsysteme",
            "Verbindet Hochenergieversorgung, Navigation, Lebenserhaltung und autonome Transporte.",
            [(ProductionItemIds.SpaceResearchPack, 8), (ProductionItemIds.ControlModule, 12), (ProductionItemIds.NuclearFuelCell, 2)],
            360,
            72,
            ResearchCategory.SpaceTechnology,
            TechnologyTier.Tier9,
            prerequisites:
            [
                DefaultResearchIds.SpaceshipComponents,
                DefaultResearchIds.NuclearPower,
                DefaultResearchIds.RadioactiveWasteManagement,
            ],
            recipes:
            [
                R("make_navigation_computer"),
                R("make_high_performance_scanner"),
                R("make_transport_drone"),
            ],
            tags: ["milestone", "space", "hightech"]);

        yield return D(
            DefaultResearchIds.DeepSpaceOptimization,
            "Tiefenraum-Optimierung",
            "Vereint alternative Hightech-Rezepte und hochskalierte Fabrikoptimierung für den Tiefenraum.",
            [(ProductionItemIds.SpaceResearchPack, 24), (ProductionItemIds.NuclearResearchPack, 12), (ProductionItemIds.PrecisionComponent, 16)],
            480,
            90,
            ResearchCategory.SpaceTechnology,
            TechnologyTier.Tier10,
            prerequisites: [DefaultResearchIds.SpaceSystems],
            recipes: [DefaultRecipeIds.MakeDeepSpaceControlCore, DefaultRecipeIds.MakeQuantumNavigationModule],
            alternativeGroups:
            [
                "precision_component",
                "advanced_power_module",
                "factory_controller",
                "navigation_computer",
                "uranium_concentrate",
                "enriched_uranium",
                "nuclear_fuel_cell",
            ],
            tags: ["endgame", "space", "optimization"]);
    }

    private static ResearchDefinition D(
        ResearchId id,
        string name,
        string description,
        IEnumerable<(ItemId Id, int Amount)> costs,
        double duration,
        double power,
        ResearchCategory category,
        TechnologyTier tier,
        IEnumerable<ResearchId>? prerequisites = null,
        IEnumerable<MachineDefinitionId>? machines = null,
        IEnumerable<RecipeId>? recipes = null,
        IEnumerable<ItemId>? discoveries = null,
        IEnumerable<string>? alternativeGroups = null,
        IEnumerable<string>? tags = null) =>
        new(
            id,
            name,
            description,
            costs.Select(cost => new ItemAmount(cost.Id, cost.Amount)),
            duration,
            power,
            prerequisites,
            machines,
            recipes,
            category,
            tier,
            discoveries,
            alternativeGroups?.Select(group => new AlternativeRecipeGroupId(group)),
            tags);

    private static MachineDefinitionId M(string value) => new(value);

    private static RecipeId R(string value) => new(value);
}

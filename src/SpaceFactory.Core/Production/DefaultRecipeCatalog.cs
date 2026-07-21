using SpaceFactory.Core.Items;
using SpaceFactory.Core.Research;

namespace SpaceFactory.Core.Production;

public static class DefaultRecipeIds
{
    public static readonly RecipeId CrushIronOre = new("crush_iron_ore");
    public static readonly RecipeId SmeltIronOre = new("smelt_iron_ore");
    public static readonly RecipeId SmeltCrushedIronOre = new("smelt_crushed_iron_ore");
    public static readonly RecipeId MakeSteel = new("make_steel");
    public static readonly RecipeId MakeIronPlate = new("make_iron_plate");
    public static readonly RecipeId MakeElectronicComponent = new("make_electronic_component");
    public static readonly RecipeId MakeEmptyFuelContainer = new("make_empty_fuel_container");
    public static readonly RecipeId MeltWaterIce = new("melt_water_ice");
    public static readonly RecipeId FillWaterContainer = new("fill_water_container");
    public static readonly RecipeId ElectrolyzeWater = new("electrolyze_water");
    public static readonly RecipeId ElectrolyzeWaterContainer = new("electrolyze_water_container");
    public static readonly RecipeId RefineStandardFuel = new("refine_standard_fuel");
    public static readonly RecipeId PackageStandardFuel = new("package_standard_fuel");
    public static readonly RecipeId FillStandardFuelContainer = new("fill_standard_fuel_container");
    public static readonly RecipeId RefineFuel = new("refine_fuel");
    public static readonly RecipeId PackageFuel = new("package_fuel");
    public static readonly RecipeId FillFuelContainer = new("fill_fuel_container");
    public static readonly RecipeId MakeAutomationResearchPack = new("make_automation_research_pack");
    public static readonly RecipeId MakeMiningTool = new("make_mining_tool");
    public static readonly RecipeId MakeMachineDismantlingTool = new("make_machine_dismantling_tool");
    public static readonly RecipeId MakeMobileMiner = new("make_mobile_miner");
    public static readonly RecipeId MakeAutomaticMiner = new("make_automatic_miner");
    public static readonly RecipeId MakeImprovedMiningTool = new("make_improved_mining_tool");
    public static readonly RecipeId MakeHighPerformanceMiningTool = new("make_high_performance_mining_tool");
    public static readonly RecipeId MakeAutomationModule = new("make_automation_module");
    public static readonly RecipeId MakeCircuitBoard = new("make_circuit_board");
    public static readonly RecipeId MakeElectronicsResearchPack = new("make_electronics_research_pack");
    public static readonly RecipeId MakeControlModule = new("make_control_module");
    public static readonly RecipeId MakeChemistryResearchPack = new("make_chemistry_research_pack");
    public static readonly RecipeId MakePrecisionComponent = new("make_precision_component");
    public static readonly RecipeId ProcessUraniumOre = new("process_uranium_ore");
    public static readonly RecipeId EnrichUranium = new("enrich_uranium");
    public static readonly RecipeId MakeNuclearResearchPack = new("make_nuclear_research_pack");
    public static readonly RecipeId MakeNuclearFuelCell = new("make_nuclear_fuel_cell");
    public static readonly RecipeId ReprocessSpentFuelCell = new("reprocess_spent_fuel_cell");
    public static readonly RecipeId StabilizeRadioactiveWaste = new("stabilize_radioactive_waste");
    public static readonly RecipeId MakeShieldedWasteContainer = new("make_shielded_waste_container");
    public static readonly RecipeId MakeSpaceResearchPack = new("make_space_research_pack");
    public static readonly RecipeId MakeHighPerformanceBattery = new("make_high_performance_battery");
    public static readonly RecipeId MakeDeepSpaceControlCore = new("make_deep_space_control_core");
    public static readonly RecipeId MakeQuantumNavigationModule = new("make_quantum_navigation_module");
}

public static class DefaultRecipeCatalog
{
    public static RecipeCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<RecipeDefinition> CreateDefinitions()
    {
        yield return R(DefaultRecipeIds.CrushIronOre, "Eisenerz zerkleinern", MachineDefinitionIds.Crusher, "Erz", [(ProductionItemIds.IronOre, 2)], [(ProductionItemIds.CrushedIronOre, 3)], 2, 5);
        yield return R("crush_copper_ore", "Kupfererz zerkleinern", MachineDefinitionIds.Crusher, "Erz", [(ProductionItemIds.CopperOre, 2)], [(ProductionItemIds.CrushedCopperOre, 3)], 2, 5);
        yield return R("crush_nickel_ore", "Nickelerz zerkleinern", MachineDefinitionIds.Crusher, "Erz", [(ProductionItemIds.NickelOre, 2)], [(ProductionItemIds.CrushedNickelOre, 3)], 2.4, 5);
        yield return R("crush_titanium_ore", "Titanerz zerkleinern", MachineDefinitionIds.Crusher, "Erz", [(ProductionItemIds.TitaniumOre, 2)], [(ProductionItemIds.CrushedTitaniumOre, 3)], 3, 6);
        yield return R("mill_silicate", "Silikatpulver herstellen", MachineDefinitionIds.Crusher, "Gestein", [(ProductionItemIds.SilicateRock, 1)], [(ProductionItemIds.SilicatePowder, 2)], 1.6, 4);
        yield return R("process_carbon", "Kohlenstoff zerkleinern", MachineDefinitionIds.Crusher, "Gestein", [(ProductionItemIds.Carbon, 1)], [(ProductionItemIds.ProcessedCarbon, 1)], 1.5, 4);

        yield return R(DefaultRecipeIds.SmeltIronOre, "Eisenbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.IronOre, 2)], [(ProductionItemIds.IronIngot, 1)], SmeltingConfiguration.GetRawOreDuration(2.8), 7);
        yield return R(DefaultRecipeIds.SmeltCrushedIronOre, "Eisenbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedIronOre, 2)], [(ProductionItemIds.IronIngot, 2)], 2.8, 7);
        yield return R("smelt_copper_ore", "Kupferbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CopperOre, 2)], [(ProductionItemIds.CopperIngot, 1)], SmeltingConfiguration.GetRawOreDuration(2.8), 7);
        yield return R("smelt_crushed_copper_ore", "Kupferbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedCopperOre, 2)], [(ProductionItemIds.CopperIngot, 2)], 2.8, 7);
        yield return R("smelt_nickel_ore", "Nickelbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.NickelOre, 2)], [(ProductionItemIds.NickelIngot, 1)], SmeltingConfiguration.GetRawOreDuration(3), 8);
        yield return R("smelt_crushed_nickel_ore", "Nickelbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedNickelOre, 2)], [(ProductionItemIds.NickelIngot, 2)], 3, 8);
        yield return R("smelt_cobalt_ore", "Kobaltbarren", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CobaltOre, 2)], [(ProductionItemIds.CobaltIngot, 1)], 5, 9);
        yield return R("smelt_titanium_ore", "Titanbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.TitaniumOre, 2)], [(ProductionItemIds.TitaniumIngot, 1)], SmeltingConfiguration.GetRawOreDuration(4), 10);
        yield return R("smelt_crushed_titanium_ore", "Titanbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedTitaniumOre, 2)], [(ProductionItemIds.TitaniumIngot, 2)], 4, 10);
        yield return R("smelt_gold_ore", "Goldbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.GoldOre, 2)], [(ProductionItemIds.GoldIngot, 1)], 5, 9);
        yield return R("smelt_platinum_ore", "Platinbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.PlatinumOre, 2)], [(ProductionItemIds.PlatinumIngot, 1)], 6, 10);
        yield return R("smelt_iridium", "Iridiumbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.Iridium, 2)], [(ProductionItemIds.IridiumIngot, 1)], 8, 12);

        yield return R(DefaultRecipeIds.MakeSteel, "Stahlbarren", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.IronIngot, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.SteelIngot, 2)], 6, 14, unlock: DefaultResearchIds.AdvancedMetallurgy);
        yield return R("make_nickel_steel", "Nickelstahl", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.NickelIngot, 1), (ProductionItemIds.IronIngot, 2)], [(ProductionItemIds.NickelSteel, 2)], 7, 14, unlock: DefaultResearchIds.AdvancedMetallurgy, alternativeGroup: "nickel_steel");
        yield return R("make_titanium_cobalt_alloy", "Titan-Kobalt-Legierung", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.TitaniumIngot, 2), (ProductionItemIds.CobaltIngot, 1)], [(ProductionItemIds.TitaniumCobaltAlloy, 2)], 9, 16, unlock: DefaultResearchIds.AdvancedMetallurgy);
        yield return R("make_copper_nickel_alloy", "Kupfer-Nickel-Legierung", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.CopperIngot, 2), (ProductionItemIds.NickelIngot, 1)], [(ProductionItemIds.CopperNickelAlloy, 2)], 7, 14, unlock: DefaultResearchIds.AdvancedMetallurgy);

        yield return R(DefaultRecipeIds.MakeIronPlate, "Eisenplatten", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.IronIngot, 1)], [(ProductionItemIds.IronPlate, 2)], 2, 6);
        yield return R("make_iron_rods", "Eisenstangen", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.IronIngot, 1)], [(ProductionItemIds.IronRod, 3)], 2, 5);
        yield return R("make_screws_from_rod", "Schrauben aus Eisenstangen", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.IronRod, 1)], [(ProductionItemIds.Screws, 8)], 1.5, 4);
        yield return R("make_screws_from_steel", "Schrauben aus Stahl", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.SteelIngot, 1)], [(ProductionItemIds.Screws, 16)], 1.5, 5);
        yield return R("make_copper_wire", "Kupferdraht", MachineDefinitionIds.Constructor, "Elektrik", [(ProductionItemIds.CopperIngot, 1)], [(ProductionItemIds.CopperWire, 4)], 2, 5);
        yield return R("make_copper_cable", "Kupferkabel", MachineDefinitionIds.Constructor, "Elektrik", [(ProductionItemIds.CopperWire, 3), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.CopperCable, 2)], 2.5, 6);
        yield return R("make_titanium_plate", "Titanplatten", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.TitaniumIngot, 1)], [(ProductionItemIds.TitaniumPlate, 2)], 3, 7);
        yield return R("make_steel_beam", "Stahlträger", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.SteelIngot, 2)], [(ProductionItemIds.SteelBeam, 1)], 3, 7);
        yield return R("make_iron_pipe", "Eisenrohre", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.IronIngot, 1)], [(ProductionItemIds.IronPipe, 2)], 2, 5);
        yield return R("make_steel_pipe", "Stahlrohre", MachineDefinitionIds.Constructor, "Bauteil", [(ProductionItemIds.SteelIngot, 1)], [(ProductionItemIds.SteelPipe, 2)], 2.5, 6);
        yield return R("make_empty_water_container", "Leere Wasserbehälter", MachineDefinitionIds.Constructor, "Behälter", [(ProductionItemIds.IronPlate, 1), (ProductionItemIds.SilicatePowder, 1)], [(ProductionItemIds.EmptyWaterContainer, 2)], 2.5, 5);
        yield return R("make_empty_gas_container", "Leere Gasbehälter", MachineDefinitionIds.Constructor, "Behälter", [(ProductionItemIds.SteelIngot, 1), (ProductionItemIds.IronPipe, 1)], [(ProductionItemIds.EmptyGasContainer, 2)], 3, 6);
        yield return R(DefaultRecipeIds.MakeEmptyFuelContainer, "Leere Treibstoffbehälter", MachineDefinitionIds.Constructor, "Behälter", [(ProductionItemIds.SteelIngot, 1), (ProductionItemIds.CopperNickelAlloy, 1)], [(ProductionItemIds.EmptyFuelContainer, 2)], 3.5, 7, unlock: DefaultResearchIds.HydrogenTechnology);

        yield return R(DefaultRecipeIds.MakeElectronicComponent, "Elektronische Bauteile", MachineDefinitionIds.Fabricator, "Elektronik", [(ProductionItemIds.CopperCable, 2), (ProductionItemIds.SilicatePowder, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.ElectronicComponent, 1)], 5, 12, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_battery_cell", "Batteriezellen", MachineDefinitionIds.Fabricator, "Elektronik", [(ProductionItemIds.NickelIngot, 1), (ProductionItemIds.CobaltIngot, 1), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.BatteryCell, 1)], 6, 12, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_machine_part", "Maschinenteile", MachineDefinitionIds.Fabricator, "Maschine", [(ProductionItemIds.IronPlate, 2), (ProductionItemIds.Screws, 4), (ProductionItemIds.CopperWire, 2)], [(ProductionItemIds.MachinePart, 1)], 5, 11, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_motor", "Motoren", MachineDefinitionIds.Fabricator, "Maschine", [(ProductionItemIds.SteelBeam, 1), (ProductionItemIds.CopperCable, 2), (ProductionItemIds.MachinePart, 1)], [(ProductionItemIds.Motor, 1)], 7, 14, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_computer_chip", "Computerchips", MachineDefinitionIds.Fabricator, "Elektronik", [(ProductionItemIds.SilicatePowder, 2), (ProductionItemIds.CopperWire, 2), (ProductionItemIds.GoldIngot, 1)], [(ProductionItemIds.ComputerChip, 1)], 7, 14, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_conveyor_part", "Förderbandteile", MachineDefinitionIds.Fabricator, "Logistik", [(ProductionItemIds.IronPlate, 2), (ProductionItemIds.SteelBeam, 1), (ProductionItemIds.CopperWire, 1)], [(ProductionItemIds.ConveyorPart, 1)], 5, 11, unlock: DefaultResearchIds.AdvancedElectronics);
        yield return R("make_spaceship_part", "Raumschiffteile", MachineDefinitionIds.Fabricator, "Raumschiff", [(ProductionItemIds.TitaniumPlate, 2), (ProductionItemIds.ElectronicComponent, 2), (ProductionItemIds.TitaniumCobaltAlloy, 1)], [(ProductionItemIds.SpaceshipPart, 1)], 10, 18, unlock: DefaultResearchIds.SpaceshipComponents);

        yield return R(DefaultRecipeIds.MeltWaterIce, "Wassereis schmelzen", MachineDefinitionIds.WaterProcessor, "Wasser", [(ProductionItemIds.WaterIce, 2)], [(ProductionItemIds.Water, 2)], 3, 5);
        yield return R(DefaultRecipeIds.FillWaterContainer, "Wasserbehälter füllen", MachineDefinitionIds.WaterProcessor, "Wasser", [(ProductionItemIds.WaterIce, 2), (ProductionItemIds.EmptyWaterContainer, 1)], [(ProductionItemIds.WaterContainer, 1)], 4, 6);

        yield return R(DefaultRecipeIds.ElectrolyzeWater, "Wasser elektrolysieren", MachineDefinitionIds.Electrolyzer, "Gas", [(ProductionItemIds.Water, 3)], [(ProductionItemIds.Hydrogen, 2), (ProductionItemIds.Oxygen, 1)], 6, 15, unlock: DefaultResearchIds.HydrogenTechnology);
        yield return R(DefaultRecipeIds.ElectrolyzeWaterContainer, "Wasserbehälter elektrolysieren", MachineDefinitionIds.Electrolyzer, "Gasbehälter", [(ProductionItemIds.WaterContainer, 1), (ProductionItemIds.EmptyGasContainer, 2)], [(ProductionItemIds.HydrogenContainer, 1), (ProductionItemIds.OxygenContainer, 1)], 8, 15, returned: [(ProductionItemIds.EmptyWaterContainer, 1)], unlock: DefaultResearchIds.HydrogenTechnology);

        yield return R(DefaultRecipeIds.RefineStandardFuel, "Standardtreibstoff mischen", MachineDefinitionIds.Electrolyzer, "Treibstoff", [(ProductionItemIds.Hydrogen, 1), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.StandardFuel, 2)], 5, 10, unlock: DefaultResearchIds.HydrogenTechnology, tier: TechnologyTier.Tier3, tags: ["chemical", "fuel", "early"]);
        yield return R(DefaultRecipeIds.PackageStandardFuel, "Standardtreibstoff abfüllen", MachineDefinitionIds.Electrolyzer, "Treibstoffbehälter", [(ProductionItemIds.StandardFuel, 2), (ProductionItemIds.EmptyFuelContainer, 1)], [(ProductionItemIds.StandardFuelContainer, 1)], 4, 8, unlock: DefaultResearchIds.HydrogenTechnology, tier: TechnologyTier.Tier3, tags: ["chemical", "fuel", "early"]);
        yield return R(DefaultRecipeIds.FillStandardFuelContainer, "Standardtreibstoffbehälter füllen", MachineDefinitionIds.Electrolyzer, "Treibstoffbehälter", [(ProductionItemIds.HydrogenContainer, 1), (ProductionItemIds.EmptyFuelContainer, 1), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.StandardFuelContainer, 1)], 7, 10, returned: [(ProductionItemIds.EmptyGasContainer, 1)], unlock: DefaultResearchIds.HydrogenTechnology, tier: TechnologyTier.Tier3, tags: ["chemical", "fuel", "early"]);

        yield return R(DefaultRecipeIds.RefineFuel, "Hochleistungstreibstoff raffinieren", MachineDefinitionIds.Refinery, "Treibstoff", [(ProductionItemIds.Hydrogen, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.HighPerformanceFuel, 2)], 8, 16, unlock: DefaultResearchIds.FuelProduction, alternativeGroup: "fuel");
        yield return R(DefaultRecipeIds.PackageFuel, "Hochleistungstreibstoff abfüllen", MachineDefinitionIds.Refinery, "Treibstoffbehälter", [(ProductionItemIds.HighPerformanceFuel, 2), (ProductionItemIds.EmptyFuelContainer, 1)], [(ProductionItemIds.HighPerformanceFuelContainer, 1)], 5, 10, unlock: DefaultResearchIds.FuelProduction);
        yield return R(DefaultRecipeIds.FillFuelContainer, "Hochleistungstreibstoffbehälter füllen", MachineDefinitionIds.Refinery, "Treibstoffbehälter", [(ProductionItemIds.HydrogenContainer, 1), (ProductionItemIds.EmptyFuelContainer, 1), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.HighPerformanceFuelContainer, 1)], 10, 16, returned: [(ProductionItemIds.EmptyGasContainer, 1)], unlock: DefaultResearchIds.FuelProduction);

        foreach (var recipe in CreateExtendedDefinitions())
        {
            yield return recipe;
        }
    }

    private static IEnumerable<RecipeDefinition> CreateExtendedDefinitions()
    {
        foreach (var recipe in CreateExtractionDefinitions())
        {
            yield return recipe;
        }

        foreach (var recipe in CreateAutomationDefinitions())
        {
            yield return recipe;
        }

        foreach (var recipe in CreateElectronicsAndChemistryDefinitions())
        {
            yield return recipe;
        }

        foreach (var recipe in CreateAdvancedAndSpaceDefinitions())
        {
            yield return recipe;
        }

        foreach (var recipe in CreateNuclearAndWasteDefinitions())
        {
            yield return recipe;
        }

        foreach (var recipe in CreateSpecialResourceDefinitions())
        {
            yield return recipe;
        }
    }

    private static IEnumerable<RecipeDefinition> CreateExtractionDefinitions()
    {
        var sources = new[]
        {
            (ProductionItemIds.IronOre, "iron_ore", "Eisenerz", 9.0, 18.0, TechnologyTier.Tier1),
            (ProductionItemIds.CopperOre, "copper_ore", "Kupfererz", 10.0, 18.0, TechnologyTier.Tier1),
            (ProductionItemIds.NickelOre, "nickel_ore", "Nickelerz", 11.0, 20.0, TechnologyTier.Tier2),
            (ProductionItemIds.CobaltOre, "cobalt_ore", "Kobalterz", 12.0, 22.0, TechnologyTier.Tier2),
            (ProductionItemIds.TitaniumOre, "titanium_ore", "Titanerz", 14.0, 25.0, TechnologyTier.Tier3),
            (ProductionItemIds.GoldOre, "gold_ore", "Golderz", 15.0, 27.0, TechnologyTier.Tier4),
            (ProductionItemIds.PlatinumOre, "platinum_ore", "Platinerz", 17.0, 29.0, TechnologyTier.Tier5),
            (ProductionItemIds.Iridium, "iridium", "Iridium", 22.0, 36.0, TechnologyTier.Tier7),
            (ProductionItemIds.SilicateRock, "silicate_rock", "Silikatgestein", 7.0, 14.0, TechnologyTier.Tier1),
            (ProductionItemIds.Carbon, "carbon", "Kohlenstoff", 7.5, 15.0, TechnologyTier.Tier1),
            (ProductionItemIds.WaterIce, "water_ice", "Wassereis", 6.5, 13.0, TechnologyTier.Tier1),
            (ProductionItemIds.Sulfur, "sulfur", "Schwefel", 8.0, 16.0, TechnologyTier.Tier2),
            (ProductionItemIds.Phosphorus, "phosphorus", "Phosphor", 10.0, 18.0, TechnologyTier.Tier3),
            (ProductionItemIds.Olivine, "olivine", "Olivin", 9.0, 17.0, TechnologyTier.Tier2),
            (ProductionItemIds.Palladium, "palladium", "Palladium", 16.0, 28.0, TechnologyTier.Tier5),
            (ProductionItemIds.Rhodium, "rhodium", "Rhodium", 18.0, 30.0, TechnologyTier.Tier6),
            (ProductionItemIds.Osmium, "osmium", "Osmium", 20.0, 34.0, TechnologyTier.Tier6),
            (ProductionItemIds.Ruthenium, "ruthenium", "Ruthenium", 18.0, 30.0, TechnologyTier.Tier6),
            (ProductionItemIds.Schreibersite, "schreibersite", "Schreibersit", 14.0, 24.0, TechnologyTier.Tier4),
            (ProductionItemIds.Troilite, "troilite", "Troilit", 11.0, 20.0, TechnologyTier.Tier3),
            (ProductionItemIds.Kamacite, "kamacite", "Kamacit", 12.0, 21.0, TechnologyTier.Tier3),
            (ProductionItemIds.Taenite, "taenite", "Taenit", 14.0, 24.0, TechnologyTier.Tier4),
            (ProductionItemIds.Halite, "halite", "Halit", 8.0, 16.0, TechnologyTier.Tier2),
            (ProductionItemIds.Sylvite, "sylvite", "Sylvin", 10.0, 18.0, TechnologyTier.Tier3),
            (ProductionItemIds.Calcite, "calcite", "Calcit", 8.5, 17.0, TechnologyTier.Tier2),
            (ProductionItemIds.UraniumOre, "uranium_ore", "Uranerz", 24.0, 42.0, TechnologyTier.Tier7),
        };

        foreach (var (source, token, name, mobileDuration, automaticPower, tier) in sources)
        {
            yield return R(
                $"mobile_extract_{token}",
                $"{name} mobil fördern",
                MachineDefinitionIds.MobileMiner,
                "Extraktion",
                [],
                [(source, 1)],
                mobileDuration,
                0,
                tier: tier,
                alternativeGroup: $"extract_{token}",
                tags: ["extraction", "mobile", "infinite-source"],
                sourceResourceId: source);
            yield return R(
                $"automatic_extract_{token}",
                $"{name} automatisch fördern",
                MachineDefinitionIds.AutomaticMiner,
                "Extraktion",
                [],
                [(source, 3)],
                Math.Max(2.5, mobileDuration * 0.32),
                automaticPower,
                tier: tier < TechnologyTier.Tier3 ? TechnologyTier.Tier3 : tier,
                alternativeGroup: $"extract_{token}",
                tags: ["extraction", "automatic", "infinite-source"],
                sourceResourceId: source);
        }
    }

    private static IEnumerable<RecipeDefinition> CreateAutomationDefinitions()
    {
        yield return R(DefaultRecipeIds.MakeMiningTool, "Erz-Abbauwerkzeug", MachineDefinitionIds.Workbench, "Werkzeug", [(ProductionItemIds.IronPlate, 2), (ProductionItemIds.CopperWire, 2), (ProductionItemIds.IronRod, 1)], [(ProductionItemIds.MiningTool, 1)], 5, 3, tier: TechnologyTier.Tier1, tags: ["tool", "mining"]);
        yield return R(DefaultRecipeIds.MakeMachineDismantlingTool, "Maschinen-Abbauwerkzeug", MachineDefinitionIds.Workbench, "Werkzeug", [(ProductionItemIds.IronPlate, 3), (ProductionItemIds.CopperWire, 2), (ProductionItemIds.Screws, 4)], [(ProductionItemIds.MachineDismantlingTool, 1)], 5, 4, tier: TechnologyTier.Tier1, tags: ["tool", "dismantling"]);
        yield return R("make_steel_plate", "Stahlplatten", MachineDefinitionIds.Constructor, "Stahl", [(ProductionItemIds.SteelIngot, 1)], [(ProductionItemIds.SteelPlate, 2)], 2.8, 7, tier: TechnologyTier.Tier2, alternativeGroup: "steel_plate", tags: ["basic", "structural"]);
        yield return R("make_steel_plate_recycled", "Stahlplatten aus Trägern", MachineDefinitionIds.Constructor, "Stahl", [(ProductionItemIds.SteelBeam, 1)], [(ProductionItemIds.SteelPlate, 2)], 2.2, 9, tier: TechnologyTier.Tier3, alternativeGroup: "steel_plate", tags: ["alternative", "fast"]);
        yield return R("make_reinforced_plate", "Verstärkte Platten", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.IronPlate, 3), (ProductionItemIds.Screws, 8)], [(ProductionItemIds.ReinforcedPlate, 1)], 5, 10, tier: TechnologyTier.Tier2, tags: ["structural"]);
        yield return R("make_reinforced_plate_steel", "Stahlverstärkte Platten", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.SteelPlate, 2), (ProductionItemIds.CopperWire, 2)], [(ProductionItemIds.ReinforcedPlate, 2)], 6, 13, tier: TechnologyTier.Tier3, alternativeGroup: "reinforced_plate", tags: ["alternative", "efficient"]);
        yield return R("make_rotor", "Rotoren", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.IronRod, 3), (ProductionItemIds.Screws, 8)], [(ProductionItemIds.Rotor, 1)], 4.5, 9, tier: TechnologyTier.Tier2, alternativeGroup: "rotor", tags: ["mechanical"]);
        yield return R("make_rotor_steel", "Stahlrotoren", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.SteelPipe, 1), (ProductionItemIds.CopperWire, 3)], [(ProductionItemIds.Rotor, 2)], 5.5, 12, tier: TechnologyTier.Tier3, alternativeGroup: "rotor", tags: ["alternative", "efficient"]);
        yield return R("make_pump", "Pumpen", MachineDefinitionIds.Assembler, "Fluidtechnik", [(ProductionItemIds.Rotor, 1), (ProductionItemIds.IronPipe, 2), (ProductionItemIds.CopperWire, 2)], [(ProductionItemIds.Pump, 1)], 5, 11, tier: TechnologyTier.Tier2, tags: ["fluid"]);
        yield return R("make_valve", "Ventile", MachineDefinitionIds.Constructor, "Fluidtechnik", [(ProductionItemIds.IronPipe, 1), (ProductionItemIds.Screws, 4)], [(ProductionItemIds.Valve, 2)], 2.5, 6, tier: TechnologyTier.Tier2, tags: ["fluid"]);
        yield return R("make_small_electric_motor", "Kleine Elektromotoren", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.Rotor, 1), (ProductionItemIds.CopperWire, 4), (ProductionItemIds.IronPlate, 1)], [(ProductionItemIds.SmallElectricMotor, 1)], 5.5, 12, tier: TechnologyTier.Tier2, tags: ["mechanical", "electrical"]);
        yield return R(DefaultRecipeIds.MakeAutomationModule, "Automatisierungsmodule", MachineDefinitionIds.Assembler, "Automatisierung", [(ProductionItemIds.SmallElectricMotor, 1), (ProductionItemIds.CircuitBoard, 1), (ProductionItemIds.ReinforcedPlate, 1)], [(ProductionItemIds.AutomationModule, 1)], 7, 16, tier: TechnologyTier.Tier3, tags: ["automation"]);
        yield return R(DefaultRecipeIds.MakeAutomationResearchPack, "Automatisierungs-Forschungspakete", MachineDefinitionIds.Assembler, "Forschung", [(ProductionItemIds.ReinforcedPlate, 1), (ProductionItemIds.Rotor, 1)], [(ProductionItemIds.AutomationResearchPack, 2)], 6, 12, tier: TechnologyTier.Tier2, tags: ["research-pack"]);
        yield return R(DefaultRecipeIds.MakeMobileMiner, "Mobiler-Miner-Bausatz", MachineDefinitionIds.Workbench, "Bausatz", [(ProductionItemIds.IronPlate, 4), (ProductionItemIds.CopperWire, 3), (ProductionItemIds.IronRod, 2)], [(ProductionItemIds.MobileMinerKit, 1)], 6, 6, tier: TechnologyTier.Tier2, tags: ["machine-kit", "mining", "placeable"]);
        yield return R(DefaultRecipeIds.MakeAutomaticMiner, "Automatischer-Miner-Bausatz", MachineDefinitionIds.AdvancedFabricator, "Bausatz", [(ProductionItemIds.MobileMinerKit, 1), (ProductionItemIds.AutomationModule, 2), (ProductionItemIds.SteelPlate, 4)], [(ProductionItemIds.AutomaticMinerKit, 1)], 12, 24, tier: TechnologyTier.Tier3, tags: ["machine-kit", "mining", "placeable"]);
        yield return R(DefaultRecipeIds.MakeImprovedMiningTool, "Verbessertes Abbauwerkzeug", MachineDefinitionIds.Workbench, "Werkzeug", [(ProductionItemIds.MiningTool, 1), (ProductionItemIds.SteelPlate, 2), (ProductionItemIds.SmallElectricMotor, 1)], [(ProductionItemIds.UpgradedMiningTool, 1)], 10, 14, tier: TechnologyTier.Tier3, tags: ["tool", "mining"]);
        yield return R(DefaultRecipeIds.MakeHighPerformanceMiningTool, "Hochleistungs-Abbauwerkzeug", MachineDefinitionIds.Workbench, "Werkzeug", [(ProductionItemIds.UpgradedMiningTool, 1), (ProductionItemIds.PrecisionComponent, 2), (ProductionItemIds.HighPerformanceBattery, 1)], [(ProductionItemIds.HighPerformanceMiningTool, 1)], 14, 28, tier: TechnologyTier.Tier7, tags: ["tool", "mining", "uranium-capable"]);
        yield return R("make_power_cable", "Stromkabel", MachineDefinitionIds.Constructor, "Logistik", [(ProductionItemIds.CopperCable, 2), (ProductionItemIds.Plastic, 1)], [(ProductionItemIds.PowerCable, 2)], 2.5, 6, tier: TechnologyTier.Tier2, tags: ["logistics"]);
        yield return R("make_conveyor_belt", "Förderbänder", MachineDefinitionIds.Assembler, "Logistik", [(ProductionItemIds.ConveyorPart, 1), (ProductionItemIds.SteelPlate, 1)], [(ProductionItemIds.ConveyorBelt, 2)], 4, 9, tier: TechnologyTier.Tier2, tags: ["logistics"]);
        yield return R("make_transport_pipe", "Transportrohre", MachineDefinitionIds.Constructor, "Logistik", [(ProductionItemIds.SteelPipe, 2), (ProductionItemIds.Valve, 1)], [(ProductionItemIds.TransportPipe, 2)], 3.5, 8, tier: TechnologyTier.Tier2, tags: ["logistics", "fluid"]);
        yield return R("make_mobile_battery_pack", "Mobile Nickel-Akkupacks", MachineDefinitionIds.Assembler, "Energie", [(ProductionItemIds.NickelIngot, 2), (ProductionItemIds.CopperCable, 1), (ProductionItemIds.IronPlate, 1)], [(ProductionItemIds.MobileBatteryPack, 1)], 7, 12, tier: TechnologyTier.Tier2, tags: ["battery", "early-automation"]);
    }

    private static IEnumerable<RecipeDefinition> CreateElectronicsAndChemistryDefinitions()
    {
        yield return R(DefaultRecipeIds.MakeCircuitBoard, "Primitive Leiterplatten", MachineDefinitionIds.Assembler, "Elektronik", [(ProductionItemIds.CopperWire, 4), (ProductionItemIds.SilicatePowder, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.CircuitBoard, 1)], 7, 15, tier: TechnologyTier.Tier2, alternativeGroup: "circuit_board", tags: ["electronics", "early-automation"]);
        yield return R("make_circuit_board_gold", "Goldkontakt-Leiterplatten", MachineDefinitionIds.PrecisionManufacturer, "Elektronik", [(ProductionItemIds.CopperWire, 2), (ProductionItemIds.GoldIngot, 1), (ProductionItemIds.Plastic, 1)], [(ProductionItemIds.CircuitBoard, 3)], 7, 22, tier: TechnologyTier.Tier5, alternativeGroup: "circuit_board", tags: ["electronics", "alternative", "efficient"]);
        yield return R("make_electronic_parts", "Elektronische Einzelteile", MachineDefinitionIds.Assembler, "Elektronik", [(ProductionItemIds.CopperWire, 3), (ProductionItemIds.ProcessedCarbon, 1), (ProductionItemIds.SilicatePowder, 1)], [(ProductionItemIds.ElectronicParts, 3)], 4, 12, tier: TechnologyTier.Tier3, alternativeGroup: "electronic_parts", tags: ["electronics"]);
        yield return R("make_electronic_parts_palladium", "Palladium-Elektronikteile", MachineDefinitionIds.PrecisionManufacturer, "Elektronik", [(ProductionItemIds.Palladium, 1), (ProductionItemIds.CopperWire, 2), (ProductionItemIds.SilicatePowder, 1)], [(ProductionItemIds.ElectronicParts, 5)], 6, 24, tier: TechnologyTier.Tier6, alternativeGroup: "electronic_parts", tags: ["electronics", "alternative", "efficient"]);
        yield return R("make_sensor", "Sensoren", MachineDefinitionIds.Assembler, "Elektronik", [(ProductionItemIds.CircuitBoard, 1), (ProductionItemIds.ElectronicParts, 2), (ProductionItemIds.GoldIngot, 1)], [(ProductionItemIds.Sensor, 1)], 6, 16, tier: TechnologyTier.Tier3, tags: ["electronics"]);
        yield return R(DefaultRecipeIds.MakeControlModule, "Steuerungsmodule", MachineDefinitionIds.AdvancedFabricator, "Elektronik", [(ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.Sensor, 1), (ProductionItemIds.ElectronicParts, 3), (ProductionItemIds.CopperCable, 2)], [(ProductionItemIds.ControlModule, 1)], 9, 24, tier: TechnologyTier.Tier4, alternativeGroup: "control_module", tags: ["electronics", "control"]);
        yield return R("make_computer", "Computer", MachineDefinitionIds.AdvancedFabricator, "Elektronik", [(ProductionItemIds.ComputerChip, 2), (ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.Computer, 1)], 10, 26, tier: TechnologyTier.Tier4, tags: ["electronics", "computing"]);
        yield return R("make_power_regulator", "Stromregler", MachineDefinitionIds.Assembler, "Elektronik", [(ProductionItemIds.CircuitBoard, 1), (ProductionItemIds.CopperCable, 3), (ProductionItemIds.BatteryCell, 1)], [(ProductionItemIds.PowerRegulator, 1)], 7, 17, tier: TechnologyTier.Tier4, alternativeGroup: "power_regulator", tags: ["electronics", "energy"]);
        yield return R("make_scanner_component", "Scannerkomponenten", MachineDefinitionIds.PrecisionManufacturer, "Elektronik", [(ProductionItemIds.Sensor, 2), (ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.GoldIngot, 1)], [(ProductionItemIds.ScannerComponent, 1)], 8, 27, tier: TechnologyTier.Tier5, tags: ["electronics", "scanner"]);
        yield return R(DefaultRecipeIds.MakeElectronicsResearchPack, "Elektronik-Forschungspakete", MachineDefinitionIds.Assembler, "Forschung", [(ProductionItemIds.CircuitBoard, 1), (ProductionItemIds.ElectronicParts, 2), (ProductionItemIds.CopperCable, 1)], [(ProductionItemIds.ElectronicsResearchPack, 2)], 7, 16, tier: TechnologyTier.Tier3, tags: ["research-pack", "electronics"]);
        yield return R("make_battery_cell_lithium_free", "Nickel-Kobalt-Batteriezellen", MachineDefinitionIds.ChemicalPlant, "Batterie", [(ProductionItemIds.NickelIngot, 1), (ProductionItemIds.CobaltIngot, 1), (ProductionItemIds.SulfuricAcid, 1)], [(ProductionItemIds.BatteryCell, 2), (ProductionItemIds.ChemicalWaste, 1)], 8, 22, tier: TechnologyTier.Tier4, alternativeGroup: "battery_cell", tags: ["battery", "chemical"]);

        yield return R("make_sulfuric_acid", "Basische Schwefelsäure", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Sulfur, 2), (ProductionItemIds.Water, 4)], [(ProductionItemIds.SulfuricAcid, 3)], 9, 20, tier: TechnologyTier.Tier4, alternativeGroup: "sulfuric_acid", tags: ["chemical", "liquid", "early-chemistry"]);
        yield return R("make_sulfuric_acid_troilite", "Schwefelsäure aus Troilit", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Troilite, 2), (ProductionItemIds.Water, 2), (ProductionItemIds.Oxygen, 1)], [(ProductionItemIds.SulfuricAcid, 3), (ProductionItemIds.IronOre, 1)], 9, 22, tier: TechnologyTier.Tier5, alternativeGroup: "sulfuric_acid", tags: ["chemical", "liquid", "alternative"]);
        yield return R("make_plastic", "Frühes Kohlenstoffpolymer", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.ProcessedCarbon, 2), (ProductionItemIds.SulfuricAcid, 1), (ProductionItemIds.Water, 1)], [(ProductionItemIds.Plastic, 2), (ProductionItemIds.ChemicalWaste, 1)], 8, 20, tier: TechnologyTier.Tier4, alternativeGroup: "plastic", tags: ["chemical", "early-chemistry"]);
        yield return R("make_plastic_fuel", "Kunststoff aus Treibstoff", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Fuel, 2), (ProductionItemIds.SulfuricAcid, 1)], [(ProductionItemIds.Plastic, 4), (ProductionItemIds.ChemicalWaste, 1)], 8, 24, tier: TechnologyTier.Tier5, alternativeGroup: "plastic", tags: ["chemical", "alternative", "efficient"]);
        yield return R("make_lubricant", "Schmiermittel", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Fuel, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.Lubricant, 3)], 6, 17, tier: TechnologyTier.Tier4, alternativeGroup: "lubricant", tags: ["chemical", "liquid"]);
        yield return R("make_lubricant_sulfur", "Hochdruck-Schmiermittel", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Fuel, 2), (ProductionItemIds.Sulfur, 1), (ProductionItemIds.Phosphorus, 1)], [(ProductionItemIds.Lubricant, 5), (ProductionItemIds.ChemicalWaste, 1)], 9, 23, tier: TechnologyTier.Tier5, alternativeGroup: "lubricant", tags: ["chemical", "liquid", "alternative"]);
        yield return R("make_coolant", "Kühlmittel", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Water, 3), (ProductionItemIds.SulfuricAcid, 1), (ProductionItemIds.CopperNickelAlloy, 1)], [(ProductionItemIds.Coolant, 4), (ProductionItemIds.ChemicalWaste, 1)], 8, 20, tier: TechnologyTier.Tier4, alternativeGroup: "coolant", tags: ["chemical", "liquid"]);
        yield return R("make_coolant_ice", "Kühlmittel aus Wassereis", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.WaterIce, 4), (ProductionItemIds.Sylvite, 1)], [(ProductionItemIds.Coolant, 3)], 7, 18, tier: TechnologyTier.Tier4, alternativeGroup: "coolant", tags: ["chemical", "liquid", "alternative"]);
        yield return R(DefaultRecipeIds.MakeChemistryResearchPack, "Chemie-Forschungspakete", MachineDefinitionIds.ChemicalPlant, "Forschung", [(ProductionItemIds.SulfuricAcid, 1), (ProductionItemIds.Plastic, 2), (ProductionItemIds.SteelPipe, 1)], [(ProductionItemIds.ChemistryResearchPack, 2), (ProductionItemIds.ChemicalWaste, 1)], 9, 22, tier: TechnologyTier.Tier4, tags: ["research-pack", "chemical"]);
        yield return R("refine_fuel_catalytic", "Katalytischer Treibstoff", MachineDefinitionIds.ChemicalPlant, "Treibstoff", [(ProductionItemIds.Hydrogen, 2), (ProductionItemIds.ProcessedCarbon, 1), (ProductionItemIds.Palladium, 1)], [(ProductionItemIds.Fuel, 4)], 7, 25, tier: TechnologyTier.Tier5, alternativeGroup: "fuel", tags: ["chemical", "alternative", "efficient"]);
        yield return R("neutralize_chemical_waste", "Chemischen Abfall neutralisieren", MachineDefinitionIds.WasteProcessor, "Abfall", [(ProductionItemIds.ChemicalWaste, 3), (ProductionItemIds.Calcite, 1)], [(ProductionItemIds.Water, 1), (ProductionItemIds.SilicatePowder, 1)], 10, 26, tier: TechnologyTier.Tier5, tags: ["waste", "chemical"]);
    }

    private static IEnumerable<RecipeDefinition> CreateAdvancedAndSpaceDefinitions()
    {
        yield return R("make_high_performance_motor", "Hochleistungsmotoren", MachineDefinitionIds.AdvancedFabricator, "Industrie", [(ProductionItemIds.Motor, 2), (ProductionItemIds.Rotor, 2), (ProductionItemIds.Lubricant, 1), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.HighPerformanceMotor, 1)], 11, 32, tier: TechnologyTier.Tier5, alternativeGroup: "high_performance_motor", tags: ["mechanical", "advanced"]);
        yield return R("make_high_performance_motor_ruthenium", "Ruthenium-Hochleistungsmotoren", MachineDefinitionIds.PrecisionManufacturer, "Industrie", [(ProductionItemIds.SmallElectricMotor, 2), (ProductionItemIds.Ruthenium, 1), (ProductionItemIds.PrecisionComponent, 1)], [(ProductionItemIds.HighPerformanceMotor, 2)], 12, 42, tier: TechnologyTier.Tier8, alternativeGroup: "high_performance_motor", tags: ["mechanical", "alternative", "optimization"]);
        yield return R("make_turbine", "Turbinen", MachineDefinitionIds.AdvancedFabricator, "Industrie", [(ProductionItemIds.HighPerformanceMotor, 1), (ProductionItemIds.TitaniumPlate, 3), (ProductionItemIds.PrecisionComponent, 2), (ProductionItemIds.Lubricant, 1)], [(ProductionItemIds.Turbine, 1)], 12, 36, tier: TechnologyTier.Tier5, tags: ["mechanical", "energy"]);
        yield return R("make_compressor", "Kompressoren", MachineDefinitionIds.AdvancedFabricator, "Industrie", [(ProductionItemIds.HighPerformanceMotor, 1), (ProductionItemIds.Pump, 2), (ProductionItemIds.PressureVessel, 1), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.Compressor, 1)], 12, 35, tier: TechnologyTier.Tier5, tags: ["mechanical", "fluid"]);
        yield return R(DefaultRecipeIds.MakePrecisionComponent, "Präzisionsbauteile", MachineDefinitionIds.PrecisionManufacturer, "Präzision", [(ProductionItemIds.TitaniumPlate, 2), (ProductionItemIds.Palladium, 1), (ProductionItemIds.ComputerChip, 1)], [(ProductionItemIds.PrecisionComponent, 2)], 9, 31, tier: TechnologyTier.Tier5, alternativeGroup: "precision_component", tags: ["precision"]);
        yield return R("make_precision_component_osmium", "Osmium-Präzisionsbauteile", MachineDefinitionIds.PrecisionManufacturer, "Präzision", [(ProductionItemIds.Osmium, 1), (ProductionItemIds.TitaniumCobaltAlloy, 1), (ProductionItemIds.ComputerChip, 1)], [(ProductionItemIds.PrecisionComponent, 4)], 12, 44, tier: TechnologyTier.Tier8, alternativeGroup: "precision_component", tags: ["precision", "alternative", "optimization"]);
        yield return R("make_titanium_frame", "Titanrahmen", MachineDefinitionIds.AdvancedFabricator, "Struktur", [(ProductionItemIds.TitaniumPlate, 4), (ProductionItemIds.ReinforcedPlate, 2), (ProductionItemIds.SteelBeam, 2)], [(ProductionItemIds.TitaniumFrame, 1)], 10, 27, tier: TechnologyTier.Tier5, tags: ["structural"]);
        yield return R("make_cooling_system", "Kühlsysteme", MachineDefinitionIds.AdvancedFabricator, "Industrie", [(ProductionItemIds.Pump, 2), (ProductionItemIds.Coolant, 3), (ProductionItemIds.CopperNickelAlloy, 2), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.CoolingSystem, 1)], 11, 31, tier: TechnologyTier.Tier5, tags: ["thermal", "fluid"]);
        yield return R("make_pressure_vessel", "Druckbehälter", MachineDefinitionIds.AdvancedFabricator, "Industrie", [(ProductionItemIds.SteelPlate, 4), (ProductionItemIds.TitaniumPlate, 2), (ProductionItemIds.Valve, 2)], [(ProductionItemIds.PressureVessel, 1)], 9, 25, tier: TechnologyTier.Tier4, alternativeGroup: "pressure_vessel", tags: ["fluid", "structural"]);
        yield return R("make_production_computer", "Produktionscomputer", MachineDefinitionIds.PrecisionManufacturer, "Computer", [(ProductionItemIds.Computer, 1), (ProductionItemIds.ControlModule, 2), (ProductionItemIds.Sensor, 2), (ProductionItemIds.PowerRegulator, 1)], [(ProductionItemIds.ProductionComputer, 1)], 12, 38, tier: TechnologyTier.Tier5, tags: ["computing", "automation"]);
        yield return R("make_factory_controller", "Fabriksteuerungen", MachineDefinitionIds.PrecisionManufacturer, "Computer", [(ProductionItemIds.ProductionComputer, 1), (ProductionItemIds.AutomationModule, 3), (ProductionItemIds.ScannerComponent, 1), (ProductionItemIds.PrecisionComponent, 1)], [(ProductionItemIds.FactoryController, 1)], 14, 44, tier: TechnologyTier.Tier6, alternativeGroup: "factory_controller", tags: ["computing", "automation"]);
        yield return R("make_factory_controller_rhodium", "Rhodium-Fabriksteuerungen", MachineDefinitionIds.PrecisionManufacturer, "Computer", [(ProductionItemIds.ProductionComputer, 1), (ProductionItemIds.Rhodium, 1), (ProductionItemIds.PrecisionComponent, 2)], [(ProductionItemIds.FactoryController, 2)], 16, 58, tier: TechnologyTier.Tier9, alternativeGroup: "factory_controller", tags: ["computing", "alternative", "optimization"]);
        yield return R("make_spaceship_module", "Raumschiffmodule", MachineDefinitionIds.AdvancedFabricator, "Raumschiff", [(ProductionItemIds.TitaniumFrame, 2), (ProductionItemIds.FactoryController, 1), (ProductionItemIds.CoolingSystem, 1), (ProductionItemIds.AdvancedPowerModule, 1)], [(ProductionItemIds.SpaceshipModule, 1)], 18, 52, tier: TechnologyTier.Tier6, tags: ["space"]);
        yield return R("make_spaceship_engine_part", "Raumschiffantriebsteile", MachineDefinitionIds.PrecisionManufacturer, "Raumschiff", [(ProductionItemIds.Turbine, 1), (ProductionItemIds.HighPerformanceMotor, 2), (ProductionItemIds.TitaniumFrame, 1), (ProductionItemIds.PrecisionComponent, 3)], [(ProductionItemIds.SpaceshipEnginePart, 1)], 18, 55, tier: TechnologyTier.Tier6, tags: ["space", "propulsion"]);
        yield return R("make_improved_landing_leg", "Verbesserte Landebeine", MachineDefinitionIds.AdvancedFabricator, "Raumschiff", [(ProductionItemIds.TitaniumFrame, 1), (ProductionItemIds.HighPerformanceMotor, 1), (ProductionItemIds.Sensor, 2), (ProductionItemIds.ReinforcedPlate, 3)], [(ProductionItemIds.ImprovedLandingLeg, 2)], 14, 39, tier: TechnologyTier.Tier6, tags: ["space", "landing"]);
        yield return R("make_high_performance_scanner", "Hochleistungsscanner", MachineDefinitionIds.PrecisionManufacturer, "Raumschiff", [(ProductionItemIds.ScannerComponent, 3), (ProductionItemIds.ProductionComputer, 1), (ProductionItemIds.Palladium, 1)], [(ProductionItemIds.HighPerformanceScanner, 1)], 15, 48, tier: TechnologyTier.Tier6, tags: ["space", "scanner"]);
        yield return R("make_navigation_computer", "Navigationscomputer", MachineDefinitionIds.PrecisionManufacturer, "Raumschiff", [(ProductionItemIds.ProductionComputer, 1), (ProductionItemIds.HighPerformanceScanner, 1), (ProductionItemIds.ControlModule, 2), (ProductionItemIds.GoldIngot, 2)], [(ProductionItemIds.NavigationComputer, 1)], 16, 51, tier: TechnologyTier.Tier6, alternativeGroup: "navigation_computer", tags: ["space", "computing"]);
        yield return R("make_navigation_computer_iridium", "Iridium-Navigationscomputer", MachineDefinitionIds.PrecisionManufacturer, "Raumschiff", [(ProductionItemIds.ProductionComputer, 1), (ProductionItemIds.IridiumIngot, 1), (ProductionItemIds.PrecisionComponent, 2)], [(ProductionItemIds.NavigationComputer, 2)], 18, 68, tier: TechnologyTier.Tier9, alternativeGroup: "navigation_computer", tags: ["space", "alternative", "optimization"]);
        yield return R("make_advanced_power_module", "Erweiterte Strommodule", MachineDefinitionIds.AdvancedFabricator, "Energie", [(ProductionItemIds.PowerRegulator, 2), (ProductionItemIds.HighPerformanceBattery, 1), (ProductionItemIds.CopperCable, 4), (ProductionItemIds.CoolingSystem, 1)], [(ProductionItemIds.AdvancedPowerModule, 1)], 13, 40, tier: TechnologyTier.Tier6, alternativeGroup: "advanced_power_module", tags: ["energy", "space"]);
        yield return R("make_fuel_pump", "Treibstoffpumpen", MachineDefinitionIds.AdvancedFabricator, "Raumschiff", [(ProductionItemIds.Pump, 2), (ProductionItemIds.HighPerformanceMotor, 1), (ProductionItemIds.Valve, 3), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.FuelPump, 1)], 12, 36, tier: TechnologyTier.Tier5, tags: ["space", "fuel"]);
        yield return R("make_life_support_system", "Lebenserhaltungssysteme", MachineDefinitionIds.AdvancedFabricator, "Raumschiff", [(ProductionItemIds.CoolingSystem, 1), (ProductionItemIds.Compressor, 1), (ProductionItemIds.ControlModule, 1), (ProductionItemIds.OxygenContainer, 2)], [(ProductionItemIds.LifeSupportSystem, 1)], 17, 47, returned: [(ProductionItemIds.EmptyGasContainer, 2)], tier: TechnologyTier.Tier6, tags: ["space", "life-support"]);
        yield return R("make_transport_drone", "Automatische Transportdrohnen", MachineDefinitionIds.PrecisionManufacturer, "Logistik", [(ProductionItemIds.HighPerformanceMotor, 2), (ProductionItemIds.NavigationComputer, 1), (ProductionItemIds.HighPerformanceBattery, 1), (ProductionItemIds.TitaniumFrame, 1)], [(ProductionItemIds.TransportDrone, 1)], 18, 53, tier: TechnologyTier.Tier7, tags: ["space", "logistics", "drone"]);
        yield return R(DefaultRecipeIds.MakeHighPerformanceBattery, "Hochleistungsbatterien", MachineDefinitionIds.ChemicalPlant, "Energie", [(ProductionItemIds.BatteryCell, 4), (ProductionItemIds.SulfuricAcid, 1), (ProductionItemIds.NickelSteel, 1), (ProductionItemIds.PowerRegulator, 1)], [(ProductionItemIds.HighPerformanceBattery, 1), (ProductionItemIds.ChemicalWaste, 1)], 11, 32, tier: TechnologyTier.Tier5, tags: ["battery", "chemical"]);
        yield return R(DefaultRecipeIds.MakeSpaceResearchPack, "Weltraum-Forschungspakete", MachineDefinitionIds.PrecisionManufacturer, "Forschung", [(ProductionItemIds.SpaceshipModule, 1), (ProductionItemIds.LifeSupportSystem, 1), (ProductionItemIds.AdvancedPowerModule, 1)], [(ProductionItemIds.SpaceResearchPack, 2)], 20, 62, tier: TechnologyTier.Tier7, tags: ["research-pack", "space"]);
        yield return R("optimize_precision_components", "Optimierte Präzisionsbauteile", MachineDefinitionIds.PrecisionManufacturer, "Optimierung", [(ProductionItemIds.PrecisionComponent, 2), (ProductionItemIds.Rhodium, 1), (ProductionItemIds.Lubricant, 1)], [(ProductionItemIds.PrecisionComponent, 4)], 13, 60, tier: TechnologyTier.Tier10, alternativeGroup: "precision_component", tags: ["optimization", "endgame"]);
        yield return R("optimize_advanced_power_module", "Optimierte Strommodule", MachineDefinitionIds.PrecisionManufacturer, "Optimierung", [(ProductionItemIds.AdvancedPowerModule, 1), (ProductionItemIds.Ruthenium, 1), (ProductionItemIds.HighPerformanceBattery, 1)], [(ProductionItemIds.AdvancedPowerModule, 2)], 16, 72, tier: TechnologyTier.Tier10, alternativeGroup: "advanced_power_module", tags: ["optimization", "endgame"]);
        yield return R(DefaultRecipeIds.MakeDeepSpaceControlCore, "Tiefenraum-Steuerkern", MachineDefinitionIds.PrecisionManufacturer, "Tiefenraum", [(ProductionItemIds.FactoryController, 2), (ProductionItemIds.AdvancedPowerModule, 2), (ProductionItemIds.NuclearFuelCell, 1), (ProductionItemIds.PrecisionComponent, 4)], [(ProductionItemIds.DeepSpaceControlCore, 1)], 32, 140, tier: TechnologyTier.Tier10, tags: ["space", "endgame", "control"]);
        yield return R(DefaultRecipeIds.MakeQuantumNavigationModule, "Quanten-Navigationsmodul", MachineDefinitionIds.PrecisionManufacturer, "Tiefenraum", [(ProductionItemIds.NavigationComputer, 2), (ProductionItemIds.IridiumIngot, 2), (ProductionItemIds.HighPerformanceScanner, 2), (ProductionItemIds.NuclearFuelCell, 1)], [(ProductionItemIds.QuantumNavigationModule, 1)], 36, 165, tier: TechnologyTier.Tier10, tags: ["space", "endgame", "navigation"]);
    }

    private static IEnumerable<RecipeDefinition> CreateNuclearAndWasteDefinitions()
    {
        yield return R("make_improved_radiation_suit", "Verbesserten Strahlenschutzanzug fertigen", MachineDefinitionIds.AdvancedFabricator, "Strahlenschutz", [(ProductionItemIds.TitaniumPlate, 4), (ProductionItemIds.CoolingSystem, 1), (ProductionItemIds.ElectronicParts, 4)], [(ProductionItemIds.ImprovedRadiationSuit, 1)], 18, 58, tier: TechnologyTier.Tier7, tags: ["nuclear", "safety", "suit"]);
        yield return R("make_nuclear_radiation_suit", "Nuklear-Schutzanzug fertigen", MachineDefinitionIds.PrecisionManufacturer, "Strahlenschutz", [(ProductionItemIds.ImprovedRadiationSuit, 1), (ProductionItemIds.ShieldedWasteContainer, 2), (ProductionItemIds.PrecisionComponent, 4), (ProductionItemIds.HighPerformanceBattery, 1)], [(ProductionItemIds.NuclearRadiationSuit, 1)], 26, 110, tier: TechnologyTier.Tier8, tags: ["nuclear", "safety", "suit"]);
        yield return R("crush_uranium_ore", "Uranerz zerkleinern", MachineDefinitionIds.Crusher, "Nuklear", [(ProductionItemIds.UraniumOre, 2)], [(ProductionItemIds.CrushedUraniumOre, 3)], 7, 18, tier: TechnologyTier.Tier7, tags: ["nuclear", "radioactive"]);
        yield return R(DefaultRecipeIds.ProcessUraniumOre, "Uranerz aufbereiten", MachineDefinitionIds.UraniumProcessor, "Nuklear", [(ProductionItemIds.CrushedUraniumOre, 4), (ProductionItemIds.SulfuricAcid, 2), (ProductionItemIds.Water, 2)], [(ProductionItemIds.UraniumConcentrate, 2), (ProductionItemIds.ChemicalWaste, 1)], 14, 85, tier: TechnologyTier.Tier7, alternativeGroup: "uranium_concentrate", tags: ["nuclear", "radioactive", "chemical"]);
        yield return R("process_uranium_ore_catalytic", "Katalytische Uranaufbereitung", MachineDefinitionIds.UraniumProcessor, "Nuklear", [(ProductionItemIds.CrushedUraniumOre, 3), (ProductionItemIds.SulfuricAcid, 2), (ProductionItemIds.Palladium, 1)], [(ProductionItemIds.UraniumConcentrate, 3), (ProductionItemIds.ChemicalWaste, 1)], 16, 105, tier: TechnologyTier.Tier8, alternativeGroup: "uranium_concentrate", tags: ["nuclear", "radioactive", "alternative", "efficient"]);
        yield return R(DefaultRecipeIds.EnrichUranium, "Uran anreichern", MachineDefinitionIds.UraniumProcessor, "Nuklear", [(ProductionItemIds.UraniumConcentrate, 3), (ProductionItemIds.Coolant, 2)], [(ProductionItemIds.EnrichedUranium, 1), (ProductionItemIds.RadioactiveWaste, 1)], 20, 160, tier: TechnologyTier.Tier7, alternativeGroup: "enriched_uranium", tags: ["nuclear", "radioactive"]);
        yield return R("enrich_uranium_cascade", "Kaskaden-Urananreicherung", MachineDefinitionIds.UraniumProcessor, "Nuklear", [(ProductionItemIds.UraniumConcentrate, 5), (ProductionItemIds.Coolant, 3), (ProductionItemIds.PrecisionComponent, 1)], [(ProductionItemIds.EnrichedUranium, 2), (ProductionItemIds.RadioactiveWaste, 1)], 24, 230, tier: TechnologyTier.Tier9, alternativeGroup: "enriched_uranium", tags: ["nuclear", "radioactive", "alternative", "efficient"]);
        yield return R(DefaultRecipeIds.MakeNuclearFuelCell, "Uran-Brennstoffzellen", MachineDefinitionIds.FuelCellFabricator, "Nuklear", [(ProductionItemIds.EnrichedUranium, 1), (ProductionItemIds.TitaniumCobaltAlloy, 2), (ProductionItemIds.PrecisionComponent, 2), (ProductionItemIds.ControlModule, 1)], [(ProductionItemIds.NuclearFuelCell, 1)], 18, 120, tier: TechnologyTier.Tier8, alternativeGroup: "nuclear_fuel_cell", tags: ["nuclear", "radioactive", "fuel-cell"]);
        yield return R("make_nuclear_fuel_cell_ruthenium", "Ruthenium-Brennstoffzellen", MachineDefinitionIds.FuelCellFabricator, "Nuklear", [(ProductionItemIds.EnrichedUranium, 1), (ProductionItemIds.Ruthenium, 1), (ProductionItemIds.PrecisionComponent, 2)], [(ProductionItemIds.NuclearFuelCell, 2)], 22, 175, tier: TechnologyTier.Tier9, alternativeGroup: "nuclear_fuel_cell", tags: ["nuclear", "radioactive", "alternative", "efficient"]);
        yield return R(DefaultRecipeIds.MakeNuclearResearchPack, "Nuklear-Forschungspakete", MachineDefinitionIds.FuelCellFabricator, "Forschung", [(ProductionItemIds.UraniumConcentrate, 1), (ProductionItemIds.ControlModule, 1), (ProductionItemIds.CoolingSystem, 1)], [(ProductionItemIds.NuclearResearchPack, 2), (ProductionItemIds.RadioactiveWaste, 1)], 16, 90, tier: TechnologyTier.Tier7, tags: ["research-pack", "nuclear", "radioactive"]);
        yield return R(DefaultRecipeIds.ReprocessSpentFuelCell, "Verbrauchte Brennstoffzellen aufarbeiten", MachineDefinitionIds.WasteProcessor, "Atommüll", [(ProductionItemIds.SpentFuelCell, 1), (ProductionItemIds.SulfuricAcid, 2)], [(ProductionItemIds.UraniumConcentrate, 1), (ProductionItemIds.RadioactiveWaste, 2)], 18, 110, tier: TechnologyTier.Tier8, tags: ["nuclear", "radioactive", "waste"]);
        yield return R(DefaultRecipeIds.StabilizeRadioactiveWaste, "Radioaktiven Abfall stabilisieren", MachineDefinitionIds.WasteProcessor, "Atommüll", [(ProductionItemIds.RadioactiveWaste, 3), (ProductionItemIds.Calcite, 2), (ProductionItemIds.SilicatePowder, 2)], [(ProductionItemIds.StabilizedWaste, 2)], 20, 95, tier: TechnologyTier.Tier8, tags: ["nuclear", "radioactive", "waste"]);
        yield return R(DefaultRecipeIds.MakeShieldedWasteContainer, "Abgeschirmte Abfallbehälter", MachineDefinitionIds.WasteProcessor, "Atommüll", [(ProductionItemIds.StabilizedWaste, 2), (ProductionItemIds.SteelPlate, 4), (ProductionItemIds.NickelSteel, 1)], [(ProductionItemIds.ShieldedWasteContainer, 1)], 22, 105, tier: TechnologyTier.Tier8, tags: ["nuclear", "radioactive", "waste", "containment"]);
    }

    private static IEnumerable<RecipeDefinition> CreateSpecialResourceDefinitions()
    {
        yield return R("separate_troilite", "Troilit trennen", MachineDefinitionIds.Refinery, "Spezialmineral", [(ProductionItemIds.Troilite, 3)], [(ProductionItemIds.Sulfur, 2), (ProductionItemIds.IronOre, 2)], 8, 18, tier: TechnologyTier.Tier3, tags: ["special-resource"]);
        yield return R("separate_schreibersite", "Schreibersit trennen", MachineDefinitionIds.Refinery, "Spezialmineral", [(ProductionItemIds.Schreibersite, 2)], [(ProductionItemIds.Phosphorus, 2), (ProductionItemIds.IronOre, 1)], 9, 21, tier: TechnologyTier.Tier4, tags: ["special-resource"]);
        yield return R("mill_olivine", "Olivin mahlen", MachineDefinitionIds.Crusher, "Spezialmineral", [(ProductionItemIds.Olivine, 2)], [(ProductionItemIds.SilicatePowder, 3), (ProductionItemIds.NickelOre, 1)], 4, 8, tier: TechnologyTier.Tier2, tags: ["special-resource"]);
        yield return R("refine_kamacite", "Kamacit raffinieren", MachineDefinitionIds.Foundry, "Meteoritenlegierung", [(ProductionItemIds.Kamacite, 3), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.IronIngot, 2), (ProductionItemIds.NickelIngot, 1)], 8, 17, tier: TechnologyTier.Tier3, tags: ["special-resource", "alloy"]);
        yield return R("refine_taenite", "Taenit raffinieren", MachineDefinitionIds.Foundry, "Meteoritenlegierung", [(ProductionItemIds.Taenite, 3), (ProductionItemIds.IronIngot, 1)], [(ProductionItemIds.NickelSteel, 3)], 9, 19, tier: TechnologyTier.Tier4, alternativeGroup: "nickel_steel", tags: ["special-resource", "alloy", "alternative"]);
        yield return R("process_halite_coolant", "Halit-Kühlmittel", MachineDefinitionIds.ChemicalPlant, "Spezialmineral", [(ProductionItemIds.Halite, 2), (ProductionItemIds.Water, 3)], [(ProductionItemIds.Coolant, 4)], 7, 16, tier: TechnologyTier.Tier4, alternativeGroup: "coolant", tags: ["special-resource", "chemical", "alternative"]);
        yield return R("process_sylvite_battery", "Sylvin-Batteriechemie", MachineDefinitionIds.ChemicalPlant, "Spezialmineral", [(ProductionItemIds.Sylvite, 2), (ProductionItemIds.NickelIngot, 1), (ProductionItemIds.SulfuricAcid, 1)], [(ProductionItemIds.BatteryCell, 3), (ProductionItemIds.ChemicalWaste, 1)], 9, 23, tier: TechnologyTier.Tier4, alternativeGroup: "battery_cell", tags: ["special-resource", "battery", "alternative"]);
        yield return R("process_calcite_neutralizer", "Calcit-Neutralisator", MachineDefinitionIds.WasteProcessor, "Spezialmineral", [(ProductionItemIds.Calcite, 3), (ProductionItemIds.ChemicalWaste, 2)], [(ProductionItemIds.SilicatePowder, 2), (ProductionItemIds.Water, 1)], 8, 18, tier: TechnologyTier.Tier5, tags: ["special-resource", "waste"]);
        yield return R("palladium_catalyst_electronics", "Palladium-katalysierte Elektronik", MachineDefinitionIds.PrecisionManufacturer, "Edelmetall", [(ProductionItemIds.Palladium, 1), (ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.CopperWire, 2)], [(ProductionItemIds.ElectronicParts, 6)], 8, 30, tier: TechnologyTier.Tier6, alternativeGroup: "electronic_parts", tags: ["special-resource", "alternative", "electronics"]);
        yield return R("rhodium_control_module", "Rhodium-Steuerungsmodule", MachineDefinitionIds.PrecisionManufacturer, "Edelmetall", [(ProductionItemIds.Rhodium, 1), (ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.Sensor, 1)], [(ProductionItemIds.ControlModule, 2)], 10, 38, tier: TechnologyTier.Tier7, alternativeGroup: "control_module", tags: ["special-resource", "alternative", "electronics"]);
        yield return R("osmium_reinforced_plate", "Osmium-Verbundplatten", MachineDefinitionIds.PrecisionManufacturer, "Edelmetall", [(ProductionItemIds.Osmium, 1), (ProductionItemIds.SteelPlate, 3), (ProductionItemIds.TitaniumPlate, 1)], [(ProductionItemIds.ReinforcedPlate, 5)], 11, 42, tier: TechnologyTier.Tier7, alternativeGroup: "reinforced_plate", tags: ["special-resource", "alternative", "structural"]);
        yield return R("ruthenium_power_regulator", "Ruthenium-Stromregler", MachineDefinitionIds.PrecisionManufacturer, "Edelmetall", [(ProductionItemIds.Ruthenium, 1), (ProductionItemIds.CircuitBoard, 2), (ProductionItemIds.CopperCable, 2)], [(ProductionItemIds.PowerRegulator, 3)], 10, 40, tier: TechnologyTier.Tier7, alternativeGroup: "power_regulator", tags: ["special-resource", "alternative", "energy"]);
        yield return R("phosphorus_electronic_parts", "Phosphor-Elektronikteile", MachineDefinitionIds.ChemicalPlant, "Chemie", [(ProductionItemIds.Phosphorus, 1), (ProductionItemIds.SilicatePowder, 2), (ProductionItemIds.CopperWire, 2)], [(ProductionItemIds.ElectronicParts, 4), (ProductionItemIds.ChemicalWaste, 1)], 8, 22, tier: TechnologyTier.Tier4, alternativeGroup: "electronic_parts", tags: ["special-resource", "chemical", "electronics"]);
        yield return R("olivine_ceramic_pressure_vessel", "Olivin-Druckbehälter", MachineDefinitionIds.AdvancedFabricator, "Keramik", [(ProductionItemIds.Olivine, 3), (ProductionItemIds.SteelPlate, 2), (ProductionItemIds.Valve, 1)], [(ProductionItemIds.PressureVessel, 2)], 10, 28, tier: TechnologyTier.Tier5, alternativeGroup: "pressure_vessel", tags: ["special-resource", "alternative", "fluid"]);
        yield return R("meteorite_precision_composite", "Meteoriten-Präzisionsverbund", MachineDefinitionIds.PrecisionManufacturer, "Optimierung", [(ProductionItemIds.Kamacite, 1), (ProductionItemIds.Taenite, 1), (ProductionItemIds.Schreibersite, 1), (ProductionItemIds.Palladium, 1)], [(ProductionItemIds.PrecisionComponent, 5)], 15, 55, tier: TechnologyTier.Tier8, alternativeGroup: "precision_component", tags: ["special-resource", "alternative", "optimization"]);
    }

    private static RecipeDefinition R(
        string id,
        string name,
        MachineDefinitionId machine,
        string category,
        IEnumerable<(ItemId Id, int Amount)> inputs,
        IEnumerable<(ItemId Id, int Amount)> outputs,
        double duration,
        double power,
        IEnumerable<(ItemId Id, int Amount)>? returned = null,
        ResearchId? unlock = null,
        TechnologyTier tier = TechnologyTier.Tier1,
        string? alternativeGroup = null,
        IEnumerable<string>? tags = null,
        ItemId? sourceResourceId = null) =>
        R(new RecipeId(id), name, machine, category, inputs, outputs, duration, power, returned, unlock,
            tier, alternativeGroup, tags, sourceResourceId);

    private static RecipeDefinition R(
        RecipeId id,
        string name,
        MachineDefinitionId machine,
        string category,
        IEnumerable<(ItemId Id, int Amount)> inputs,
        IEnumerable<(ItemId Id, int Amount)> outputs,
        double duration,
        double power,
        IEnumerable<(ItemId Id, int Amount)>? returned = null,
        ResearchId? unlock = null,
        TechnologyTier tier = TechnologyTier.Tier1,
        string? alternativeGroup = null,
        IEnumerable<string>? tags = null,
        ItemId? sourceResourceId = null) =>
        new(
            id,
            name,
            machine,
            category,
            inputs.Select(input => new ItemAmount(input.Id, input.Amount)),
            outputs.Select(output => new ItemAmount(output.Id, output.Amount)),
            duration,
            power,
            returned?.Select(item => new ItemAmount(item.Id, item.Amount)),
            unlock,
            tier,
            alternativeGroup,
            tags,
            sourceResourceId);
}

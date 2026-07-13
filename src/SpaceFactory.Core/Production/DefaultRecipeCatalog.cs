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
    public static readonly RecipeId RefineFuel = new("refine_fuel");
    public static readonly RecipeId PackageFuel = new("package_fuel");
    public static readonly RecipeId FillFuelContainer = new("fill_fuel_container");
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
        yield return R("process_carbon", "Kohlenstoff verarbeiten", MachineDefinitionIds.Crusher, "Gestein", [(ProductionItemIds.Carbon, 1)], [(ProductionItemIds.ProcessedCarbon, 1)], 1.5, 4);

        yield return R(DefaultRecipeIds.SmeltIronOre, "Eisenbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.IronOre, 2)], [(ProductionItemIds.IronIngot, 1)], 4, 7);
        yield return R(DefaultRecipeIds.SmeltCrushedIronOre, "Eisenbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedIronOre, 2)], [(ProductionItemIds.IronIngot, 2)], 2.8, 7);
        yield return R("smelt_copper_ore", "Kupferbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CopperOre, 2)], [(ProductionItemIds.CopperIngot, 1)], 4, 7);
        yield return R("smelt_crushed_copper_ore", "Kupferbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedCopperOre, 2)], [(ProductionItemIds.CopperIngot, 2)], 2.8, 7);
        yield return R("smelt_nickel_ore", "Nickelbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.NickelOre, 2)], [(ProductionItemIds.NickelIngot, 1)], 4.5, 8);
        yield return R("smelt_crushed_nickel_ore", "Nickelbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedNickelOre, 2)], [(ProductionItemIds.NickelIngot, 2)], 3, 8);
        yield return R("smelt_cobalt_ore", "Kobaltbarren", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CobaltOre, 2)], [(ProductionItemIds.CobaltIngot, 1)], 5, 9);
        yield return R("smelt_titanium_ore", "Titanbarren aus Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.TitaniumOre, 2)], [(ProductionItemIds.TitaniumIngot, 1)], 6, 10);
        yield return R("smelt_crushed_titanium_ore", "Titanbarren aus zerkleinertem Erz", MachineDefinitionIds.Smelter, "Metall", [(ProductionItemIds.CrushedTitaniumOre, 2)], [(ProductionItemIds.TitaniumIngot, 2)], 4, 10);
        yield return R("smelt_gold_ore", "Goldbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.GoldOre, 2)], [(ProductionItemIds.GoldIngot, 1)], 5, 9);
        yield return R("smelt_platinum_ore", "Platinbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.PlatinumOre, 2)], [(ProductionItemIds.PlatinumIngot, 1)], 6, 10);
        yield return R("smelt_iridium", "Iridiumbarren", MachineDefinitionIds.Smelter, "Edelmetall", [(ProductionItemIds.Iridium, 2)], [(ProductionItemIds.IridiumIngot, 1)], 8, 12);

        yield return R(DefaultRecipeIds.MakeSteel, "Stahlbarren", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.IronIngot, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.SteelIngot, 2)], 6, 14, unlock: DefaultResearchIds.AdvancedMetallurgy);
        yield return R("make_nickel_steel", "Nickelstahl", MachineDefinitionIds.Foundry, "Legierung", [(ProductionItemIds.NickelIngot, 1), (ProductionItemIds.IronIngot, 2)], [(ProductionItemIds.NickelSteel, 2)], 7, 14, unlock: DefaultResearchIds.AdvancedMetallurgy);
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
        yield return R(DefaultRecipeIds.MakeEmptyFuelContainer, "Leere Treibstoffbehälter", MachineDefinitionIds.Constructor, "Behälter", [(ProductionItemIds.SteelIngot, 1), (ProductionItemIds.CopperNickelAlloy, 1)], [(ProductionItemIds.EmptyFuelContainer, 2)], 3.5, 7, unlock: DefaultResearchIds.FuelProduction);

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

        yield return R(DefaultRecipeIds.RefineFuel, "Treibstoff raffinieren", MachineDefinitionIds.Refinery, "Treibstoff", [(ProductionItemIds.Hydrogen, 2), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.Fuel, 2)], 8, 16, unlock: DefaultResearchIds.FuelProduction);
        yield return R(DefaultRecipeIds.PackageFuel, "Treibstoff abfüllen", MachineDefinitionIds.Refinery, "Treibstoffbehälter", [(ProductionItemIds.Fuel, 2), (ProductionItemIds.EmptyFuelContainer, 1)], [(ProductionItemIds.FuelContainer, 1)], 5, 10, unlock: DefaultResearchIds.FuelProduction);
        yield return R(DefaultRecipeIds.FillFuelContainer, "Treibstoffbehälter füllen", MachineDefinitionIds.Refinery, "Treibstoffbehälter", [(ProductionItemIds.HydrogenContainer, 1), (ProductionItemIds.EmptyFuelContainer, 1), (ProductionItemIds.ProcessedCarbon, 1)], [(ProductionItemIds.FuelContainer, 1)], 10, 16, returned: [(ProductionItemIds.EmptyGasContainer, 1)], unlock: DefaultResearchIds.FuelProduction);
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
        ResearchId? unlock = null) =>
        R(new RecipeId(id), name, machine, category, inputs, outputs, duration, power, returned, unlock);

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
        ResearchId? unlock = null) =>
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
            unlock);
}

using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Research;

public static class DefaultResearchIds
{
    public static readonly ResearchId AdvancedMetallurgy = new("advanced_metallurgy");
    public static readonly ResearchId HydrogenTechnology = new("hydrogen_technology");
    public static readonly ResearchId FuelProduction = new("fuel_production");
    public static readonly ResearchId ImprovedEnergySupply = new("improved_energy_supply");
    public static readonly ResearchId AdvancedElectronics = new("advanced_electronics");
    public static readonly ResearchId SpaceshipComponents = new("spaceship_components");
}

public static class DefaultResearchCatalog
{
    public static ResearchCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<ResearchDefinition> CreateDefinitions()
    {
        yield return D(
            DefaultResearchIds.AdvancedMetallurgy,
            "Erweiterte Metallverarbeitung",
            "Schaltet die Giesserei und hochwertige Legierungen frei.",
            [(ProductionItemIds.IronIngot, 20), (ProductionItemIds.CopperIngot, 12)],
            45,
            9,
            machines: [MachineDefinitionIds.Foundry],
            recipes:
            [
                DefaultRecipeIds.MakeSteel,
                new RecipeId("make_nickel_steel"),
                new RecipeId("make_titanium_cobalt_alloy"),
                new RecipeId("make_copper_nickel_alloy"),
            ]);
        yield return D(
            DefaultResearchIds.HydrogenTechnology,
            "Wasserstofftechnologie",
            "Schaltet Elektrolyse und Gasbehälter-Verarbeitung frei.",
            [(ProductionItemIds.WaterContainer, 4), (ProductionItemIds.CopperCable, 8)],
            55,
            10,
            machines: [MachineDefinitionIds.Electrolyzer],
            recipes: [DefaultRecipeIds.ElectrolyzeWater, DefaultRecipeIds.ElectrolyzeWaterContainer]);
        yield return D(
            DefaultResearchIds.FuelProduction,
            "Treibstoffproduktion",
            "Schaltet die Raffinerie und Treibstoffbehälter frei.",
            [(ProductionItemIds.HydrogenContainer, 4), (ProductionItemIds.ProcessedCarbon, 10)],
            70,
            12,
            prerequisites: [DefaultResearchIds.HydrogenTechnology, DefaultResearchIds.AdvancedMetallurgy],
            machines: [MachineDefinitionIds.Refinery],
            recipes:
            [
                DefaultRecipeIds.MakeEmptyFuelContainer,
                DefaultRecipeIds.RefineFuel,
                DefaultRecipeIds.PackageFuel,
                DefaultRecipeIds.FillFuelContainer,
            ]);
        yield return D(
            DefaultResearchIds.ImprovedEnergySupply,
            "Verbesserte Energieversorgung",
            "Schaltet den leistungsfähigen Treibstoffgenerator frei.",
            [(ProductionItemIds.FuelContainer, 3), (ProductionItemIds.Motor, 2)],
            80,
            14,
            prerequisites: [DefaultResearchIds.FuelProduction, DefaultResearchIds.AdvancedElectronics],
            machines: [MachineDefinitionIds.FuelGenerator]);
        yield return D(
            DefaultResearchIds.AdvancedElectronics,
            "Fortgeschrittene Elektronik",
            "Schaltet den Fabrikator und komplexe Elektronik frei.",
            [(ProductionItemIds.CopperCable, 14), (ProductionItemIds.GoldIngot, 4), (ProductionItemIds.SilicatePowder, 12)],
            75,
            13,
            prerequisites: [DefaultResearchIds.AdvancedMetallurgy],
            machines: [MachineDefinitionIds.Fabricator],
            recipes:
            [
                DefaultRecipeIds.MakeElectronicComponent,
                new RecipeId("make_battery_cell"),
                new RecipeId("make_machine_part"),
                new RecipeId("make_motor"),
                new RecipeId("make_computer_chip"),
                new RecipeId("make_conveyor_part"),
            ]);
        yield return D(
            DefaultResearchIds.SpaceshipComponents,
            "Raumschiffbauteile",
            "Schaltet belastbare Komponenten für spätere Raumschiff-Upgrades frei.",
            [(ProductionItemIds.TitaniumPlate, 10), (ProductionItemIds.ElectronicComponent, 8), (ProductionItemIds.TitaniumCobaltAlloy, 5)],
            100,
            18,
            prerequisites: [DefaultResearchIds.AdvancedElectronics],
            recipes: [new RecipeId("make_spaceship_part")]);
    }

    private static ResearchDefinition D(
        ResearchId id,
        string name,
        string description,
        IEnumerable<(SpaceFactory.Core.Items.ItemId Id, int Amount)> costs,
        double duration,
        double power,
        IEnumerable<ResearchId>? prerequisites = null,
        IEnumerable<MachineDefinitionId>? machines = null,
        IEnumerable<RecipeId>? recipes = null) =>
        new(
            id,
            name,
            description,
            costs.Select(cost => new ItemAmount(cost.Id, cost.Amount)),
            duration,
            power,
            prerequisites,
            machines,
            recipes);
}

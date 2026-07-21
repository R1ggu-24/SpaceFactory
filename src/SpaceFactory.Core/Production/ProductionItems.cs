using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public enum ProductionItemCategory
{
    RawMaterial,
    Intermediate,
    Metal,
    Alloy,
    Component,
    Container,
    Tool,
    Research,
    Waste,
}

public enum ProductionItemPhase
{
    Solid,
    Liquid,
    Gas,
}

public enum ItemHazardKind
{
    None,
    Chemical,
    Radioactive,
}

public enum ItemContainmentRequirement
{
    None,
    ChemicalResistant,
    RadiationShielding,
    ShieldedWasteContainer,
}

public enum TransportContainerType
{
    None,
    Water,
    Gas,
    Fuel,
}

public sealed record TransportContainerDefinition(
    TransportContainerType Type,
    ItemId? ContainedSubstanceId,
    double CurrentAmount,
    double MaximumAmount,
    ItemId EmptyContainerId)
{
    public bool IsEmpty => ContainedSubstanceId is null || CurrentAmount <= 0;

    public void Validate()
    {
        if (Type == TransportContainerType.None || MaximumAmount <= 0 || CurrentAmount < 0 ||
            CurrentAmount > MaximumAmount || (ContainedSubstanceId is null && CurrentAmount > 0))
        {
            throw new ArgumentException("The transport container definition is invalid.");
        }
    }
}

public sealed record ProductionItemDefinition(
    ItemId Id,
    string DisplayName,
    ProductionItemCategory Category,
    string IconKey,
    int MaximumStackSize = 200,
    TransportContainerDefinition? Container = null,
    ProductionItemPhase Phase = ProductionItemPhase.Solid,
    ItemHazardKind HazardKind = ItemHazardKind.None,
    double HazardStrength = 0,
    ItemContainmentRequirement ContainmentRequirement = ItemContainmentRequirement.None)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(IconKey) || MaximumStackSize <= 0 ||
            !double.IsFinite(HazardStrength) || HazardStrength < 0 ||
            (HazardKind == ItemHazardKind.None &&
             (HazardStrength != 0 || ContainmentRequirement != ItemContainmentRequirement.None)) ||
            (HazardKind != ItemHazardKind.None && HazardStrength <= 0))
        {
            throw new ArgumentException($"Production item definition '{Id}' is invalid.");
        }

        Container?.Validate();
    }
}

public static class ProductionItemIds
{
    public static readonly ItemId IronOre = new("iron_ore");
    public static readonly ItemId CopperOre = new("copper_ore");
    public static readonly ItemId NickelOre = new("nickel_ore");
    public static readonly ItemId CobaltOre = new("cobalt_ore");
    public static readonly ItemId TitaniumOre = new("titanium_ore");
    public static readonly ItemId GoldOre = new("gold_ore");
    public static readonly ItemId PlatinumOre = new("platinum_ore");
    public static readonly ItemId Iridium = new("iridium");
    public static readonly ItemId SilicateRock = new("silicate_rock");
    public static readonly ItemId Carbon = new("carbon");
    public static readonly ItemId WaterIce = new("water_ice");
    public static readonly ItemId Sulfur = new("sulfur");
    public static readonly ItemId Phosphorus = new("phosphorus");
    public static readonly ItemId Olivine = new("olivine");
    public static readonly ItemId Palladium = new("palladium");
    public static readonly ItemId Rhodium = new("rhodium");
    public static readonly ItemId Osmium = new("osmium");
    public static readonly ItemId Ruthenium = new("ruthenium");
    public static readonly ItemId Schreibersite = new("schreibersite");
    public static readonly ItemId Troilite = new("troilite");
    public static readonly ItemId Kamacite = new("kamacite");
    public static readonly ItemId Taenite = new("taenite");
    public static readonly ItemId Halite = new("halite");
    public static readonly ItemId Sylvite = new("sylvite");
    public static readonly ItemId Calcite = new("calcite");
    public static readonly ItemId UraniumOre = new("uranium_ore");

    public static readonly ItemId CrushedIronOre = new("crushed_iron_ore");
    public static readonly ItemId CrushedCopperOre = new("crushed_copper_ore");
    public static readonly ItemId CrushedNickelOre = new("crushed_nickel_ore");
    public static readonly ItemId CrushedTitaniumOre = new("crushed_titanium_ore");
    public static readonly ItemId SilicatePowder = new("silicate_powder");
    public static readonly ItemId ProcessedCarbon = new("processed_carbon");
    public static readonly ItemId CrushedUraniumOre = new("crushed_uranium_ore");
    public static readonly ItemId UraniumConcentrate = new("uranium_concentrate");

    public static readonly ItemId IronIngot = new("iron_ingot");
    public static readonly ItemId CopperIngot = new("copper_ingot");
    public static readonly ItemId NickelIngot = new("nickel_ingot");
    public static readonly ItemId CobaltIngot = new("cobalt_ingot");
    public static readonly ItemId TitaniumIngot = new("titanium_ingot");
    public static readonly ItemId GoldIngot = new("gold_ingot");
    public static readonly ItemId PlatinumIngot = new("platinum_ingot");
    public static readonly ItemId IridiumIngot = new("iridium_ingot");

    public static readonly ItemId SteelIngot = new("steel_ingot");
    public static readonly ItemId NickelSteel = new("nickel_steel");
    public static readonly ItemId TitaniumCobaltAlloy = new("titanium_cobalt_alloy");
    public static readonly ItemId CopperNickelAlloy = new("copper_nickel_alloy");

    public static readonly ItemId IronPlate = new("iron_plate");
    public static readonly ItemId IronRod = new("iron_rod");
    public static readonly ItemId Screws = new("screws");
    public static readonly ItemId CopperWire = new("copper_wire");
    public static readonly ItemId CopperCable = new("copper_cable");
    public static readonly ItemId TitaniumPlate = new("titanium_plate");
    public static readonly ItemId SteelBeam = new("steel_beam");
    public static readonly ItemId IronPipe = new("iron_pipe");
    public static readonly ItemId SteelPipe = new("steel_pipe");
    public static readonly ItemId ElectronicComponent = new("electronic_component");
    public static readonly ItemId BatteryCell = new("battery_cell");
    public static readonly ItemId Motor = new("motor");
    public static readonly ItemId ComputerChip = new("computer_chip");
    public static readonly ItemId MachinePart = new("machine_part");
    public static readonly ItemId ConveyorPart = new("conveyor_part");
    public static readonly ItemId SpaceshipPart = new("spaceship_part");
    public static readonly ItemId SteelPlate = new("steel_plate");
    public static readonly ItemId ReinforcedPlate = new("reinforced_plate");
    public static readonly ItemId Rotor = new("rotor");
    public static readonly ItemId Pump = new("pump");
    public static readonly ItemId Valve = new("valve");
    public static readonly ItemId AutomationModule = new("automation_module");
    public static readonly ItemId SmallElectricMotor = new("small_electric_motor");
    public static readonly ItemId CircuitBoard = new("circuit_board");
    public static readonly ItemId ElectronicParts = new("electronic_parts");
    public static readonly ItemId Sensor = new("sensor");
    public static readonly ItemId ControlModule = new("control_module");
    public static readonly ItemId Computer = new("computer");
    public static readonly ItemId PowerRegulator = new("power_regulator");
    public static readonly ItemId ScannerComponent = new("scanner_component");
    public static readonly ItemId Plastic = new("plastic");
    public static readonly ItemId HighPerformanceMotor = new("high_performance_motor");
    public static readonly ItemId Turbine = new("turbine");
    public static readonly ItemId Compressor = new("compressor");
    public static readonly ItemId PrecisionComponent = new("precision_component");
    public static readonly ItemId TitaniumFrame = new("titanium_frame");
    public static readonly ItemId CoolingSystem = new("cooling_system");
    public static readonly ItemId PressureVessel = new("pressure_vessel");
    public static readonly ItemId ProductionComputer = new("production_computer");
    public static readonly ItemId FactoryController = new("factory_controller");
    public static readonly ItemId SpaceshipModule = new("spaceship_module");
    public static readonly ItemId SpaceshipEnginePart = new("spaceship_engine_part");
    public static readonly ItemId ImprovedLandingLeg = new("improved_landing_leg");
    public static readonly ItemId HighPerformanceScanner = new("high_performance_scanner");
    public static readonly ItemId NavigationComputer = new("navigation_computer");
    public static readonly ItemId AdvancedPowerModule = new("advanced_power_module");
    public static readonly ItemId FuelPump = new("fuel_pump");
    public static readonly ItemId LifeSupportSystem = new("life_support_system");
    public static readonly ItemId TransportDrone = new("transport_drone");
    public static readonly ItemId DeepSpaceControlCore = new("deep_space_control_core");
    public static readonly ItemId QuantumNavigationModule = new("quantum_navigation_module");
    public static readonly ItemId HighPerformanceBattery = new("high_performance_battery");
    public static readonly ItemId MobileBatteryPack = new("mobile_battery_pack");
    public static readonly ItemId MobileMinerKit = new("mobile_miner_kit");
    public static readonly ItemId AutomaticMinerKit = new("automatic_miner_kit");
    public static readonly ItemId AutomationResearchPack = new("automation_research_pack");
    public static readonly ItemId ElectronicsResearchPack = new("electronics_research_pack");
    public static readonly ItemId ChemistryResearchPack = new("chemistry_research_pack");
    public static readonly ItemId NuclearResearchPack = new("nuclear_research_pack");
    public static readonly ItemId SpaceResearchPack = new("space_research_pack");
    public static readonly ItemId EnrichedUranium = new("enriched_uranium");
    public static readonly ItemId NuclearFuelCell = new("nuclear_fuel_cell");
    public static readonly ItemId SpentFuelCell = new("spent_fuel_cell");
    public static readonly ItemId RadioactiveWaste = new("radioactive_waste");
    public static readonly ItemId StabilizedWaste = new("stabilized_waste");
    public static readonly ItemId ShieldedWasteContainer = new("shielded_waste_container");
    public static readonly ItemId PowerCable = new("power_cable");
    public static readonly ItemId ConveyorBelt = new("conveyor_belt");
    public static readonly ItemId TransportPipe = new("transport_pipe");
    public static readonly ItemId MiningTool = new("mining_tool");
    public static readonly ItemId MachineDismantlingTool = new("machine_dismantling_tool");
    public static readonly ItemId UpgradedMiningTool = new("upgraded_mining_tool");
    public static readonly ItemId HighPerformanceMiningTool = new("high_performance_mining_tool");
    public static readonly ItemId ImprovedRadiationSuit = new("improved_radiation_suit");
    public static readonly ItemId NuclearRadiationSuit = new("nuclear_radiation_suit");

    public static readonly ItemId Water = new("water");
    public static readonly ItemId Hydrogen = new("hydrogen");
    public static readonly ItemId Oxygen = new("oxygen");
    public static readonly ItemId StandardFuel = new("standard_fuel");
    public static readonly ItemId Fuel = new("fuel");
    public static ItemId HighPerformanceFuel => Fuel;
    public static readonly ItemId SulfuricAcid = new("sulfuric_acid");
    public static readonly ItemId Lubricant = new("lubricant");
    public static readonly ItemId Coolant = new("coolant");
    public static readonly ItemId ChemicalWaste = new("chemical_waste");
    public static readonly ItemId EmptyWaterContainer = new("empty_water_container");
    public static readonly ItemId EmptyGasContainer = new("empty_gas_container");
    public static readonly ItemId EmptyFuelContainer = new("empty_fuel_container");
    public static readonly ItemId WaterContainer = new("water_container");
    public static readonly ItemId HydrogenContainer = new("hydrogen_container");
    public static readonly ItemId OxygenContainer = new("oxygen_container");
    public static readonly ItemId StandardFuelContainer = new("standard_fuel_container");
    public static readonly ItemId FuelContainer = new("fuel_container");
    public static ItemId HighPerformanceFuelContainer => FuelContainer;
}

public sealed class ProductionItemCatalog
{
    private readonly IReadOnlyDictionary<ItemId, ProductionItemDefinition> _definitions;

    public ProductionItemCatalog(IEnumerable<ProductionItemDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        foreach (var definition in materialized)
        {
            definition.Validate();
        }

        _definitions = materialized.ToDictionary(definition => definition.Id);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Production item IDs must be unique.", nameof(definitions));
        }
    }

    public IReadOnlyCollection<ProductionItemDefinition> All => _definitions.Values.ToArray();

    public ProductionItemDefinition Get(ItemId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown production item '{id}'.");

    public bool TryGet(ItemId id, out ProductionItemDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);
}

public static class DefaultProductionItemCatalog
{
    public const double ContainerCapacity = 100;

    public static ProductionItemCatalog Instance { get; } = new(CreateDefinitions());

    private static IEnumerable<ProductionItemDefinition> CreateDefinitions()
    {
        static ProductionItemDefinition Item(
            ItemId id,
            string name,
            ProductionItemCategory category,
            ProductionItemPhase phase = ProductionItemPhase.Solid,
            ItemHazardKind hazard = ItemHazardKind.None,
            double hazardStrength = 0,
            ItemContainmentRequirement containment = ItemContainmentRequirement.None,
            int maximumStackSize = 200) =>
            new(
                id,
                name,
                category,
                $"production/{id.Value}",
                maximumStackSize,
                Phase: phase,
                HazardKind: hazard,
                HazardStrength: hazardStrength,
                ContainmentRequirement: containment);

        yield return Item(ProductionItemIds.IronOre, "Eisenerz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.CopperOre, "Kupfererz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.NickelOre, "Nickelerz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.CobaltOre, "Kobalterz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.TitaniumOre, "Titanerz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.GoldOre, "Golderz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.PlatinumOre, "Platinerz", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Iridium, "Iridium", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.SilicateRock, "Silikatgestein", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Carbon, "Kohlenstoff", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.WaterIce, "Wassereis", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Sulfur, "Schwefel", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Phosphorus, "Phosphor", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Olivine, "Olivin", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Palladium, "Palladium", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Rhodium, "Rhodium", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Osmium, "Osmium", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Ruthenium, "Ruthenium", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Schreibersite, "Schreibersit", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Troilite, "Troilit", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Kamacite, "Kamacit", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Taenite, "Taenit", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Halite, "Halit", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Sylvite, "Sylvin", ProductionItemCategory.RawMaterial);
        yield return Item(ProductionItemIds.Calcite, "Calcit", ProductionItemCategory.RawMaterial);
        yield return Item(
            ProductionItemIds.UraniumOre,
            "Uranerz",
            ProductionItemCategory.RawMaterial,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.22,
            containment: ItemContainmentRequirement.RadiationShielding);

        yield return Item(ProductionItemIds.CrushedIronOre, "Zerkleinertes Eisenerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedCopperOre, "Zerkleinertes Kupfererz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedNickelOre, "Zerkleinertes Nickelerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedTitaniumOre, "Zerkleinertes Titanerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.SilicatePowder, "Silikatpulver", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.ProcessedCarbon, "Zerkleinerter Kohlenstoff", ProductionItemCategory.Intermediate);
        yield return Item(
            ProductionItemIds.CrushedUraniumOre,
            "Zerkleinertes Uranerz",
            ProductionItemCategory.Intermediate,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.38,
            containment: ItemContainmentRequirement.RadiationShielding);
        yield return Item(
            ProductionItemIds.UraniumConcentrate,
            "Urankonzentrat",
            ProductionItemCategory.Intermediate,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.62,
            containment: ItemContainmentRequirement.RadiationShielding);
        yield return Item(ProductionItemIds.Water, "Wasser", ProductionItemCategory.Intermediate, ProductionItemPhase.Liquid);
        yield return Item(ProductionItemIds.Hydrogen, "Wasserstoff", ProductionItemCategory.Intermediate, ProductionItemPhase.Gas);
        yield return Item(ProductionItemIds.Oxygen, "Sauerstoff", ProductionItemCategory.Intermediate, ProductionItemPhase.Gas);
        yield return Item(ProductionItemIds.StandardFuel, "Standardtreibstoff", ProductionItemCategory.Intermediate, ProductionItemPhase.Liquid);
        yield return Item(ProductionItemIds.HighPerformanceFuel, "Hochleistungstreibstoff", ProductionItemCategory.Intermediate, ProductionItemPhase.Liquid);
        yield return Item(
            ProductionItemIds.SulfuricAcid,
            "Schwefelsäure",
            ProductionItemCategory.Intermediate,
            ProductionItemPhase.Liquid,
            ItemHazardKind.Chemical,
            0.65,
            ItemContainmentRequirement.ChemicalResistant);
        yield return Item(ProductionItemIds.Lubricant, "Schmiermittel", ProductionItemCategory.Intermediate, ProductionItemPhase.Liquid);
        yield return Item(ProductionItemIds.Coolant, "Kühlmittel", ProductionItemCategory.Intermediate, ProductionItemPhase.Liquid);
        yield return Item(
            ProductionItemIds.ChemicalWaste,
            "Chemischer Abfall",
            ProductionItemCategory.Waste,
            ProductionItemPhase.Liquid,
            ItemHazardKind.Chemical,
            0.48,
            ItemContainmentRequirement.ChemicalResistant);

        foreach (var (id, name) in new[]
        {
            (ProductionItemIds.IronIngot, "Eisenbarren"),
            (ProductionItemIds.CopperIngot, "Kupferbarren"),
            (ProductionItemIds.NickelIngot, "Nickelbarren"),
            (ProductionItemIds.CobaltIngot, "Kobaltbarren"),
            (ProductionItemIds.TitaniumIngot, "Titanbarren"),
            (ProductionItemIds.GoldIngot, "Goldbarren"),
            (ProductionItemIds.PlatinumIngot, "Platinbarren"),
            (ProductionItemIds.IridiumIngot, "Iridiumbarren"),
        })
        {
            yield return Item(id, name, ProductionItemCategory.Metal);
        }

        foreach (var (id, name) in new[]
        {
            (ProductionItemIds.SteelIngot, "Stahlbarren"),
            (ProductionItemIds.NickelSteel, "Nickelstahl"),
            (ProductionItemIds.TitaniumCobaltAlloy, "Titan-Kobalt-Legierung"),
            (ProductionItemIds.CopperNickelAlloy, "Kupfer-Nickel-Legierung"),
        })
        {
            yield return Item(id, name, ProductionItemCategory.Alloy);
        }

        foreach (var (id, name) in new[]
        {
            (ProductionItemIds.IronPlate, "Eisenplatte"),
            (ProductionItemIds.IronRod, "Eisenstange"),
            (ProductionItemIds.Screws, "Schrauben"),
            (ProductionItemIds.CopperWire, "Kupferdraht"),
            (ProductionItemIds.CopperCable, "Kupferkabel"),
            (ProductionItemIds.TitaniumPlate, "Titanplatte"),
            (ProductionItemIds.SteelBeam, "Stahlträger"),
            (ProductionItemIds.IronPipe, "Eisenrohr"),
            (ProductionItemIds.SteelPipe, "Stahlrohr"),
            (ProductionItemIds.ElectronicComponent, "Elektronisches Bauteil"),
            (ProductionItemIds.BatteryCell, "Batteriezelle"),
            (ProductionItemIds.Motor, "Motor"),
            (ProductionItemIds.ComputerChip, "Computerchip"),
            (ProductionItemIds.MachinePart, "Maschinenteil"),
            (ProductionItemIds.ConveyorPart, "Förderbandteil"),
            (ProductionItemIds.SpaceshipPart, "Raumschiffteil"),
            (ProductionItemIds.PowerCable, "Stromkabel"),
            (ProductionItemIds.ConveyorBelt, "Förderband"),
            (ProductionItemIds.TransportPipe, "Transportrohr"),
            (ProductionItemIds.SteelPlate, "Stahlplatte"),
            (ProductionItemIds.ReinforcedPlate, "Verstärkte Platte"),
            (ProductionItemIds.Rotor, "Rotor"),
            (ProductionItemIds.Pump, "Pumpe"),
            (ProductionItemIds.Valve, "Ventil"),
            (ProductionItemIds.AutomationModule, "Automatisierungsmodul"),
            (ProductionItemIds.SmallElectricMotor, "Kleiner Elektromotor"),
            (ProductionItemIds.CircuitBoard, "Leiterplatte"),
            (ProductionItemIds.ElectronicParts, "Elektronische Einzelteile"),
            (ProductionItemIds.Sensor, "Sensor"),
            (ProductionItemIds.ControlModule, "Steuerungsmodul"),
            (ProductionItemIds.Computer, "Computer"),
            (ProductionItemIds.PowerRegulator, "Stromregler"),
            (ProductionItemIds.ScannerComponent, "Scannerkomponente"),
            (ProductionItemIds.Plastic, "Kunststoff"),
            (ProductionItemIds.HighPerformanceMotor, "Hochleistungsmotor"),
            (ProductionItemIds.Turbine, "Turbine"),
            (ProductionItemIds.Compressor, "Kompressor"),
            (ProductionItemIds.PrecisionComponent, "Präzisionsbauteil"),
            (ProductionItemIds.TitaniumFrame, "Titanrahmen"),
            (ProductionItemIds.CoolingSystem, "Kühlsystem"),
            (ProductionItemIds.PressureVessel, "Druckbehälter"),
            (ProductionItemIds.ProductionComputer, "Produktionscomputer"),
            (ProductionItemIds.FactoryController, "Fabriksteuerung"),
            (ProductionItemIds.SpaceshipModule, "Raumschiffmodul"),
            (ProductionItemIds.SpaceshipEnginePart, "Raumschiffantriebsteil"),
            (ProductionItemIds.ImprovedLandingLeg, "Verbessertes Landebein"),
            (ProductionItemIds.HighPerformanceScanner, "Hochleistungsscanner"),
            (ProductionItemIds.NavigationComputer, "Navigationscomputer"),
            (ProductionItemIds.AdvancedPowerModule, "Erweitertes Strommodul"),
            (ProductionItemIds.FuelPump, "Treibstoffpumpe"),
            (ProductionItemIds.LifeSupportSystem, "Lebenserhaltungssystem"),
            (ProductionItemIds.TransportDrone, "Automatische Transportdrohne"),
            (ProductionItemIds.DeepSpaceControlCore, "Tiefenraum-Steuerkern"),
            (ProductionItemIds.QuantumNavigationModule, "Quanten-Navigationsmodul"),
            (ProductionItemIds.HighPerformanceBattery, "Hochleistungsbatterie"),
            (ProductionItemIds.MobileBatteryPack, "Mobiler Akkupack"),
            (ProductionItemIds.MobileMinerKit, "Mobiler-Miner-Bausatz"),
            (ProductionItemIds.AutomaticMinerKit, "Automatischer-Miner-Bausatz"),
        })
        {
            yield return Item(id, name, ProductionItemCategory.Component);
        }

        yield return new ProductionItemDefinition(
            ProductionItemIds.MiningTool,
            "Abbauwerkzeug",
            ProductionItemCategory.Tool,
            "res://assets/sprites/tools/mining_tool.png",
            MaximumStackSize: 1);
        yield return new ProductionItemDefinition(
            ProductionItemIds.MachineDismantlingTool,
            "Maschinen-Abbauwerkzeug",
            ProductionItemCategory.Tool,
            "res://assets/sprites/tools/machine_dismantling_tool.png",
            MaximumStackSize: 1);
        yield return Item(
            ProductionItemIds.UpgradedMiningTool,
            "Verbessertes Abbauwerkzeug",
            ProductionItemCategory.Tool,
            maximumStackSize: 1);
        yield return Item(
            ProductionItemIds.HighPerformanceMiningTool,
            "Hochleistungs-Abbauwerkzeug",
            ProductionItemCategory.Tool,
            maximumStackSize: 1);
        yield return Item(
            ProductionItemIds.ImprovedRadiationSuit,
            "Verbesserter Strahlenschutzanzug",
            ProductionItemCategory.Tool,
            maximumStackSize: 1);
        yield return Item(
            ProductionItemIds.NuclearRadiationSuit,
            "Nuklear-Schutzanzug",
            ProductionItemCategory.Tool,
            maximumStackSize: 1);

        foreach (var (id, name) in new[]
        {
            (ProductionItemIds.AutomationResearchPack, "Automatisierungs-Forschungspaket"),
            (ProductionItemIds.ElectronicsResearchPack, "Elektronik-Forschungspaket"),
            (ProductionItemIds.ChemistryResearchPack, "Chemie-Forschungspaket"),
            (ProductionItemIds.NuclearResearchPack, "Nuklear-Forschungspaket"),
            (ProductionItemIds.SpaceResearchPack, "Weltraum-Forschungspaket"),
        })
        {
            yield return Item(id, name, ProductionItemCategory.Research);
        }

        yield return Item(
            ProductionItemIds.EnrichedUranium,
            "Angereichertes Uran",
            ProductionItemCategory.Intermediate,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.9,
            containment: ItemContainmentRequirement.RadiationShielding);
        yield return Item(
            ProductionItemIds.NuclearFuelCell,
            "Uran-Brennstoffzelle",
            ProductionItemCategory.Component,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.76,
            containment: ItemContainmentRequirement.RadiationShielding);
        yield return Item(
            ProductionItemIds.SpentFuelCell,
            "Verbrauchte Brennstoffzelle",
            ProductionItemCategory.Waste,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 1.0,
            containment: ItemContainmentRequirement.RadiationShielding);
        yield return Item(
            ProductionItemIds.RadioactiveWaste,
            "Radioaktiver Abfall",
            ProductionItemCategory.Waste,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.95,
            containment: ItemContainmentRequirement.ShieldedWasteContainer);
        yield return Item(
            ProductionItemIds.StabilizedWaste,
            "Stabilisierter Atommüll",
            ProductionItemCategory.Waste,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.34,
            containment: ItemContainmentRequirement.ShieldedWasteContainer);
        yield return Item(
            ProductionItemIds.ShieldedWasteContainer,
            "Abgeschirmter Abfallbehälter",
            ProductionItemCategory.Container,
            hazard: ItemHazardKind.Radioactive,
            hazardStrength: 0.12,
            containment: ItemContainmentRequirement.ShieldedWasteContainer);

        yield return Container(ProductionItemIds.EmptyWaterContainer, "Leerer Wasserbehälter", TransportContainerType.Water, null, ProductionItemIds.EmptyWaterContainer);
        yield return Container(ProductionItemIds.EmptyGasContainer, "Leerer Gasbehälter", TransportContainerType.Gas, null, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.EmptyFuelContainer, "Leerer Treibstoffbehälter", TransportContainerType.Fuel, null, ProductionItemIds.EmptyFuelContainer);
        yield return Container(ProductionItemIds.WaterContainer, "Wasserbehälter", TransportContainerType.Water, ProductionItemIds.Water, ProductionItemIds.EmptyWaterContainer);
        yield return Container(ProductionItemIds.HydrogenContainer, "Wasserstoffbehälter", TransportContainerType.Gas, ProductionItemIds.Hydrogen, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.OxygenContainer, "Sauerstoffbehälter", TransportContainerType.Gas, ProductionItemIds.Oxygen, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.StandardFuelContainer, "Standardtreibstoffbehälter", TransportContainerType.Fuel, ProductionItemIds.StandardFuel, ProductionItemIds.EmptyFuelContainer);
        yield return Container(ProductionItemIds.HighPerformanceFuelContainer, "Hochleistungstreibstoffbehälter", TransportContainerType.Fuel, ProductionItemIds.HighPerformanceFuel, ProductionItemIds.EmptyFuelContainer);
    }

    private static ProductionItemDefinition Container(
        ItemId id,
        string name,
        TransportContainerType type,
        ItemId? substanceId,
        ItemId emptyContainerId) =>
        new(
            id,
            name,
            ProductionItemCategory.Container,
            $"production/{id.Value}",
            Container: new TransportContainerDefinition(
                type,
                substanceId,
                substanceId is null ? 0 : ContainerCapacity,
                ContainerCapacity,
                emptyContainerId));
}

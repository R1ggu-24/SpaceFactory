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
    TransportContainerDefinition? Container = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(IconKey) || MaximumStackSize <= 0)
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

    public static readonly ItemId CrushedIronOre = new("crushed_iron_ore");
    public static readonly ItemId CrushedCopperOre = new("crushed_copper_ore");
    public static readonly ItemId CrushedNickelOre = new("crushed_nickel_ore");
    public static readonly ItemId CrushedTitaniumOre = new("crushed_titanium_ore");
    public static readonly ItemId SilicatePowder = new("silicate_powder");
    public static readonly ItemId ProcessedCarbon = new("processed_carbon");

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

    public static readonly ItemId Water = new("water");
    public static readonly ItemId Hydrogen = new("hydrogen");
    public static readonly ItemId Oxygen = new("oxygen");
    public static readonly ItemId Fuel = new("fuel");
    public static readonly ItemId EmptyWaterContainer = new("empty_water_container");
    public static readonly ItemId EmptyGasContainer = new("empty_gas_container");
    public static readonly ItemId EmptyFuelContainer = new("empty_fuel_container");
    public static readonly ItemId WaterContainer = new("water_container");
    public static readonly ItemId HydrogenContainer = new("hydrogen_container");
    public static readonly ItemId OxygenContainer = new("oxygen_container");
    public static readonly ItemId FuelContainer = new("fuel_container");
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
        static ProductionItemDefinition Item(ItemId id, string name, ProductionItemCategory category) =>
            new(id, name, category, $"production/{id.Value}");

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

        yield return Item(ProductionItemIds.CrushedIronOre, "Zerkleinertes Eisenerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedCopperOre, "Zerkleinertes Kupfererz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedNickelOre, "Zerkleinertes Nickelerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.CrushedTitaniumOre, "Zerkleinertes Titanerz", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.SilicatePowder, "Silikatpulver", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.ProcessedCarbon, "Verarbeiteter Kohlenstoff", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.Water, "Wasser", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.Hydrogen, "Wasserstoff", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.Oxygen, "Sauerstoff", ProductionItemCategory.Intermediate);
        yield return Item(ProductionItemIds.Fuel, "Treibstoff", ProductionItemCategory.Intermediate);

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
        })
        {
            yield return Item(id, name, ProductionItemCategory.Component);
        }

        yield return Container(ProductionItemIds.EmptyWaterContainer, "Leerer Wasserbehälter", TransportContainerType.Water, null, ProductionItemIds.EmptyWaterContainer);
        yield return Container(ProductionItemIds.EmptyGasContainer, "Leerer Gasbehälter", TransportContainerType.Gas, null, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.EmptyFuelContainer, "Leerer Treibstoffbehälter", TransportContainerType.Fuel, null, ProductionItemIds.EmptyFuelContainer);
        yield return Container(ProductionItemIds.WaterContainer, "Wasserbehälter", TransportContainerType.Water, ProductionItemIds.Water, ProductionItemIds.EmptyWaterContainer);
        yield return Container(ProductionItemIds.HydrogenContainer, "Wasserstoffbehälter", TransportContainerType.Gas, ProductionItemIds.Hydrogen, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.OxygenContainer, "Sauerstoffbehälter", TransportContainerType.Gas, ProductionItemIds.Oxygen, ProductionItemIds.EmptyGasContainer);
        yield return Container(ProductionItemIds.FuelContainer, "Treibstoffbehälter", TransportContainerType.Fuel, ProductionItemIds.Fuel, ProductionItemIds.EmptyFuelContainer);
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

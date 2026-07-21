using Godot;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Describes how a central item definition is presented in inventory UI.
/// Raw resources retain their deposit-derived icon style; manufactured items
/// use a stable procedural glyph and palette entry.
/// </summary>
public sealed record ItemPresentationViewModel(
    ItemId Id,
    string DisplayName,
    Color Color,
    int MaximumStackSize,
    ItemIconGlyph Glyph,
    string IconKey,
    ResourceDefinition? Resource = null,
    ProductionItemDefinition? Product = null,
    string? TexturePath = null)
{
    public bool UsesResourceIcon => Resource is not null;

    public bool IsContainer => Product?.Category == ProductionItemCategory.Container;

    public bool IsEmptyContainer => Product?.Container?.IsEmpty == true;
}

public enum ItemIconGlyph
{
    Unknown,
    RawResource,
    Powder,
    Ingot,
    Alloy,
    Plate,
    Rod,
    Fastener,
    Wire,
    Cable,
    Pipe,
    Circuit,
    Battery,
    Motor,
    Chip,
    MachinePart,
    Conveyor,
    ShipPart,
    Liquid,
    Gas,
    Fuel,
    Container,
    Tool,
    Research,
    Radioactive,
    Waste,
}

/// <summary>
/// Central presentation mapping for every item that may occur in a slot.
/// Resource definitions deliberately override matching production definitions,
/// keeping the established procedural ore icons intact.
/// </summary>
public sealed class ItemPresentationCatalog
{
    private readonly IReadOnlyDictionary<ItemId, ItemPresentationViewModel> _items;

    private ItemPresentationCatalog(IReadOnlyDictionary<ItemId, ItemPresentationViewModel> items)
    {
        _items = items;
    }

    public static ItemPresentationCatalog Create(
        IEnumerable<ResourceDefinition> resources,
        ProductionItemCatalog? productionItems = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        productionItems ??= DefaultProductionItemCatalog.Instance;

        var items = new Dictionary<ItemId, ItemPresentationViewModel>();
        foreach (var product in productionItems.All)
        {
            var presentation = FromProduct(product);
            if (presentation.Glyph == ItemIconGlyph.Unknown)
            {
                throw new InvalidOperationException($"Production item '{product.Id}' has no presentation glyph.");
            }

            items[product.Id] = presentation;
        }

        foreach (var resource in resources)
        {
            items[resource.Id] = FromResource(resource);
        }

        return new ItemPresentationCatalog(items);
    }

    public bool TryGet(ItemId id, out ItemPresentationViewModel? item) =>
        _items.TryGetValue(id, out item);

    public ItemPresentationViewModel GetOrCreateFallback(ItemId id, int maximumStackSize) =>
        _items.TryGetValue(id, out var item)
            ? item
            : CreateFallback(id, maximumStackSize);

    private static ItemPresentationViewModel FromResource(ResourceDefinition resource) => new(
        resource.Id,
        resource.DisplayName,
        Color.FromHtml(resource.BaseColorHex),
        resource.MaximumStackSize,
        ItemIconGlyph.RawResource,
        resource.InventoryIconPath,
        Resource: resource);

    private static ItemPresentationViewModel FromProduct(ProductionItemDefinition product) => new(
        product.Id,
        product.DisplayName,
        ResolveProductColor(product),
        product.MaximumStackSize,
        ResolveProductGlyph(product),
        product.IconKey,
        Product: product,
        TexturePath: product.IconKey.StartsWith("res://", StringComparison.Ordinal)
            ? product.IconKey
            : null);

    private static ItemIconGlyph ResolveProductGlyph(ProductionItemDefinition product)
    {
        if (product.Category == ProductionItemCategory.Waste)
        {
            return ItemIconGlyph.Waste;
        }

        if (product.HazardKind == ItemHazardKind.Radioactive)
        {
            return ItemIconGlyph.Radioactive;
        }

        if (product.Category == ProductionItemCategory.Research)
        {
            return ItemIconGlyph.Research;
        }

        if (product.Category == ProductionItemCategory.Tool)
        {
            return ItemIconGlyph.Tool;
        }

        if (product.Phase == ProductionItemPhase.Gas)
        {
            return ItemIconGlyph.Gas;
        }

        if (product.Phase == ProductionItemPhase.Liquid)
        {
            return product.Id == ProductionItemIds.StandardFuel ||
                   product.Id == ProductionItemIds.HighPerformanceFuel
                ? ItemIconGlyph.Fuel
                : ItemIconGlyph.Liquid;
        }

        return product.Id.Value switch
        {
            "crushed_iron_ore" or "crushed_copper_ore" or "crushed_nickel_ore" or
                "crushed_titanium_ore" or "silicate_powder" or "processed_carbon" => ItemIconGlyph.Powder,
            "iron_ingot" or "copper_ingot" or "nickel_ingot" or "cobalt_ingot" or
                "titanium_ingot" or "gold_ingot" or "platinum_ingot" or "iridium_ingot" => ItemIconGlyph.Ingot,
            "steel_ingot" or "nickel_steel" or "titanium_cobalt_alloy" or "copper_nickel_alloy" =>
                ItemIconGlyph.Alloy,
            "iron_plate" or "titanium_plate" or "steel_plate" or "reinforced_plate" or "steel_beam" =>
                ItemIconGlyph.Plate,
            "iron_rod" or "improved_landing_leg" => ItemIconGlyph.Rod,
            "screws" => ItemIconGlyph.Fastener,
            "copper_wire" => ItemIconGlyph.Wire,
            "copper_cable" or "power_cable" => ItemIconGlyph.Cable,
            "iron_pipe" or "steel_pipe" or "transport_pipe" => ItemIconGlyph.Pipe,
            "electronic_component" or "electronic_parts" or "circuit_board" or "sensor" or
                "control_module" or "power_regulator" or "automation_module" => ItemIconGlyph.Circuit,
            "battery_cell" or "high_performance_battery" or "mobile_battery_pack" => ItemIconGlyph.Battery,
            "motor" or "small_electric_motor" or "high_performance_motor" or "rotor" or "turbine" or
                "compressor" or "pump" or "fuel_pump" => ItemIconGlyph.Motor,
            "computer_chip" or "computer" or "production_computer" or "factory_controller" or
                "navigation_computer" or "deep_space_control_core" => ItemIconGlyph.Chip,
            "conveyor_part" or "conveyor_belt" => ItemIconGlyph.Conveyor,
            "spaceship_part" or "spaceship_module" or "spaceship_engine_part" or "high_performance_scanner" or
                "transport_drone" or "life_support_system" or "quantum_navigation_module" => ItemIconGlyph.ShipPart,
            _ => product.Category switch
            {
                ProductionItemCategory.RawMaterial => ItemIconGlyph.RawResource,
                ProductionItemCategory.Intermediate => ItemIconGlyph.Powder,
                ProductionItemCategory.Metal => ItemIconGlyph.Ingot,
                ProductionItemCategory.Alloy => ItemIconGlyph.Alloy,
                ProductionItemCategory.Component => ItemIconGlyph.MachinePart,
                ProductionItemCategory.Container => ItemIconGlyph.Container,
                ProductionItemCategory.Tool => ItemIconGlyph.Tool,
                ProductionItemCategory.Research => ItemIconGlyph.Research,
                ProductionItemCategory.Waste => ItemIconGlyph.Waste,
                _ => ItemIconGlyph.MachinePart,
            },
        };
    }

    private static Color ResolveProductColor(ProductionItemDefinition product)
    {
        var hex = product.Id.Value switch
        {
            "crushed_iron_ore" or "iron_ingot" or "iron_plate" or "iron_rod" or "iron_pipe" => "#9B8A82",
            "crushed_copper_ore" or "copper_ingot" or "copper_wire" or "copper_cable" => "#D77B48",
            "crushed_nickel_ore" or "nickel_ingot" or "nickel_steel" => "#AEBBB6",
            "cobalt_ingot" => "#5F91C6",
            "crushed_titanium_ore" or "titanium_ingot" or "titanium_plate" => "#B4A8C2",
            "gold_ingot" => "#E5B93F",
            "platinum_ingot" => "#D7E1E5",
            "iridium_ingot" => "#B9F0E8",
            "steel_ingot" or "steel_beam" or "steel_pipe" => "#81949E",
            "titanium_cobalt_alloy" => "#718BC5",
            "copper_nickel_alloy" => "#C69170",
            "silicate_powder" => "#A69B89",
            "processed_carbon" => "#555B66",
            "screws" or "machine_part" or "conveyor_part" => "#8FA8B2",
            "power_cable" => "#42CFE8",
            "conveyor_belt" => "#B5C0C5",
            "transport_pipe" => "#5CB8D8",
            "electronic_component" or "computer_chip" => "#36C9A5",
            "battery_cell" => "#9DD14A",
            "motor" => "#D58C45",
            "spaceship_part" => "#68B9DB",
            "deep_space_control_core" => "#66E0FF",
            "quantum_navigation_module" => "#9A8CFF",
            "mining_tool" or "upgraded_mining_tool" or "high_performance_mining_tool" => "#D5E5EA",
            "automation_research_pack" => "#45CDE8",
            "electronics_research_pack" => "#49E0B4",
            "chemistry_research_pack" => "#A9DA55",
            "nuclear_research_pack" => "#D1E84A",
            "space_research_pack" => "#8EBBFF",
            "crushed_uranium_ore" or "uranium_concentrate" or "enriched_uranium" or
                "nuclear_fuel_cell" => "#A8D93D",
            "spent_fuel_cell" or "radioactive_waste" => "#D5B52D",
            "stabilized_waste" or "shielded_waste_container" => "#9DAA52",
            "chemical_waste" => "#98A63B",
            "water" or "water_container" or "empty_water_container" => "#56BEE5",
            "hydrogen" or "hydrogen_container" => "#8ED8FF",
            "oxygen" or "oxygen_container" or "empty_gas_container" => "#70E0CF",
            "standard_fuel" or "standard_fuel_container" => "#C6A55C",
            "fuel" or "fuel_container" => "#F1A63A",
            "empty_fuel_container" => "#8D99A3",
            _ => product.Category switch
            {
                ProductionItemCategory.RawMaterial => "#8E8278",
                ProductionItemCategory.Intermediate => "#7E9BA6",
                ProductionItemCategory.Metal => "#A9B5BA",
                ProductionItemCategory.Alloy => "#7998A7",
                ProductionItemCategory.Component => "#48AFC7",
                ProductionItemCategory.Container => "#5DBDDA",
                ProductionItemCategory.Tool => "#D5E5EA",
                ProductionItemCategory.Research => "#55CAE8",
                ProductionItemCategory.Waste => "#B6A23A",
                _ => "#6EABB9",
            },
        };
        return Color.FromHtml(hex);
    }

    private static ItemPresentationViewModel CreateFallback(ItemId id, int maximumStackSize)
    {
        var value = id.Value;
        var glyph = value.Contains("tool", StringComparison.OrdinalIgnoreCase)
            ? ItemIconGlyph.Tool
            : value.Contains("research", StringComparison.OrdinalIgnoreCase)
                ? ItemIconGlyph.Research
                : value.Contains("waste", StringComparison.OrdinalIgnoreCase)
                    ? ItemIconGlyph.Waste
                    : value.Contains("uranium", StringComparison.OrdinalIgnoreCase) ||
                      value.Contains("nuclear", StringComparison.OrdinalIgnoreCase)
                        ? ItemIconGlyph.Radioactive
                        : ItemIconGlyph.MachinePart;
        var color = glyph switch
        {
            ItemIconGlyph.Tool => Color.FromHtml("#D5E5EA"),
            ItemIconGlyph.Research => Color.FromHtml("#55CAE8"),
            ItemIconGlyph.Waste => Color.FromHtml("#B6A23A"),
            ItemIconGlyph.Radioactive => Color.FromHtml("#A8D93D"),
            _ => new Color(0.42f, 0.62f, 0.7f),
        };
        return new ItemPresentationViewModel(
            id,
            HumanizeId(value),
            color,
            maximumStackSize,
            glyph,
            $"fallback/{value}");
    }

    private static string HumanizeId(string value)
    {
        var words = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}

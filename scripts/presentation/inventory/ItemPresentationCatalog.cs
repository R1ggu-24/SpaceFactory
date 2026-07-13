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
    ProductionItemDefinition? Product = null)
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
            items[product.Id] = FromProduct(product);
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
            : new ItemPresentationViewModel(
                id,
                HumanizeId(id.Value),
                new Color(0.42f, 0.62f, 0.7f),
                maximumStackSize,
                ItemIconGlyph.Unknown,
                $"fallback/{id.Value}");

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
        Product: product);

    private static ItemIconGlyph ResolveProductGlyph(ProductionItemDefinition product) => product.Id.Value switch
    {
        "crushed_iron_ore" or "crushed_copper_ore" or "crushed_nickel_ore" or
            "crushed_titanium_ore" or "silicate_powder" or "processed_carbon" => ItemIconGlyph.Powder,
        "iron_ingot" or "copper_ingot" or "nickel_ingot" or "cobalt_ingot" or
            "titanium_ingot" or "gold_ingot" or "platinum_ingot" or "iridium_ingot" => ItemIconGlyph.Ingot,
        "steel_ingot" or "nickel_steel" or "titanium_cobalt_alloy" or "copper_nickel_alloy" =>
            ItemIconGlyph.Alloy,
        "iron_plate" or "titanium_plate" or "steel_beam" => ItemIconGlyph.Plate,
        "iron_rod" => ItemIconGlyph.Rod,
        "screws" => ItemIconGlyph.Fastener,
        "copper_wire" => ItemIconGlyph.Wire,
        "copper_cable" => ItemIconGlyph.Cable,
        "iron_pipe" or "steel_pipe" => ItemIconGlyph.Pipe,
        "electronic_component" => ItemIconGlyph.Circuit,
        "battery_cell" => ItemIconGlyph.Battery,
        "motor" => ItemIconGlyph.Motor,
        "computer_chip" => ItemIconGlyph.Chip,
        "machine_part" => ItemIconGlyph.MachinePart,
        "conveyor_part" => ItemIconGlyph.Conveyor,
        "spaceship_part" => ItemIconGlyph.ShipPart,
        "water" => ItemIconGlyph.Liquid,
        "hydrogen" or "oxygen" => ItemIconGlyph.Gas,
        "fuel" => ItemIconGlyph.Fuel,
        _ when product.Category == ProductionItemCategory.Container => ItemIconGlyph.Container,
        _ => ItemIconGlyph.Unknown,
    };

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
            "electronic_component" or "computer_chip" => "#36C9A5",
            "battery_cell" => "#9DD14A",
            "motor" => "#D58C45",
            "spaceship_part" => "#68B9DB",
            "water" or "water_container" or "empty_water_container" => "#56BEE5",
            "hydrogen" or "hydrogen_container" => "#8ED8FF",
            "oxygen" or "oxygen_container" or "empty_gas_container" => "#70E0CF",
            "fuel" or "fuel_container" or "empty_fuel_container" => "#F1A63A",
            _ => product.Category switch
            {
                ProductionItemCategory.RawMaterial => "#8E8278",
                ProductionItemCategory.Intermediate => "#7E9BA6",
                ProductionItemCategory.Metal => "#A9B5BA",
                ProductionItemCategory.Alloy => "#7998A7",
                ProductionItemCategory.Component => "#48AFC7",
                ProductionItemCategory.Container => "#5DBDDA",
                _ => "#6EABB9",
            },
        };
        return Color.FromHtml(hex);
    }

    private static string HumanizeId(string value)
    {
        var words = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}

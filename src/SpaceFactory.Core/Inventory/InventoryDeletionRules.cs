using SpaceFactory.Core.Production;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Central safety policy for destructive inventory actions. Presentation decides how to ask for
/// confirmation, while rarity, hazard and item semantics remain testable without Godot.
/// </summary>
public static class InventoryDeletionRules
{
    public static bool RequiresConfirmation(
        int maximumStackSize,
        ResourceDefinition? resource,
        ProductionItemDefinition? product)
    {
        if (maximumStackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStackSize));
        }

        return maximumStackSize == 1 ||
               resource?.Rarity is >= ResourceRarity.Rare ||
               product is not null &&
               (product.Category is ProductionItemCategory.Alloy or
                    ProductionItemCategory.Component or
                    ProductionItemCategory.Tool or
                    ProductionItemCategory.Research or
                    ProductionItemCategory.Waste ||
                product.HazardKind != ItemHazardKind.None ||
                product.Container is { IsEmpty: false });
    }
}

using SpaceFactory.Core.Items;
using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>One option matrix shared by inventory, storage and persistent-hotbar popups.</summary>
public static class InventoryItemContextRules
{
    public static IReadOnlyList<InventoryItemContextAction> BuildActions(
        ItemId itemId,
        int amount,
        bool isTool,
        string sourceInventoryId)
    {
        if (amount <= 0)
        {
            return [];
        }

        var actions = new List<InventoryItemContextAction>(5);
        if (isTool || IsPlaceable(itemId))
        {
            actions.Add(InventoryItemContextAction.Select);
        }

        if (amount > 1)
        {
            actions.Add(InventoryItemContextAction.SplitStack);
            actions.Add(InventoryItemContextAction.TakeSingleItem);
        }

        actions.Add(InventoryItemContextAction.DropStack);
        if (!isTool && sourceInventoryId != InventoryMenuController.HotbarInventoryId)
        {
            actions.Add(InventoryItemContextAction.EquipToHotbar);
        }

        return actions;
    }

    public static bool IsPlaceable(ItemId itemId) =>
        DefaultMachineCatalog.Instance.TryGetByPlacementItem(itemId, out _) ||
        DefaultConnectionTypeCatalog.Instance.ForBuildItem(itemId).Count > 0;
}

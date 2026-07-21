using SpaceFactory.Core.Items;

namespace SpaceFactory.Presentation.InventoryUI;

public readonly record struct InventoryItemContextRequest(
    InventorySlotAddress Address,
    ItemId ExpectedItemId,
    string DisplayName,
    int Amount);

public enum InventoryItemContextAction
{
    Select,
    SplitStack,
    TakeSingleItem,
    DropStack,
    EquipToHotbar,
}

public sealed record InventoryItemContextMenuModel(
    InventoryItemContextRequest Request,
    IReadOnlyList<InventoryItemContextAction> Actions);

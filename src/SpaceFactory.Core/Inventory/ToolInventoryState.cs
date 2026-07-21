using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Owns the astronaut's four dedicated tool slots and hand-mode selection. Item eligibility
/// is derived exclusively from the production item catalog, so UI, transfers and gameplay
/// never need a second list of tool IDs.
/// </summary>
public sealed class ToolInventoryState
{
    private readonly ProductionItemCatalog _items;

    public ToolInventoryState(
        SlotInventory inventory,
        ProductionItemCatalog items,
        int selectedSlotIndex = 0,
        bool isHandModeActive = false)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);
        if (inventory.SlotCount != InventoryConfiguration.ToolSlotCount)
        {
            throw new ArgumentException(
                $"A tool inventory must contain exactly {InventoryConfiguration.ToolSlotCount} slots.",
                nameof(inventory));
        }

        _items = items;
        if (inventory.Slots.Any(slot => slot.ItemId is { } itemId && !IsTool(items, itemId)))
        {
            throw new ArgumentException("A tool inventory contains a non-tool item.", nameof(inventory));
        }

        var representativeNonTool = items.All.FirstOrDefault(definition =>
            definition.Category != ProductionItemCategory.Tool);
        if (representativeNonTool is not null && inventory.AcceptsItem(representativeNonTool.Id))
        {
            throw new ArgumentException(
                "A tool inventory must enforce the central tool-category acceptance rule.",
                nameof(inventory));
        }

        Inventory = inventory;
        SelectSlot(selectedSlotIndex);
        IsHandModeActive = isHandModeActive;
    }

    public SlotInventory Inventory { get; }

    public int SelectedSlotIndex { get; private set; }

    public bool IsHandModeActive { get; private set; }

    public ItemId? SelectedToolId => Inventory.GetSlot(SelectedSlotIndex).ItemId;

    public ItemId? EquippedToolId => IsHandModeActive ? SelectedToolId : null;

    public static ToolInventoryState Create(
        ProductionItemCatalog? items = null,
        int selectedSlotIndex = 0,
        bool isHandModeActive = false)
    {
        var catalog = items ?? DefaultProductionItemCatalog.Instance;
        return new ToolInventoryState(
            CreateInventory(catalog),
            catalog,
            selectedSlotIndex,
            isHandModeActive);
    }

    public static SlotInventory CreateInventory(ProductionItemCatalog? items = null)
    {
        var catalog = items ?? DefaultProductionItemCatalog.Instance;
        return new SlotInventory(
            InventoryConfiguration.ToolSlotCount,
            InventoryConfiguration.MaximumStackSize,
            itemId => catalog.TryGet(itemId, out var definition) && definition is not null
                ? definition.MaximumStackSize
                : InventoryConfiguration.MaximumStackSize,
            itemId => IsTool(catalog, itemId));
    }

    public bool AcceptsItem(ItemId itemId) => IsTool(_items, itemId);

    public bool ActivateHandMode()
    {
        if (IsHandModeActive)
        {
            return false;
        }

        IsHandModeActive = true;
        return true;
    }

    public bool DeactivateHandMode()
    {
        if (!IsHandModeActive)
        {
            return false;
        }

        IsHandModeActive = false;
        return true;
    }

    public bool SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryConfiguration.ToolSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        if (slotIndex == SelectedSlotIndex)
        {
            return false;
        }

        SelectedSlotIndex = slotIndex;
        return true;
    }

    public bool SelectPreviousTool() => SelectRelativeTool(-1);

    public bool SelectNextTool() => SelectRelativeTool(1);

    private bool SelectRelativeTool(int direction)
    {
        for (var offset = 1; offset <= InventoryConfiguration.ToolSlotCount; offset++)
        {
            var candidate = (SelectedSlotIndex + (direction * offset) + InventoryConfiguration.ToolSlotCount * 2) %
                            InventoryConfiguration.ToolSlotCount;
            if (!Inventory.GetSlot(candidate).IsEmpty)
            {
                return SelectSlot(candidate);
            }
        }

        return false;
    }

    private static bool IsTool(ProductionItemCatalog items, ItemId itemId) =>
        items.TryGet(itemId, out var definition) &&
        definition is { Category: ProductionItemCategory.Tool };
}

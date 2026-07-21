using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Owns the astronaut's six quick-access slots and the stable selected slot index.
/// Equipment is derived from the selected slot, so moving an item immediately changes
/// the active tool without binding any particular item to a particular slot.
/// </summary>
public sealed class HotbarState
{
    public HotbarState(SlotInventory inventory, int activeSlotIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.SlotCount != InventoryConfiguration.HotbarSlotCount)
        {
            throw new ArgumentException(
                $"A hotbar must contain exactly {InventoryConfiguration.HotbarSlotCount} slots.",
                nameof(inventory));
        }

        Inventory = inventory;
        SelectSlot(activeSlotIndex);
    }

    public SlotInventory Inventory { get; }

    public int ActiveSlotIndex { get; private set; }

    public ItemId? ActiveItemId => Inventory.GetSlot(ActiveSlotIndex).ItemId;

    public bool SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryConfiguration.HotbarSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        if (slotIndex == ActiveSlotIndex)
        {
            return false;
        }

        ActiveSlotIndex = slotIndex;
        return true;
    }
}

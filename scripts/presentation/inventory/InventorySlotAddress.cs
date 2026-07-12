namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Identifies a slot without exposing UI controls to the inventory domain model.
/// </summary>
public readonly record struct InventorySlotAddress(string InventoryId, int SlotIndex);

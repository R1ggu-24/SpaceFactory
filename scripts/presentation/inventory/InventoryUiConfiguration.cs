namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>Shared pixel metrics for the persistent hotbar and its hand-slot mirror.</summary>
public static class InventoryUiConfiguration
{
    // Inventory and storage slots deliberately remain fixed. Responsive layouts reclaim
    // chrome and margins instead of making item targets harder to read or drag.
    public const float InventorySlotSize = 64;
    public const int InventorySlotGap = 6;
    public const float ToolSlotSize = 44;
    public const int InventoryPanelMarginCompact = 8;
    public const int InventoryPanelMarginRegular = 12;
    public const int CompactActionButtonSize = 32;
    public const int CompactTrashTargetSize = 34;

    public const float HotbarSlotSize = 58;
    public const int HotbarSlotGap = 7;
    public const int HotbarHandSlotExtraGap = 9;
    public const int HotbarFramePaddingHorizontal = 8;
    public const int HotbarFramePaddingVertical = 4;
    public const int HotbarBorderWidth = 1;
    public const int HotbarAccentBorderWidth = 2;
    public const float HotbarBottomMargin = 16;
}

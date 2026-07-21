using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Stable starter ownership rules shared by new-game creation and save migration. Slot selection
/// remains a persistence/UI concern; item identity and quantities live here.
/// </summary>
public static class StarterEquipmentConfiguration
{
    public static ItemAmount StartingMiningTool { get; } =
        new(ProductionItemIds.MiningTool, 1);

    public static ItemAmount StartingMachineDismantlingTool { get; } =
        new(ProductionItemIds.MachineDismantlingTool, 1);

    public static IReadOnlyList<ItemAmount> RequiredStarterTools { get; } =
    [
        StartingMiningTool,
        StartingMachineDismantlingTool,
    ];

    public static bool IsRequiredStarterTool(ItemId itemId) =>
        RequiredStarterTools.Any(item => item.ItemId == itemId);
}

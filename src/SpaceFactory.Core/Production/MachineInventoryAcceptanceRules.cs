using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Production;

public static class MachineInventoryAcceptanceRules
{
    public static bool CanStore(
        MachineDefinition machine,
        ItemId itemId,
        ProductionItemCatalog? itemCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var item = (itemCatalog ?? DefaultProductionItemCatalog.Instance).Get(itemId);
        return machine.Archetype switch
        {
            MachineArchetype.LiquidStorage => item.Phase == ProductionItemPhase.Liquid,
            MachineArchetype.GasStorage => item.Phase == ProductionItemPhase.Gas,
            MachineArchetype.FluidTransport => item.Phase is ProductionItemPhase.Liquid or ProductionItemPhase.Gas,
            MachineArchetype.SolidStorage when machine.Id == MachineDefinitionIds.NuclearWasteStorage =>
                item.Phase == ProductionItemPhase.Solid && item.HazardKind == ItemHazardKind.Radioactive,
            _ => true,
        };
    }
}

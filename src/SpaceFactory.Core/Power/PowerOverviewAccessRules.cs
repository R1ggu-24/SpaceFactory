using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Power;

/// <summary>
/// Defines which placed machine types act as central access points for a power-grid overview.
/// Consumers remain valid cable endpoints, but their sockets do not open the overview directly.
/// </summary>
public static class PowerOverviewAccessRules
{
    public static bool CanOpenAt(MachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Kind == MachineKind.Generator ||
               definition.Id == MachineDefinitionIds.PowerPole;
    }
}

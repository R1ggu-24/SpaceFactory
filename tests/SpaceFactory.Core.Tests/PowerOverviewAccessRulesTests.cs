using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Tests;

public sealed class PowerOverviewAccessRulesTests
{
    [Fact]
    public void GeneratorsAndPowerPole_AreCentralPowerOverviewAccessPoints()
    {
        var catalog = DefaultMachineCatalog.Instance;

        Assert.True(PowerOverviewAccessRules.CanOpenAt(catalog.Get(MachineDefinitionIds.BasicGenerator)));
        Assert.True(PowerOverviewAccessRules.CanOpenAt(catalog.Get(MachineDefinitionIds.FuelGenerator)));
        Assert.True(PowerOverviewAccessRules.CanOpenAt(catalog.Get(MachineDefinitionIds.PowerPole)));
    }

    [Fact]
    public void ProductionAndOtherMachines_AreNotPowerOverviewAccessPoints()
    {
        var catalog = DefaultMachineCatalog.Instance;
        var forbidden = catalog.All
            .Where(definition => definition.Kind != MachineKind.Generator &&
                                 definition.Id != MachineDefinitionIds.PowerPole)
            .ToArray();

        Assert.NotEmpty(forbidden);
        Assert.All(forbidden, definition => Assert.False(PowerOverviewAccessRules.CanOpenAt(definition)));
    }
}

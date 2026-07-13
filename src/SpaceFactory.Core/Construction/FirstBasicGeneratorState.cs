using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Construction;

public sealed class FirstBasicGeneratorState
{
    public FirstBasicGeneratorState(bool freeGeneratorAlreadyBuilt = false)
    {
        FreeGeneratorAlreadyBuilt = freeGeneratorAlreadyBuilt;
    }

    public bool FreeGeneratorAlreadyBuilt { get; private set; }

    public bool IsFreeBuildAvailable(MachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Id == MachineDefinitionIds.BasicGenerator && !FreeGeneratorAlreadyBuilt;
    }

    public IReadOnlyList<ItemAmount> GetEffectiveBuildCosts(MachineDefinition definition) =>
        IsFreeBuildAvailable(definition) ? [] : definition.BuildCosts;

    public bool TryConsumeFreeBuild(MachineDefinition definition)
    {
        if (!IsFreeBuildAvailable(definition))
        {
            return false;
        }

        FreeGeneratorAlreadyBuilt = true;
        return true;
    }
}

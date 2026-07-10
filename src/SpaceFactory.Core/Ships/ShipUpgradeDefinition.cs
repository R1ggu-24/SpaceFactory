namespace SpaceFactory.Core.Ships;

public sealed record ShipUpgradeDefinition(
    ShipUpgradeId Id,
    string DisplayName,
    int StorageCapacityIncrease)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || StorageCapacityIncrease <= 0)
        {
            throw new ArgumentException("The ship upgrade definition is invalid.");
        }
    }
}

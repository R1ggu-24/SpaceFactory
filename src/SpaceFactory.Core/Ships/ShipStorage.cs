namespace SpaceFactory.Core.Ships;

public sealed class ShipStorage
{
    private readonly HashSet<ShipUpgradeId> _appliedUpgrades = [];

    public ShipStorage(int capacity)
    {
        Inventory = new Inventory.Inventory(capacity);
    }

    public Inventory.Inventory Inventory { get; }

    public bool Apply(ShipUpgradeDefinition upgrade)
    {
        upgrade.Validate();
        if (!_appliedUpgrades.Add(upgrade.Id))
        {
            return false;
        }

        Inventory.IncreaseCapacity(upgrade.StorageCapacityIncrease);
        return true;
    }
}

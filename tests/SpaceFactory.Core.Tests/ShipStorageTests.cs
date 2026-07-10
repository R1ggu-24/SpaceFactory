using SpaceFactory.Core.Ships;

namespace SpaceFactory.Core.Tests;

public sealed class ShipStorageTests
{
    [Fact]
    public void Constructor_CreatesExpectedCapacity() => Assert.Equal(20, new ShipStorage(20).Inventory.Capacity);

    [Fact]
    public void Apply_CargoUpgrade_IncreasesCapacityOnce()
    {
        var storage = new ShipStorage(20);
        var upgrade = new ShipUpgradeDefinition(new ShipUpgradeId("cargo_1"), "Cargo I", 50);

        Assert.True(storage.Apply(upgrade));
        Assert.False(storage.Apply(upgrade));
        Assert.Equal(70, storage.Inventory.Capacity);
    }

    [Fact]
    public void Apply_InvalidUpgrade_Throws()
    {
        var upgrade = new ShipUpgradeDefinition(new ShipUpgradeId("bad"), "", 0);

        Assert.Throws<ArgumentException>(() => new ShipStorage(20).Apply(upgrade));
    }
}

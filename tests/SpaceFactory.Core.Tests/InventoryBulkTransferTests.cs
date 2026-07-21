using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Tests;

public sealed class InventoryBulkTransferTests
{
    private static readonly ItemId Iron = new("bulk-iron");
    private static readonly ItemId Copper = new("bulk-copper");

    [Fact]
    public void TransferAllThatFits_MergesFirstAndLeavesOnlyOverflowInSource()
    {
        var source = new SlotInventory(2);
        var target = new SlotInventory(2);
        Assert.True(source.Add(Iron, 150).Succeeded);
        Assert.True(source.Add(Copper, 100).Succeeded);
        Assert.True(target.Add(Iron, 190).Succeeded);
        Assert.True(target.Add(Copper, 200).Succeeded);

        var result = InventoryBulkTransfer.TransferAllThatFits(source, target);

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.MovedItemCount);
        Assert.Equal(140, source.GetAmount(Iron));
        Assert.Equal(100, source.GetAmount(Copper));
        Assert.Equal(200, target.GetAmount(Iron));
        Assert.Equal(200, target.GetAmount(Copper));
        Assert.Equal(640, source.TotalItemCount + target.TotalItemCount);
    }

    [Fact]
    public void TransferAllThatFits_HonoursSpecializedAcceptanceWithoutMutation()
    {
        var source = new SlotInventory(2);
        var target = new SlotInventory(2);
        Assert.True(source.Add(Iron, 20).Succeeded);

        var result = InventoryBulkTransfer.TransferAllThatFits(
            source,
            target,
            item => item == Copper);

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryBulkTransferFailure.NothingToMove, result.Failure);
        Assert.Equal(20, source.GetAmount(Iron));
        Assert.Equal(0, target.TotalItemCount);
    }
}

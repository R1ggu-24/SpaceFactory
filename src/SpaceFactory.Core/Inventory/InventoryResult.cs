namespace SpaceFactory.Core.Inventory;

public enum InventoryFailure
{
    None,
    InvalidAmount,
    CapacityExceeded,
    StackLimitExceeded,
    InsufficientItems,
}

public readonly record struct InventoryResult(bool Succeeded, InventoryFailure Failure)
{
    public static InventoryResult Success() => new(true, InventoryFailure.None);

    public static InventoryResult Failed(InventoryFailure failure) => new(false, failure);
}

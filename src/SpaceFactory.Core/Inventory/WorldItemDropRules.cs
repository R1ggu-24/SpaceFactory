using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.Inventory;

/// <summary>
/// Central tuning and transactional inventory rules for item stacks released into zero gravity.
/// Presentation is allowed to commit a reservation only after it found a safe world position.
/// Otherwise the exact source slot is restored without spilling into unrelated slots.
/// </summary>
public static class WorldItemDropConfiguration
{
    public const double MaximumInheritedSpeed = 420;
    public const double StationaryDropImpulse = 24;
    public const double MinimumAngularSpeedRadians = 0.18;
    public const double MaximumAngularSpeedRadians = 0.55;
    public const double PickupRadius = 54;
    public const double SpawnClearanceRadius = 26;
    public const double SpawnOffset = 76;
}

public readonly record struct WorldItemDropReservation(
    int SourceSlotIndex,
    ItemId ItemId,
    int Amount)
{
    public static WorldItemDropReservation Empty { get; } = new(-1, default, 0);

    public bool IsValid => SourceSlotIndex >= 0 && !string.IsNullOrWhiteSpace(ItemId.Value) && Amount > 0;
}

public static class WorldItemDropTransaction
{
    public static bool TryReserveEntireStack(
        SlotInventory source,
        int sourceSlotIndex,
        out WorldItemDropReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(source);
        reservation = WorldItemDropReservation.Empty;
        if (sourceSlotIndex < 0 || sourceSlotIndex >= source.SlotCount)
        {
            return false;
        }

        var slot = source.GetSlot(sourceSlotIndex);
        if (slot.ItemId is not { } itemId || slot.Amount <= 0)
        {
            return false;
        }

        var amount = slot.Amount;
        var result = source.RemoveFromSlot(sourceSlotIndex, itemId, amount);
        if (!result.Succeeded)
        {
            return false;
        }

        reservation = new WorldItemDropReservation(sourceSlotIndex, itemId, amount);
        return true;
    }

    public static bool Rollback(SlotInventory source, WorldItemDropReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!reservation.IsValid || reservation.SourceSlotIndex >= source.SlotCount)
        {
            return false;
        }

        return source.AddToSlot(
            reservation.SourceSlotIndex,
            reservation.ItemId,
            reservation.Amount).Succeeded;
    }
}

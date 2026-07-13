using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Production;
using SpaceFactory.Core.Research;
using SpaceFactory.Core.Ships.Fuel;

namespace SpaceFactory.Application.Factory;

/// <summary>
/// Complete persistent state owned by the factory simulation. The generated base world is
/// deliberately not part of this snapshot and continues to be reconstructed from its seed.
/// </summary>
public sealed record FactoryStateData(
    int Version,
    IReadOnlyList<MachineStateSnapshot> Machines,
    ResearchStateSnapshot Research,
    bool FirstBasicGeneratorBuilt,
    double ShipFuel,
    IReadOnlyDictionary<string, DateTimeOffset> LastSimulatedUtcByComet,
    IReadOnlyList<InventorySlotState> AstronautInventory,
    IReadOnlyList<InventorySlotState> ShipInventory,
    string? ActiveResearchStationId)
{
    public const int CurrentVersion = 2;

    public static FactoryStateData CreateDefault() => new(
        CurrentVersion,
        [],
        new ResearchState().CreateSnapshot(),
        false,
        ShipFuelConfiguration.TankCapacity,
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        [],
        [],
        null);
}

/// <summary>
/// Primitive, catalog-independent representation of one occupied player inventory slot.
/// Empty slots are omitted; their stable position is preserved by <see cref="Index"/>.
/// </summary>
public sealed record InventorySlotState(int Index, string ItemId, int Amount);

/// <summary>
/// Maps the mutable Core inventory to and from persistence DTOs without losing slot positions.
/// The mapper deliberately accepts every syntactically valid item ID so raw resources, factory
/// products and filled or empty containers all use the same save path.
/// </summary>
public static class InventoryStatePersistence
{
    public static IReadOnlyList<InventorySlotState> Capture(SlotInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return inventory.Slots
            .Where(slot => !slot.IsEmpty)
            .Select(slot => new InventorySlotState(
                slot.Index,
                slot.ItemId!.Value.Value,
                slot.Amount))
            .ToArray();
    }

    public static void Restore(
        SlotInventory inventory,
        IReadOnlyList<InventorySlotState> occupiedSlots)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(occupiedSlots);
        ValidateForInventory(inventory, occupiedSlots);

        var previousState = Capture(inventory);
        try
        {
            ApplyValidated(inventory, occupiedSlots);
        }
        catch
        {
            ApplyValidated(inventory, previousState);
            throw;
        }
    }

    private static void ValidateForInventory(
        SlotInventory inventory,
        IReadOnlyList<InventorySlotState> occupiedSlots)
    {
        var indices = new HashSet<int>();
        foreach (var slot in occupiedSlots)
        {
            if (slot is null || slot.Index < 0 || slot.Index >= inventory.SlotCount ||
                !indices.Add(slot.Index) || string.IsNullOrWhiteSpace(slot.ItemId) ||
                slot.Amount <= 0 || slot.Amount > inventory.MaximumStackSize)
            {
                throw new ArgumentException(
                    "The persisted inventory contains an invalid occupied slot.",
                    nameof(occupiedSlots));
            }
        }
    }

    private static void ApplyValidated(
        SlotInventory inventory,
        IReadOnlyList<InventorySlotState> occupiedSlots)
    {
        foreach (var itemGroup in inventory.Slots
                     .Where(slot => !slot.IsEmpty)
                     .GroupBy(slot => slot.ItemId!.Value))
        {
            var amount = itemGroup.Sum(slot => slot.Amount);
            if (!inventory.Remove(itemGroup.Key, amount).Succeeded)
            {
                throw new InvalidOperationException("The existing inventory could not be cleared.");
            }
        }

        foreach (var slot in occupiedSlots.OrderBy(slot => slot.Index))
        {
            var staging = new SlotInventory(1, inventory.MaximumStackSize);
            var itemId = new ItemId(slot.ItemId);
            if (!staging.Add(itemId, slot.Amount).Succeeded ||
                !InventoryTransfer.Transfer(staging, 0, inventory, slot.Index).Succeeded)
            {
                throw new InvalidOperationException("The persisted inventory slot could not be restored.");
            }
        }
    }
}

using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.Logistics;
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
    IReadOnlyList<MachineConnectionSnapshot> Connections,
    ResearchStateSnapshot Research,
    bool FirstBasicGeneratorBuilt,
    double ShipFuel,
    IReadOnlyDictionary<string, DateTimeOffset> LastSimulatedUtcByComet,
    IReadOnlyList<InventorySlotState> AstronautInventory,
    IReadOnlyList<InventorySlotState> ShipInventory,
    string? ActiveResearchStationId,
    IReadOnlyList<PowerNetworkControlState> PowerNetworkControls,
    ShipPowerState ShipPower,
    ShipDockingStateData ShipDocking)
{
    public const int CurrentVersion = 4;

    public static FactoryStateData CreateDefault() => new(
        CurrentVersion,
        [],
        [],
        new ResearchState().CreateSnapshot(),
        false,
        ShipFuelConfiguration.TankCapacity,
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        [],
        [
            new InventorySlotState(
                0,
                ProductionItemIds.PowerCable.Value,
                LogisticsConfiguration.StartingPowerCableCount),
            new InventorySlotState(
                1,
                ProductionItemIds.ConveyorBelt.Value,
                LogisticsConfiguration.StartingConveyorBeltCount),
            new InventorySlotState(
                2,
                ProductionItemIds.TransportPipe.Value,
                LogisticsConfiguration.StartingTransportPipeCount),
        ],
        null,
        [],
        ShipPowerState.Default,
        ShipDockingStateData.Detached);
}

/// <summary>
/// Persistent protection and switch state for one topology-derived power network.
/// The 60-second chart history is intentionally transient and is rebuilt after loading.
/// </summary>
public sealed record PowerNetworkControlState(
    string NetworkId,
    bool IsEnabled,
    bool BreakerTripped,
    double OverloadElapsedSeconds);

/// <summary>
/// Independent switches for the two physical ship power sockets.
/// </summary>
public sealed record ShipPowerState(bool ConnectorAEnabled, bool ConnectorBEnabled)
{
    public static ShipPowerState Default { get; } = new(true, true);
}

/// <summary>
/// Stable ship pose used to restore both docked and freely drifting save games. The comet-local
/// pose keeps an attached ship exact across chunk reloads; the global pose is the fallback for a
/// detached ship and while the attached comet is being restored.
/// </summary>
public sealed record ShipDockingStateData(
    bool IsAttached,
    string? CometId,
    int SectorX,
    int SectorY,
    double RelativePositionX,
    double RelativePositionY,
    double RelativeRotationRadians,
    double LandingLegProgress,
    double GlobalPositionX,
    double GlobalPositionY,
    double GlobalRotationRadians)
{
    public static ShipDockingStateData Detached { get; } = new(
        false,
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        2500,
        2500,
        0);
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

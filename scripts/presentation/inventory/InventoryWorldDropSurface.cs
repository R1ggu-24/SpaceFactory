using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Full-screen drag target placed behind the inventory frame. Slot controls win inside the frame;
/// only a release over the visible world backdrop reaches this target.
/// </summary>
public partial class InventoryWorldDropSurface : ColorRect
{
    public event Action<InventorySlotAddress, Vector2>? WorldDropRequested;

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        InventorySlotControl.TryReadDragAddress(data, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InventorySlotControl.TryReadDragAddress(data, out var address))
        {
            WorldDropRequested?.Invoke(address, GetGlobalMousePosition());
        }
    }
}

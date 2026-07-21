using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Presentation.InventoryUI;

namespace SpaceFactory.Presentation.Building;

/// <summary>Drag source adapter for physical machine-output slots.</summary>
public partial class MachineOutputDropControl : PanelContainer
{
    private InventorySlotAddress _address;
    private ItemPresentationViewModel? _item;
    private int _amount;
    private Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? _previewTransfer;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _transferRequested;
    private bool _hovered;

    public bool CanStartDrag => _item is not null && _amount > 0 && _address.SlotIndex >= 0;

    public override void _Ready()
    {
        MouseEntered += HandleMouseEntered;
        MouseExited += HandleMouseExited;
    }

    public override void _ExitTree()
    {
        ItemHoverNamePresenter.End(this);
        MouseEntered -= HandleMouseEntered;
        MouseExited -= HandleMouseExited;
    }

    public void ConfigureDrag(string inventoryId, int slotIndex, ItemPresentationViewModel? item, int amount)
    {
        _address = new InventorySlotAddress(inventoryId, slotIndex);
        _item = item;
        _amount = amount;
        TooltipText = string.Empty;
        if (_hovered)
        {
            ItemHoverNamePresenter.End(this);
            if (_item is not null && _amount > 0)
            {
                ItemHoverNamePresenter.Begin(this, _item.DisplayName);
            }
        }
    }

    public void ConfigureTransfer(
        Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? previewTransfer,
        Func<InventorySlotAddress, InventorySlotAddress, bool>? transferRequested)
    {
        _previewTransfer = previewTransfer;
        _transferRequested = transferRequested;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        _ = atPosition;
        return _address.SlotIndex >= 0 &&
               InventorySlotControl.TryReadDragAddress(data, out var source) &&
               source != _address &&
               (_previewTransfer?.Invoke(source, _address).Succeeded ?? false);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _ = atPosition;
        if (InventorySlotControl.TryReadDragAddress(data, out var source) && source != _address)
        {
            _transferRequested?.Invoke(source, _address);
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!CanStartDrag)
        {
            return default;
        }

        var preview = new ResourceIconControl
        {
            CustomMinimumSize = new Vector2(62, 62),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        preview.Configure(_item);
        SetDragPreview(preview);
        return InventorySlotControl.CreateDragData(_address);
    }

    private void HandleMouseEntered()
    {
        _hovered = true;
        if (_item is not null && _amount > 0)
        {
            ItemHoverNamePresenter.Begin(this, _item.DisplayName);
        }
    }

    private void HandleMouseExited()
    {
        _hovered = false;
        ItemHoverNamePresenter.End(this);
    }
}

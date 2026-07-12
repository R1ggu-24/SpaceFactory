using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryPanelControl : PanelContainer
{
    private const string SlotScenePath = "res://scenes/ui/inventory/InventorySlot.tscn";
    private readonly List<InventorySlotControl> _slotControls = [];
    private Label _title = null!;
    private Label _summary = null!;
    private Label _hint = null!;
    private GridContainer _grid = null!;
    private SlotInventory? _inventory;
    private IReadOnlyDictionary<string, ResourceDefinition>? _resources;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _transferRequested;
    private string _inventoryId = string.Empty;

    public SlotInventory Inventory => _inventory ??
        throw new InvalidOperationException("The inventory panel has not been configured.");

    public override void _Ready()
    {
        _title = GetNode<Label>("Margin/Layout/Header/Title");
        _summary = GetNode<Label>("Margin/Layout/Header/Summary");
        _hint = GetNode<Label>("Margin/Layout/Hint");
        _grid = GetNode<GridContainer>("Margin/Layout/GridScroll/Grid");
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.004f, 0.027f, 0.042f, 0.97f),
            BorderColor = new Color(0.045f, 0.54f, 0.7f, 0.88f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
    }

    public void Configure(
        string inventoryId,
        string title,
        int columns,
        SlotInventory inventory,
        IReadOnlyDictionary<string, ResourceDefinition> resources,
        Func<InventorySlotAddress, InventorySlotAddress, bool> transferRequested)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(resources);
        _inventoryId = inventoryId;
        _inventory = inventory;
        _resources = resources;
        _transferRequested = transferRequested;
        _title.Text = title;
        _grid.Columns = columns;
        _hint.Text = $"{columns} Spalten  •  max. {inventory.MaximumStackSize} je Slot";
        BuildSlots();
        Refresh();
    }

    public void Refresh()
    {
        if (_inventory is null || _resources is null)
        {
            return;
        }

        for (var index = 0; index < _slotControls.Count; index++)
        {
            var slot = _inventory.GetSlot(index);
            _slotControls[index].Refresh(slot, FindResource(slot));
        }

        _summary.Text = $"{_inventory.UsedSlotCount} / {_inventory.SlotCount} SLOTS  •  {_inventory.TotalItemCount} EINHEITEN";
    }

    private void BuildSlots()
    {
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _slotControls.Clear();
        var slotScene = GD.Load<PackedScene>(SlotScenePath) ??
            throw new InvalidOperationException($"Inventory slot scene is missing at {SlotScenePath}.");
        foreach (var slot in Inventory.Slots)
        {
            var control = slotScene.Instantiate<InventorySlotControl>();
            _grid.AddChild(control);
            control.Configure(
                _inventoryId,
                slot,
                FindResource(slot),
                _transferRequested ?? throw new InvalidOperationException("Transfer handler is missing."));
            _slotControls.Add(control);
        }
    }

    private ResourceDefinition? FindResource(InventorySlot slot)
    {
        if (slot.ItemId is not { } itemId || _resources is null)
        {
            return null;
        }

        return _resources.TryGetValue(itemId.Value, out var resource) ? resource : null;
    }
}

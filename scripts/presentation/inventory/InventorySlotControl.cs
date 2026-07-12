using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventorySlotControl : PanelContainer
{
    private const string DragKind = "space_factory_inventory_slot";
    private ResourceIconControl _icon = null!;
    private Label _amount = null!;
    private Label _slotNumber = null!;
    private Label _emptyMarker = null!;
    private ResourceDefinition? _resource;
    private bool _isEmpty = true;
    private bool _hovered;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _transferRequested;

    public string InventoryId { get; private set; } = string.Empty;

    public int SlotIndex { get; private set; }

    public InventorySlotAddress Address => new(InventoryId, SlotIndex);

    public override void _Ready()
    {
        _icon = GetNode<ResourceIconControl>("Margin/SlotContent/Icon");
        _amount = GetNode<Label>("Margin/SlotContent/Amount");
        _slotNumber = GetNode<Label>("Margin/SlotContent/SlotNumber");
        _emptyMarker = GetNode<Label>("Margin/SlotContent/EmptyMarker");
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        RefreshStyle();
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    public void Configure(
        string inventoryId,
        InventorySlot slot,
        ResourceDefinition? resource,
        Func<InventorySlotAddress, InventorySlotAddress, bool> transferRequested)
    {
        InventoryId = inventoryId;
        SlotIndex = slot.Index;
        _transferRequested = transferRequested;
        Refresh(slot, resource);
    }

    public void Refresh(InventorySlot slot, ResourceDefinition? resource)
    {
        SlotIndex = slot.Index;
        _resource = resource;
        _isEmpty = slot.IsEmpty;
        _slotNumber.Text = $"{slot.Index + 1:00}";
        _emptyMarker.Visible = slot.IsEmpty;
        _amount.Visible = !slot.IsEmpty;
        _amount.Text = slot.IsEmpty ? string.Empty : slot.Amount.ToString();
        _icon.Configure(slot.IsEmpty ? null : resource);
        TooltipText = slot.IsEmpty
            ? $"Leerer Slot {slot.Index + 1}"
            : $"{resource?.DisplayName ?? slot.ItemId?.Value ?? "Unbekannter Rohstoff"}\n{slot.Amount} / {slot.MaximumAmount}";
        RefreshStyle();
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_isEmpty || _resource is null)
        {
            return default;
        }

        var dragData = new Godot.Collections.Dictionary
        {
            ["kind"] = DragKind,
            ["inventory_id"] = InventoryId,
            ["slot_index"] = SlotIndex,
        };
        SetDragPreview(CreateDragPreview());
        return dragData;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return _transferRequested is not null &&
               TryReadAddress(data, out var source) &&
               source != Address;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_transferRequested is null || !TryReadAddress(data, out var source) || source == Address)
        {
            return;
        }

        _transferRequested(source, Address);
    }

    private Control CreateDragPreview()
    {
        var preview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(68, 68),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        preview.AddThemeStyleboxOverride("panel", CreateStyle(
            new Color(0.02f, 0.14f, 0.19f, 0.96f),
            new Color(0.12f, 0.9f, 1, 0.95f),
            2));

        var content = new Control { CustomMinimumSize = new Vector2(64, 64), MouseFilter = MouseFilterEnum.Ignore };
        var icon = new ResourceIconControl
        {
            MouseFilter = MouseFilterEnum.Ignore,
            LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        icon.Configure(_resource);
        content.AddChild(icon);
        preview.AddChild(content);
        return preview;
    }

    private static bool TryReadAddress(Variant data, out InventorySlotAddress address)
    {
        address = default;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") ||
            dictionary["kind"].AsString() != DragKind ||
            !dictionary.ContainsKey("inventory_id") ||
            !dictionary.ContainsKey("slot_index"))
        {
            return false;
        }

        address = new InventorySlotAddress(
            dictionary["inventory_id"].AsString(),
            dictionary["slot_index"].AsInt32());
        return !string.IsNullOrWhiteSpace(address.InventoryId) && address.SlotIndex >= 0;
    }

    private void OnMouseEntered()
    {
        _hovered = true;
        RefreshStyle();
    }

    private void OnMouseExited()
    {
        _hovered = false;
        RefreshStyle();
    }

    private void RefreshStyle()
    {
        var background = _isEmpty
            ? new Color(0.012f, 0.035f, 0.052f, 0.94f)
            : new Color(0.02f, 0.075f, 0.096f, 0.98f);
        var border = _hovered
            ? new Color(0.12f, 0.9f, 1, 0.98f)
            : _isEmpty
                ? new Color(0.08f, 0.29f, 0.38f, 0.82f)
                : new Color(0.06f, 0.56f, 0.72f, 0.92f);
        AddThemeStyleboxOverride("panel", CreateStyle(background, border, _hovered ? 2 : 1));
    }

    private static StyleBoxFlat CreateStyle(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
    };
}

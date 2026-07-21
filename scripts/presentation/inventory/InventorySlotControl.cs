using Godot;
using SpaceFactory.Core.Inventory;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventorySlotControl : PanelContainer
{
    public const string DragDataKind = "space_factory_inventory_slot";

    private enum DropTargetState
    {
        None,
        Valid,
        Invalid,
    }

    private ResourceIconControl _icon = null!;
    private PanelContainer _amountBadge = null!;
    private Label _amount = null!;
    private Label _fullMarker = null!;
    private Label _shortcut = null!;
    private ItemPresentationViewModel? _item;
    private bool _isEmpty = true;
    private bool _hovered;
    private bool _selected;
    private bool _dragEnabled = true;
    private bool _isHandSlot;
    private int _amountValue;
    private string _baseTooltip = string.Empty;
    private DropTargetState _dropTargetState;
    private Tween? _feedbackTween;
    private Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult>? _previewTransfer;
    private Func<InventorySlotAddress, InventorySlotAddress, bool>? _transferRequested;
    private Action<InventorySlotAddress>? _selectionRequested;
    private Action<InventoryItemContextRequest, Vector2>? _contextRequested;

    public string InventoryId { get; private set; } = string.Empty;

    public int SlotIndex { get; private set; }

    public InventorySlotAddress Address => new(InventoryId, SlotIndex);

    public bool HasShortcutLabel => _shortcut.Visible && !string.IsNullOrWhiteSpace(_shortcut.Text);

    public bool IsHandSlotPresentation => _isHandSlot;

    public bool HasLegacyItemMarkers => _fullMarker.Visible || _shortcut.Visible;

    public bool HasUnobstructedDragSurface =>
        GetNode<Control>("Margin").MouseFilter == MouseFilterEnum.Ignore &&
        GetNode<Control>("Margin/SlotContent").MouseFilter == MouseFilterEnum.Ignore;

    public float PresentedSlotSize => CustomMinimumSize.X;

    public override void _Ready()
    {
        // The slot root owns selection and drag. Decorative children must never become
        // the GUI pick target, otherwise Godot asks the inner MarginContainer for drag data.
        GetNode<Control>("Margin").MouseFilter = MouseFilterEnum.Ignore;
        GetNode<Control>("Margin/SlotContent").MouseFilter = MouseFilterEnum.Ignore;
        _icon = GetNode<ResourceIconControl>("Margin/SlotContent/Icon");
        _amountBadge = GetNode<PanelContainer>("Margin/SlotContent/AmountBadge");
        _amount = GetNode<Label>("Margin/SlotContent/AmountBadge/Amount");
        _fullMarker = GetNode<Label>("Margin/SlotContent/FullMarker");
        _shortcut = GetNode<Label>("Margin/SlotContent/Shortcut");
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        SetProcess(false);
        RefreshAmountBadgeStyle();
        RefreshStyle();
    }

    public override void _ExitTree()
    {
        ItemHoverNamePresenter.End(this);
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        _feedbackTween?.Kill();
    }

    public override void _Process(double delta)
    {
        if (_dropTargetState == DropTargetState.None || GetViewport().GuiIsDragging())
        {
            return;
        }

        _dropTargetState = DropTargetState.None;
        TooltipText = string.Empty;
        SetProcess(false);
        RefreshStyle();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } pointer)
        {
            return;
        }

        if (pointer.ButtonIndex == MouseButton.Left)
        {
            _selectionRequested?.Invoke(Address);
            return;
        }

        if (pointer.ButtonIndex == MouseButton.Right && !_isEmpty && _item is not null)
        {
            _contextRequested?.Invoke(
                new InventoryItemContextRequest(Address, _item.Id, _item.DisplayName, _amountValue),
                GetGlobalMousePosition());
            AcceptEvent();
        }
    }

    public void Configure(
        string inventoryId,
        InventorySlot slot,
        ItemPresentationViewModel? item,
        Func<InventorySlotAddress, InventorySlotAddress, InventoryTransferResult> previewTransfer,
        Func<InventorySlotAddress, InventorySlotAddress, bool> transferRequested,
        Action<InventorySlotAddress> selectionRequested,
        Action<InventoryItemContextRequest, Vector2>? contextRequested = null)
    {
        InventoryId = inventoryId;
        SlotIndex = slot.Index;
        _previewTransfer = previewTransfer;
        _transferRequested = transferRequested;
        _selectionRequested = selectionRequested;
        _contextRequested = contextRequested;
        Refresh(slot, item);
    }

    public void Refresh(InventorySlot slot, ItemPresentationViewModel? item)
    {
        SlotIndex = slot.Index;
        _item = item;
        _isEmpty = slot.IsEmpty;
        var stackLimit = item is null
            ? slot.MaximumAmount
            : Math.Min(slot.MaximumAmount, item.MaximumStackSize);
        _amountValue = slot.Amount;
        _amountBadge.Visible = !slot.IsEmpty && stackLimit > 1;
        _fullMarker.Visible = false;
        _shortcut.Visible = false;
        _amount.Text = slot.IsEmpty || stackLimit == 1 ? string.Empty : slot.Amount.ToString();
        _icon.Configure(slot.IsEmpty ? null : item);
        _baseTooltip = slot.IsEmpty
            ? $"Leerer Slot {slot.Index + 1}"
            : $"{item?.DisplayName ?? slot.ItemId?.Value ?? "Unbekannter Gegenstand"}\n{slot.Amount} / {stackLimit}";
        TooltipText = string.Empty;
        if (_hovered)
        {
            ItemHoverNamePresenter.End(this);
            if (!_isEmpty && _item is not null)
            {
                ItemHoverNamePresenter.Begin(this, _item.DisplayName);
            }
        }
        RefreshStyle();
    }

    public void SetSlotSize(float size, bool compact)
    {
        size = Mathf.Clamp(size, InventoryUiConfiguration.ToolSlotSize, 84);
        CustomMinimumSize = new Vector2(size, size);
        GetNode<Control>("Margin/SlotContent").CustomMinimumSize = new Vector2(
            Mathf.Max(30, size - 8),
            Mathf.Max(30, size - 8));
        _amount.AddThemeFontSizeOverride("font_size", compact ? 11 : size >= 70 ? 14 : 12);
        _fullMarker.AddThemeFontSizeOverride("font_size", compact ? 7 : 8);
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        RefreshStyle();
    }

    public void SetShortcutLabel(string? text)
    {
        _ = text;
        _shortcut.Text = string.Empty;
        _shortcut.Visible = false;
    }

    public void SetDragEnabled(bool enabled) => _dragEnabled = enabled;

    public void SetHandSlotPresentation(bool enabled)
    {
        _isHandSlot = enabled;
        RefreshStyle();
    }

    public void PlayTransferFeedback(bool succeeded)
    {
        _feedbackTween?.Kill();
        SelfModulate = succeeded
            ? new Color(0.7f, 1, 0.9f, 1)
            : new Color(1, 0.68f, 0.68f, 1);
        _feedbackTween = CreateTween();
        _feedbackTween.SetEase(Tween.EaseType.Out);
        _feedbackTween.SetTrans(Tween.TransitionType.Cubic);
        _feedbackTween.TweenProperty(this, new NodePath("self_modulate"), Colors.White, 0.28);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!_dragEnabled || _isEmpty)
        {
            return default;
        }

        _selectionRequested?.Invoke(Address);
        var dragData = CreateDragData(Address);
        SetDragPreview(CreateDragPreview());
        return dragData;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadDragAddress(data, out var source))
        {
            return false;
        }

        var preview = _previewTransfer?.Invoke(source, Address) ??
                      InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks);
        var valid = source != Address && preview.Succeeded;
        _dropTargetState = valid ? DropTargetState.Valid : DropTargetState.Invalid;
        TooltipText = valid
            ? $"{_baseTooltip}\n\nGültiges Transferziel"
            : $"{_baseTooltip}\n\n{DescribeInvalidTarget(source, preview.Failure)}";
        SetProcess(true);
        RefreshStyle();
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_transferRequested is null || !TryReadDragAddress(data, out var source) || source == Address)
        {
            return;
        }

        var succeeded = _transferRequested(source, Address);
        _dropTargetState = DropTargetState.None;
        TooltipText = string.Empty;
        SetProcess(false);
        PlayTransferFeedback(succeeded);
        RefreshStyle();
    }

    private Control CreateDragPreview()
    {
        var preview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(78, 78),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        preview.AddThemeStyleboxOverride("panel", CreateStyle(
            new Color(0.01f, 0.075f, 0.1f, 0.98f),
            new Color(0.16f, 0.87f, 1, 0.98f),
            2));

        var content = new Control
        {
            CustomMinimumSize = new Vector2(72, 72),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var icon = new ResourceIconControl { MouseFilter = MouseFilterEnum.Ignore };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = 7;
        icon.OffsetTop = 7;
        icon.OffsetRight = -7;
        icon.OffsetBottom = -7;
        icon.Configure(_item);
        content.AddChild(icon);

        var amount = new Label
        {
            Text = _item?.MaximumStackSize == 1 ? string.Empty : _amountValue.ToString(),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        amount.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        amount.OffsetRight = -5;
        amount.OffsetBottom = -4;
        amount.AddThemeColorOverride("font_color", Colors.White);
        amount.AddThemeColorOverride("font_shadow_color", Colors.Black);
        amount.AddThemeConstantOverride("shadow_offset_x", 1);
        amount.AddThemeConstantOverride("shadow_offset_y", 1);
        amount.AddThemeFontSizeOverride("font_size", 14);
        content.AddChild(amount);
        preview.AddChild(content);
        return preview;
    }

    public static bool TryReadDragAddress(Variant data, out InventorySlotAddress address)
    {
        address = default;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") ||
            dictionary["kind"].AsString() != DragDataKind ||
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

    public static Godot.Collections.Dictionary CreateDragData(InventorySlotAddress address) => new()
    {
        ["kind"] = DragDataKind,
        ["inventory_id"] = address.InventoryId,
        ["slot_index"] = address.SlotIndex,
    };

    private void OnMouseEntered()
    {
        _hovered = true;
        if (!_isEmpty && _item is not null)
        {
            ItemHoverNamePresenter.Begin(this, _item.DisplayName);
        }
        RefreshStyle();
    }

    private void OnMouseExited()
    {
        ItemHoverNamePresenter.End(this);
        _hovered = false;
        _dropTargetState = DropTargetState.None;
        TooltipText = string.Empty;
        SetProcess(false);
        RefreshStyle();
    }

    private void RefreshAmountBadgeStyle()
    {
        _amountBadge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.002f, 0.009f, 0.014f, 0.91f),
            BorderColor = new Color(0.12f, 0.36f, 0.44f, 0.8f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
        });
    }

    private void RefreshStyle()
    {
        var background = _isEmpty
            ? new Color(0.008f, 0.026f, 0.038f, 0.96f)
            : new Color(0.012f, 0.052f, 0.068f, 0.985f);
        var border = _isEmpty
            ? new Color(0.07f, 0.25f, 0.31f, 0.78f)
            : new Color(0.06f, 0.49f, 0.61f, 0.9f);
        var borderWidth = _isHandSlot ? 2 : 1;

        if (_isHandSlot)
        {
            background = new Color(0.025f, 0.035f, 0.065f, 0.985f);
            border = new Color(0.38f, 0.58f, 0.92f, 0.94f);
        }

        if (_selected)
        {
            background = background.Lightened(0.035f);
            border = _isHandSlot
                ? new Color(0.48f, 0.9f, 1f, 1f)
                : new Color(0.23f, 0.78f, 0.96f, 0.98f);
        }

        if (_hovered)
        {
            background = background.Lightened(0.055f);
        }

        if (_dropTargetState == DropTargetState.Valid)
        {
            background = new Color(0.015f, 0.13f, 0.105f, 0.99f);
            border = new Color(0.28f, 0.95f, 0.68f, 1);
        }
        else if (_dropTargetState == DropTargetState.Invalid)
        {
            background = new Color(0.13f, 0.032f, 0.04f, 0.99f);
            border = new Color(1, 0.31f, 0.33f, 1);
        }

        AddThemeStyleboxOverride("panel", CreateStyle(background, border, borderWidth));
    }

    private static StyleBoxFlat CreateStyle(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
    };

    private string DescribeInvalidTarget(InventorySlotAddress source, InventoryTransferFailure failure)
    {
        if (source == Address)
        {
            return "Quelle und Ziel sind identisch";
        }

        return failure switch
        {
            InventoryTransferFailure.SourceEmpty => "Ungültig: Quellslot ist leer",
            InventoryTransferFailure.TargetStackFull => "Ungültig: Zielstapel ist voll",
            InventoryTransferFailure.IncompatibleStacks => "Ungültig: Stapel nicht kompatibel",
            InventoryTransferFailure.ItemNotAccepted => "Ungültig: Gegenstandstyp nicht erlaubt",
            InventoryTransferFailure.StackLimitExceeded => "Ungültig: Stapelgrenze überschritten",
            InventoryTransferFailure.InsufficientItems => "Ungültig: Menge nicht verfügbar",
            _ => "Ungültiges Transferziel",
        };
    }
}

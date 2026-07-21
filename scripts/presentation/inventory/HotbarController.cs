using Godot;
using SpaceFactory.Core.Inventory;
using SpaceFactory.Core.Settings;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>
/// Lightweight always-on view over the same six slots shown beneath the personal inventory.
/// It never owns item data and registers its slot controls exactly once.
/// </summary>
public partial class HotbarController : CanvasLayer
{
    private const string SlotScenePath = "res://scenes/ui/inventory/InventorySlot.tscn";
    private readonly List<InventorySlotControl> _slots = [];
    private PanelContainer _frame = null!;
    private MarginContainer _frameMargin = null!;
    private HBoxContainer _slotsRow = null!;
    private GridContainer _grid = null!;
    private MarginContainer _handSlotHost = null!;
    private InventoryItemContextMenu _itemContextMenu = null!;
    private InventorySlotControl? _handSlot;
    private HotbarState? _state;
    private ToolInventoryState? _toolState;
    private ItemPresentationCatalog? _items;
    private Action<int>? _selectionRequested;
    private Action? _handModeRequested;

    public int SlotControlCount => _slots.Count;

    public bool HandSlotVisible => _handSlotHost.Visible && _handSlot is not null;

    public event Action<InventoryItemContextAction, InventoryItemContextRequest>? ContextActionRequested;

    public override void _Ready()
    {
        _frame = GetNode<PanelContainer>("Root/Frame");
        _frameMargin = GetNode<MarginContainer>("Root/Frame/Margin");
        _slotsRow = GetNode<HBoxContainer>("Root/Frame/Margin/Slots");
        _grid = GetNode<GridContainer>("Root/Frame/Margin/Slots/Grid");
        _handSlotHost = GetNode<MarginContainer>("Root/Frame/Margin/Slots/HandSlotHost");
        _itemContextMenu = GetNode<InventoryItemContextMenu>("Root/ItemContextMenu");
        _itemContextMenu.ActionRequested += HandleContextActionRequested;
        _frame.AddThemeStyleboxOverride("panel", CreateFrameStyle());
        ApplyLayoutMetrics();
        if (_state is not null && _items is not null)
        {
            BuildSlots();
        }
    }

    public override void _ExitTree()
    {
        if (_itemContextMenu is not null)
        {
            _itemContextMenu.ActionRequested -= HandleContextActionRequested;
        }
    }

    public void Initialize(
        HotbarState state,
        ItemPresentationCatalog items,
        Action<int> selectionRequested)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selectionRequested);
        _state = state;
        _items = items;
        _selectionRequested = selectionRequested;
        if (IsNodeReady())
        {
            BuildSlots();
        }
    }

    public void Initialize(
        HotbarState state,
        ToolInventoryState toolState,
        ItemPresentationCatalog items,
        Action<int> selectionRequested,
        Action handModeRequested)
    {
        ArgumentNullException.ThrowIfNull(toolState);
        ArgumentNullException.ThrowIfNull(handModeRequested);
        _toolState = toolState;
        _handModeRequested = handModeRequested;
        Initialize(state, items, selectionRequested);
    }

    /// <summary>
    /// Adds the read-only hand mirror after the legacy hotbar initialization path.
    /// This keeps the presentation API compatible while save migration is integrated.
    /// </summary>
    public void ConfigureToolState(ToolInventoryState toolState, Action handModeRequested)
    {
        ArgumentNullException.ThrowIfNull(toolState);
        ArgumentNullException.ThrowIfNull(handModeRequested);
        _toolState = toolState;
        _handModeRequested = handModeRequested;
        if (IsNodeReady())
        {
            BuildSlots();
        }
    }

    public void Refresh()
    {
        if (_state is null || _items is null || _slots.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _slots.Count; index++)
        {
            var slot = _state.Inventory.GetSlot(index);
            var item = slot.ItemId is { } itemId
                ? _items.GetOrCreateFallback(itemId, slot.MaximumAmount)
                : null;
            _slots[index].Refresh(slot, item);
            _slots[index].SetSelected(index == _state.ActiveSlotIndex &&
                                      _toolState?.IsHandModeActive != true);
            _slots[index].SetShortcutLabel(null);
        }

        RefreshHandSlot();
    }

    public void SetGameplayVisible(bool visible)
    {
        if (!visible)
        {
            _itemContextMenu?.Close();
        }
        Visible = visible;
        if (visible)
        {
            Refresh();
        }
    }

    public bool TryCloseTransientUi() => _itemContextMenu?.TryClose() == true;

#if DEBUG
    public void RunConstructionSmokeTest()
    {
        if (_state is null || _slots.Count != InventoryConfiguration.HotbarSlotCount ||
            _grid.Columns != InventoryConfiguration.HotbarSlotCount)
        {
            throw new InvalidOperationException("The standalone hotbar did not create exactly six slots.");
        }


        if (_grid.GetThemeConstant("h_separation") != InventoryUiConfiguration.HotbarSlotGap ||
            _slotsRow.GetThemeConstant("separation") != InventoryUiConfiguration.HotbarSlotGap ||
            _handSlotHost.GetThemeConstant("margin_left") != InventoryUiConfiguration.HotbarHandSlotExtraGap ||
            _slots.Any(slot => slot.CustomMinimumSize != new Vector2(
                InventoryUiConfiguration.HotbarSlotSize,
                InventoryUiConfiguration.HotbarSlotSize)) ||
            _slots.Any(slot => slot.HasShortcutLabel) ||
            (_handSlot is not null && _handSlot.CustomMinimumSize != new Vector2(
                InventoryUiConfiguration.HotbarSlotSize,
                InventoryUiConfiguration.HotbarSlotSize)))
        {
            throw new InvalidOperationException("Hotbar slots do not use the shared equal-size and equal-gap metrics.");
        }


        if (_toolState is not null &&
            (_handSlot is null || !HandSlotVisible || !_handSlot.IsHandSlotPresentation))
        {
            throw new InvalidOperationException("The configured hand slot is not visible beside the hotbar.");
        }

        Refresh();
        GD.Print("HOTBAR_UI_SMOKE_OK: six equal slots, distinct hand slot, no permanent key labels");
    }
#endif

    private void BuildSlots()
    {
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _slots.Clear();
        foreach (var child in _handSlotHost.GetChildren())
        {
            _handSlotHost.RemoveChild(child);
            child.QueueFree();
        }
        _handSlot = null;
        _handSlotHost.Visible = _toolState is not null;
        _grid.Columns = InventoryConfiguration.HotbarSlotCount;
        var scene = GD.Load<PackedScene>(SlotScenePath) ??
                    throw new InvalidOperationException($"Inventory slot scene is missing at {SlotScenePath}.");
        foreach (var slot in _state!.Inventory.Slots)
        {
            var control = scene.Instantiate<InventorySlotControl>();
            _grid.AddChild(control);
            var item = slot.ItemId is { } itemId
                ? _items!.GetOrCreateFallback(itemId, slot.MaximumAmount)
                : null;
            control.Configure(
                InventoryMenuController.HotbarInventoryId,
                slot,
                item,
                static (_, _) => InventoryTransferResult.Failed(InventoryTransferFailure.IncompatibleStacks),
                static (_, _) => false,
                address => _selectionRequested?.Invoke(address.SlotIndex),
                HandleSlotContextRequested);
            control.SetDragEnabled(false);
            control.SetSlotSize(InventoryUiConfiguration.HotbarSlotSize, compact: true);
            _slots.Add(control);
        }

        if (_toolState is not null)
        {
            var selected = _toolState.Inventory.GetSlot(_toolState.SelectedSlotIndex);
            var item = selected.ItemId is { } itemId
                ? _items!.GetOrCreateFallback(itemId, selected.MaximumAmount)
                : null;
            _handSlot = scene.Instantiate<InventorySlotControl>();
            _handSlotHost.AddChild(_handSlot);
            _handSlot.Configure(
                InventoryMenuController.ToolInventoryId,
                selected,
                item,
                static (_, _) => InventoryTransferResult.Failed(InventoryTransferFailure.ItemNotAccepted),
                static (_, _) => false,
                _ => _handModeRequested?.Invoke(),
                contextRequested: null);
            _handSlot.SetDragEnabled(false);
            _handSlot.SetSlotSize(InventoryUiConfiguration.HotbarSlotSize, compact: true);
            _handSlot.SetHandSlotPresentation(true);
        }

        ApplyLayoutMetrics();
        Refresh();
    }

    private void ApplyLayoutMetrics()
    {
        var visibleSlotCount = InventoryConfiguration.HotbarSlotCount + (_toolState is null ? 0 : 1);
        var width = (visibleSlotCount * InventoryUiConfiguration.HotbarSlotSize) +
                    ((visibleSlotCount - 1) * InventoryUiConfiguration.HotbarSlotGap) +
                    (_toolState is null ? 0 : InventoryUiConfiguration.HotbarHandSlotExtraGap) +
                    (2 * InventoryUiConfiguration.HotbarFramePaddingHorizontal);
        var height = InventoryUiConfiguration.HotbarSlotSize +
                     (2 * InventoryUiConfiguration.HotbarFramePaddingVertical);
        _frame.OffsetLeft = -width * 0.5f;
        _frame.OffsetRight = width * 0.5f;
        _frame.OffsetTop = -(height + InventoryUiConfiguration.HotbarBottomMargin);
        _frame.OffsetBottom = -InventoryUiConfiguration.HotbarBottomMargin;
        _frameMargin.AddThemeConstantOverride("margin_left", InventoryUiConfiguration.HotbarFramePaddingHorizontal);
        _frameMargin.AddThemeConstantOverride("margin_right", InventoryUiConfiguration.HotbarFramePaddingHorizontal);
        _frameMargin.AddThemeConstantOverride("margin_top", InventoryUiConfiguration.HotbarFramePaddingVertical);
        _frameMargin.AddThemeConstantOverride("margin_bottom", InventoryUiConfiguration.HotbarFramePaddingVertical);
        _grid.AddThemeConstantOverride("h_separation", InventoryUiConfiguration.HotbarSlotGap);
        _slotsRow.AddThemeConstantOverride("separation", InventoryUiConfiguration.HotbarSlotGap);
        _handSlotHost.AddThemeConstantOverride("margin_left", InventoryUiConfiguration.HotbarHandSlotExtraGap);
    }

    private void RefreshHandSlot()
    {
        if (_toolState is null || _items is null || _handSlot is null)
        {
            return;
        }

        var selected = _toolState.Inventory.GetSlot(_toolState.SelectedSlotIndex);
        var item = selected.ItemId is { } itemId
            ? _items.GetOrCreateFallback(itemId, selected.MaximumAmount)
            : null;
        _handSlot.Refresh(selected, item);
        _handSlot.SetSelected(_toolState.IsHandModeActive);
        _handSlot.SetShortcutLabel(null);
        _handSlot.TooltipText = string.Empty;
    }

    private void HandleSlotContextRequested(InventoryItemContextRequest request, Vector2 screenPosition)
    {
        var actions = InventoryItemContextRules.BuildActions(
            request.ExpectedItemId,
            request.Amount,
            isTool: false,
            request.Address.InventoryId);
        _itemContextMenu.Open(new InventoryItemContextMenuModel(request, actions), screenPosition);
    }

    private void HandleContextActionRequested(
        InventoryItemContextAction action,
        InventoryItemContextRequest request) =>
        ContextActionRequested?.Invoke(action, request);

    private static StyleBoxFlat CreateFrameStyle() => new()
    {
        BgColor = new Color(0.003f, 0.018f, 0.028f, 0.88f),
        BorderColor = new Color(0.06f, 0.48f, 0.62f, 0.9f),
        BorderWidthLeft = InventoryUiConfiguration.HotbarBorderWidth,
        BorderWidthTop = InventoryUiConfiguration.HotbarAccentBorderWidth,
        BorderWidthRight = InventoryUiConfiguration.HotbarBorderWidth,
        BorderWidthBottom = InventoryUiConfiguration.HotbarBorderWidth,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ShadowColor = new Color(0, 0, 0, 0.48f),
        ShadowSize = 8,
    };
}

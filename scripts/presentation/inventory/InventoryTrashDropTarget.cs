using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

public partial class InventoryTrashDropTarget : PanelContainer
{
    private bool _hovered;

    public event Action<InventorySlotAddress>? DeleteRequested;

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        _ = atPosition;
        var valid = InventorySlotControl.TryReadDragAddress(data, out _);
        if (_hovered != valid)
        {
            _hovered = valid;
            RefreshStyle();
        }

        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _ = atPosition;
        if (InventorySlotControl.TryReadDragAddress(data, out var address))
        {
            DeleteRequested?.Invoke(address);
        }

        _hovered = false;
        RefreshStyle();
    }

    public override void _Ready() => RefreshStyle();

    public override void _Draw()
    {
        var color = _hovered
            ? new Color(1, 0.52f, 0.56f, 1)
            : new Color(0.86f, 0.38f, 0.43f, 0.94f);
        var centerX = Size.X * 0.5f;
        var centerY = Size.Y * 0.5f;
        var body = new Rect2(centerX - 6.5f, centerY - 6, 13, 13);
        DrawRect(body, color with { A = 0.18f }, filled: true);
        DrawRect(body, color, filled: false, width: 1.6f);
        DrawLine(new Vector2(centerX - 8.5f, centerY - 9), new Vector2(centerX + 8.5f, centerY - 9), color, 1.8f, true);
        DrawLine(new Vector2(centerX - 3, centerY - 12), new Vector2(centerX + 3, centerY - 12), color, 1.8f, true);
        DrawLine(new Vector2(centerX - 2.7f, centerY - 3), new Vector2(centerX - 2.7f, centerY + 4), color, 1.2f, true);
        DrawLine(new Vector2(centerX + 2.7f, centerY - 3), new Vector2(centerX + 2.7f, centerY + 4), color, 1.2f, true);
    }

    private void RefreshStyle()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _hovered ? new Color(0.19f, 0.035f, 0.045f, 0.98f) : new Color(0.045f, 0.025f, 0.032f, 0.93f),
            BorderColor = _hovered ? new Color(1, 0.34f, 0.38f, 1) : new Color(0.58f, 0.18f, 0.22f, 0.85f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        });
        QueueRedraw();
    }
}

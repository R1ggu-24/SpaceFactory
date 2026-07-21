using Godot;

namespace SpaceFactory.Presentation.InventoryUI;

/// <summary>Compact icon-only bulk transfer button shared by combined inventory views.</summary>
public partial class InventoryBulkActionButton : Button
{
    [Export]
    public bool PointsIntoStorage { get; set; }

    private bool _hovered;

    public override void _Ready()
    {
        Text = string.Empty;
        CustomMinimumSize = Vector2.One * InventoryUiConfiguration.CompactActionButtonSize;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        RefreshStyle();
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    public override void _Draw()
    {
        var color = Disabled
            ? new Color(0.3f, 0.43f, 0.47f, 0.62f)
            : _hovered
                ? new Color(0.48f, 0.93f, 1, 1)
                : new Color(0.25f, 0.75f, 0.9f, 0.96f);
        var center = Size * 0.5f;
        var direction = PointsIntoStorage ? 1f : -1f;
        var boxX = center.X + (direction * 6f);
        var arrowStartX = center.X - (direction * 9f);
        var arrowEndX = center.X + (direction * 4f);

        DrawRect(new Rect2(boxX - 6, center.Y - 8, 12, 16), color with { A = 0.12f }, true);
        DrawRect(new Rect2(boxX - 6, center.Y - 8, 12, 16), color, false, 1.5f, true);
        DrawLine(new Vector2(arrowStartX, center.Y), new Vector2(arrowEndX, center.Y), color, 2f, true);
        DrawLine(
            new Vector2(arrowEndX, center.Y),
            new Vector2(arrowEndX - (direction * 5f), center.Y - 5f),
            color,
            2f,
            true);
        DrawLine(
            new Vector2(arrowEndX, center.Y),
            new Vector2(arrowEndX - (direction * 5f), center.Y + 5f),
            color,
            2f,
            true);
    }

    private void OnMouseEntered()
    {
        _hovered = true;
        QueueRedraw();
    }

    private void OnMouseExited()
    {
        _hovered = false;
        QueueRedraw();
    }

    private void RefreshStyle()
    {
        AddThemeStyleboxOverride("normal", CreateStyle(
            new Color(0.008f, 0.04f, 0.055f, 0.97f),
            new Color(0.08f, 0.42f, 0.53f, 0.9f)));
        AddThemeStyleboxOverride("hover", CreateStyle(
            new Color(0.014f, 0.09f, 0.115f, 0.99f),
            new Color(0.22f, 0.78f, 0.94f, 1)));
        AddThemeStyleboxOverride("pressed", CreateStyle(
            new Color(0.02f, 0.13f, 0.16f, 1),
            new Color(0.4f, 0.9f, 1, 1)));
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBoxFlat CreateStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };
}

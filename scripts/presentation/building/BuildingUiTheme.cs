using Godot;

namespace SpaceFactory.Presentation.Building;

internal static class BuildingUiTheme
{
    public static readonly Color Background = new(0.002f, 0.012f, 0.019f, 0.965f);
    public static readonly Color PanelBackground = new(0.005f, 0.027f, 0.04f, 0.98f);
    public static readonly Color CardBackground = new(0.008f, 0.039f, 0.055f, 0.985f);
    public static readonly Color Accent = new(0.08f, 0.72f, 0.94f, 1);
    public static readonly Color AccentMuted = new(0.04f, 0.38f, 0.52f, 0.88f);
    public static readonly Color Text = new(0.79f, 0.91f, 0.95f, 1);
    public static readonly Color TextMuted = new(0.48f, 0.64f, 0.7f, 1);
    public static readonly Color Success = new(0.35f, 0.94f, 0.67f, 1);
    public static readonly Color Warning = new(1, 0.7f, 0.28f, 1);
    public static readonly Color Failure = new(1, 0.37f, 0.4f, 1);

    public static Theme CreateTheme()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", Text);
        theme.SetFontSize("font_size", "Label", 14);
        theme.SetColor("font_color", "Button", new Color(0.68f, 0.86f, 0.92f, 1));
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(0.29f, 0.4f, 0.44f, 1));
        theme.SetStylebox("normal", "Button", CreateButtonStyle(CardBackground, AccentMuted));
        theme.SetStylebox("hover", "Button", CreateButtonStyle(new Color(0.025f, 0.105f, 0.14f, 1), Accent));
        theme.SetStylebox("pressed", "Button", CreateButtonStyle(new Color(0.04f, 0.18f, 0.23f, 1), Accent));
        theme.SetStylebox("disabled", "Button", CreateButtonStyle(new Color(0.008f, 0.025f, 0.033f, 0.82f), new Color(0.1f, 0.2f, 0.23f, 0.55f)));
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine
        {
            Color = new Color(0.05f, 0.4f, 0.52f, 0.52f),
            Thickness = 1,
        });
        return theme;
    }

    public static StyleBoxFlat CreateFrameStyle() => CreatePanelStyle(
        Background,
        new Color(0.04f, 0.43f, 0.57f, 0.94f),
        2,
        shadowSize: 14);

    public static StyleBoxFlat CreatePanelStyle(
        Color background,
        Color border,
        int borderWidth = 1,
        int shadowSize = 0)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ShadowColor = new Color(0, 0, 0, shadowSize > 0 ? 0.56f : 0),
            ShadowSize = shadowSize,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        };
    }

    public static StyleBoxFlat CreateButtonStyle(Color background, Color border) => new()
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
        ContentMarginLeft = 14,
        ContentMarginTop = 8,
        ContentMarginRight = 14,
        ContentMarginBottom = 8,
    };
}

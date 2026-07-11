namespace SpaceFactory.Core.Settings;

public enum WindowMode
{
    Windowed,
    Fullscreen,
    BorderlessFullscreen
}

public enum GraphicsQualityPreset
{
    Low,
    Medium,
    High,
    Ultra,
    Custom
}

public enum QualityLevel
{
    Low,
    Medium,
    High,
    Ultra
}

public enum AntiAliasingMode
{
    Off,
    Fxaa,
    Msaa2X,
    Msaa4X,
    Msaa8X
}

public sealed record ScreenResolution(int Width, int Height)
{
    public ScreenResolution Normalize() => new(
        Math.Clamp(Width, 640, 7680),
        Math.Clamp(Height, 360, 4320));
}

public sealed record VideoSettings(
    ScreenResolution Resolution,
    WindowMode WindowMode,
    int RefreshRate,
    int FpsLimit,
    bool VSyncEnabled,
    GraphicsQualityPreset GraphicsQuality,
    QualityLevel TextureQuality,
    QualityLevel ShadowQuality,
    QualityLevel EffectQuality,
    QualityLevel ParticleDensity,
    AntiAliasingMode AntiAliasing,
    int BrightnessPercent,
    int UserInterfaceScalePercent)
{
    public static VideoSettings Default { get; } = new(
        Resolution: new ScreenResolution(1280, 720),
        WindowMode: WindowMode.Windowed,
        RefreshRate: 60,
        FpsLimit: 60,
        VSyncEnabled: true,
        GraphicsQuality: GraphicsQualityPreset.High,
        TextureQuality: QualityLevel.High,
        ShadowQuality: QualityLevel.High,
        EffectQuality: QualityLevel.High,
        ParticleDensity: QualityLevel.High,
        AntiAliasing: AntiAliasingMode.Msaa2X,
        BrightnessPercent: 100,
        UserInterfaceScalePercent: 100);

    public bool IsFullscreen => WindowMode is WindowMode.Fullscreen or WindowMode.BorderlessFullscreen;

    public bool IsBorderless => WindowMode == WindowMode.BorderlessFullscreen;

    public VideoSettings Normalize() => this with
    {
        Resolution = (Resolution ?? Default.Resolution).Normalize(),
        WindowMode = NormalizeEnum(WindowMode, Default.WindowMode),
        RefreshRate = Math.Clamp(RefreshRate, 30, 360),
        FpsLimit = FpsLimit == 0 ? 0 : Math.Clamp(FpsLimit, 30, 500),
        GraphicsQuality = NormalizeEnum(GraphicsQuality, Default.GraphicsQuality),
        TextureQuality = NormalizeEnum(TextureQuality, Default.TextureQuality),
        ShadowQuality = NormalizeEnum(ShadowQuality, Default.ShadowQuality),
        EffectQuality = NormalizeEnum(EffectQuality, Default.EffectQuality),
        ParticleDensity = NormalizeEnum(ParticleDensity, Default.ParticleDensity),
        AntiAliasing = NormalizeEnum(AntiAliasing, Default.AntiAliasing),
        BrightnessPercent = Math.Clamp(BrightnessPercent, 25, 200),
        UserInterfaceScalePercent = Math.Clamp(UserInterfaceScalePercent, 75, 150)
    };

    public VideoSettings ApplyPreset(GraphicsQualityPreset preset)
    {
        if (!Enum.IsDefined(preset))
        {
            throw new ArgumentOutOfRangeException(nameof(preset), preset, "The quality preset is invalid.");
        }

        return preset switch
        {
            GraphicsQualityPreset.Low => WithQuality(preset, QualityLevel.Low, AntiAliasingMode.Off),
            GraphicsQualityPreset.Medium => WithQuality(preset, QualityLevel.Medium, AntiAliasingMode.Fxaa),
            GraphicsQualityPreset.High => WithQuality(preset, QualityLevel.High, AntiAliasingMode.Msaa2X),
            GraphicsQualityPreset.Ultra => WithQuality(preset, QualityLevel.Ultra, AntiAliasingMode.Msaa4X),
            GraphicsQualityPreset.Custom => this with { GraphicsQuality = preset },
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "The quality preset is invalid.")
        };
    }

    private VideoSettings WithQuality(
        GraphicsQualityPreset preset,
        QualityLevel quality,
        AntiAliasingMode antiAliasing) => this with
        {
            GraphicsQuality = preset,
            TextureQuality = quality,
            ShadowQuality = quality,
            EffectQuality = quality,
            ParticleDensity = quality,
            AntiAliasing = antiAliasing
        };

    private static TEnum NormalizeEnum<TEnum>(TEnum value, TEnum fallback)
        where TEnum : struct, Enum => Enum.IsDefined(value) ? value : fallback;
}

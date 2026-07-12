using SpaceFactory.Core.Settings;

namespace SpaceFactory.Presentation.Settings;

public static class GraphicsQualityRuntime
{
    public static QualityLevel TextureQuality { get; private set; } = QualityLevel.High;

    public static QualityLevel ShadowQuality { get; private set; } = QualityLevel.High;

    public static QualityLevel EffectQuality { get; private set; } = QualityLevel.High;

    public static QualityLevel ParticleDensity { get; private set; } = QualityLevel.High;

    public static int MiningParticleCount => ParticleDensity switch
    {
        QualityLevel.Low => 6,
        QualityLevel.Medium => 10,
        QualityLevel.High => 14,
        QualityLevel.Ultra => 22,
        _ => 14,
    };

    public static void Apply(VideoSettings settings)
    {
        TextureQuality = settings.TextureQuality;
        ShadowQuality = settings.ShadowQuality;
        EffectQuality = settings.EffectQuality;
        ParticleDensity = settings.ParticleDensity;
    }
}

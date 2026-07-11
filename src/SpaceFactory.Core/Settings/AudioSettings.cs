namespace SpaceFactory.Core.Settings;

public sealed record AudioSettings(
    int MasterVolume,
    int MusicVolume,
    int SoundEffectsVolume,
    int AmbientVolume,
    int UserInterfaceVolume,
    bool IsMuted,
    bool IsMusicMuted)
{
    public static AudioSettings Default { get; } = new(
        MasterVolume: 80,
        MusicVolume: 70,
        SoundEffectsVolume: 80,
        AmbientVolume: 65,
        UserInterfaceVolume: 75,
        IsMuted: false,
        IsMusicMuted: false);

    public AudioSettings Normalize() => this with
    {
        MasterVolume = ClampPercentage(MasterVolume),
        MusicVolume = ClampPercentage(MusicVolume),
        SoundEffectsVolume = ClampPercentage(SoundEffectsVolume),
        AmbientVolume = ClampPercentage(AmbientVolume),
        UserInterfaceVolume = ClampPercentage(UserInterfaceVolume)
    };

    private static int ClampPercentage(int value) => Math.Clamp(value, 0, 100);
}

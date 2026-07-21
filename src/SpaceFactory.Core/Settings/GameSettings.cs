namespace SpaceFactory.Core.Settings;

public sealed record GameSettings(
    int SchemaVersion,
    InputSettings Input,
    AudioSettings Audio,
    VideoSettings Video,
    bool HotbarMouseWheelEnabled = true)
{
    public const int CurrentVersion = 2;

    public static GameSettings CreateDefault() => new(
        CurrentVersion,
        InputSettings.CreateDefault(),
        AudioSettings.Default,
        VideoSettings.Default,
        HotbarMouseWheelEnabled: true);

    public GameSettings Normalize() => new(
        CurrentVersion,
        (Input ?? InputSettings.CreateDefault()).Normalize(),
        (Audio ?? AudioSettings.Default).Normalize(),
        (Video ?? VideoSettings.Default).Normalize(),
        SchemaVersion < 2 || HotbarMouseWheelEnabled);
}

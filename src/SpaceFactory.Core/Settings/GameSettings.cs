namespace SpaceFactory.Core.Settings;

public sealed record GameSettings(
    int SchemaVersion,
    InputSettings Input,
    AudioSettings Audio,
    VideoSettings Video)
{
    public const int CurrentVersion = 1;

    public static GameSettings CreateDefault() => new(
        CurrentVersion,
        InputSettings.CreateDefault(),
        AudioSettings.Default,
        VideoSettings.Default);

    public GameSettings Normalize() => new(
        CurrentVersion,
        (Input ?? InputSettings.CreateDefault()).Normalize(),
        (Audio ?? AudioSettings.Default).Normalize(),
        (Video ?? VideoSettings.Default).Normalize());
}

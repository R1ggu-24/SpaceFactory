using SpaceFactory.Core.Settings;
using System.Text.Json;

namespace SpaceFactory.Core.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void DefaultInputSettings_ContainAllActionsWithUniqueBindings()
    {
        var settings = InputSettings.CreateDefault();

        Assert.Equal(14, settings.Bindings.Count);
        Assert.Equal(Enum.GetValues<GameAction>().Length, settings.Bindings.Count);
        Assert.Empty(settings.FindConflicts());
    }

    [Fact]
    public void DefaultInputSettings_PreserveCoreInteractionBindings()
    {
        var settings = InputSettings.CreateDefault();

        Assert.Equal(InputBinding.Key(InputBindingCodes.E), settings.GetBinding(GameAction.EnterShip));
        Assert.Equal(InputBinding.Key(InputBindingCodes.F), settings.GetBinding(GameAction.ExitShip));
        Assert.Equal(
            InputBinding.MouseButton(InputBindingCodes.LeftMouseButton),
            settings.GetBinding(GameAction.UseMiningTool));
    }

    [Fact]
    public void WithBinding_ReturnsChangedCopyAndDetectsConflict()
    {
        var original = InputSettings.CreateDefault();
        var changed = original.WithBinding(
            GameAction.Interact,
            original.GetBinding(GameAction.EnterShip));

        var conflict = Assert.Single(changed.FindConflicts());
        Assert.Equal(InputBinding.Key(InputBindingCodes.E), conflict.Binding);
        Assert.Equal([GameAction.Interact, GameAction.EnterShip], conflict.Actions);
        Assert.Equal(InputBinding.Key(InputBindingCodes.Q), original.GetBinding(GameAction.Interact));
    }

    [Fact]
    public void FindConflicts_IncludesPermanentArrowFallbacks()
    {
        var settings = InputSettings.CreateDefault().WithBinding(
            GameAction.Interact,
            InputBinding.Key(InputBindingCodes.Up));

        var conflict = Assert.Single(settings.FindConflicts());

        Assert.Equal(InputBinding.Key(InputBindingCodes.Up), conflict.Binding);
        Assert.Equal([GameAction.MoveUp, GameAction.Interact], conflict.Actions);
    }

    [Fact]
    public void NormalizeInputSettings_FillsMissingActionsWithDefaults()
    {
        var partial = new InputSettings(new Dictionary<GameAction, InputBinding>
        {
            [GameAction.MoveUp] = InputBinding.Key(InputBindingCodes.R)
        });

        var normalized = partial.Normalize();

        Assert.Equal(14, normalized.Bindings.Count);
        Assert.Equal(InputBinding.Key(InputBindingCodes.R), normalized.GetBinding(GameAction.MoveUp));
        Assert.Equal(InputBinding.Key(InputBindingCodes.E), normalized.GetBinding(GameAction.EnterShip));
    }

    [Fact]
    public void NormalizeAudioSettings_ClampsEveryVolumePercentage()
    {
        var settings = new AudioSettings(-1, 101, 40, 400, -200, true, true);

        var normalized = settings.Normalize();

        Assert.Equal(0, normalized.MasterVolume);
        Assert.Equal(100, normalized.MusicVolume);
        Assert.Equal(40, normalized.SoundEffectsVolume);
        Assert.Equal(100, normalized.AmbientVolume);
        Assert.Equal(0, normalized.UserInterfaceVolume);
        Assert.True(normalized.IsMuted);
        Assert.True(normalized.IsMusicMuted);
    }

    [Fact]
    public void NormalizeVideoSettings_ClampsRangesAndRepairsEnums()
    {
        var settings = VideoSettings.Default with
        {
            Resolution = new ScreenResolution(200, 9000),
            WindowMode = (WindowMode)999,
            RefreshRate = 5,
            FpsLimit = 900,
            GraphicsQuality = (GraphicsQualityPreset)999,
            BrightnessPercent = 5,
            UserInterfaceScalePercent = 900
        };

        var normalized = settings.Normalize();

        Assert.Equal(new ScreenResolution(640, 4320), normalized.Resolution);
        Assert.Equal(WindowMode.Windowed, normalized.WindowMode);
        Assert.Equal(30, normalized.RefreshRate);
        Assert.Equal(500, normalized.FpsLimit);
        Assert.Equal(GraphicsQualityPreset.High, normalized.GraphicsQuality);
        Assert.Equal(25, normalized.BrightnessPercent);
        Assert.Equal(150, normalized.UserInterfaceScalePercent);
    }

    [Fact]
    public void NormalizeVideoSettings_PreservesUnlimitedFps()
    {
        var normalized = (VideoSettings.Default with { FpsLimit = 0 }).Normalize();

        Assert.Equal(0, normalized.FpsLimit);
    }

    [Theory]
    [InlineData(GraphicsQualityPreset.Low, QualityLevel.Low, AntiAliasingMode.Off)]
    [InlineData(GraphicsQualityPreset.Medium, QualityLevel.Medium, AntiAliasingMode.Fxaa)]
    [InlineData(GraphicsQualityPreset.High, QualityLevel.High, AntiAliasingMode.Msaa2X)]
    [InlineData(GraphicsQualityPreset.Ultra, QualityLevel.Ultra, AntiAliasingMode.Msaa4X)]
    public void ApplyPreset_UpdatesEveryGraphicsQuality(
        GraphicsQualityPreset preset,
        QualityLevel quality,
        AntiAliasingMode antiAliasing)
    {
        var changed = VideoSettings.Default.ApplyPreset(preset);

        Assert.Equal(preset, changed.GraphicsQuality);
        Assert.Equal(quality, changed.TextureQuality);
        Assert.Equal(quality, changed.ShadowQuality);
        Assert.Equal(quality, changed.EffectQuality);
        Assert.Equal(quality, changed.ParticleDensity);
        Assert.Equal(antiAliasing, changed.AntiAliasing);
    }

    [Fact]
    public void NormalizeGameSettings_UpdatesSchemaAndNestedSettings()
    {
        var settings = new GameSettings(
            0,
            InputSettings.CreateDefault(),
            AudioSettings.Default with { MasterVolume = 150 },
            VideoSettings.Default with { RefreshRate = 1 });

        var normalized = settings.Normalize();

        Assert.Equal(GameSettings.CurrentVersion, normalized.SchemaVersion);
        Assert.Equal(100, normalized.Audio.MasterVolume);
        Assert.Equal(30, normalized.Video.RefreshRate);
    }

    [Fact]
    public void GameSettings_RoundTripThroughJson_PreservesBindingsAndOptions()
    {
        var original = GameSettings.CreateDefault() with
        {
            Input = InputSettings.CreateDefault().WithBinding(
                GameAction.Interact,
                InputBinding.MouseButton(2)),
            Audio = AudioSettings.Default with { MusicVolume = 37 },
            Video = VideoSettings.Default with { WindowMode = WindowMode.BorderlessFullscreen }
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<GameSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(InputBinding.MouseButton(2), restored.Input.GetBinding(GameAction.Interact));
        Assert.Equal(37, restored.Audio.MusicVolume);
        Assert.Equal(WindowMode.BorderlessFullscreen, restored.Video.WindowMode);
    }
}

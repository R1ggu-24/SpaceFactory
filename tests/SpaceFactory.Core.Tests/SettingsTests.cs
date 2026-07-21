using SpaceFactory.Core.Settings;
using System.Text.Json;

namespace SpaceFactory.Core.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void DefaultInputSettings_ContainAllActionsWithUniqueBindings()
    {
        var settings = InputSettings.CreateDefault();

        Assert.Equal(InputActionCatalog.All.Count, settings.Bindings.Count);
        Assert.Empty(settings.FindConflicts());
    }

    [Fact]
    public void DefaultInputSettings_PreserveCoreInteractionBindings()
    {
        var settings = InputSettings.CreateDefault();

        Assert.Equal(InputBinding.Key(InputBindingCodes.E), settings.GetBinding(GameAction.ShipInteraction));
        Assert.Equal(InputBinding.Key(InputBindingCodes.Shift), settings.GetBinding(GameAction.ShipBoost));
        Assert.Equal(InputBinding.Key(InputBindingCodes.H), settings.GetBinding(GameAction.ShipDocking));
        Assert.Equal(InputBinding.Key(InputBindingCodes.B), settings.GetBinding(GameAction.OpenBuildMenu));
        Assert.Equal(
            InputBinding.MouseButton(InputBindingCodes.LeftMouseButton),
            settings.GetBinding(GameAction.UseMiningTool));
        Assert.Equal(
            InputBinding.MouseButton(InputBindingCodes.MiddleMouseButton),
            settings.GetBinding(GameAction.ActivateHandSlot));
        Assert.Equal(InputBinding.Key(InputBindingCodes.Up), settings.GetBinding(GameAction.PreviousTool));
        Assert.Equal(InputBinding.Key(InputBindingCodes.Down), settings.GetBinding(GameAction.NextTool));
        Assert.Equal(
            [InputBindingCodes.One, InputBindingCodes.Two, InputBindingCodes.Three,
                InputBindingCodes.Four, InputBindingCodes.Five, InputBindingCodes.Six],
            InputActionCatalog.HotbarActions
                .Select(action => settings.GetBinding(action).Code)
                .ToArray());
    }

    [Fact]
    public void HotbarActions_AreConfigurableAndMappedToCentralInputActions()
    {
        Assert.Equal(6, InputActionCatalog.HotbarActions.Count);
        Assert.Equal(
            ["hotbar_slot_1", "hotbar_slot_2", "hotbar_slot_3",
                "hotbar_slot_4", "hotbar_slot_5", "hotbar_slot_6"],
            InputActionCatalog.HotbarActions
                .Select(action => InputActionCatalog.Get(action).InputMapAction)
                .ToArray());

        var rebound = InputSettings.CreateDefault().WithBinding(
            GameAction.HotbarSlot6,
            InputBinding.Key(InputBindingCodes.F));

        Assert.Equal(InputBinding.Key(InputBindingCodes.F), rebound.GetBinding(GameAction.HotbarSlot6));
        Assert.Empty(rebound.FindConflicts());
    }

    [Fact]
    public void WithBinding_ReturnsChangedCopyAndDetectsConflict()
    {
        var original = InputSettings.CreateDefault();
        var changed = original.WithBinding(
            GameAction.Interact,
            original.GetBinding(GameAction.ShipInteraction));

        var conflict = Assert.Single(changed.FindConflicts());
        Assert.Equal(InputBinding.Key(InputBindingCodes.E), conflict.Binding);
        Assert.Equal([GameAction.Interact, GameAction.ShipInteraction], conflict.Actions);
        Assert.Equal(InputBinding.Key(InputBindingCodes.Q), original.GetBinding(GameAction.Interact));
    }

    [Fact]
    public void ToolCycleActions_HaveNoHiddenPermanentArrowAlternatives()
    {
        Assert.Empty(InputActionCatalog.GetPermanentBindings(GameAction.PreviousTool));
        Assert.Empty(InputActionCatalog.GetPermanentBindings(GameAction.NextTool));
        Assert.Null(InputActionCatalog.FindPermanentBindingOwner(InputBinding.Key(InputBindingCodes.Left)));
        Assert.Null(InputActionCatalog.FindPermanentBindingOwner(InputBinding.Key(InputBindingCodes.Right)));
    }

    [Fact]
    public void NormalizeInputSettings_FillsMissingActionsWithDefaults()
    {
        var partial = new InputSettings(new Dictionary<GameAction, InputBinding>
        {
            [GameAction.MoveUp] = InputBinding.Key(InputBindingCodes.R)
        });

        var normalized = partial.Normalize();

        Assert.Equal(InputActionCatalog.All.Count, normalized.Bindings.Count);
        Assert.Equal(InputBinding.Key(InputBindingCodes.R), normalized.GetBinding(GameAction.MoveUp));
        Assert.Equal(InputBinding.Key(InputBindingCodes.E), normalized.GetBinding(GameAction.ShipInteraction));
        Assert.Equal(InputBinding.Key(InputBindingCodes.Shift), normalized.GetBinding(GameAction.ShipBoost));
        Assert.Equal(InputBinding.Key(InputBindingCodes.H), normalized.GetBinding(GameAction.ShipDocking));
        Assert.Equal(InputBinding.Key(InputBindingCodes.B), normalized.GetBinding(GameAction.OpenBuildMenu));
    }

    [Fact]
    public void ShipDockingAction_UsesConfigurableGodotInputMapEntry()
    {
        var definition = InputActionCatalog.Get(GameAction.ShipDocking);

        Assert.Equal("ship_docking", definition.InputMapAction);
        Assert.Equal("Am Kometen befestigen / Vom Kometen lösen", definition.DisplayName);
        Assert.Equal(InputBinding.Key(InputBindingCodes.H), definition.DefaultBinding);

        var rebound = InputSettings.CreateDefault().WithBinding(
            GameAction.ShipDocking,
            InputBinding.Key(InputBindingCodes.F));

        Assert.Equal(InputBinding.Key(InputBindingCodes.F), rebound.GetBinding(GameAction.ShipDocking));
        Assert.Empty(rebound.FindConflicts());
    }

    [Fact]
    public void OpenBuildMenuAction_UsesConfigurableConflictFreeGodotInputMapEntry()
    {
        var definition = InputActionCatalog.Get(GameAction.OpenBuildMenu);

        Assert.Equal("build_menu", definition.InputMapAction);
        Assert.Equal("Baumenü öffnen", definition.DisplayName);
        Assert.Equal(InputBinding.Key(InputBindingCodes.B), definition.DefaultBinding);
        Assert.DoesNotContain(InputActionCatalog.All, item => item.Action == GameAction.Build);

        var rebound = InputSettings.CreateDefault().WithBinding(
            GameAction.OpenBuildMenu,
            InputBinding.Key(InputBindingCodes.F));

        Assert.Equal(InputBinding.Key(InputBindingCodes.F), rebound.GetBinding(GameAction.OpenBuildMenu));
        Assert.Empty(rebound.FindConflicts());
    }

    [Fact]
    public void NormalizeInputSettings_MigratesLegacyEnterShipBindingAndDropsLegacyActions()
    {
        var legacy = new InputSettings(new Dictionary<GameAction, InputBinding>
        {
            [GameAction.EnterShip] = InputBinding.Key(InputBindingCodes.R),
            [GameAction.ExitShip] = InputBinding.Key(InputBindingCodes.F),
        });

        var normalized = legacy.Normalize();

        Assert.Equal(InputBinding.Key(InputBindingCodes.R), normalized.GetBinding(GameAction.ShipInteraction));
        Assert.False(normalized.Bindings.ContainsKey(GameAction.EnterShip));
        Assert.False(normalized.Bindings.ContainsKey(GameAction.ExitShip));
    }

    [Fact]
    public void NormalizeInputSettings_MigratesLegacyBuildBindingAndDropsLegacyAction()
    {
        var legacy = new InputSettings(new Dictionary<GameAction, InputBinding>
        {
            [GameAction.Build] = InputBinding.Key(InputBindingCodes.F),
        });

        var normalized = legacy.Normalize();

        Assert.Equal(InputBinding.Key(InputBindingCodes.F), normalized.GetBinding(GameAction.OpenBuildMenu));
        Assert.False(normalized.Bindings.ContainsKey(GameAction.Build));
        Assert.Empty(normalized.FindConflicts());
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
        Assert.True(normalized.HotbarMouseWheelEnabled);
    }

    [Fact]
    public void NormalizeGameSettings_MigratesMouseWheelOptionAndPreservesExplicitChoice()
    {
        var legacy = new GameSettings(
            1,
            InputSettings.CreateDefault(),
            AudioSettings.Default,
            VideoSettings.Default,
            HotbarMouseWheelEnabled: false);
        var current = GameSettings.CreateDefault() with { HotbarMouseWheelEnabled = false };

        Assert.True(legacy.Normalize().HotbarMouseWheelEnabled);
        Assert.False(current.Normalize().HotbarMouseWheelEnabled);
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
            Video = VideoSettings.Default with { WindowMode = WindowMode.BorderlessFullscreen },
            HotbarMouseWheelEnabled = false,
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<GameSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(InputBinding.MouseButton(2), restored.Input.GetBinding(GameAction.Interact));
        Assert.Equal(37, restored.Audio.MusicVolume);
        Assert.Equal(WindowMode.BorderlessFullscreen, restored.Video.WindowMode);
        Assert.False(restored.HotbarMouseWheelEnabled);
    }
}

using Godot;
using SpaceFactory.Core.Settings;
using CoreWindowMode = SpaceFactory.Core.Settings.WindowMode;

namespace SpaceFactory.Presentation.Settings;

public sealed class SettingsRuntimeApplier
{
    private static readonly string[] RequiredAudioBuses = ["Music", "SFX", "Ambience", "UI"];
    private readonly Window _window;
    private readonly CanvasModulate _brightnessModulate;

    public SettingsRuntimeApplier(Window window, CanvasModulate brightnessModulate)
    {
        _window = window;
        _brightnessModulate = brightnessModulate;
    }

    public void ApplyAll(GameSettings settings)
    {
        ApplyInput(settings.Input);
        ApplyAudio(settings.Audio);
        ApplyVideo(settings.Video);
    }

    public void ApplyInput(InputSettings settings)
    {
        foreach (var definition in InputActionCatalog.All)
        {
            var action = new StringName(definition.InputMapAction);
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action);
            }

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, CreateInputEvent(settings.GetBinding(definition.Action)));
            foreach (var permanentBinding in InputActionCatalog.GetPermanentBindings(definition.Action))
            {
                InputMap.ActionAddEvent(action, CreateInputEvent(permanentBinding));
            }
        }
    }

    public void ApplyAudio(AudioSettings settings)
    {
        EnsureAudioBuses();
        SetBus("Master", settings.MasterVolume, settings.IsMuted);
        SetBus("Music", settings.MusicVolume, settings.IsMusicMuted);
        SetBus("SFX", settings.SoundEffectsVolume, false);
        SetBus("Ambience", settings.AmbientVolume, false);
        SetBus("UI", settings.UserInterfaceVolume, false);
    }

    public void ApplyVideo(VideoSettings settings)
    {
        var normalized = settings.Normalize();
        Engine.MaxFps = normalized.FpsLimit;
        _window.ContentScaleFactor = normalized.UserInterfaceScalePercent / 100.0f;
        var brightness = normalized.BrightnessPercent / 100.0f;
        _brightnessModulate.Color = new Color(brightness, brightness, brightness, 1);
        GraphicsQualityRuntime.Apply(normalized);
        _window.GetTree().CallGroup("quality_sensitive_visuals", CanvasItem.MethodName.QueueRedraw);
        ApplyAntiAliasing(normalized.AntiAliasing);

        if (IsHeadless())
        {
            return;
        }

        DisplayServer.WindowSetVsyncMode(normalized.VSyncEnabled
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        ApplyWindowMode(normalized.WindowMode, normalized.Resolution);
    }

    private static InputEvent CreateInputEvent(InputBinding binding) => binding.Kind switch
    {
        InputBindingKind.Key => new InputEventKey { PhysicalKeycode = (Key)binding.Code },
        InputBindingKind.MouseButton => new InputEventMouseButton { ButtonIndex = (MouseButton)binding.Code },
        _ => throw new ArgumentOutOfRangeException(nameof(binding)),
    };

    private static void EnsureAudioBuses()
    {
        foreach (var busName in RequiredAudioBuses)
        {
            if (AudioServer.GetBusIndex(busName) >= 0)
            {
                continue;
            }

            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, busName);
        }
    }

    private static void SetBus(string name, int volume, bool muted)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index < 0)
        {
            return;
        }

        var linear = Mathf.Max(volume / 100.0f, 0.0001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(index, muted || volume == 0);
    }

    private void ApplyAntiAliasing(AntiAliasingMode mode)
    {
        _window.ScreenSpaceAA = mode == AntiAliasingMode.Fxaa
            ? Viewport.ScreenSpaceAAEnum.Fxaa
            : Viewport.ScreenSpaceAAEnum.Disabled;
        _window.Msaa2D = mode switch
        {
            AntiAliasingMode.Msaa2X => Viewport.Msaa.Msaa2X,
            AntiAliasingMode.Msaa4X => Viewport.Msaa.Msaa4X,
            AntiAliasingMode.Msaa8X => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };
    }

    private static void ApplyWindowMode(CoreWindowMode mode, ScreenResolution resolution)
    {
        switch (mode)
        {
            case CoreWindowMode.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetSize(new Vector2I(resolution.Width, resolution.Height));
                break;
            case CoreWindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetSize(new Vector2I(resolution.Width, resolution.Height));
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case CoreWindowMode.BorderlessFullscreen:
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown window mode.");
        }
    }

    private static bool IsHeadless() =>
        string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase);
}

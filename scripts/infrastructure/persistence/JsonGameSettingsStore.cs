using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SpaceFactory.Application.Settings;
using SpaceFactory.Core.Settings;

namespace SpaceFactory.Infrastructure.Persistence;

public sealed class JsonGameSettingsStore : IGameSettingsStore
{
    private const string SavePath = "user://settings.json";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public GameSettings Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath))
        {
            return GameSettings.CreateDefault();
        }

        try
        {
            var json = Godot.FileAccess.GetFileAsString(SavePath);
            var document = JsonSerializer.Deserialize<SettingsDocument>(json, JsonOptions);
            if (document is null)
            {
                return GameSettings.CreateDefault();
            }

            var input = document.InputBindings is null
                ? InputSettings.CreateDefault()
                : new InputSettings(document.InputBindings);
            return new GameSettings(
                document.SchemaVersion,
                input,
                document.Audio ?? AudioSettings.Default,
                document.Video ?? VideoSettings.Default).Normalize();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Settings could not be loaded; defaults are used: {exception.Message}");
            return GameSettings.CreateDefault();
        }
    }

    public void Save(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        var document = new SettingsDocument(
            normalized.SchemaVersion,
            normalized.Input.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value),
            normalized.Audio,
            normalized.Video);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"Could not save settings to '{SavePath}'.");
            return;
        }

        file.StoreString(json);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SettingsDocument(
        int SchemaVersion,
        Dictionary<GameAction, InputBinding>? InputBindings,
        AudioSettings? Audio,
        VideoSettings? Video);
}

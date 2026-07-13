using Godot;
using SpaceFactory.Application.Factory;

namespace SpaceFactory.Infrastructure.Persistence;

public sealed class JsonFactoryStateStore : IFactoryStateStore
{
    public const string SavePath = "user://factory_state.json";
    private const string SaveFileName = "factory_state.json";
    private const string TemporaryPath = "user://factory_state.json.tmp";
    private const string TemporaryFileName = "factory_state.json.tmp";
    private static readonly object FileLock = new();

    public FactoryStateData Load()
    {
        lock (FileLock)
        {
            return LoadCore();
        }
    }

    public bool Save(FactoryStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (FileLock)
        {
            return SaveCore(state);
        }
    }

    private static FactoryStateData LoadCore()
    {
        if (Godot.FileAccess.FileExists(SavePath))
        {
            if (TryLoadPath(SavePath, out var state, out var error))
            {
                RemoveStaleTemporaryFile();
                return state;
            }

            GD.PushWarning($"Factory state could not be loaded; recovery is attempted: {error}");
        }

        if (Godot.FileAccess.FileExists(TemporaryPath))
        {
            if (TryLoadPath(TemporaryPath, out var recovered, out var recoveryError))
            {
                if (!TryPromoteTemporaryFile(out var promotionError))
                {
                    GD.PushWarning(
                        "Recovered factory state from the temporary file, but could not promote it: " +
                        promotionError);
                }

                return recovered;
            }

            GD.PushWarning($"Temporary factory state is invalid and was ignored: {recoveryError}");
        }

        return FactoryStateData.CreateDefault();
    }

    private static bool SaveCore(FactoryStateData state)
    {
        var json = FactoryStateJsonCodec.Serialize(state);
        if (!TryWriteTemporaryFile(json, out var writeError))
        {
            GD.PushError($"Could not write factory state temporary file: {writeError}");
            return false;
        }

        if (!TryPromoteTemporaryFile(out var promotionError))
        {
            GD.PushError(
                $"Could not atomically replace '{SavePath}': {promotionError}. " +
                "The previous save remains intact and the temporary file is kept for recovery.");
            return false;
        }

        return true;
    }

    private static bool TryLoadPath(
        string path,
        out FactoryStateData state,
        out string error)
    {
        try
        {
            var json = Godot.FileAccess.GetFileAsString(path);
            return FactoryStateJsonCodec.TryDeserialize(json, out state, out error);
        }
        catch (Exception exception)
        {
            state = FactoryStateData.CreateDefault();
            error = exception.Message;
            return false;
        }
    }

    private static bool TryWriteTemporaryFile(string json, out string error)
    {
        try
        {
            using var file = Godot.FileAccess.Open(TemporaryPath, Godot.FileAccess.ModeFlags.Write);
            if (file is null)
            {
                error = $"Godot error {Godot.FileAccess.GetOpenError()}.";
                return false;
            }

            file.StoreString(json);
            file.Flush();
            var fileError = file.GetError();
            if (fileError != Error.Ok)
            {
                error = $"Godot error {fileError}.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryPromoteTemporaryFile(out string error)
    {
        using var directory = DirAccess.Open("user://");
        if (directory is null)
        {
            error = $"Could not open user directory (Godot error {DirAccess.GetOpenError()}).";
            return false;
        }

        var renameError = directory.Rename(TemporaryFileName, SaveFileName);
        if (renameError != Error.Ok)
        {
            error = $"Godot error {renameError}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void RemoveStaleTemporaryFile()
    {
        using var directory = DirAccess.Open("user://");
        if (directory is not null && directory.FileExists(TemporaryFileName))
        {
            _ = directory.Remove(TemporaryFileName);
        }
    }
}

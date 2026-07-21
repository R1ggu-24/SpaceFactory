using System.Text.Json;
using Godot;
using SpaceFactory.Application.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Infrastructure.Persistence;

public sealed class JsonResourceStateStore : IResourceStateStore
{
    private const string LegacySavePath = "user://resource_deposits.json";
    private const string LegacySaveFileName = "resource_deposits.json";
    public const string DefaultWorldScope = "default";
    private static readonly object FileLock = new();
#if DEBUG
    private static bool _finiteOreStonePersistenceSmokeCompleted;
#endif
    private readonly StoragePaths _paths;
    private readonly Dictionary<string, ResourceDepositState> _changes;

    /// <summary>
    /// Creates a world-scoped resource state store. The default scope represents the existing
    /// single-save world; future save slots can pass their stable world/save identifier without
    /// changing the persistence format.
    /// </summary>
    public JsonResourceStateStore(string worldScope = DefaultWorldScope)
    {
#if DEBUG
        RunFiniteOreStonePersistenceSmokeTest();
#endif
        _paths = CreateStoragePaths(worldScope);
        lock (FileLock)
        {
            _changes = LoadChanges(
                _paths,
                migrateLegacyState: string.Equals(worldScope, DefaultWorldScope, StringComparison.Ordinal));
        }
    }

    public int GetRemainingAmount(ResourceDepositDefinition deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        // Version-2 sources are infinite. Their stable IDs intentionally differ from legacy
        // deposits, and an old exhaustion delta must never hide or drain them.
        if (deposit.IsInfinite)
        {
            return Math.Max(1, deposit.OriginalAmount);
        }

        lock (FileLock)
        {
            return _changes.TryGetValue(deposit.Id, out var state)
                ? Math.Clamp(state.RemainingAmount, 0, deposit.OriginalAmount)
                : deposit.OriginalAmount;
        }
    }

    public void SetRemainingAmount(
        ResourceDepositDefinition deposit,
        int remainingAmount,
        int sectorX,
        int sectorY)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        if (deposit.IsInfinite)
        {
            return;
        }

        if (remainingAmount < 0 || remainingAmount > deposit.OriginalAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingAmount));
        }

        lock (FileLock)
        {
            _changes[deposit.Id] = new ResourceDepositState(
                deposit.Id,
                deposit.CometId,
                sectorX,
                sectorY,
                deposit.VisualSeed,
                deposit.NormalizedPosition.X,
                deposit.NormalizedPosition.Y,
                deposit.OriginalAmount,
                remainingAmount,
                remainingAmount == 0,
                deposit.ResourceId.Value,
                deposit.Kind);
            if (!TrySaveChanges(_paths, _changes, out var error))
            {
                GD.PushError(
                    $"Could not atomically write resource state '{_paths.SavePath}': {error}. " +
                    "The previous save remains intact and the temporary file is kept for recovery.");
            }
        }
    }

    private static Dictionary<string, ResourceDepositState> LoadChanges(
        StoragePaths paths,
        bool migrateLegacyState)
    {
        if (Godot.FileAccess.FileExists(paths.SavePath))
        {
            if (TryLoadPath(paths.SavePath, out var states, out var error))
            {
                RemoveFile(paths.TemporaryFileName);
                return states;
            }

            GD.PushWarning($"Resource state could not be loaded; recovery is attempted: {error}");
        }

        if (Godot.FileAccess.FileExists(paths.TemporaryPath))
        {
            if (TryLoadPath(paths.TemporaryPath, out var recovered, out var recoveryError))
            {
                if (!TryPromoteTemporaryFile(paths, out var promotionError))
                {
                    GD.PushWarning(
                        "Recovered resource state from the temporary file, but could not promote it: " +
                        promotionError);
                }

                return recovered;
            }

            GD.PushWarning($"Temporary resource state is invalid and was ignored: {recoveryError}");
        }

        if (migrateLegacyState && Godot.FileAccess.FileExists(LegacySavePath))
        {
            if (!TryLoadPath(LegacySavePath, out var legacyStates, out var legacyError))
            {
                GD.PushWarning($"Legacy resource state could not be migrated: {legacyError}");
                return [];
            }

            if (TrySaveChanges(paths, legacyStates, out var migrationError))
            {
                RemoveFile(LegacySaveFileName);
            }
            else
            {
                // Keep the legacy source untouched. It remains a lossless recovery source for the
                // next launch even if the scoped migration could not be promoted this time.
                GD.PushWarning(
                    $"Legacy resource state is active but could not be promoted to the scoped " +
                    $"save: {migrationError}");
            }

            return legacyStates;
        }

        return [];
    }

    private static bool TryLoadPath(
        string path,
        out Dictionary<string, ResourceDepositState> states,
        out string error)
    {
        try
        {
            var json = Godot.FileAccess.GetFileAsString(path);
            var restored = JsonSerializer.Deserialize<List<ResourceDepositState>>(json) ?? [];
            states = restored.ToDictionary(state => state.DepositId, StringComparer.Ordinal);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or ArgumentException)
        {
            states = new Dictionary<string, ResourceDepositState>(StringComparer.Ordinal);
            error = exception.Message;
            return false;
        }
    }

    private static bool TrySaveChanges(
        StoragePaths paths,
        IReadOnlyDictionary<string, ResourceDepositState> changes,
        out string error)
    {
        var orderedStates = changes.Values
            .OrderBy(state => state.DepositId, StringComparer.Ordinal)
            .ToArray();
        var json = JsonSerializer.Serialize(
            orderedStates,
            new JsonSerializerOptions { WriteIndented = true });
        if (!TryWriteTemporaryFile(paths.TemporaryPath, json, out error))
        {
            return false;
        }

        return TryPromoteTemporaryFile(paths, out error);
    }

    private static bool TryWriteTemporaryFile(string temporaryPath, string json, out string error)
    {
        try
        {
            using var file = Godot.FileAccess.Open(temporaryPath, Godot.FileAccess.ModeFlags.Write);
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

    private static bool TryPromoteTemporaryFile(StoragePaths paths, out string error)
    {
        using var directory = DirAccess.Open("user://");
        if (directory is null)
        {
            error = $"Could not open user directory (Godot error {DirAccess.GetOpenError()}).";
            return false;
        }

        var renameError = directory.Rename(paths.TemporaryFileName, paths.SaveFileName);
        if (renameError != Error.Ok)
        {
            error = $"Godot error {renameError}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static StoragePaths CreateStoragePaths(string worldScope)
    {
        if (string.IsNullOrWhiteSpace(worldScope) ||
            worldScope.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "A resource-state world scope may contain only ASCII letters, numbers, '-' and '_'.",
                nameof(worldScope));
        }

        var saveFileName = $"resource_deposits.{worldScope}.json";
        var temporaryFileName = $"{saveFileName}.tmp";
        return new StoragePaths(
            $"user://{saveFileName}",
            $"user://{temporaryFileName}",
            saveFileName,
            temporaryFileName);
    }

    private static void RemoveFile(string fileName)
    {
        using var directory = DirAccess.Open("user://");
        if (directory is not null && directory.FileExists(fileName))
        {
            var error = directory.Remove(fileName);
            if (error != Error.Ok)
            {
                GD.PushWarning($"Could not remove stale resource-state file '{fileName}': {error}.");
            }
        }
    }

#if DEBUG
    private static void RunFiniteOreStonePersistenceSmokeTest()
    {
        if (_finiteOreStonePersistenceSmokeCompleted ||
            !(OS.HasFeature("headless") ||
              DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Set before touching the file helpers so a future constructor-based smoke extension
        // cannot recurse into itself.
        _finiteOreStonePersistenceSmokeCompleted = true;
        var expected = new ResourceDepositState(
            "comet:smoke:ore-stone:v1:0",
            "comet:smoke",
            2,
            -3,
            741_029_384,
            0.25,
            -0.31,
            7,
            4,
            false,
            "iron_ore",
            ResourceDepositKind.FiniteOreStone);
        var json = JsonSerializer.Serialize(new[] { expected });
        var restored = JsonSerializer.Deserialize<List<ResourceDepositState>>(json)?.Single();
        if (restored is null || restored != expected)
        {
            throw new InvalidOperationException("Finite ore-stone persistence round-trip failed.");
        }

        var legacyJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                DepositId = "legacy:deposit:0",
                CometId = "legacy:comet",
                SectorX = 0,
                SectorY = 0,
                Seed = 17UL,
                NormalizedPositionX = 0.2,
                NormalizedPositionY = 0.3,
                OriginalAmount = 12,
                RemainingAmount = 8,
                IsExhausted = false,
            },
        });
        var restoredLegacy = JsonSerializer.Deserialize<List<ResourceDepositState>>(legacyJson)?.Single();
        if (restoredLegacy is null ||
            restoredLegacy.Kind != ResourceDepositKind.LegacyDeposit ||
            restoredLegacy.ResourceId != string.Empty ||
            restoredLegacy.RemainingAmount != 8)
        {
            throw new InvalidOperationException("Legacy resource-state migration failed.");
        }

        var smokeScope = $"smoke-{Guid.NewGuid():N}";
        var paths = CreateStoragePaths(smokeScope);
        try
        {
            var changes = new Dictionary<string, ResourceDepositState>(StringComparer.Ordinal)
            {
                [expected.DepositId] = expected,
            };
            if (!TrySaveChanges(paths, changes, out var firstSaveError))
            {
                throw new InvalidOperationException(
                    $"Finite ore-stone atomic first write failed: {firstSaveError}");
            }

            var exhausted = expected with { RemainingAmount = 0, IsExhausted = true };
            changes[expected.DepositId] = exhausted;
            if (!TrySaveChanges(paths, changes, out var replacementError))
            {
                throw new InvalidOperationException(
                    $"Finite ore-stone atomic replacement failed: {replacementError}");
            }

            if (!TryLoadPath(paths.SavePath, out var restoredChanges, out var loadError) ||
                restoredChanges.GetValueOrDefault(expected.DepositId) != exhausted ||
                Godot.FileAccess.FileExists(paths.TemporaryPath))
            {
                throw new InvalidOperationException(
                    $"Finite ore-stone atomic replacement verification failed: {loadError}");
            }

            var otherPaths = CreateStoragePaths($"{smokeScope}-other");
            if (string.Equals(paths.SavePath, otherPaths.SavePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Resource-state world scopes are not isolated.");
            }
        }
        finally
        {
            RemoveFile(paths.SaveFileName);
            RemoveFile(paths.TemporaryFileName);
        }

        GD.Print(
            "FINITE_ORE_STONE_PERSISTENCE_SMOKE_OK: scoped atomic replace, id, resource, " +
            "position, hits, exhausted state, legacy migration");
    }
#endif

    private sealed record StoragePaths(
        string SavePath,
        string TemporaryPath,
        string SaveFileName,
        string TemporaryFileName);

    private sealed record ResourceDepositState(
        string DepositId,
        string CometId,
        int SectorX,
        int SectorY,
        ulong Seed,
        double NormalizedPositionX,
        double NormalizedPositionY,
        int OriginalAmount,
        int RemainingAmount,
        bool IsExhausted,
        string ResourceId = "",
        ResourceDepositKind Kind = ResourceDepositKind.LegacyDeposit);
}

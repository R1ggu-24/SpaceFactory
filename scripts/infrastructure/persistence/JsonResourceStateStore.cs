using System.Text.Json;
using Godot;
using SpaceFactory.Application.Exploration;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Infrastructure.Persistence;

public sealed class JsonResourceStateStore : IResourceStateStore
{
    private const string SavePath = "user://resource_deposits.json";
    private readonly Dictionary<string, ResourceDepositState> _changes;

    public JsonResourceStateStore()
    {
        _changes = LoadChanges();
    }

    public int GetRemainingAmount(ResourceDepositDefinition deposit) =>
        _changes.TryGetValue(deposit.Id, out var state) ? state.RemainingAmount : deposit.OriginalAmount;

    public void SetRemainingAmount(
        ResourceDepositDefinition deposit,
        int remainingAmount,
        int sectorX,
        int sectorY)
    {
        if (remainingAmount < 0 || remainingAmount > deposit.OriginalAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingAmount));
        }

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
            remainingAmount == 0);
        SaveChanges();
    }

    private static Dictionary<string, ResourceDepositState> LoadChanges()
    {
        if (!Godot.FileAccess.FileExists(SavePath))
        {
            return [];
        }

        try
        {
            var json = Godot.FileAccess.GetFileAsString(SavePath);
            var states = JsonSerializer.Deserialize<List<ResourceDepositState>>(json) ?? [];
            return states.ToDictionary(state => state.DepositId);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Resource state could not be loaded: {exception.Message}");
            return [];
        }
    }

    private void SaveChanges()
    {
        var json = JsonSerializer.Serialize(_changes.Values, new JsonSerializerOptions { WriteIndented = true });
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"Could not write resource state to '{SavePath}'.");
            return;
        }

        file.StoreString(json);
    }

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
        bool IsExhausted);
}

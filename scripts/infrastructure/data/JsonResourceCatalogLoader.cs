using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Resources;

namespace SpaceFactory.Infrastructure.Data;

public sealed class JsonResourceCatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public IReadOnlyList<ResourceDefinition> Load(string resourcePath)
    {
        var json = Godot.FileAccess.GetFileAsString(resourcePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Resource catalog '{resourcePath}' is empty or missing.");
        }

        var entries = JsonSerializer.Deserialize<List<ResourceDefinitionDto>>(json, SerializerOptions) ?? [];
        var definitions = entries.Select(ToDefinition).ToArray();
        if (definitions.Length == 0)
        {
            throw new InvalidOperationException("The resource catalog contains no resources.");
        }

        if (definitions.Select(definition => definition.Id).Distinct().Count() != definitions.Length)
        {
            throw new InvalidOperationException("The resource catalog contains duplicate IDs.");
        }

        foreach (var definition in definitions)
        {
            definition.Validate();
        }

        return definitions;
    }

    private static ResourceDefinition ToDefinition(ResourceDefinitionDto dto) => new(
        new ItemId(dto.Id),
        dto.DisplayName,
        dto.Rarity,
        dto.SpawnWeight,
        dto.BaseColorHex,
        dto.VisualStyle,
        dto.MiningTimeSeconds,
        dto.MinimumAmount,
        dto.MaximumAmount,
        dto.Hardness,
        dto.ParticleEffectId,
        dto.InventoryIconPath,
        dto.MaximumStackSize,
        dto.Uses,
        dto.PossibleCometSizes,
        dto.MinimumDepositRadiusFactor,
        dto.MaximumDepositRadiusFactor,
        dto.SourceRadiusWorldUnits,
        dto.BaseExtractionUnitsPerMinute,
        dto.ManualYieldPerCycle,
        dto.IsInfiniteSource);

    private sealed class ResourceDefinitionDto
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public ResourceRarity Rarity { get; init; }
        public double SpawnWeight { get; init; }
        public string BaseColorHex { get; init; } = string.Empty;
        public ResourceVisualStyle VisualStyle { get; init; }
        public double MiningTimeSeconds { get; init; }
        public int MinimumAmount { get; init; }
        public int MaximumAmount { get; init; }
        public double Hardness { get; init; }
        public string ParticleEffectId { get; init; } = string.Empty;
        public string InventoryIconPath { get; init; } = string.Empty;
        public int MaximumStackSize { get; init; }
        public List<string> Uses { get; init; } = [];
        public List<AsteroidSize> PossibleCometSizes { get; init; } = [];
        public double MinimumDepositRadiusFactor { get; init; }
        public double MaximumDepositRadiusFactor { get; init; }
        public double SourceRadiusWorldUnits { get; init; } = MiningConfiguration.DefaultSourceRadiusWorldUnits;
        public double BaseExtractionUnitsPerMinute { get; init; } =
            MiningConfiguration.DefaultExtractionUnitsPerMinute;
        public int ManualYieldPerCycle { get; init; } = MiningConfiguration.DefaultManualYieldPerCycle;
        public bool IsInfiniteSource { get; init; } = true;
    }
}

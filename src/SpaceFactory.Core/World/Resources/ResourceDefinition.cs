using SpaceFactory.Core.Items;
using SpaceFactory.Core.World.Asteroids;

namespace SpaceFactory.Core.World.Resources;

public sealed record ResourceDefinition(
    ItemId Id,
    string DisplayName,
    ResourceRarity Rarity,
    double SpawnWeight,
    string BaseColorHex,
    ResourceVisualStyle VisualStyle,
    double MiningTimeSeconds,
    int MinimumAmount,
    int MaximumAmount,
    double Hardness,
    string ParticleEffectId,
    string InventoryIconPath,
    int MaximumStackSize,
    IReadOnlyList<string> Uses,
    IReadOnlyList<AsteroidSize> PossibleCometSizes,
    double MinimumDepositRadiusFactor,
    double MaximumDepositRadiusFactor,
    double SourceRadiusWorldUnits = MiningConfiguration.DefaultSourceRadiusWorldUnits,
    double BaseExtractionUnitsPerMinute = MiningConfiguration.DefaultExtractionUnitsPerMinute,
    int ManualYieldPerCycle = MiningConfiguration.DefaultManualYieldPerCycle,
    bool IsInfiniteSource = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || SpawnWeight <= 0 || MiningTimeSeconds <= 0 ||
            MinimumAmount <= 0 || MaximumAmount < MinimumAmount || Hardness <= 0 ||
            MaximumStackSize <= 0 || PossibleCometSizes.Count == 0 ||
            MinimumDepositRadiusFactor <= 0 || MaximumDepositRadiusFactor < MinimumDepositRadiusFactor ||
            !double.IsFinite(SourceRadiusWorldUnits) || SourceRadiusWorldUnits <= 0 ||
            SourceRadiusWorldUnits > MiningConfiguration.MaximumSourceRadiusWorldUnits ||
            !double.IsFinite(BaseExtractionUnitsPerMinute) || BaseExtractionUnitsPerMinute <= 0 ||
            ManualYieldPerCycle <= 0)
        {
            throw new ArgumentException($"Resource definition '{Id}' is invalid.");
        }

        if (BaseColorHex.Length != 7 || BaseColorHex[0] != '#')
        {
            throw new ArgumentException($"Resource '{Id}' needs a #RRGGBB color.");
        }
    }
}

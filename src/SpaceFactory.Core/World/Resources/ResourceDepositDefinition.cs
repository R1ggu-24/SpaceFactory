using SpaceFactory.Core.Common;
using SpaceFactory.Core.Items;

namespace SpaceFactory.Core.World.Resources;

public sealed record ResourceDepositDefinition(
    string Id,
    string CometId,
    ItemId ResourceId,
    WorldPosition NormalizedPosition,
    double RadiusFactor,
    int OriginalAmount,
    double MiningTimeSeconds,
    ulong VisualSeed);

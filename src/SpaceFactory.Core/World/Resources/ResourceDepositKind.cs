namespace SpaceFactory.Core.World.Resources;

/// <summary>
/// Identifies extraction semantics without inferring them from display names or IDs.
/// LegacyDeposit keeps old saves readable, InfiniteSource represents the normal miner-ready
/// source, and FiniteOreStone is depleted one deterministic hit at a time.
/// </summary>
public enum ResourceDepositKind
{
    LegacyDeposit,
    InfiniteSource,
    FiniteOreStone,
}

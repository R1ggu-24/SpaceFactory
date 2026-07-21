namespace SpaceFactory.Core.Research;

/// <summary>
/// Broad, presentation-independent technology branches. The category is metadata; dependency
/// edges remain the authoritative source for progression and can cross category boundaries.
/// </summary>
public enum ResearchCategory
{
    Fundamentals,
    Mining,
    Automation,
    Metallurgy,
    Electronics,
    Chemistry,
    Energy,
    Nuclear,
    SpaceTechnology,
}

/// <summary>
/// Long-term progression band used for balancing and technology-tree layout.
/// </summary>
public enum TechnologyTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5,
    Tier6 = 6,
    Tier7 = 7,
    Tier8 = 8,
    Tier9 = 9,
    Tier10 = 10,
}

/// <summary>
/// Stable identifier for a family of mutually substitutable or specialized recipes. Research
/// can unlock a family without coupling the research domain to one concrete recipe variant.
/// </summary>
public readonly record struct AlternativeRecipeGroupId
{
    public AlternativeRecipeGroupId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An alternative recipe group ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

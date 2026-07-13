namespace SpaceFactory.Core.Research;

public readonly record struct ResearchId
{
    public ResearchId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A research ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

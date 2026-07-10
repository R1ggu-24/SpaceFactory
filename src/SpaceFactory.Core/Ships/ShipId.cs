namespace SpaceFactory.Core.Ships;

public readonly record struct ShipId
{
    public ShipId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A ship ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

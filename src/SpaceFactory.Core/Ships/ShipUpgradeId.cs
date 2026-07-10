namespace SpaceFactory.Core.Ships;

public readonly record struct ShipUpgradeId
{
    public ShipUpgradeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An upgrade ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

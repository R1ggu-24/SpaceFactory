namespace SpaceFactory.Core.World.Generation;

public sealed record CometFieldSample(string? FieldId, double Density)
{
    public bool IsInsideField => FieldId is not null;

    public static CometFieldSample Empty { get; } = new(null, 0);
}

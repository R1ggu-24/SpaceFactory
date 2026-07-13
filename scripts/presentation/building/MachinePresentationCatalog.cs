using Godot;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Presentation.Building;

public sealed record MachinePresentationDefinition(
    MachineDefinitionId DefinitionId,
    Vector2 Footprint,
    MachineGlyph Glyph,
    Color BodyColor,
    Color AccentColor,
    float InteractionRadius)
{
    public void Validate()
    {
        if (Footprint.X <= 0 || Footprint.Y <= 0 || InteractionRadius <= 0)
        {
            throw new ArgumentException($"Machine presentation '{DefinitionId}' is invalid.");
        }
    }
}

public sealed class MachinePresentationCatalog
{
    public const float PlacementGridSize = 24;
    public const float RotationStepDegrees = 10;
    public const float RotationStepRadians = RotationStepDegrees * Mathf.Pi / 180f;
    public const float DefaultInteractionRadius = 150;

    private readonly IReadOnlyDictionary<MachineDefinitionId, MachinePresentationDefinition> _definitions;

    public MachinePresentationCatalog(IEnumerable<MachinePresentationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        foreach (var definition in materialized)
        {
            definition.Validate();
        }

        _definitions = materialized.ToDictionary(definition => definition.DefinitionId);
        if (_definitions.Count != materialized.Length)
        {
            throw new ArgumentException("Machine presentation IDs must be unique.", nameof(definitions));
        }
    }

    public static MachinePresentationCatalog Instance { get; } = new(CreateDefinitions());

    public IReadOnlyCollection<MachinePresentationDefinition> All => _definitions.Values.ToArray();

    public MachinePresentationDefinition Get(MachineDefinitionId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"No presentation exists for machine '{id}'.");

    public bool TryGet(MachineDefinitionId id, out MachinePresentationDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);

    private static IEnumerable<MachinePresentationDefinition> CreateDefinitions()
    {
        var processingBody = new Color(0.075f, 0.12f, 0.14f);
        var processingAccent = new Color(0.08f, 0.72f, 0.94f);
        var manufacturingBody = new Color(0.09f, 0.105f, 0.15f);
        var manufacturingAccent = new Color(0.37f, 0.62f, 1.0f);
        var energyBody = new Color(0.14f, 0.105f, 0.055f);
        var energyAccent = new Color(1.0f, 0.67f, 0.2f);

        yield return Definition(MachineDefinitionIds.Crusher, 96, 72, MachineGlyph.Crusher, processingBody, processingAccent, 145);
        yield return Definition(MachineDefinitionIds.Smelter, 96, 96, MachineGlyph.Smelter, processingBody, new Color(1.0f, 0.43f, 0.18f), 155);
        yield return Definition(MachineDefinitionIds.Foundry, 120, 96, MachineGlyph.Foundry, processingBody, new Color(0.95f, 0.56f, 0.2f), 170);
        yield return Definition(MachineDefinitionIds.Refinery, 144, 96, MachineGlyph.Refinery, processingBody, new Color(0.25f, 0.83f, 0.74f), 185);
        yield return Definition(MachineDefinitionIds.WaterProcessor, 96, 72, MachineGlyph.WaterProcessor, processingBody, new Color(0.26f, 0.73f, 1.0f), 145);
        yield return Definition(MachineDefinitionIds.Electrolyzer, 120, 72, MachineGlyph.Electrolyzer, processingBody, new Color(0.35f, 0.9f, 1.0f), 160);
        yield return Definition(MachineDefinitionIds.Constructor, 96, 72, MachineGlyph.Constructor, manufacturingBody, manufacturingAccent, 145);
        yield return Definition(MachineDefinitionIds.Fabricator, 120, 96, MachineGlyph.Fabricator, manufacturingBody, new Color(0.62f, 0.47f, 1.0f), 170);
        yield return Definition(MachineDefinitionIds.BasicGenerator, 72, 72, MachineGlyph.BasicGenerator, energyBody, energyAccent, 135);
        yield return Definition(MachineDefinitionIds.FuelGenerator, 96, 72, MachineGlyph.FuelGenerator, energyBody, new Color(1.0f, 0.49f, 0.18f), 150);
        yield return Definition(MachineDefinitionIds.StorageContainer, 120, 72, MachineGlyph.Storage, new Color(0.075f, 0.12f, 0.105f), new Color(0.35f, 0.92f, 0.66f), 160);
        yield return Definition(MachineDefinitionIds.ResearchStation, 120, 96, MachineGlyph.Research, new Color(0.11f, 0.085f, 0.15f), new Color(0.72f, 0.48f, 1.0f), 170);
    }

    private static MachinePresentationDefinition Definition(
        MachineDefinitionId id,
        float width,
        float height,
        MachineGlyph glyph,
        Color body,
        Color accent,
        float interactionRadius = DefaultInteractionRadius) =>
        new(id, new Vector2(width, height), glyph, body, accent, interactionRadius);
}

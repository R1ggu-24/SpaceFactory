using Godot;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Visual connection anchors. They are presentation coordinates only; cable,
/// item and fluid routing remains owned by the respective gameplay systems.
/// </summary>
public enum MachineConnectionAnchorKind
{
    Power,
    ItemInput,
    ItemOutput,
    PipeInput,
    PipeOutput,
}

public sealed record MachinePresentationDefinition(
    MachineDefinitionId DefinitionId,
    Vector2 Footprint,
    MachineGlyph Glyph,
    Color BodyColor,
    Color AccentColor,
    float InteractionRadius)
{
    public int PowerPortCount => Glyph == MachineGlyph.PowerPole
        ? PowerGridConfiguration.PowerPolePortCount
        : 1;

    public void Validate()
    {
        if (Footprint.X <= 0 || Footprint.Y <= 0 || InteractionRadius <= 0)
        {
            throw new ArgumentException($"Machine presentation '{DefinitionId}' is invalid.");
        }
    }

    public Vector2 GetConnectionAnchor(MachineConnectionAnchorKind anchor)
    {
        var half = Footprint * 0.5f;
        return anchor switch
        {
            MachineConnectionAnchorKind.Power => GetPowerPortAnchor(0),
            MachineConnectionAnchorKind.ItemInput => new Vector2(-half.X + 2, 0),
            MachineConnectionAnchorKind.ItemOutput => new Vector2(half.X - 2, 0),
            MachineConnectionAnchorKind.PipeInput => new Vector2(-half.X * 0.27f, -half.Y + 2),
            MachineConnectionAnchorKind.PipeOutput => new Vector2(half.X * 0.27f, -half.Y + 2),
            _ => throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null),
        };
    }

    public Vector2 GetPowerPortAnchor(int portIndex)
    {
        if (portIndex < 0 || portIndex >= PowerPortCount)
        {
            throw new ArgumentOutOfRangeException(nameof(portIndex), portIndex,
                $"Machine '{DefinitionId}' exposes {PowerPortCount} power port(s).");
        }

        if (Glyph != MachineGlyph.PowerPole)
        {
            return new Vector2(0, (Footprint.Y * 0.5f) - 2);
        }

        var portsPerSide = PowerGridConfiguration.PowerPolePortCount / 2;
        var side = portIndex < portsPerSide ? -1.0f : 1.0f;
        var row = portIndex < portsPerSide
            ? portIndex
            : (PowerGridConfiguration.PowerPolePortCount - 1) - portIndex;
        var usableHeight = Footprint.Y - 22;
        var y = (row - ((portsPerSide - 1) * 0.5f)) *
                (portsPerSide <= 1 ? 0 : usableHeight / (portsPerSide - 1));
        return new Vector2(side * (Footprint.X * 0.5f), y);
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
        // The bodies intentionally stay inside one narrow titanium/graphite palette.
        // Function is communicated by silhouette and small accents instead of by
        // painting the complete machine in a category colour.
        var processingBody = new Color(0.085f, 0.105f, 0.115f);
        var processingAccent = new Color(0.11f, 0.69f, 0.84f);
        var manufacturingBody = new Color(0.095f, 0.105f, 0.12f);
        var manufacturingAccent = new Color(0.2f, 0.69f, 0.9f);
        var energyBody = new Color(0.105f, 0.105f, 0.095f);
        var energyAccent = new Color(0.92f, 0.62f, 0.2f);

        yield return Definition(MachineDefinitionIds.Crusher, 96, 72, MachineGlyph.Crusher, processingBody, processingAccent, 145);
        yield return Definition(MachineDefinitionIds.Smelter, 96, 96, MachineGlyph.Smelter, processingBody, new Color(0.95f, 0.42f, 0.16f), 155);
        yield return Definition(MachineDefinitionIds.Foundry, 120, 96, MachineGlyph.Foundry, processingBody, new Color(0.92f, 0.55f, 0.22f), 170);
        yield return Definition(MachineDefinitionIds.Refinery, 144, 96, MachineGlyph.Refinery, processingBody, new Color(0.18f, 0.76f, 0.72f), 185);
        yield return Definition(MachineDefinitionIds.WaterProcessor, 96, 72, MachineGlyph.WaterProcessor, processingBody, new Color(0.22f, 0.65f, 0.92f), 145);
        yield return Definition(MachineDefinitionIds.Electrolyzer, 120, 72, MachineGlyph.Electrolyzer, processingBody, new Color(0.25f, 0.82f, 0.91f), 160);
        yield return Definition(MachineDefinitionIds.Constructor, 96, 72, MachineGlyph.Constructor, manufacturingBody, manufacturingAccent, 145);
        yield return Definition(MachineDefinitionIds.Fabricator, 120, 96, MachineGlyph.Fabricator, manufacturingBody, new Color(0.24f, 0.76f, 0.94f), 170);
        yield return Definition(MachineDefinitionIds.BasicGenerator, 72, 72, MachineGlyph.BasicGenerator, energyBody, energyAccent, 135);
        yield return Definition(MachineDefinitionIds.FuelGenerator, 96, 72, MachineGlyph.FuelGenerator, energyBody, new Color(0.94f, 0.46f, 0.16f), 150);
        yield return Definition(
            MachineDefinitionIds.PowerPole,
            (float)PowerGridConfiguration.PowerPoleFootprintWidth,
            (float)PowerGridConfiguration.PowerPoleFootprintHeight,
            MachineGlyph.PowerPole,
            new Color(0.075f, 0.09f, 0.098f),
            new Color(0.12f, 0.76f, 0.91f),
            110);
        yield return Definition(MachineDefinitionIds.StorageContainer, 120, 72, MachineGlyph.Storage, new Color(0.085f, 0.11f, 0.105f), new Color(0.25f, 0.78f, 0.61f), 160);
        yield return Definition(MachineDefinitionIds.ResearchStation, 120, 96, MachineGlyph.Research, new Color(0.085f, 0.1f, 0.125f), new Color(0.22f, 0.74f, 0.94f), 170);
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

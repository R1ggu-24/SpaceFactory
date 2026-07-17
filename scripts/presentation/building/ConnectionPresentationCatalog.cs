using Godot;
using SpaceFactory.Core.Logistics;

namespace SpaceFactory.Presentation.Building;

/// <summary>
/// Presentation metadata for the four logistics connection modes. Gameplay
/// compatibility, throughput and required build items stay in the Core catalog.
/// </summary>
public sealed record ConnectionPresentationDefinition(
    ConnectionTypeId TypeId,
    MachineGlyph Glyph,
    string Description,
    string FunctionSummary,
    Color PrimaryColor,
    Color FlowColor,
    float LineWidth);

public static class ConnectionPresentationCatalog
{
    public const float MaximumConnectionLength = 900;
    public const float MachineSelectionRadius = 175;

    private static readonly IReadOnlyDictionary<ConnectionTypeId, ConnectionPresentationDefinition> Definitions =
        CreateDefinitions().ToDictionary(definition => definition.TypeId);

    public static IReadOnlyCollection<ConnectionPresentationDefinition> All => Definitions.Values.ToArray();

    public static ConnectionPresentationDefinition Get(ConnectionTypeId typeId) =>
        Definitions.TryGetValue(typeId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"No connection presentation exists for '{typeId}'.");

    public static bool TryGet(string buildId, out ConnectionPresentationDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(buildId))
        {
            definition = null;
            return false;
        }

        return Definitions.TryGetValue(new ConnectionTypeId(buildId), out definition);
    }

    public static MachineConnectionAnchorKind GetSourceAnchor(ConnectionKind kind) => kind switch
    {
        ConnectionKind.PowerCable => MachineConnectionAnchorKind.Power,
        ConnectionKind.ConveyorBelt => MachineConnectionAnchorKind.ItemOutput,
        ConnectionKind.LiquidPipe or ConnectionKind.GasPipe => MachineConnectionAnchorKind.PipeOutput,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static MachineConnectionAnchorKind GetTargetAnchor(ConnectionKind kind) => kind switch
    {
        ConnectionKind.PowerCable => MachineConnectionAnchorKind.Power,
        ConnectionKind.ConveyorBelt => MachineConnectionAnchorKind.ItemInput,
        ConnectionKind.LiquidPipe or ConnectionKind.GasPipe => MachineConnectionAnchorKind.PipeInput,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static IEnumerable<ConnectionPresentationDefinition> CreateDefinitions()
    {
        yield return new ConnectionPresentationDefinition(
            ConnectionTypeIds.PowerCable,
            MachineGlyph.PowerCable,
            "Flexibles, abgeschirmtes Energiekabel für ein lokales Maschinennetz.",
            "Verteilt Strom zwischen verbundenen Maschinen",
            new Color(0.11f, 0.24f, 0.28f),
            new Color(0.26f, 0.88f, 1.0f),
            7);
        yield return new ConnectionPresentationDefinition(
            ConnectionTypeIds.ConveyorBelt,
            MachineGlyph.ConveyorBelt,
            "Gerichtetes Industrieförderband für feste Rohstoffe und Bauteile.",
            "Transportiert feste Items vom Ausgang zum Eingang",
            new Color(0.17f, 0.2f, 0.21f),
            new Color(0.74f, 0.84f, 0.86f),
            14);
        yield return new ConnectionPresentationDefinition(
            ConnectionTypeIds.LiquidPipe,
            MachineGlyph.LiquidPipe,
            "Druckfestes Rohr für Wasser und flüssigen Treibstoff.",
            "Transportiert ausschliesslich Flüssigkeiten",
            new Color(0.08f, 0.27f, 0.36f),
            new Color(0.24f, 0.72f, 1.0f),
            11);
        yield return new ConnectionPresentationDefinition(
            ConnectionTypeIds.GasPipe,
            MachineGlyph.GasPipe,
            "Versiegelte Gasleitung für Wasserstoff und Sauerstoff.",
            "Transportiert ausschliesslich Gase",
            new Color(0.13f, 0.25f, 0.29f),
            new Color(0.37f, 0.94f, 0.78f),
            11);
    }
}

using SpaceFactory.Core.Items;
using SpaceFactory.Core.Power;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Logistics;

public enum ConnectionKind
{
    PowerCable,
    ConveyorBelt,
    LiquidPipe,
    GasPipe,
}

public enum TransportMedium
{
    Power,
    Solid,
    Liquid,
    Gas,
}

public sealed record ConnectionTypeDefinition(
    ConnectionTypeId Id,
    ConnectionKind Kind,
    string DisplayName,
    TransportMedium Medium,
    ItemId RequiredBuildItemId,
    bool IsDirectional,
    double TransferUnitsPerSecond)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) ||
            !double.IsFinite(TransferUnitsPerSecond) ||
            TransferUnitsPerSecond < 0 ||
            (Medium == TransportMedium.Power && (IsDirectional || TransferUnitsPerSecond != 0)) ||
            (Medium != TransportMedium.Power && (!IsDirectional || TransferUnitsPerSecond <= 0)))
        {
            throw new ArgumentException($"Connection type '{Id}' is invalid.");
        }
    }
}

public sealed class ConnectionTypeCatalog
{
    private readonly IReadOnlyDictionary<ConnectionTypeId, ConnectionTypeDefinition> _definitions;
    private readonly IReadOnlyDictionary<ConnectionKind, ConnectionTypeDefinition> _definitionsByKind;

    public ConnectionTypeCatalog(IEnumerable<ConnectionTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        foreach (var definition in materialized)
        {
            definition.Validate();
        }

        _definitions = materialized.ToDictionary(definition => definition.Id);
        _definitionsByKind = materialized.ToDictionary(definition => definition.Kind);
        if (_definitions.Count != materialized.Length || _definitionsByKind.Count != materialized.Length)
        {
            throw new ArgumentException("Connection type IDs and kinds must be unique.", nameof(definitions));
        }
    }

    public IReadOnlyCollection<ConnectionTypeDefinition> All => _definitions.Values.ToArray();

    public ConnectionTypeDefinition Get(ConnectionTypeId id) => _definitions.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown connection type '{id}'.");

    public ConnectionTypeDefinition Get(ConnectionKind kind) => _definitionsByKind.TryGetValue(kind, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown connection kind '{kind}'.");

    public bool TryGet(ConnectionTypeId id, out ConnectionTypeDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);

    public bool TryGet(ConnectionKind kind, out ConnectionTypeDefinition? definition) =>
        _definitionsByKind.TryGetValue(kind, out definition);
}

public static class LogisticsConfiguration
{
    public const int StartingPowerCableCount = 5;
    public const int StartingConveyorBeltCount = 5;
    public const int StartingTransportPipeCount = 5;
    public const int MaximumMaterialConnectionsPerPort = 1;
    public const int MaximumPowerConnectionsPerPort = PowerGridConfiguration.MaximumCablesPerPort;
    public const double ConveyorTransferUnitsPerSecond = 8;
    public const double PipeTransferUnitsPerSecond = 20;
    public const double MaximumStoredTransferCreditSeconds = 1;
}

public static class DefaultConnectionTypeCatalog
{
    public static ConnectionTypeCatalog Instance { get; } = new(
    [
        new ConnectionTypeDefinition(
            ConnectionTypeIds.PowerCable,
            ConnectionKind.PowerCable,
            "Stromkabel",
            TransportMedium.Power,
            ProductionItemIds.PowerCable,
            false,
            0),
        new ConnectionTypeDefinition(
            ConnectionTypeIds.ConveyorBelt,
            ConnectionKind.ConveyorBelt,
            "Förderband",
            TransportMedium.Solid,
            ProductionItemIds.ConveyorBelt,
            true,
            LogisticsConfiguration.ConveyorTransferUnitsPerSecond),
        new ConnectionTypeDefinition(
            ConnectionTypeIds.LiquidPipe,
            ConnectionKind.LiquidPipe,
            "Flüssigkeitsrohr",
            TransportMedium.Liquid,
            ProductionItemIds.TransportPipe,
            true,
            LogisticsConfiguration.PipeTransferUnitsPerSecond),
        new ConnectionTypeDefinition(
            ConnectionTypeIds.GasPipe,
            ConnectionKind.GasPipe,
            "Gasrohr",
            TransportMedium.Gas,
            ProductionItemIds.TransportPipe,
            true,
            LogisticsConfiguration.PipeTransferUnitsPerSecond),
    ]);
}

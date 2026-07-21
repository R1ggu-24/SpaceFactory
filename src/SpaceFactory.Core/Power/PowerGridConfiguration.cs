using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Power;

public static class PowerGridConfiguration
{
    public const int PowerPolePortCount = 6;
    public const int ShipPortCount = 2;
    public const int MaximumCablesPerPort = 1;
    public const double ShipConnectorPowerKilowatts = 40;
    public const double ShipFuelPerKilowattSecond = 0.000_5;
    public const double MaximumCableLengthWorldUnits = 900;
    public const double PortSelectionRadiusWorldUnits = 44;
    public const double PortInteractionRadiusWorldUnits = 64;
    public const double OverloadToleranceSeconds = 1.5;
    public const double HistoryDurationSeconds = 60;
    public const double MeasurementIntervalSeconds = 1;
    public const int HistorySampleCapacity =
        (int)(HistoryDurationSeconds / MeasurementIntervalSeconds);
    public const double PowerPoleFootprintWidth = 48;
    public const double PowerPoleFootprintHeight = 48;
    public const double PowerPoleConstructionDurationSeconds = 1.2;

    public static IReadOnlyList<ItemAmount> PowerPoleBuildCosts { get; } =
    [
        new(ProductionItemIds.IronPlate, 4),
        new(ProductionItemIds.CopperWire, 6),
    ];
}

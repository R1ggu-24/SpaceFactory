using SpaceFactory.Core.Logistics;
using SpaceFactory.Core.Production;

namespace SpaceFactory.Presentation.Building;

public readonly record struct PowerInteractionTarget(
    MachineInstanceId NodeId,
    MachinePortId PortId);

public enum PowerMenuNetworkStatus
{
    Offline,
    Stable,
    Limited,
    Overloaded,
    CircuitBreakerTripped,
}

/// <summary>
/// One chart sample. Samples are supplied oldest-to-newest; gameplay owns the
/// configured ring buffer and its one-second sampling cadence.
/// </summary>
public sealed record PowerHistorySampleViewModel(
    double Capacity,
    double ActualProduction,
    double ActualConsumption,
    double RequestedPower);

public sealed record PowerPortViewModel(
    string PortId,
    string DisplayName,
    bool IsConnected,
    string ConnectedObjectName,
    bool IsEnabled,
    bool CanToggle,
    double OutputPower,
    string NetworkDisplayName,
    bool CanDisconnect);

public sealed record PowerSourceViewModel(
    string SourceId,
    string DisplayName,
    bool IsEnabled,
    double OutputPower,
    double FuelConsumptionPerMinute,
    double? EstimatedFuelMinutesRemaining,
    bool CanToggle);

/// <summary>
/// Complete read-only UI snapshot for a pole, generator, consumer or ship
/// connector. No gameplay objects are retained by the menu.
/// </summary>
public sealed record PowerMenuViewModel(
    string NetworkId,
    string Title,
    string NetworkDisplayName,
    bool IsNetworkEnabled,
    bool CanToggleNetwork,
    PowerMenuNetworkStatus Status,
    double Capacity,
    double ActualProduction,
    double ActualConsumption,
    double RequestedPower,
    double Reserve,
    double FuelConsumptionPerMinute,
    double? EstimatedFuelMinutesRemaining,
    IReadOnlyList<PowerHistorySampleViewModel> History,
    IReadOnlyList<PowerSourceViewModel> Sources,
    IReadOnlyList<PowerPortViewModel> Ports);

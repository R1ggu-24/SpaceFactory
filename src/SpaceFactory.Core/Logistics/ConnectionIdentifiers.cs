using SpaceFactory.Core.Production;

namespace SpaceFactory.Core.Logistics;

public readonly record struct MachineConnectionId
{
    public MachineConnectionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A machine connection ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ConnectionTypeId
{
    public ConnectionTypeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A connection type ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct MachinePortId
{
    public MachinePortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A machine port ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static class ConnectionTypeIds
{
    public static readonly ConnectionTypeId PowerCable = new("power_cable");
    public static readonly ConnectionTypeId ConveyorBelt = new("conveyor_belt");
    public static readonly ConnectionTypeId LiquidPipe = new("liquid_pipe");
    public static readonly ConnectionTypeId GasPipe = new("gas_pipe");
}

public static class MachinePortIds
{
    public static readonly MachinePortId Power = new("power");
    public static readonly MachinePortId Power1 = new("power_1");
    public static readonly MachinePortId Power2 = new("power_2");
    public static readonly MachinePortId Power3 = new("power_3");
    public static readonly MachinePortId Power4 = new("power_4");
    public static readonly MachinePortId Power5 = new("power_5");
    public static readonly MachinePortId Power6 = new("power_6");
    public static readonly MachinePortId ShipPowerA = new("ship_power_a");
    public static readonly MachinePortId ShipPowerB = new("ship_power_b");
    public static readonly MachinePortId SolidInput = new("solid_input");
    public static readonly MachinePortId SolidOutput = new("solid_output");
    public static readonly MachinePortId LiquidInput = new("liquid_input");
    public static readonly MachinePortId LiquidOutput = new("liquid_output");
    public static readonly MachinePortId GasInput = new("gas_input");
    public static readonly MachinePortId GasOutput = new("gas_output");
    public static readonly MachinePortId SolidStorage = new("solid_storage");
    public static readonly MachinePortId LiquidStorage = new("liquid_storage");
    public static readonly MachinePortId GasStorage = new("gas_storage");

    public static IReadOnlyList<MachinePortId> PowerPolePorts { get; } =
    [Power1, Power2, Power3, Power4, Power5, Power6];

    public static IReadOnlyList<MachinePortId> ShipPowerPorts { get; } =
    [ShipPowerA, ShipPowerB];

    public static MachinePortId InputFor(ProductionItemPhase phase) => phase switch
    {
        ProductionItemPhase.Solid => SolidInput,
        ProductionItemPhase.Liquid => LiquidInput,
        ProductionItemPhase.Gas => GasInput,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    public static MachinePortId OutputFor(ProductionItemPhase phase) => phase switch
    {
        ProductionItemPhase.Solid => SolidOutput,
        ProductionItemPhase.Liquid => LiquidOutput,
        ProductionItemPhase.Gas => GasOutput,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    public static MachinePortId StorageFor(ProductionItemPhase phase) => phase switch
    {
        ProductionItemPhase.Solid => SolidStorage,
        ProductionItemPhase.Liquid => LiquidStorage,
        ProductionItemPhase.Gas => GasStorage,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };
}

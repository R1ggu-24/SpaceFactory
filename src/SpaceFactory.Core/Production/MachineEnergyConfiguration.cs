namespace SpaceFactory.Core.Production;

/// <summary>
/// Energy values for self-powered extraction and grid storage. They are kept out of the runtime
/// controller so balancing changes do not require changes to simulation code.
/// </summary>
public static class MachineEnergyConfiguration
{
    public const double MobileMinerInternalPowerKilowatts = 0.35;
    public const double MobileMinerInitialEnergyKilowattSeconds = 210;
    public const double MobileBatteryPackEnergyKilowattSeconds = 420;
    public const double BatteryBankCapacityKilowattSeconds = 18_000;
    public const double BatteryBankMaximumChargeKilowatts = 120;
    public const double BatteryBankMaximumDischargeKilowatts = 120;
}

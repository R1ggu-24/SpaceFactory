namespace SpaceFactory.Core.Ships.Docking;

public readonly record struct ShipDockingContext(
    bool IsShipControlled,
    bool IsPauseMenuOpen,
    double ShipSpeed,
    ShipDockingCandidate? Candidate);

namespace SpaceFactory.Core.Settings;

public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Interact,
    ShipInteraction,
    // Retained so settings written by older builds can be migrated during normalization.
    EnterShip,
    ExitShip,
    OpenInventory,
    OpenMap,
    UseMiningTool,
    Build,
    RotateBuilding,
    CancelAction,
    OpenPauseMenu
}

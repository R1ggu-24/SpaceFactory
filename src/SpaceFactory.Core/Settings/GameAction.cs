namespace SpaceFactory.Core.Settings;

public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Interact,
    ShipInteraction,
    ShipBoost,
    ShipDocking,
    // Retained so settings written by older builds can be migrated during normalization.
    EnterShip,
    ExitShip,
    OpenInventory,
    OpenMap,
    OpenBuildMenu,
    UseMiningTool,
    // Retained so settings written by older builds can migrate from build_mode to build_menu.
    Build,
    RotateBuilding,
    CancelAction,
    OpenPauseMenu
}

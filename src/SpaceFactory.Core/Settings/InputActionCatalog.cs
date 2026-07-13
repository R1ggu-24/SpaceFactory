using System.Collections.ObjectModel;

namespace SpaceFactory.Core.Settings;

public sealed record InputActionDefinition(
    GameAction Action,
    string InputMapAction,
    string DisplayName,
    InputBinding DefaultBinding);

public static class InputActionCatalog
{
    private static readonly ReadOnlyCollection<InputActionDefinition> Definitions =
        Array.AsReadOnly<InputActionDefinition>(
        [
            new(GameAction.MoveUp, "move_up", "Nach oben bewegen", InputBinding.Key(InputBindingCodes.W)),
            new(GameAction.MoveDown, "move_down", "Nach unten bewegen", InputBinding.Key(InputBindingCodes.S)),
            new(GameAction.MoveLeft, "move_left", "Nach links bewegen", InputBinding.Key(InputBindingCodes.A)),
            new(GameAction.MoveRight, "move_right", "Nach rechts bewegen", InputBinding.Key(InputBindingCodes.D)),
            new(GameAction.Interact, "interact", "Interagieren", InputBinding.Key(InputBindingCodes.Q)),
            new(GameAction.ShipInteraction, "ship_interaction", "Raumschiff betreten / verlassen", InputBinding.Key(InputBindingCodes.E)),
            new(GameAction.ShipBoost, "ship_boost", "Raumschiff-Boost", InputBinding.Key(InputBindingCodes.Shift)),
            new(GameAction.ShipDocking, "ship_docking", "Am Kometen befestigen / Vom Kometen lösen", InputBinding.Key(InputBindingCodes.H)),
            new(GameAction.OpenInventory, "inventory", "Inventar öffnen", InputBinding.Key(InputBindingCodes.I)),
            new(GameAction.OpenMap, "open_map", "Karte öffnen", InputBinding.Key(InputBindingCodes.M)),
            new(GameAction.OpenBuildMenu, "build_menu", "Baumenü öffnen", InputBinding.Key(InputBindingCodes.B)),
            new(GameAction.UseMiningTool, "use_mining_tool", "Abbauwerkzeug verwenden", InputBinding.MouseButton(InputBindingCodes.LeftMouseButton)),
            new(GameAction.RotateBuilding, "rotate_building", "Gebäude drehen", InputBinding.Key(InputBindingCodes.R)),
            new(GameAction.CancelAction, "cancel_action", "Aktion abbrechen", InputBinding.Key(InputBindingCodes.X)),
            new(GameAction.OpenPauseMenu, "pause", "Pausemenü öffnen", InputBinding.Key(InputBindingCodes.Escape))
        ]);

    private static readonly IReadOnlyDictionary<GameAction, InputActionDefinition> DefinitionsByAction =
        new ReadOnlyDictionary<GameAction, InputActionDefinition>(
            Definitions.ToDictionary(definition => definition.Action));

    private static readonly IReadOnlyDictionary<GameAction, IReadOnlyList<InputBinding>> PermanentBindingsByAction =
        new ReadOnlyDictionary<GameAction, IReadOnlyList<InputBinding>>(
            new Dictionary<GameAction, IReadOnlyList<InputBinding>>
            {
                [GameAction.MoveUp] = [InputBinding.Key(InputBindingCodes.Up)],
                [GameAction.MoveDown] = [InputBinding.Key(InputBindingCodes.Down)],
                [GameAction.MoveLeft] = [InputBinding.Key(InputBindingCodes.Left)],
                [GameAction.MoveRight] = [InputBinding.Key(InputBindingCodes.Right)],
            });

    public static IReadOnlyList<InputActionDefinition> All => Definitions;

    public static bool Contains(GameAction action) => DefinitionsByAction.ContainsKey(action);

    public static InputActionDefinition Get(GameAction action)
    {
        if (!DefinitionsByAction.TryGetValue(action, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "The game action is invalid.");
        }

        return definition;
    }

    public static IReadOnlyList<InputBinding> GetPermanentBindings(GameAction action) =>
        PermanentBindingsByAction.TryGetValue(action, out var bindings) ? bindings : [];

    public static GameAction? FindPermanentBindingOwner(InputBinding binding)
    {
        foreach (var pair in PermanentBindingsByAction)
        {
            if (pair.Value.Contains(binding))
            {
                return pair.Key;
            }
        }

        return null;
    }
}

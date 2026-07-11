using System.Collections.ObjectModel;

namespace SpaceFactory.Core.Settings;

public sealed record BindingConflict(
    InputBinding Binding,
    IReadOnlyList<GameAction> Actions);

public sealed record InputSettings
{
    private readonly IReadOnlyDictionary<GameAction, InputBinding> _bindings;

    public InputSettings(IReadOnlyDictionary<GameAction, InputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var copy = new Dictionary<GameAction, InputBinding>();
        foreach (var (action, binding) in bindings)
        {
            if (!Enum.IsDefined(action))
            {
                throw new ArgumentOutOfRangeException(nameof(bindings), action, "A game action is invalid.");
            }

            ArgumentNullException.ThrowIfNull(binding);
            copy[action] = binding;
        }

        _bindings = new ReadOnlyDictionary<GameAction, InputBinding>(copy);
    }

    public IReadOnlyDictionary<GameAction, InputBinding> Bindings => _bindings;

    public static InputSettings CreateDefault() => new(
        InputActionCatalog.All.ToDictionary(
            definition => definition.Action,
            definition => definition.DefaultBinding));

    public InputBinding GetBinding(GameAction action)
    {
        if (!_bindings.TryGetValue(action, out var binding))
        {
            throw new KeyNotFoundException($"No input binding exists for {action}.");
        }

        return binding;
    }

    public InputSettings WithBinding(GameAction action, InputBinding binding)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "The game action is invalid.");
        }

        ArgumentNullException.ThrowIfNull(binding);

        var changed = _bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        changed[action] = binding;

        return new InputSettings(changed);
    }

    public InputSettings Normalize()
    {
        var normalized = InputActionCatalog.All.ToDictionary(
            definition => definition.Action,
            definition => definition.DefaultBinding);

        foreach (var (action, binding) in _bindings)
        {
            normalized[action] = binding;
        }

        return new InputSettings(normalized);
    }

    public IReadOnlyList<BindingConflict> FindConflicts() =>
        _bindings
            .Select(pair => (Action: pair.Key, Binding: pair.Value))
            .Concat(InputActionCatalog.All.SelectMany(definition =>
                InputActionCatalog.GetPermanentBindings(definition.Action)
                    .Select(binding => (Action: definition.Action, Binding: binding))))
            .GroupBy(pair => pair.Binding, pair => pair.Action)
            .Select(group => new
            {
                Binding = group.Key,
                Actions = group.Distinct().OrderBy(action => action).ToArray(),
            })
            .Where(group => group.Actions.Length > 1)
            .Select(group => new BindingConflict(
                group.Binding,
                Array.AsReadOnly(group.Actions)))
            .OrderBy(conflict => conflict.Binding.Kind)
            .ThenBy(conflict => conflict.Binding.Code)
            .ToArray();

    public bool HasConflicts => FindConflicts().Count > 0;
}

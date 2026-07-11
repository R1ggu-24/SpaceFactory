using Godot;
using SpaceFactory.Core.Settings;

namespace SpaceFactory.Presentation.Settings;

public static class InputBindingFormatter
{
    public static string Format(InputBinding binding) => binding.Kind switch
    {
        InputBindingKind.Key => FormatKey((Key)binding.Code),
        InputBindingKind.MouseButton => FormatMouse((MouseButton)binding.Code),
        _ => "Nicht belegt",
    };

    public static string FormatAction(string inputMapAction)
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(inputMapAction))
        {
            if (inputEvent is InputEventKey key)
            {
                var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                return FormatKey(code);
            }

            if (inputEvent is InputEventMouseButton mouse)
            {
                return FormatMouse(mouse.ButtonIndex);
            }
        }

        return "Nicht belegt";
    }

    private static string FormatKey(Key key)
    {
        var label = OS.GetKeycodeString(key);
        return string.IsNullOrWhiteSpace(label) ? key.ToString() : label.ToUpperInvariant();
    }

    private static string FormatMouse(MouseButton button) => button switch
    {
        MouseButton.Left => "MAUS 1",
        MouseButton.Right => "MAUS 2",
        MouseButton.Middle => "MAUS 3",
        MouseButton.WheelUp => "MAUSRAD HOCH",
        MouseButton.WheelDown => "MAUSRAD RUNTER",
        _ => $"MAUS {(int)button}",
    };
}

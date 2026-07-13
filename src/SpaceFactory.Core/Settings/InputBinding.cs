namespace SpaceFactory.Core.Settings;

public enum InputBindingKind
{
    Key,
    MouseButton
}

public sealed record InputBinding
{
    public InputBinding(InputBindingKind kind, long code)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The input binding kind is invalid.");
        }

        if (code <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The input code must be positive.");
        }

        Kind = kind;
        Code = code;
    }

    public InputBindingKind Kind { get; }

    public long Code { get; }

    public static InputBinding Key(long physicalKeyCode) =>
        new(InputBindingKind.Key, physicalKeyCode);

    public static InputBinding MouseButton(long buttonCode) =>
        new(InputBindingKind.MouseButton, buttonCode);
}

public static class InputBindingCodes
{
    public const long A = 65;
    public const long B = 66;
    public const long D = 68;
    public const long E = 69;
    public const long F = 70;
    public const long H = 72;
    public const long I = 73;
    public const long M = 77;
    public const long Q = 81;
    public const long R = 82;
    public const long S = 83;
    public const long W = 87;
    public const long X = 88;
    public const long Escape = 4_194_305;
    public const long Shift = 4_194_325;
    public const long Up = 4_194_320;
    public const long Down = 4_194_322;
    public const long Left = 4_194_319;
    public const long Right = 4_194_321;
    public const long LeftMouseButton = 1;
}

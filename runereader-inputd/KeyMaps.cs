#nullable enable
namespace RuneReader.InputD;

internal static class KeyMaps
{
    // Linux evdev key codes from linux/input-event-codes.h
    internal static class EvdevKeys
    {
        // digits top row
        public const ushort KEY_1 = 2;
        public const ushort KEY_2 = 3;
        public const ushort KEY_3 = 4;
        public const ushort KEY_4 = 5;
        public const ushort KEY_5 = 6;
        public const ushort KEY_6 = 7;
        public const ushort KEY_7 = 8;
        public const ushort KEY_8 = 9;
        public const ushort KEY_9 = 10;
        public const ushort KEY_0 = 11;

        public const ushort KEY_MINUS = 12;     // '-'
        public const ushort KEY_EQUAL = 13;     // '='
        public const ushort KEY_GRAVE = 41;     // '`' (aka GRAVE)

        // letters
        public const ushort KEY_Q = 16;
        public const ushort KEY_W = 17;
        public const ushort KEY_E = 18;

        // punctuation
        public const ushort KEY_LEFTBRACE  = 26; // '['
        public const ushort KEY_RIGHTBRACE = 27; // ']'
        public const ushort KEY_SEMICOLON  = 39; // ';'
        public const ushort KEY_APOSTROPHE = 40; // '\''
        public const ushort KEY_BACKSLASH  = 43; // '\\'
        public const ushort KEY_COMMA      = 51; // ','
        public const ushort KEY_DOT        = 52; // '.'
        public const ushort KEY_SLASH      = 53; // '/'

        // modifiers
        public const ushort KEY_LEFTCTRL  = 29;
        public const ushort KEY_LEFTSHIFT = 42;
        public const ushort KEY_LEFTALT   = 56;

        // function keys
        public const ushort KEY_F1  = 59;
        public const ushort KEY_F2  = 60;
        public const ushort KEY_F3  = 61;
        public const ushort KEY_F4  = 62;
        public const ushort KEY_F5  = 63;
        public const ushort KEY_F6  = 64;
        public const ushort KEY_F7  = 65;
        public const ushort KEY_F8  = 66;
        public const ushort KEY_F9  = 67;
        public const ushort KEY_F10 = 68;
        public const ushort KEY_F11 = 87;
        public const ushort KEY_F12 = 88;
    }

    // Allowed Activation keys the daemon will monitor:
    // '1', '2', '3', GRAVE, Q, E, W
    public static readonly Dictionary<string, ushort> ActivationKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = EvdevKeys.KEY_1,
        ["2"] = EvdevKeys.KEY_2,
        ["3"] = EvdevKeys.KEY_3,
        ["`"] = EvdevKeys.KEY_GRAVE,
        ["GRAVE"] = EvdevKeys.KEY_GRAVE,
        ["Q"] = EvdevKeys.KEY_Q,
        ["E"] = EvdevKeys.KEY_E,
        ["W"] = EvdevKeys.KEY_W,
    };

    // Keys that can be injected into focused window:
    public static readonly Dictionary<string, ushort> InjectKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = EvdevKeys.KEY_1,
        ["2"] = EvdevKeys.KEY_2,
        ["3"] = EvdevKeys.KEY_3,
        ["4"] = EvdevKeys.KEY_4,
        ["5"] = EvdevKeys.KEY_5,
        ["6"] = EvdevKeys.KEY_6,
        ["7"] = EvdevKeys.KEY_7,
        ["8"] = EvdevKeys.KEY_8,
        ["9"] = EvdevKeys.KEY_9,
        ["0"] = EvdevKeys.KEY_0,

        ["-"] = EvdevKeys.KEY_MINUS,
        ["="] = EvdevKeys.KEY_EQUAL,

        ["F1"]  = EvdevKeys.KEY_F1,
        ["F2"]  = EvdevKeys.KEY_F2,
        ["F3"]  = EvdevKeys.KEY_F3,
        ["F4"]  = EvdevKeys.KEY_F4,
        ["F5"]  = EvdevKeys.KEY_F5,
        ["F6"]  = EvdevKeys.KEY_F6,
        ["F7"]  = EvdevKeys.KEY_F7,
        ["F8"]  = EvdevKeys.KEY_F8,
        ["F9"]  = EvdevKeys.KEY_F9,
        ["F10"] = EvdevKeys.KEY_F10,
        ["F11"] = EvdevKeys.KEY_F11,
        ["F12"] = EvdevKeys.KEY_F12,

        ["`"] = EvdevKeys.KEY_GRAVE,
        ["GRAVE"] = EvdevKeys.KEY_GRAVE,
        [";"]  = EvdevKeys.KEY_SEMICOLON,
        ["'"]  = EvdevKeys.KEY_APOSTROPHE,
        ["/"]  = EvdevKeys.KEY_SLASH,
        ["["]  = EvdevKeys.KEY_LEFTBRACE,
        ["]"]  = EvdevKeys.KEY_RIGHTBRACE,
        ["\\"] = EvdevKeys.KEY_BACKSLASH,
        [","]  = EvdevKeys.KEY_COMMA,
        ["."]  = EvdevKeys.KEY_DOT,

        // modifiers
        ["CTRL"]  = EvdevKeys.KEY_LEFTCTRL,
        ["ALT"]   = EvdevKeys.KEY_LEFTALT,
        ["SHIFT"] = EvdevKeys.KEY_LEFTSHIFT,
    };

    public static ushort[] ActivationKeyCodesDistinct =>
        ActivationKeyMap.Values.Distinct().ToArray();

    public static ushort[] InjectableKeyCodesDistinct =>
        InjectKeyMap.Values.Distinct().ToArray();

    // For modifier monitoring:
    public static readonly Dictionary<ushort, string> ModifierCodeToName = new()
    {
        [EvdevKeys.KEY_LEFTCTRL] = "CTRL",
        [EvdevKeys.KEY_LEFTALT] = "ALT",
        [EvdevKeys.KEY_LEFTSHIFT] = "SHIFT",
    };
}

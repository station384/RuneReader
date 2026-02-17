#if LINUX
using System;
using System.Collections.Generic;
using RuneReader.Classes.Platform;

namespace RuneReader.Classes.Platform.Linux;

public sealed class LinuxEvdevKeycodeMapper : IKeycodeMapper
{
    // Small subset you actually use (extend anytime)
    private static readonly Dictionary<string, int> KeyMappingsExclude = new()
    {
        {"1", 2}, 
        {"2", 3}, 
        {"3", 4}, 
        {"4", 5}, 
        {"5", 6}, 
        {"6", 7}, 
        {"7", 8}, 
        {"8", 9}, 
        {"9", 10}, 
        {"0", 11},
        {"AF4", 62},     // KEY_F4 (you’ll still exclude it at a higher level)
        {"-", 12},       // KEY_MINUS
        {"=", 13},       // KEY_EQUAL
    };

    private static readonly Dictionary<string, int> KeyMappings = new()
    {
        {"1", 2}, 
        {"2", 3}, 
        {"3", 4}, 
        {"4", 5}, 
        {"5", 6}, 
        {"6", 7}, 
        {"7", 8}, 
        {"8", 9}, 
        {"9", 10}, 
        {"0", 11},
        {"-", 12}, 
        {"=", 13},

        {"F1", 59}, {"F2", 60}, {"F3", 61}, {"F4", 62}, {"F5", 63}, {"F6", 64},
        {"F7", 65}, {"F8", 66}, {"F9", 67}, {"F10", 68}, {"F11", 87}, {"F12", 88},

        // Your “future” tokens can stay, same code; modifiers handled by your app logic
        {"CF1", 59}, {"CF2", 60}, {"CF3", 61}, {"CF4", 62}, {"CF5", 63}, {"CF6", 64},
        {"CF7", 65}, {"CF8", 66}, {"CF9", 67}, {"CF10", 68}, {"CF11", 87}, {"CF12", 88},

        {"AF1", 59}, {"AF2", 60}, {"AF3", 61}, {"AF5", 63}, {"AF6", 64},
        {"AF7", 65}, {"AF8", 66}, {"AF9", 67}, {"AF10", 68}, {"AF11", 87}, {"AF12", 88},

        {"`", 41},   // KEY_GRAVE

        {";", 39},   // KEY_SEMICOLON
        {"'", 40},   // KEY_APOSTROPHE
        {"/", 53},   // KEY_SLASH
        {"[", 26},   // KEY_LEFTBRACE
        {"\\", 43},  // KEY_BACKSLASH
        {"]", 27},   // KEY_RIGHTBRACE
        {",", 51},   // KEY_COMMA
        {".", 52},   // KEY_DOT

        // activation keys you mentioned
        {"Q", 16}, {"W", 17}, {"E", 18},

        // modifiers (for IInputSender)
        {"CTRL", 29},   // KEY_LEFTCTRL
        {"SHIFT", 42},  // KEY_LEFTSHIFT
        {"ALT", 56},    // KEY_LEFTALT
    };

    public string[] AllowedActivationKeys => new[] { "1", "2", "3", "`", "Q", "E", "W" };

    public int GetKeyCode(string keyToken)
    {
        if (keyToken.Contains("N/A", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (KeyMappings.TryGetValue(keyToken, out var code))
            return code;

        throw new ArgumentException($"Key not found: {keyToken}", nameof(keyToken));
    }

    public string GetTokenFromKeyCode(int keyCode)
    {
        foreach (var kv in KeyMappings)
            if (kv.Value == keyCode)
                return kv.Key;

        throw new ArgumentException($"Key not found for keyCode {keyCode}", nameof(keyCode));
    }

    public bool HasExcludeKey(string keyToken) => KeyMappingsExclude.ContainsKey(keyToken);
    public bool HasKey(string keyToken) => KeyMappings.ContainsKey(keyToken);
}
#endif
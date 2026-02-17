using System;

namespace RuneReader.Classes.Platform;

public interface IKeycodeMapper
{
    /// Returns platform-native keycode (Windows VK, Linux evdev KEY_*)
    int GetKeyCode(string keyToken);

    /// Reverse lookup (used when decoding/monitoring raw keycodes)
    string GetTokenFromKeyCode(int keyCode);

    bool HasKey(string keyToken);
    bool HasExcludeKey(string keyToken);

    /// Optional: for your daemon config UI
    string[] AllowedActivationKeys { get; }
}
using System;
using System.Collections.Generic;
using static RuneReader.Classes.Utilities.VirtualKeyCodes;


namespace RuneReader.Classes.Utilities;

public static class ActivationKeyCodeMapper
{

    // Virtual-Key codes: https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
    private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
    {
        {"1", (int) VirtualKeyStates.VK_Alphanumeric_1 }, // VK_1
        {"2", (int) VirtualKeyStates.VK_Alphanumeric_2 }, // VK_2
        {"3", (int) VirtualKeyStates.VK_Alphanumeric_2  }, // VK_3
        {"`", (int) VirtualKeyStates.VK_OEM_3 }, // VK_OEM_3 (grave/tilde)
        {"Q", (int) VirtualKeyStates.VK_Q }, // VK_Q
        {"W", (int) VirtualKeyStates.VK_W }, // VK_W
        {"E", (int) VirtualKeyStates.VK_E }, // VK_E
            
    };

    public static int GetVirtualKeyCode(string? key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        if (KeyMappings.TryGetValue(key, out int vkCode))
        {
            return vkCode;
        }
        throw new ArgumentException("Key not found.", nameof(key));
    }

    public static bool HasKey(string key)
    {
        return KeyMappings.ContainsKey(key);
    }
}
using System;
using System.Collections.Generic;
// WPF's System.Windows.Input.Key isn't used in the Avalonia build.

namespace RuneReader
{
    public static class ActivationKeyCodeMapper
    {

        // Virtual-Key codes: https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
            {"1", 0x31 }, // VK_1
            {"2", 0x32 }, // VK_2
            {"3", 0x33 }, // VK_3
            {"`", 0xC0 }, // VK_OEM_3 (grave/tilde)
            {"Q", 0x51 }, // VK_Q
            {"W", 0x57 }, // VK_W
            {"E", 0x45 }, // VK_E
            
                // ... add additional key mappings as needed
        };

        public static int GetVirtualKeyCode(string key)
        {
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
}

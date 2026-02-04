using System;
using System.Collections.Generic;


namespace RuneReader
{
    public static class ActivationKeyCodeMapper
    {

        
        
        
        
        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
            {
                "1",
                35
            },
            {
                "2",
                36
            },
            {
                "3",
                37
            },
            {
                "'",
                146
            },
            {
                "W",
                47
            },
            {
                "Q",
                60
            },
            {
                "E",
                48 /*0x30*/
            }
            // {"1", (int)Key.D1 },
            // {"2", (int)Key.D2 },
            // {"3", (int)Key.D3 },
            // {"'", (int)Key.Oem3 },
            // {"W", (int)Key.D},
            // {"Q", (int)Key.Q},
            // {"E", (int)Key.E},
            
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

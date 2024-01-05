using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HekiliHelper
{
    public static class ActivationKeyCodeMapper
    {

        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
            {"1", (int)Key.D1 },
            {"2", (int)Key.D2 },
            {"3", (int)Key.D3 },
            {"'", (int)Key.Oem3 },
            {"W", (int)Key.D},
            {"Q", (int)Key.Q},
            {"E", (int)Key.E},
            
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

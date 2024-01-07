using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HekiliHelper
{
    // This is the list of acceptable keys we can send to the game and the associated Windows virtual key to send.
    // We can use this for comparison or use it for looking up the matching key
    public static class VirtualKeyCodeMapper
    {

        private static readonly Dictionary<string, int> KeyMappingsExclude = new Dictionary<string, int>
        {
            {"1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
            {"2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
            {"3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
            {"4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
            {"5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
            {"6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
            {"7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
            {"8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
            {"9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
            {"0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
            {"AF4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},   // Durr.....  What would happen if you sent ALT-F4 to the game....   

            // Had to remove these keys as that can't be detected using OCR very well.   only about a 30% accuarcy
           {"-", (int)VirtualKeyCodes.VirtualKeyStates.VK_OEM_MINUS},
            {"=", 187}, // This key can be different depending on country, i.e.  US its the = key,  Spanish is the ? (upside down)
        };



        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
            //{"1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
            //{"2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
            //{"3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
            //{"4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
            //{"5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
            //{"6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
            //{"7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
            //{"8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
            //{"9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
            //{"0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
            // Had to remove these keys as that can't be detected using OCR very well.   only about a 30% accuarcy
            // {"-", (int)VirtualKeyCodes.VirtualKeyStates.VK_OEM_MINUS},
          //    {"=", 187}, // This key can be different depending on country, i.e.  US its the = key,  Spanish is the ? (upside down)
            {"F1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"F2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"F3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
            {"F4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"F5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"F6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"F7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"F8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"F9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"F10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
            {"F11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
            {"F12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},
        
            // This is here just for future,  to accually use these key the value in the key value pair of the diction would need to be an object 
            // to store the CTRL, ALT, SHIFT states
            {"CF1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"CF2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"CF3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
            {"CF4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"CF5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"CF6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"CF7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"CF8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"CF9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"CF10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
            {"CF11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
            {"CF12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},



            {"AF1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"AF2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"AF3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
           // {"AF4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"AF5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"AF6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"AF7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"AF8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"AF9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"AF10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
                        {"AF11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
                                    {"AF12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},
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

        public static bool HasExcludeKey(string key)
        {
            return KeyMappingsExclude.ContainsKey(key);
        }

        public static bool HasKey(string key)
        {
            return KeyMappings.ContainsKey(key);
        }

    }
}

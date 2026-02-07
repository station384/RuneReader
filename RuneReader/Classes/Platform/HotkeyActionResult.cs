namespace RuneReader.Classes.Platform
{
    public enum HotkeyState { PRESSED, RELEASED }
    public class HotkeyActionResult(HotkeyState keyState)
    {
        public HotkeyState KeyState { get; } = keyState;
    }
}
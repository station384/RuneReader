namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsPlatformServices(string? s, IForegroundWindow foregroundWindow, IInputSender input)
        : IPlatformServices
    {
        public IGlobalHotkeys Hotkeys { get; } = new WindowsGlobalHotkeys(s);
        public IForegroundWindow ForegroundWindow { get; } = foregroundWindow;
        public IInputSender Input { get; } = input;
    }
}
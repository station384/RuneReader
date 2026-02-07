namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsPlatformServices : IPlatformServices
    {
        public IGlobalHotkeys Hotkeys { get; }
        public IForegroundWindow ForegroundWindow { get; }
        public IInputSender Input { get; }
    }
}
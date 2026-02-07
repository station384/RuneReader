namespace RuneReader.Classes.Platform.Linux
{
    public class LinuxPlatformServices : IPlatformServices
    {
        public IGlobalHotkeys Hotkeys { get; }
        public IForegroundWindow ForegroundWindow { get; }
        public IInputSender Input { get; }
    }
}
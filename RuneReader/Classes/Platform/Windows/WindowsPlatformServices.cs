namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsPlatformServices
        : IPlatformServices
    {
        private static readonly IForegroundWindow WindowHandler = new WindowsWindowFunctions();
        public IGlobalHotkeys Hotkeys { get; }
        public IInputSender Input { get; } 
        public IForegroundWindow ForegroundWindow { get; } 
        
        public WindowsPlatformServices (string? s)
        {
            Hotkeys = new WindowsGlobalHotkeys(s);
            ForegroundWindow = WindowHandler;
            Input = new WindowsInputSender(WindowHandler);
   

        }

    }
}
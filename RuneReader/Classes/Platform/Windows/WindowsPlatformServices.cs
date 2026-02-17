#if WINDOWS
using System;

namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsPlatformServices(string? s) : IPlatformServices, IDisposable
    {
        private static readonly IForegroundWindow WindowHandler = new WindowsWindowFunctions();
        public IGlobalHotkeys Hotkeys { get; } = new WindowsGlobalHotkeys(s);
        public IInputSender Input { get; } = new WindowsInputSender(WindowHandler);
        public IForegroundWindow ForegroundWindow { get; } = WindowHandler;
        public IScreenCaptureProvider ScreenCapture { get; } = new WindowsCaptureScreen();
        public IKeycodeMapper Keycodes { get; } = new WindowsKeycodeMapper() ;


        public void Dispose()
        {
            Hotkeys.Dispose();
            ScreenCapture.Dispose();
        }
    }
}
#endif
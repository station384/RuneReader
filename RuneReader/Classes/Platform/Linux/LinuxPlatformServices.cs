using System;
using RuneReader.Classes.Platform.Linux.Wayland.PipeWire;

namespace RuneReader.Classes.Platform.Linux
{
    public class LinuxPlatformServices (string? s) : IPlatformServices, IDisposable
    {
        public IGlobalHotkeys Hotkeys { get; } = null;
        public IForegroundWindow ForegroundWindow { get; } = null;
        public IInputSender Input { get; } = null;
        
        // This will need to be broken out in the future to detect if we are using wayland or X11 use the right one accordingly.
        public IScreenCaptureProvider ScreenCapture { get; } = new PipeWireWaylandCaptureProvider();  

        public void Dispose()
        {
            Hotkeys.Dispose();
            ForegroundWindow.Dispose();
            Input.Dispose();
            ScreenCapture.Dispose();
        }
    }
}
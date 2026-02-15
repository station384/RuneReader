using System;
using System.Threading;
using RuneReader.Classes.Platform.Linux.Wayland;

namespace RuneReader.Classes.Platform.Linux
{
    public class LinuxPlatformServices  : IPlatformServices, IDisposable
    {
        public IGlobalHotkeys Hotkeys { get; }
        public IForegroundWindow ForegroundWindow { get; } 
        public IInputSender Input { get; } 
        
        // This will need to be broken out in the future to detect if we are using wayland or X11 use the right one accordingly.
        public IScreenCaptureProvider ScreenCapture { get; } 


        public LinuxPlatformServices(string? s)
        {
            Hotkeys = null;
            ForegroundWindow = null;
            Input = null;
            ScreenCapture = new WaylandScreenCaptureProvider(0);
        }
        
        
        public void Dispose()
        {
            Hotkeys.Dispose();
            ForegroundWindow.Dispose();
            Input.Dispose();
            ScreenCapture.Dispose();
        }
    }
}
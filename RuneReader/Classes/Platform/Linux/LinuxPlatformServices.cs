#if LINUX
using System;
using System.Threading;
using RuneReader.Classes.Platform.Linux.Wayland;

namespace RuneReader.Classes.Platform.Linux
{
    public partial class LinuxPlatformServices  : IPlatformServices, IDisposable
    {
        public IGlobalHotkeys Hotkeys { get; }
        public IForegroundWindow ForegroundWindow { get; } 
        public IInputSender Input { get; } 
        public IKeycodeMapper Keycodes { get; }
        
        // This will need to be broken out in the future to detect if we are using wayland or X11 use the right one accordingly.
        public IScreenCaptureProvider ScreenCapture { get; } 


        public LinuxPlatformServices(string? s)
        {
            var sock = Environment.GetEnvironmentVariable("RUNEREADER_INPUTD_SOCK") ?? "/run/runereader-inputd.sock";
            var key  = Environment.GetEnvironmentVariable("RUNEREADER_INPUTD_KEY")  ?? "change-me";
            Keycodes = new LinuxEvdevKeycodeMapper();
            //activationKey ??= "1";
            Hotkeys = new RunereaderInputdHotkeysClient(s, sock, key);
            Input   = new RunereaderInputdInputSenderClient(sock, key);
            
            //Hotkeys = null;
            ForegroundWindow = new NullForegroundWindow();
            //Input = null;
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
#endif
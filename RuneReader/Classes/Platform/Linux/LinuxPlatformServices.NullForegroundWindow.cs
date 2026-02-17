#if LINUX
using System;

namespace RuneReader.Classes.Platform.Linux
{
    public partial class LinuxPlatformServices
    {
        private sealed class NullForegroundWindow : IForegroundWindow, IDisposable
        {
            public void Dispose() { }
            // add methods as required by your IForegroundWindow interface
            public bool IsWindowFound()
            {
                return true;
            }

            public void SetWindowToFind(string windowName)
            {
                
            }

            public string? GetWindowTitle()
            {
                return "NA";
            }

            public IntPtr GetWindowHandle()
            {
                return IntPtr.Zero;
            }

            public string? GetActiveWindowTitle()
            {
                return "NA";
            }

            public bool IsActiveWindow()
            {
                return true;
            }
        }
    }
}
#endif
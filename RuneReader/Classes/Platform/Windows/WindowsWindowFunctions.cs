using System;

namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsWindowFunctions : IForegroundWindow
    {
        private static volatile IntPtr _windowHandle;
        private string? _windowTitle;
        
        
        public bool IsWindowFound()
        {
            var result = _windowHandle != IntPtr.Zero;
            return result;
        }

        public void SetWindowToFind(string windowName)
        {
            if (string.IsNullOrEmpty(windowName))
            {
                throw new ArgumentNullException(windowName, @"Cannot set window to null or empty string");
            }

            // if (windowName == _windowTitle)  // Don't need to set again.  its the same.
            //     return;
            var res = WindowsApiCalls.FindWowWindow(_windowTitle);
            if (res != _windowHandle)
            {
                _windowTitle = windowName;
                _windowHandle = WindowsApiCalls.FindWowWindow(_windowTitle);
            }
        }

        public string? GetWindowTitle()
        {
            return _windowTitle;
        }

        public IntPtr GetWindowHandle()
        {
            return _windowHandle;
        }

        public string? GetActiveWindowTitle()
        {
            return  WindowsApiCalls.GetActiveWindowTitle();
        }

        public bool IsActiveWindow()
        {
            if (string.IsNullOrEmpty(_windowTitle) )
                return false;
            var result = WindowsApiCalls.IsCurrentWindowWithTitle(_windowTitle);
            return result;
        }
    }
}
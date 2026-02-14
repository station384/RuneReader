#if WINDOWS
using System;

namespace RuneReader.Classes.Platform.Windows
{
    public class WindowsInputSender (IForegroundWindow windowHandler) : IInputSender
    {
        public bool TrySendKey(int key, bool pressed)
        {
            if (!windowHandler.IsWindowFound())
            {
                throw new ArgumentNullException("Window not found");
            }

            if (pressed)
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYDOWN, key, 0);
            }
            else
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYUP, key, 0);
            }

            return true;
        }

        public bool TrySendCtrlKey(bool pressed)
        {

            if (!windowHandler.IsWindowFound())
            {
                throw new ArgumentNullException("Window not found");
            }
            if (pressed)
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYDOWN, WindowsApiCalls.VK_CONTROL, 0);
            }
            else
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_CONTROL, 0);
            }
            return true;
        }

        public bool TrySendAltKey(bool pressed)
        {
            if (!windowHandler.IsWindowFound())
            {
                throw new ArgumentNullException("Window not found");
            }
            if (pressed)
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYDOWN, WindowsApiCalls.VK_MENU, 0);
            }
            else
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_MENU, 0);
            }
            return true;
        }

        public bool TrySendShiftKey(bool pressed)
        {
            if (!windowHandler.IsWindowFound())
            {
                throw new ArgumentNullException("Window not found");
            }
            if (pressed)
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYDOWN, WindowsApiCalls.VK_LSHIFT, 0);
            }
            else
            {
                WindowsApiCalls.PostMessage(windowHandler.GetWindowHandle(), WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_LSHIFT, 0);
            }

            return true;
        }
    }
}
#endif
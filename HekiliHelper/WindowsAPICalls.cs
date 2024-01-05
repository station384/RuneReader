using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HekiliHelper
{
    internal class WindowsAPICalls
    {
        public delegate IntPtr WindowsMessageProc(int nCode, IntPtr wParam, IntPtr lParam);

        #region Win32 Calls
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("USER32.dll")]
        public static extern short GetKeyState(VirtualKeyCodes.VirtualKeyStates nVirtKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        public static uint OCR_NORMAL = 32512;
        public static int IDC_HAND = 32649;

        // Windows message constants
        public static uint WM_KEYDOWN = 0x0100;
        public static uint WM_KEYUP = 0x0101;
        public static int WH_MOUSE_LL = 14;
        public static int WM_LBUTTONDOWN = 0x0201;
        public static int VK_CONTROL = 0x11;
        public static int VK_MENU = 0x12; // Alt key

        // Virtual-Key codes for numeric keys "1" to "0"
        public static int VK_1 = 0x31;
        public static int VK_2 = 0x32;
        public static int VK_3 = 0x33;
        public static int VK_4 = 0x34;
        public static int VK_5 = 0x35;
        public static int VK_6 = 0x36;
        public static int VK_7 = 0x37;
        public static int VK_8 = 0x38;
        public static int VK_9 = 0x39;
        public static int VK_0 = 0x30; // Virtual-Key code for the "0" key
        public static int WH_KEYBOARD_LL = 13;



        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, WindowsMessageProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        public struct POINT
        {
            public int x;
            public int y;
        }

        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        #endregion

        public static string GetActiveWindowTitle()
        {
            IntPtr hwnd = WindowsAPICalls.GetForegroundWindow();
            int length = WindowsAPICalls.GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            WindowsAPICalls.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool IsCurrentWindowWithTitle(string title)
        {
            var currentTitle = GetActiveWindowTitle();
            return currentTitle?.Equals(title, StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }
}

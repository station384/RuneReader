﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RuneReader.Classes.Utilities
{
    internal class WindowsAPICalls
    {
        public delegate nint WindowsMessageProc(int nCode, nint wParam, nint lParam);

        #region Win32 Calls
        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(nint hWnd, uint Msg, int wParam, int lParam);

        [DllImport("USER32.dll")]
        public static extern short GetKeyState(VirtualKeyCodes.VirtualKeyStates nVirtKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetSystemCursor(nint hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint LoadCursor(nint hInstance, int lpCursorName);

        // Import necessary APIs from Dxva2.dll
        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetMonitorCapabilities(nint hMonitor, out uint pdwMonitorCapabilities, out uint pdwSupportedColorTemperatures);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

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
        public static extern nint SetWindowsHookEx(int idHook, WindowsMessageProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint GetModuleHandle(string lpModuleName);
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
            public nint dwExtraInfo;
        }
        #endregion


        // Define the structure that will hold monitor information
        [StructLayout(LayoutKind.Sequential)]
        public struct PHYSICAL_MONITOR
        {
            public nint hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        // Define the capabilities and color temperature flags
        // These values are taken from highlevelmonitorconfigurationapi.h
        [Flags]
        public enum MC_CAPS
        {
            MC_CAPS_NONE = 0x00000000,
            MC_CAPS_MONITOR_TECHNOLOGY_TYPE = 0x00000001,
            MC_CAPS_BRIGHTNESS = 0x00000002,
            MC_CAPS_CONTRAST = 0x00000004,
            MC_CAPS_COLOR_TEMPERATURE = 0x00000008,
            MC_CAPS_RED_GREEN_BLUE_GAIN = 0x00000010,
            MC_CAPS_RED_GREEN_BLUE_DRIVE = 0x00000020,
            MC_CAPS_DEGAUSS = 0x00000040,
            MC_CAPS_DISPLAY_AREA_POSITION = 0x00000080,
            MC_CAPS_DISPLAY_AREA_SIZE = 0x00000100,
            MC_CAPS_RESTORE_FACTORY_DEFAULTS = 0x00000400,
            MC_CAPS_RESTORE_FACTORY_COLOR_DEFAULTS = 0x00000800,
            MC_RESTORE_FACTORY_DEFAULTS_ENABLES_MONITOR_SETTINGS = 0x00001000
        }

        [Flags]
        public enum MC_DISPLAY_TECHNOLOGY_TYPE
        {
            MC_SHADOW_MASK_CATHODE_RAY_TUBE,
            MC_APERTURE_GRILL_CATHODE_RAY_TUBE,
            MC_THIN_FILM_TRANSISTOR,
            MC_LIQUID_CRYSTAL_ON_SILICON,
            MC_PLASMA,
            MC_ORGANIC_LIGHT_EMITTING_DIODE,
            MC_ELECTROLUMINESCENT,
            MC_MICROELECTROMECHANICAL,
            MC_FIELD_EMISSION_DEVICE,
        }

        public static nint FindWowWindow(string lpWindowName)

        {
            var result = nint.Zero;
            result = FindWindow(null, "World of Warcraft");
            return result;
        }
        public static string GetActiveWindowTitle()
        {
            nint hwnd = GetForegroundWindow();
            int length = GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool IsCurrentWindowWithTitle(string title)
        {
            var currentTitle = GetActiveWindowTitle();
            return currentTitle?.Equals(title, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static bool IsKeyPressed(int vKey)
        {
            // Get the state of the specified key
            short keyState = GetAsyncKeyState(vKey);

            // If the most significant bit is set, the key is down
            return (keyState & 0x8000) != 0;
        }
    }
}

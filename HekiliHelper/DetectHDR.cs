
namespace HekiliHelper
{

    using System;
    using System.Runtime.InteropServices;

    public static class DetectHDR
    {
        // Define the structure that will hold monitor information
        [StructLayout(LayoutKind.Sequential)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
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

        // Import necessary APIs from Dxva2.dll
        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("Dxva2.dll", SetLastError = true)]
        public static extern bool GetMonitorCapabilities(IntPtr hMonitor, out uint pdwMonitorCapabilities, out uint pdwSupportedColorTemperatures);

        // Method to check if HDR is enabled
        public static bool IsWindowsHDRModeEnabled()
        {
            bool hdrEnabled = false;

            // Get the monitor handle for the primary monitor
            IntPtr primaryMonitorHandle = GetPrimaryMonitorHandle();

            // Check the number of physical monitors associated with the handle
            uint monitorCount = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, ref monitorCount))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            // Get the physical monitor array
            PHYSICAL_MONITOR[] physicalMonitors = new PHYSICAL_MONITOR[monitorCount];
            if (!GetPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, monitorCount, physicalMonitors))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            // Check the capabilities of the monitor
            foreach (var monitor in physicalMonitors)
            {
                if (GetMonitorCapabilities(monitor.hPhysicalMonitor, out uint capabilities, out _))
                {
                    if ((capabilities & (uint)MC_CAPS.MC_CAPS_MONITOR_TECHNOLOGY_TYPE) != 0)
                    {
                        hdrEnabled = true;
                    }
                }

                // Clean up
                DestroyPhysicalMonitors(monitorCount, physicalMonitors);
            }

            return hdrEnabled;
        }

        private static IntPtr GetPrimaryMonitorHandle()
        {
            // This would need to be implemented to get the primary monitor handle, possibly using EnumDisplayMonitors and MonitorFromWindow.
            // Placeholder for actual implementation
            return IntPtr.Zero;
        }
    }

}


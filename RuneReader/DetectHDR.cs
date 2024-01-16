
namespace RuneReader
{

    using System;
    using System.Runtime.InteropServices;

    public static class DetectHDR
    {


        // Method to check if HDR is enabled
        public static bool IsWindowsHDRModeEnabled()
        {
            bool hdrEnabled = false;

            // Get the monitor handle for the primary monitor
            IntPtr primaryMonitorHandle = GetPrimaryMonitorHandle();

            // Check the number of physical monitors associated with the handle
            uint monitorCount = 0;
            if (!WindowsAPICalls.GetNumberOfPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, ref monitorCount))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            // Get the physical monitor array
            WindowsAPICalls.PHYSICAL_MONITOR[] physicalMonitors = new WindowsAPICalls.PHYSICAL_MONITOR[monitorCount];
            if (!WindowsAPICalls.GetPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, monitorCount, physicalMonitors))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            // Check the capabilities of the monitor
            foreach (var monitor in physicalMonitors)
            {
                if (WindowsAPICalls.GetMonitorCapabilities(monitor.hPhysicalMonitor, out uint capabilities, out _))
                {
                    if ((capabilities & (uint)WindowsAPICalls.MC_CAPS.MC_CAPS_MONITOR_TECHNOLOGY_TYPE) != 0)
                    {
                        hdrEnabled = true;
                    }
                }

                // Clean up
                WindowsAPICalls.DestroyPhysicalMonitors(monitorCount, physicalMonitors);
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


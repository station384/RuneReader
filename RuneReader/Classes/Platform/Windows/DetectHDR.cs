using System;
using System.Runtime.InteropServices;

namespace RuneReader.Classes.Platform.Windows;

public static class DetectHdr
{
#if WINDOWS
    // Method to check if HDR is enabled in windows
    private static bool IsWindowsHdrModeEnabled()
    {
        bool hdrEnabled = false;

        // Get the monitor handle for the primary monitor
        IntPtr primaryMonitorHandle = GetPrimaryMonitorHandle();

        // Check the number of physical monitors associated with the handle
        uint monitorCount = 0;
        if (!WindowsApiCalls.GetNumberOfPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, ref monitorCount))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        // Get the physical monitor array
        WindowsApiCalls.PHYSICAL_MONITOR[] physicalMonitors = new WindowsApiCalls.PHYSICAL_MONITOR[monitorCount];
        if (!WindowsApiCalls.GetPhysicalMonitorsFromHMONITOR(primaryMonitorHandle, monitorCount, physicalMonitors))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        // Check the capabilities of the monitor
        foreach (var monitor in physicalMonitors)
        {
            if (WindowsApiCalls.GetMonitorCapabilities(monitor.hPhysicalMonitor, out uint capabilities, out _))
            {
                if ((capabilities & (uint)WindowsApiCalls.MC_CAPS.MC_CAPS_MONITOR_TECHNOLOGY_TYPE) != 0)
                {
                    hdrEnabled = true;
                }
            }

            // Clean up
            WindowsApiCalls.DestroyPhysicalMonitors(monitorCount, physicalMonitors);
        }

        return hdrEnabled;
    }

    private static IntPtr GetPrimaryMonitorHandle()
    {
        // This would need to be implemented to get the primary monitor handle, possibly using EnumDisplayMonitors and MonitorFromWindow.
        // Placeholder for actual implementation
        return IntPtr.Zero;
    }
#endif

#if LINUX
    // todo: add a method that will return true or false depending on if HDR is enabled in Linux
    private static bool IsLinuxHdrModeEnabled()
    {
        bool hdrEnabled = false;
        return hdrEnabled;
    }
#endif

    public static bool HdrEnabled
    {
#if WINDOWS
        get { return IsWindowsHdrModeEnabled(); }
#endif

#if LINUX
        get { return IsWindowsHdrModeEnabled(); }
#endif
    }
}
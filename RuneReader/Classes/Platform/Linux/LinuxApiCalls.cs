#if LINUX
using System;

namespace RuneReader.Classes.Platform.Linux;

internal class LinuxApiCalls
{
    public bool isWayland = OperatingSystem.IsLinux() &&
                            string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);    
}
#endif
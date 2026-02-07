#if WINDOWS
using RuneReader.Classes.Platform.Windows;
#elif LINUX
using RuneReader.Classes.Platform.Linux;
#endif
namespace RuneReader.Classes.Platform;

public static class PlatformFactory
{
    public static IPlatformServices Create()
    {
#if WINDOWS
        return new WindowsPlatformServices();
#elif LINUX
        return new LinuxPlatformServices();
#else
        throw new PlatformNotSupportedException();
#endif
    }
}
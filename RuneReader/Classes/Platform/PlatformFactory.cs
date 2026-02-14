#if WINDOWS
using RuneReader.Classes.Platform.Windows;
#elif LINUX
using RuneReader.Classes.Platform.Linux;
#endif
namespace RuneReader.Classes.Platform;

public static class PlatformFactory 
{
    public static IPlatformServices Create(string? activationKey)
    {
#if WINDOWS
        return new WindowsPlatformServices(activationKey);
#elif LINUX
        return new LinuxPlatformServices(activationKey);
#else
        throw new PlatformNotSupportedException();
#endif
    }


    
}
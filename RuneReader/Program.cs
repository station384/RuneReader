using System;
using System.Threading;
using Avalonia;
using Velopack;

namespace RuneReader;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack must run before normal app startup. It applies staged updates
        // and exits cleanly when launched by the updater.
        VelopackApp.Build().Run();

        // Single-instance guard (matches old WPF behavior)
        const string appName = "RuneReaderAvalonia";
        _mutex = new Mutex(true, appName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running.
            return 1;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

#nullable enable
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace runereader_inputd;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        
        
        // Socket path: default /run/runereader-inputd.sock
        // You can change this to /run/user/<uid>/... if you want non-root service with udev grants.
        var socketPath = Environment.GetEnvironmentVariable("RUNEREADER_INPUTD_SOCK")
                         ?? "/run/runereader-inputd.sock";

        var sharedKey = Environment.GetEnvironmentVariable("RUNEREADER_INPUTD_KEY") ?? "change-me";

        Console.WriteLine($"runereader-inputd starting");
        Console.WriteLine($"Socket: {socketPath}");
        Console.WriteLine($"Auth: {(sharedKey == "change-me" ? "DEFAULT (change-me) - CHANGE THIS" : "enabled")}");

        // Ensure old socket removed
        try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch { /* ignore */ }

        // Create uinput virtual keyboard (injection)
        using var uinput = new UInputKeyboard(new UInputKeyboard.Options
        {
            DeviceName = "runereader-inputd-virtual-kbd",
            EnabledKeys = KeyMaps.InjectableKeyCodesDistinct
        });

        // Best-effort cleanup: prevent stuck keys on Ctrl+C / SIGTERM.
        Console.CancelKeyPress += (_, e) =>
        {
            try
            {
                uinput.ReleaseAllEnabled();
                e.Cancel = true;
                cts.Cancel();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                
            }

        };
        
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        };
        
        
        if (OperatingSystem.IsLinux())
        {
            try
            {
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
                {
                    try { uinput.ReleaseAllEnabled(); } catch { }
                    Environment.Exit(0);
                });
            }
            catch { /* ignore */ }
        }

        // Start input monitor (evdev)
        using var monitor = new EvdevKeyboardMonitor(
            activationKeys: KeyMaps.ActivationKeyCodesDistinct,
            monitorModifiers: true);

        var server = new InputDServer(
            socketPath: socketPath,
            sharedKey: sharedKey,
            uinput: uinput,
            monitor: monitor);

        // Start monitoring before accepting clients so early key events work.
        monitor.Start();

        await server.RunAsync(cts.Token);
    }
}
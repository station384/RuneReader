#nullable enable
namespace runereader_inputd;

internal static class Program
{
    public static async Task Main(string[] args)
    {
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

        await server.RunAsync();
    }
}
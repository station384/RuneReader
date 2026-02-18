#if LINUX
using System;

namespace RuneReader.Classes.Platform.Linux;
// All this is required to work around wayland.   
// in X11 we can just call the X11 server.
public sealed class RunereaderInputdInputSenderClient : IInputSender
{
    private readonly InputdConnection _conn;
    private bool _connected;

    public RunereaderInputdInputSenderClient(string socketPath, string sharedKey)
    {
        _conn = new InputdConnection(socketPath, sharedKey);
    }

    private void EnsureConnected()
    {
        if (_connected) return;
        _conn.Connect();
        _connected = true;
    }

    public bool TrySendKey(int key, bool pressed)
    {
        try
        {
            EnsureConnected();
            // send by numeric code: INJECTC DOWN 60
            var resp = _conn.SendAndReadLine($"INJECTC {(pressed ? "DOWN" : "UP")} {key}");
            return resp.StartsWith("OK", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public bool TrySendCtrlKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTCTRL, pressed);
    public bool TrySendAltKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTALT, pressed);
    public bool TrySendShiftKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTSHIFT, pressed);

    public void Dispose() => _conn.Dispose();


    // todo this is redundant.   use that key mapper.
    private static class LinuxEvdevCodes
    {
        public const int KEY_LEFTCTRL = 29;
        public const int KEY_LEFTSHIFT = 42;
        public const int KEY_LEFTALT = 56;
    }
}
#endif
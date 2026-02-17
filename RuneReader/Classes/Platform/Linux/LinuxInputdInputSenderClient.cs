#if LINUX
using System;

namespace RuneReader.Classes.Platform.Linux;

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
            // This requires a tiny daemon addition OR we convert here to a token.
            // To avoid daemon changes, we'll send as token if you provide mapping upstream.
            // Since your interface is int, simplest is: add daemon command INJECTC.
            // I'll include both paths:
            //
            // If you do NOT want to modify daemon, then treat `key` as an ASCII token id is impossible.
            // So: add INJECTC support to daemon.

            var resp = _conn.SendAndReadLine($"INJECTC {(pressed ? "DOWN" : "UP")} {key}");
            return resp.StartsWith("OK", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public bool TrySendCtrlKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTCTRL, pressed);
    public bool TrySendAltKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTALT, pressed);
    public bool TrySendShiftKey(bool pressed) => TrySendKey(LinuxEvdevCodes.KEY_LEFTSHIFT, pressed);

    public void Dispose() => _conn.Dispose();

    // Minimal evdev codes you need for modifiers.
    // You can reuse your daemon's map constants instead.
    private static class LinuxEvdevCodes
    {
        public const int KEY_LEFTCTRL = 29;
        public const int KEY_LEFTSHIFT = 42;
        public const int KEY_LEFTALT = 56;
    }
}
#endif
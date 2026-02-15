#nullable enable
using System.Runtime.InteropServices;

namespace RuneReader.InputD;

internal sealed class EvdevKeyboardMonitor : IDisposable
{
    public sealed class KeyEventArgs : EventArgs
    {
        public ushort Code { get; }
        public bool Pressed { get; }
        public KeyEventArgs(ushort code, bool pressed) { Code = code; Pressed = pressed; }
    }

    public event EventHandler<KeyEventArgs>? KeyEvent;

    private readonly ushort[] _activationKeys;
    private readonly bool _monitorModifiers;
    private readonly List<EvdevDevice> _devices = new();

    private CancellationTokenSource? _cts;
    private Task? _task;

    public EvdevKeyboardMonitor(ushort[] activationKeys, bool monitorModifiers)
    {
        _activationKeys = activationKeys;
        _monitorModifiers = monitorModifiers;
    }

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();

        // Open all /dev/input/event* devices that look like keyboards.
        foreach (var path in Directory.EnumerateFiles("/dev/input", "event*"))
        {
            try
            {
                var dev = EvdevDevice.TryOpenKeyboardLike(path, _activationKeys, _monitorModifiers);
                if (dev != null)
                {
                    _devices.Add(dev);
                    Console.WriteLine($"Monitor: {path} ({dev.Name})");
                }
            }
            catch { /* ignore */ }
        }

        if (_devices.Count == 0)
            Console.WriteLine("WARNING: No keyboard-like evdev devices opened. (Need root or proper udev rules.)");

        _task = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Simple: poll reads in round-robin.
        // Each evdev read is blocking; we open O_NONBLOCK so we can iterate.
        while (!ct.IsCancellationRequested)
        {
            bool any = false;

            foreach (var d in _devices)
            {
                while (d.TryReadEvent(out var ev))
                {
                    any = true;

                    // EV_KEY = 0x01
                    if (ev.type != 0x01) continue;

                    // value: 1 down, 0 up, 2 repeat
                    if (ev.value == 2) continue;

                    bool pressed = ev.value == 1;
                    KeyEvent?.Invoke(this, new KeyEventArgs(ev.code, pressed));
                }
            }

            if (!any)
                await Task.Delay(2, ct).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _task?.Wait(200); } catch { }
        foreach (var d in _devices) d.Dispose();
        _devices.Clear();
        _cts = null;
        _task = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_event
    {
        public timeval time;
        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct timeval
    {
        public long tv_sec;
        public long tv_usec;
    }

    private sealed class EvdevDevice : IDisposable
    {
        private readonly int _fd;
        public string Name { get; }

        private EvdevDevice(int fd, string name)
        {
            _fd = fd;
            Name = name;
        }

        public static EvdevDevice? TryOpenKeyboardLike(string path, ushort[] activationKeys, bool monitorModifiers)
        {
            int fd = Sys.open(path, Sys.O_RDONLY | Sys.O_NONBLOCK);
            if (fd < 0) return null;

            try
            {
                // Grab name
                var name = Sys.GetEvdevName(fd) ?? "unknown";

                // Very lightweight filter: if device supports any activation key, treat as keyboard-like.
                // (Good enough for real keyboards; avoids mice/joysticks.)
                bool ok = false;

                foreach (var k in activationKeys)
                {
                    if (Sys.EvdevHasKey(fd, k)) { ok = true; break; }
                }

                if (!ok && monitorModifiers)
                {
                    ok = Sys.EvdevHasKey(fd, KeyMaps.EvdevKeys.KEY_LEFTCTRL)
                         || Sys.EvdevHasKey(fd, KeyMaps.EvdevKeys.KEY_LEFTALT)
                         || Sys.EvdevHasKey(fd, KeyMaps.EvdevKeys.KEY_LEFTSHIFT);
                }

                if (!ok)
                {
                    Sys.close(fd);
                    return null;
                }

                return new EvdevDevice(fd, name);
            }
            catch
            {
                Sys.close(fd);
                return null;
            }
        }

        public bool TryReadEvent(out input_event ev)
        {
            int size = Marshal.SizeOf<input_event>();
            Span<byte> buf = stackalloc byte[size];

            int n = Sys.read(_fd, buf);
            if (n != size)
            {
                ev = default;
                return false;
            }

            ev = MemoryMarshal.Read<input_event>(buf);
            return true;
        }

        public void Dispose()
        {
            try { Sys.close(_fd); } catch { }
        }
    }
}

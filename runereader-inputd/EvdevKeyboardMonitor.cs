#nullable enable
using System.Runtime.InteropServices;

namespace runereader_inputd;

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
                if (dev == null)
                {
                    continue;
                }

                _devices.Add(dev);
                Console.WriteLine($"Monitor: {path} ({dev.Name})");
            }
            catch { /* ignore */ }
        }

        if (_devices.Count == 0)
            Console.WriteLine("WARNING: No keyboard-like evdev devices opened. (Need root or proper udev rules.)");

        _task = Task.Run(() => LoopAsync(_cts.Token));
    }


    private Task LoopAsync(CancellationToken ct)
    {
        // Build poll list once 
        // this is not optimal.  it will miss if a keybaord is pulled or inserted.
        // meet to make a note in linux notes.
        var pfds = _devices.Select(d => new Sys.pollfd
        {
            fd = d.Fd,
            events = Sys.POLLIN,
            revents = 0
        }).ToArray();

        // Run as a blocking worker loop
        while (!ct.IsCancellationRequested)
        {
            // timeoutMs:
            // -1 blocks forever, but then cancellation won’t break out.
            // So use a long-ish timeout to check ct occasionally.
            int rc = Sys.poll(pfds, pfds.Length, 1000); // 1s

            if (ct.IsCancellationRequested) break;
            if (rc <= 0) continue; // timeout or EINTR

            for (int i = 0; i < pfds.Length; i++)
            {
                if ((pfds[i].revents & Sys.POLLIN) == 0)
                    continue;

                // Drain all queued events from that device (nonblocking read)
                var dev = _devices[i];
                while (dev.TryReadEvent(out var ev))
                {
                    if (ev.type != 0x01) continue; // EV_KEY
                    if (ev.value == 2) continue;   // repeat ignore

                    bool pressed = ev.value == 1;
                    KeyEvent?.Invoke(this, new KeyEventArgs(ev.code, pressed));
                }
            }
        }

        return Task.CompletedTask;
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
    private struct InputEvent
    {
        public TimeValue time;
        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeValue
    {
        public long tv_sec;
        public long tv_usec;
    }

    private sealed class EvdevDevice : IDisposable
    {
        private readonly int _fd;
        public string Name { get; }
        public int Fd => _fd;
        
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

        public bool TryReadEvent(out InputEvent ev)
        {
            int size = Marshal.SizeOf<InputEvent>();
            Span<byte> buf = stackalloc byte[size];

            int n = Sys.read(_fd, buf);
            if (n != size)
            {
                ev = default;
                return false;
            }

            ev = MemoryMarshal.Read<InputEvent>(buf);
            return true;
        }

        public void Dispose()
        {
            try { Sys.close(_fd); } catch { }
        }
    }
}

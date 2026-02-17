#nullable enable
using System.Runtime.InteropServices;
using static runereader_inputd.Sys;

namespace runereader_inputd;

internal sealed class UInputKeyboard : IDisposable
{
    public sealed class Options
    {
        public string DeviceName { get; set; } = "runereader-virtual-kbd";
        public ushort[] EnabledKeys { get; set; } = Array.Empty<ushort>();
    }

    private readonly int _fd;
    private readonly ushort[] _enabledKeys;
    private bool _disposed;

    public UInputKeyboard(Options opt)
    {
        _enabledKeys = (opt.EnabledKeys ?? Array.Empty<ushort>()).Distinct().ToArray();

        _fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
        if (_fd < 0)
            throw new InvalidOperationException("Failed to open /dev/uinput (need root or udev permission).");

        // Enable key + syn events
        ioctl(_fd, UI_SET_EVBIT, EV_KEY);
        ioctl(_fd, UI_SET_EVBIT, EV_SYN);

        foreach (var k in _enabledKeys)
            ioctl(_fd, UI_SET_KEYBIT, k);

        // Create device using legacy uinput_user_dev (widely compatible)
        var uidev = new uinput_user_dev
        {
            name = opt.DeviceName,
            id =  new input_id() { bustype =BUS_USB, vendor = 0x1234, product = 0x5678, version = 1},
            absmax = new int[64],
            absmin = new int[64],
            absfuzz = new int[64],
            absflat = new int[64],
        };

        WriteStruct(_fd, uidev);
        ioctl(_fd, UI_DEV_CREATE, 0);

        // Small delay so kernel creates it
        Thread.Sleep(50);
    }

    public void EmitKey(ushort code, bool pressed)
    {
        if (_disposed) return;

        // EV_KEY event
        var ev1 = new InputEvent
        {
            type = EV_KEY,
            code = code,
            value = pressed ? 1 : 0
        };
        WriteStruct(_fd, ev1);

        // SYN_REPORT
        var ev2 = new InputEvent
        {
            type = EV_SYN,
            code = SYN_REPORT,
            value = 0
        };
        WriteStruct(_fd, ev2);
    }

    /// <summary>
    /// Releases (key-up) all keys this virtual device is allowed to emit.
    /// Safe to call repeatedly.
    /// </summary>
    public void ReleaseAllEnabled()
    {
        if (_disposed) return;
        foreach (var k in _enabledKeys)
        {
            // Sending UP for a key that isn't down is harmless.
            EmitKey(k, pressed: false);
        }
    }

    /// <summary>
    /// Releases (key-up) the provided key codes.
    /// </summary>
    public void ReleaseKeys(IEnumerable<ushort> keys)
    {
        if (_disposed) return;
        foreach (var k in keys)
            EmitKey(k, pressed: false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Best-effort: prevent stuck keys if the daemon is stopped while keys are held.
        try { ReleaseAllEnabled(); } catch { }

        try { ioctl(_fd, UI_DEV_DESTROY, 0); } catch { }
        try { close(_fd); } catch { }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct input_id
    {
        public ushort bustype;
        public ushort vendor;
        public ushort product;
        public ushort version;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct uinput_user_dev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;

        public input_id id;

        public uint ff_effects_max;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmax;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmin;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absfuzz;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absflat;
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

    // private static unsafe void WriteStruct<T>(int fd, T value) where T : unmanaged
    // {
    //     int size = sizeof(T);
    //     Span<byte> buf = stackalloc byte[size];
    //     MemoryMarshal.Write(buf, in value);
    //     int written = Sys.write(fd, buf);
    //     if (written != size)
    //         throw new InvalidOperationException("uinput write failed.");
    // }
    
    private static void WriteStruct<T>(int fd, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr mem = Marshal.AllocHGlobal(size);
        try
        {
            // Copy managed struct -> unmanaged memory
            Marshal.StructureToPtr(value, mem, fDeleteOld: false);

            // Copy unmanaged memory -> managed byte[]
            byte[] buf = new byte[size];
            Marshal.Copy(mem, buf, 0, size);

            int written = Sys.write(fd, buf);
            if (written != size)
            {
                int errno = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"uinput write failed. wrote={written} expected={size} errno={errno}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }
}

#nullable enable
using System.Runtime.InteropServices;
using static RuneReader.InputD.Sys;

namespace RuneReader.InputD;

internal sealed class UInputKeyboard : IDisposable
{
    public sealed class Options
    {
        public string DeviceName { get; set; } = "runereader-virtual-kbd";
        public ushort[] EnabledKeys { get; set; } = Array.Empty<ushort>();
    }

    private readonly int _fd;
    private bool _disposed;

    public UInputKeyboard(Options opt)
    {
        _fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
        if (_fd < 0)
            throw new InvalidOperationException("Failed to open /dev/uinput (need root or udev permission).");

        // Enable key + syn events
        ioctl(_fd, UI_SET_EVBIT, EV_KEY);
        ioctl(_fd, UI_SET_EVBIT, EV_SYN);

        foreach (var k in opt.EnabledKeys.Distinct())
            ioctl(_fd, UI_SET_KEYBIT, k);

        // Create device using legacy uinput_user_dev (widely compatible)
        var uidev = new uinput_user_dev
        {
            name = opt.DeviceName,
            id_bustype = BUS_USB,
            id_vendor = 0x1234,
            id_product = 0x5678,
            id_version = 1
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
        var ev1 = new input_event
        {
            type = EV_KEY,
            code = code,
            value = pressed ? 1 : 0
        };
        WriteStruct(_fd, ev1);

        // SYN_REPORT
        var ev2 = new input_event
        {
            type = EV_SYN,
            code = SYN_REPORT,
            value = 0
        };
        WriteStruct(_fd, ev2);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { ioctl(_fd, UI_DEV_DESTROY, 0); } catch { }
        try { close(_fd); } catch { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct uinput_user_dev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;

        public ushort id_bustype;
        public ushort id_vendor;
        public ushort id_product;
        public ushort id_version;

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
    private struct input_event
    {
        public timeval time;
        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct timeval
    {
        public long tv_sec;
        public long tv_usec;
    }

    private static unsafe void WriteStruct<T>(int fd, T value) where T : unmanaged
    {
        int size = sizeof(T);
        Span<byte> buf = stackalloc byte[size];
        MemoryMarshal.Write(buf, in value);
        int written = Sys.write(fd, buf);
        if (written != size)
            throw new InvalidOperationException("uinput write failed.");
    }
}

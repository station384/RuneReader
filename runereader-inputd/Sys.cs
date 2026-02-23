#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable InconsistentNaming

namespace runereader_inputd;

[SuppressMessage("Interoperability", "SYSLIB1054:Use \'LibraryImportAttribute\' instead of \'DllImportAttribute\' to generate P/Invoke marshalling code at compile time")]
internal static class Sys
{
    // open flags
    public const int O_RDONLY = 0x0000;
    public const int O_WRONLY = 0x0001;
    public const int O_NONBLOCK = 0x0800;

    // evdev
    public const ushort EV_KEY = 0x01;
    public const ushort EV_SYN = 0x00;
    public const ushort SYN_REPORT = 0;

    // uinput ioctls
    public const uint UI_SET_EVBIT = 0x40045564;
    public const uint UI_SET_KEYBIT = 0x40045565;
    public const uint UI_DEV_CREATE = 0x5501;
    public const uint UI_DEV_DESTROY = 0x5502;

    public const ushort BUS_USB = 0x03;

    // evdev ioctl
    private const uint EVIOCGNAME_256 = 0x82004506; // _IOR('E', 0x06, char[256])
    private const uint EVIOCGBIT_EV_0_64 = 0x80404520; // _IOR('E', 0x20 + 0, char[64])
    private const uint EVIOCGBIT_KEY_0_512 = 0x82004521; // _IOR('E', 0x20 + 1, char[512])

    [DllImport("libc", SetLastError = true)]
    [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments")]
    public static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, IntPtr arg);

    public static int ioctl(int fd, uint request, int arg)
        => ioctl(fd, request, (IntPtr)arg);

    public static int ioctl(int fd, uint request, ushort arg)
        => ioctl(fd, request, (IntPtr)arg);

    [DllImport("libc", SetLastError = true)]
    public static extern int chmod(string pathname, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int read(int fd, IntPtr buf, int count);

    [DllImport("libc", SetLastError = true)]
    private static extern int write(int fd, IntPtr buf, int count);

    [DllImport("libc", SetLastError = true)]
    internal static extern int unlink(string pathname);
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct pollfd
    {
        public int fd;
        public short events;
        public short revents;
    }

    internal const short POLLIN = 0x0001;

    [DllImport("libc", SetLastError = true)]
    internal static extern int poll([In, Out] pollfd[] fds, int nfds, int timeout);
    
    // optional helper
    internal static void UnlinkNoThrow(string path)
    {
        try { unlink(path); } catch { }
    }
    
    public static int read(int fd, Span<byte> buf)
    {
        unsafe
        {
            fixed (byte* p = buf)
                return read(fd, (IntPtr)p, buf.Length);
        }
    }

    public static int write(int fd, ReadOnlySpan<byte> buf)
    {
        unsafe
        {
            fixed (byte* p = buf)
                return write(fd, (IntPtr)p, buf.Length);
        }
    }

    public static string? GetEvdevName(int fd)
    {
        Span<byte> buf = stackalloc byte[256];
        buf.Clear();

        unsafe
        {
            fixed (byte* p = buf)
            {
                int rc = ioctl(fd, EVIOCGNAME_256, (IntPtr)p);
                if (rc < 0) return null;
            }
        }

        int len = buf.IndexOf((byte)0);
        if (len < 0) len = buf.Length;
        return Encoding.UTF8.GetString(buf[..len]);
    }

    public static bool EvdevHasKey(int fd, ushort keyCode)
    {
        // Read key bitfield. 512 bytes covers keys up to 4096 bits (enough).
        Span<byte> keyBits = stackalloc byte[512];
        keyBits.Clear();

        unsafe
        {
            fixed (byte* p = keyBits)
            {
                int rc = ioctl(fd, EVIOCGBIT_KEY_0_512, (IntPtr)p);
                if (rc < 0) return false;
            }
        }

        int bit = keyCode;
        int idx = bit / 8;
        int mask = 1 << (bit % 8);

        if (idx < 0 || idx >= keyBits.Length) return false;
        return (keyBits[idx] & mask) != 0;
    }
}
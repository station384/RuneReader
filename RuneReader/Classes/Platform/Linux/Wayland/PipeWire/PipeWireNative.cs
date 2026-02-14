using System;
using System.Runtime.InteropServices;

namespace RuneReader.Classes.Platform.Linux.Wayland.PipeWire;
internal static class PipeWireNative
{
    private const string Lib = "pipewire-0.3";

    // Opaque handles
    internal readonly struct pw_thread_loop { public readonly IntPtr Ptr; public pw_thread_loop(IntPtr p) => Ptr = p; }
    internal readonly struct pw_context { public readonly IntPtr Ptr; public pw_context(IntPtr p) => Ptr = p; }
    internal readonly struct pw_core { public readonly IntPtr Ptr; public pw_core(IntPtr p) => Ptr = p; }
    internal readonly struct pw_stream { public readonly IntPtr Ptr; public pw_stream(IntPtr p) => Ptr = p; }

    [DllImport(Lib)] internal static extern void pw_init(IntPtr argc, IntPtr argv);

    [DllImport(Lib)] internal static extern IntPtr pw_thread_loop_new([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr props);
    [DllImport(Lib)] internal static extern void pw_thread_loop_destroy(IntPtr loop);
    [DllImport(Lib)] internal static extern int pw_thread_loop_start(IntPtr loop);
    [DllImport(Lib)] internal static extern void pw_thread_loop_stop(IntPtr loop);
    [DllImport(Lib)] internal static extern void pw_thread_loop_lock(IntPtr loop);
    [DllImport(Lib)] internal static extern void pw_thread_loop_unlock(IntPtr loop);

    [DllImport(Lib)] internal static extern IntPtr pw_context_new(IntPtr loop, IntPtr props, int user_data_size);
    [DllImport(Lib)] internal static extern void pw_context_destroy(IntPtr context);

    // Connect using existing fd from portal (pw_core_connect_fd exists in newer pipewire)
    [DllImport(Lib)] internal static extern IntPtr pw_context_connect_fd(IntPtr context, int fd, IntPtr props, int user_data_size);
    [DllImport(Lib)] internal static extern void pw_core_disconnect(IntPtr core);

    [DllImport(Lib)] internal static extern IntPtr pw_stream_new(IntPtr core, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr props);
    [DllImport(Lib)] internal static extern void pw_stream_destroy(IntPtr stream);

    // Minimal stream events
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ProcessCallback(IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct pw_stream_events
    {
        public uint version;
        public IntPtr destroy;
        public IntPtr state_changed;
        public IntPtr control_info;
        public IntPtr io_changed;
        public IntPtr param_changed;
        public IntPtr add_buffer;
        public IntPtr remove_buffer;
        public IntPtr process; // ProcessCallback
        public IntPtr drained;
        public IntPtr command;
        public IntPtr trigger_done;
    }

    [DllImport(Lib)]
    internal static extern int pw_stream_add_listener(IntPtr stream, out spa_hook listener, ref pw_stream_events events, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct spa_hook
    {
        public IntPtr link;
        public IntPtr removed;
        public IntPtr list;
        public IntPtr cb;
    }

    // Connect stream
    // pw_stream_connect(stream, direction, target_id, flags, params, n_params)
    [DllImport(Lib)]
    internal static extern int pw_stream_connect(
        IntPtr stream,
        int direction,
        uint target_id,
        uint flags,
        IntPtr[] @params,
        uint n_params);

    // Buffers
    [DllImport(Lib)] internal static extern IntPtr pw_stream_dequeue_buffer(IntPtr stream);
    [DllImport(Lib)] internal static extern int pw_stream_queue_buffer(IntPtr stream, IntPtr buffer);

    // PipeWire buffer structs (minimal overlay)
    [StructLayout(LayoutKind.Sequential)]
    internal struct pw_buffer
    {
        public IntPtr buffer; // spa_buffer*
        public IntPtr user_data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct spa_buffer
    {
        public uint n_datas;
        public IntPtr datas; // spa_data*
        public IntPtr metas;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct spa_data
    {
        public int type;
        public uint flags;
        public IntPtr fd;
        public uint mapoffset;
        public uint maxsize;
        public IntPtr data; // pointer to memory
        public uint chunk;  // spa_chunk* typically, but we’ll ignore
    }

    // Stream direction
    internal const int PW_DIRECTION_INPUT = 0;

    // Stream flags (subset)
    internal const uint PW_STREAM_FLAG_AUTOCONNECT = 1u << 0;
    internal const uint PW_STREAM_FLAG_MAP_BUFFERS = 1u << 1;
    internal const uint PW_STREAM_FLAG_RT_PROCESS = 1u << 2;
}

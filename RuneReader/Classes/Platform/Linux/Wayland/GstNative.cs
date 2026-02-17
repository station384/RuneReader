#if LINUX
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RuneReader.Classes.Platform.Linux.Wayland
{
    /// <summary>
    /// Minimal P/Invoke for GStreamer + gst-app (appsink).
    /// Requires: libgstreamer-1.0.so, libgstapp-1.0.so, libgobject-2.0.so, libglib-2.0.so
    /// </summary>
    internal static class GstNative
    {
        private const string GstLib = "libgstreamer-1.0.so.0";
        private const string GstAppLib = "libgstapp-1.0.so.0";
        private const string GObjectLib = "libgobject-2.0.so.0";
        private const string GLibLib = "libglib-2.0.so.0";

        public const int GST_MAP_READ = 1;

        public enum GstState
        {
            GST_STATE_VOID_PENDING = 0,
            GST_STATE_NULL = 1,
            GST_STATE_READY = 2,
            GST_STATE_PAUSED = 3,
            GST_STATE_PLAYING = 4
        }

        public enum GstStateChangeReturn
        {
            GST_STATE_CHANGE_FAILURE = 0,
            GST_STATE_CHANGE_SUCCESS = 1,
            GST_STATE_CHANGE_ASYNC = 2,
            GST_STATE_CHANGE_NO_PREROLL = 3
        }

        public const GstState GST_STATE_NULL = GstState.GST_STATE_NULL;
        public const GstState GST_STATE_PLAYING = GstState.GST_STATE_PLAYING;
        public const GstStateChangeReturn GST_STATE_CHANGE_FAILURE = GstStateChangeReturn.GST_STATE_CHANGE_FAILURE;

        [StructLayout(LayoutKind.Sequential)]
        public struct GstMapInfo
        {
            public ulong memory;
            public ulong flags;
            public nint data;
            public ulong size;
            public nint user_data0;
            public nint user_data1;
            public nint user_data2;
            public nint user_data3;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GError
        {
            public uint domain;
            public int code;
            public nint message; // gchar*
        }

        [DllImport(GstLib)]
        public static extern void gst_init(nint argc, nint argv);

        [DllImport(GstLib)]
        public static extern nint gst_parse_launch(
            [MarshalAs(UnmanagedType.LPStr)] string pipeline_description,
            out nint error);

        [DllImport(GstLib)]
        public static extern GstStateChangeReturn gst_element_set_state(nint element, GstState state);

        [DllImport(GstLib)]
        public static extern nint gst_bin_get_by_name(nint bin, [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(GstLib)]
        public static extern void gst_object_unref(nint obj);

        // sample/caps/buffer
        [DllImport(GstLib)]
        public static extern nint gst_sample_get_buffer(nint sample);

        [DllImport(GstLib)]
        public static extern nint gst_sample_get_caps(nint sample);

        [DllImport(GstLib)]
        public static extern void gst_sample_unref(nint sample);

        [DllImport(GstLib)]
        public static extern nint gst_caps_get_structure(nint caps, uint index);

        [DllImport(GstLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool gst_structure_get_int(nint structure,
            [MarshalAs(UnmanagedType.LPStr)] string fieldname,
            out int value);

        // buffer map/unmap
        [DllImport(GstLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool gst_buffer_map(nint buffer, out GstMapInfo info, int flags);

        [DllImport(GstLib)]
        public static extern void gst_buffer_unmap(nint buffer, ref GstMapInfo info);

        // appsink pull sample
        [DllImport(GstAppLib)]
        public static extern nint gst_app_sink_try_pull_sample(nint appsink, ulong timeout);

        // error conversion
        public static string GErrorToStringAndFree(nint gerrorPtr)
        {
            if (gerrorPtr == nint.Zero) return string.Empty;
            try
            {
                var err = Marshal.PtrToStructure<GError>(gerrorPtr);
                string msg = err.message != nint.Zero ? Marshal.PtrToStringUTF8(err.message) ?? "GError" : "GError";
                return msg;
            }
            finally
            {
                g_error_free(gerrorPtr);
            }
        }

        [DllImport(GLibLib)]
        private static extern void g_error_free(nint error);
        
        internal const ulong GST_CLOCK_TIME_NONE = ulong.MaxValue;

        [DllImport(GstLib)]
        public static extern ulong gst_buffer_get_pts(nint buffer);

        [DllImport(GstLib)]
        public static extern ulong gst_buffer_get_dts(nint buffer);
    }
    
    
    
}
#endif
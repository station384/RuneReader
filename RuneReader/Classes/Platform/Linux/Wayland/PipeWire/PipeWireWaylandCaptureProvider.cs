using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace RuneReader.Classes.Platform.Linux.Wayland.PipeWire;

public sealed class PipeWireWaylandCaptureProvider : IScreenCaptureProvider
{
    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public int ScreenNumber { get; private set; }
    public Rect CaptureRegion { get; set; }
    public bool EnableRegion { get; set; } = true;
    public bool EnableFullScreen { get; set; } = false;

    public event Action<Mat>? OnRegionUpdated;
    public event Action<Mat>? OnFullScreenUpdated;

    private PortalScreencastSession? _portal;
    private SafeHandle? _pwFdHandle;
    private int _pwFd;
    private uint _nodeId;

    private IntPtr _loop;
    private IntPtr _context;
    private IntPtr _core;
    private IntPtr _stream;

    private PipeWireNative.spa_hook _hook;
    private PipeWireNative.pw_stream_events _events;
    private PipeWireNative.ProcessCallback? _processCb; // keep alive

    private volatile int _pendingCaptureOnce; // 0/1
    private volatile int _running;

    private readonly object _sync = new();

    // Assumption: compositor provides BGRx (4 bytes per pixel).
    // We convert to BGR for OpenCV consumers if needed, or keep BGRA/BGRx.
    private const int BytesPerPixel = 4;

    public PipeWireWaylandCaptureProvider(int screenNumber = 0)
    {
        ScreenNumber = screenNumber;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1) return;

        _portal = new PortalScreencastSession();
        var res = await _portal.StartAsync(ct).ConfigureAwait(false);

        _pwFdHandle = res.PipeWireFd;
        _pwFd = GetFdFromSafeHandle(_pwFdHandle);
        _nodeId = res.NodeId;

        // width/height sometimes come back as 0 depending on portal; we’ll learn from frames later.
        ScreenWidth = res.Width;
        ScreenHeight = res.Height;

        PipeWireNative.pw_init(IntPtr.Zero, IntPtr.Zero);

        _loop = PipeWireNative.pw_thread_loop_new("pwcap-csharp", IntPtr.Zero);
        if (_loop == IntPtr.Zero) throw new InvalidOperationException("pw_thread_loop_new failed.");

        _context = PipeWireNative.pw_context_new(_loop, IntPtr.Zero, 0);
        if (_context == IntPtr.Zero) throw new InvalidOperationException("pw_context_new failed.");

        _core = PipeWireNative.pw_context_connect_fd(_context, _pwFd, IntPtr.Zero, 0);
        if (_core == IntPtr.Zero) throw new InvalidOperationException("pw_context_connect_fd failed.");

        _stream = PipeWireNative.pw_stream_new(_core, "pwcap-stream", IntPtr.Zero);
        if (_stream == IntPtr.Zero) throw new InvalidOperationException("pw_stream_new failed.");

        _processCb = OnProcess;
        _events = new PipeWireNative.pw_stream_events
        {
            version = 0,
            process = Marshal.GetFunctionPointerForDelegate(_processCb),
        };

        PipeWireNative.pw_stream_add_listener(_stream, out _hook, ref _events, IntPtr.Zero);

        // Build format param
        var pod = new SpaPodBuilder().BuildRawVideoFormat(
            width: ScreenWidth > 0 ? ScreenWidth : 1920,
            height: ScreenHeight > 0 ? ScreenHeight : 1080,
            fpsNum: 60,
            fpsDen: 1,
            alpha: false);

        IntPtr podMem = Marshal.AllocHGlobal(pod.Length);
        Marshal.Copy(pod, 0, podMem, pod.Length);

        try
        {
            var paramPtrs = new[] { podMem };

            uint flags =
                PipeWireNative.PW_STREAM_FLAG_AUTOCONNECT |
                PipeWireNative.PW_STREAM_FLAG_MAP_BUFFERS |
                PipeWireNative.PW_STREAM_FLAG_RT_PROCESS;

            int rc;
            PipeWireNative.pw_thread_loop_lock(_loop);
            try
            {
                rc = PipeWireNative.pw_stream_connect(
                    _stream,
                    PipeWireNative.PW_DIRECTION_INPUT,
                    _nodeId,
                    flags,
                    paramPtrs,
                    1);
            }
            finally
            {
                PipeWireNative.pw_thread_loop_unlock(_loop);
            }

            if (rc < 0)
                throw new InvalidOperationException($"pw_stream_connect failed rc={rc}");

            var started = PipeWireNative.pw_thread_loop_start(_loop);
            if (started < 0)
                throw new InvalidOperationException($"pw_thread_loop_start failed rc={started}");
        }
        finally
        {
            Marshal.FreeHGlobal(podMem);
        }
    }

    public void CaptureOnce()
    {
        // In a continuous stream, "CaptureOnce" means: next frame that arrives triggers events.
        Volatile.Write(ref _pendingCaptureOnce, 1);
    }

    private unsafe void OnProcess(IntPtr _)
    {
        // Called on PipeWire thread.
        // Minimize work. Do not allocate large things repeatedly.
        try
        {
            if (!EnableRegion && !EnableFullScreen && Volatile.Read(ref _pendingCaptureOnce) == 0)
            {
                // Still must drain buffers to avoid stalling, but we can skip copying.
            }

            var pwBufPtr = PipeWireNative.pw_stream_dequeue_buffer(_stream);
            if (pwBufPtr == IntPtr.Zero) return;

            try
            {
                var pwBuf = Marshal.PtrToStructure<PipeWireNative.pw_buffer>(pwBufPtr);
                if (pwBuf.buffer == IntPtr.Zero) return;

                var spaBuf = Marshal.PtrToStructure<PipeWireNative.spa_buffer>(pwBuf.buffer);
                if (spaBuf.n_datas == 0 || spaBuf.datas == IntPtr.Zero) return;

                var data0 = Marshal.PtrToStructure<PipeWireNative.spa_data>(spaBuf.datas);

                if (data0.data == IntPtr.Zero || data0.maxsize == 0) return;

                // If we don’t know width/height, we can’t build Mat correctly.
                // In practice, portal should provide size; if not, set CaptureRegion manually or tune negotiation POD.
                if (ScreenWidth <= 0 || ScreenHeight <= 0)
                {
                    // Best-effort guess: try derive from maxsize assuming 4 bytes per pixel and 16:9.
                    // You should replace this with real negotiation parsing if portal doesn’t provide size.
                    var pixels = (int)(data0.maxsize / BytesPerPixel);
                    ScreenWidth = 1920;
                    ScreenHeight = Math.Max(1, pixels / ScreenWidth);
                }

                bool doOne = Interlocked.Exchange(ref _pendingCaptureOnce, 0) == 1;

                if (EnableFullScreen || doOne)
                {
                    // Create a Mat that COPIES the frame into managed-owned memory.
                    // (We do NOT wrap PipeWire memory directly because lifetime is owned by PipeWire buffer queue.)
                    var full = new Mat(ScreenHeight, ScreenWidth, MatType.CV_8UC4);
                    Buffer.MemoryCopy((void*)data0.data, (void*)full.Data, full.Total() * full.ElemSize(), full.Total() * full.ElemSize());

                    OnFullScreenUpdated?.Invoke(full);
                }

                if (EnableRegion)
                {
                    var r = CaptureRegion;
                    if (r.Width <= 0 || r.Height <= 0)
                    {
                        r = new Rect(0, 0, ScreenWidth, ScreenHeight);
                    }

                    // Clamp
                    int x = Math.Clamp(r.X, 0, ScreenWidth - 1);
                    int y = Math.Clamp(r.Y, 0, ScreenHeight - 1);
                    int w = Math.Clamp(r.Width, 1, ScreenWidth - x);
                    int h = Math.Clamp(r.Height, 1, ScreenHeight - y);

                    // Copy full frame to allow ROI extraction safely (still one copy).
                    var full = new Mat(ScreenHeight, ScreenWidth, MatType.CV_8UC4);
                    Buffer.MemoryCopy((void*)data0.data, (void*)full.Data, full.Total() * full.ElemSize(), full.Total() * full.ElemSize());

                    var roi = new Mat(full, new OpenCvSharp.Rect(x, y, w, h));
                    // Clone so consumer owns region without depending on 'full'
                    var region = roi.Clone();

                    OnRegionUpdated?.Invoke(region);

                    full.Dispose(); // roi is view; region is clone.
                }
            }
            finally
            {
                PipeWireNative.pw_stream_queue_buffer(_stream, pwBufPtr);
            }
        }
        catch
        {
            // Avoid throwing across native callback boundary
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (Interlocked.Exchange(ref _running, 0) == 0) return;

            try
            {
                if (_loop != IntPtr.Zero)
                    PipeWireNative.pw_thread_loop_stop(_loop);
            }
            catch { /* best effort */ }

            if (_stream != IntPtr.Zero)
            {
                PipeWireNative.pw_stream_destroy(_stream);
                _stream = IntPtr.Zero;
            }

            if (_core != IntPtr.Zero)
            {
                PipeWireNative.pw_core_disconnect(_core);
                _core = IntPtr.Zero;
            }

            if (_context != IntPtr.Zero)
            {
                PipeWireNative.pw_context_destroy(_context);
                _context = IntPtr.Zero;
            }

            if (_loop != IntPtr.Zero)
            {
                PipeWireNative.pw_thread_loop_destroy(_loop);
                _loop = IntPtr.Zero;
            }

            if (_pwFdHandle is not null)
            {
                _pwFdHandle.Dispose();
                _pwFdHandle = null;
            }

            if (_portal is not null)
            {
                _portal.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _portal = null;
            }
        }
    }

    private static int GetFdFromSafeHandle(SafeHandle h)
    {
        // Tmds.DBus uses SafeHandle for unix fds; in practice this is a SafeFileHandle-like wrapper.
        // This method works for SafeHandle types exposing DangerousGetHandle() as fd integer.
        var fdPtr = h.DangerousGetHandle();
        return fdPtr.ToInt32();
    }
}

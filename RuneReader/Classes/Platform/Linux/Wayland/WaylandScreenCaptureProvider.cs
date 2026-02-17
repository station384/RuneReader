#if LINUX
using System;
using System.Diagnostics;
using OpenCvSharp;


namespace RuneReader.Classes.Platform.Linux.Wayland;

/// <summary>
/// Option A:
/// xdg-desktop-portal ScreenCast -> PipeWire fd + node id -> GStreamer pipewiresrc -> appsink -> OpenCvSharp Mat.
/// Works on GNOME/KDE Wayland (with portal).
/// </summary>
public sealed class WaylandScreenCaptureProvider : RuneReader.Classes.Platform.IScreenCaptureProvider
{
    // --- IScreenCaptureProvider ---
    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public int ScreenNumber { get; private set; } = 0;

    public Rect CaptureRegion { get; set; } = new Rect(0, 0, 128, 128);
    public bool EnableRegion { get; set; } = true;
    public bool EnableFullScreen { get; set; } = false;

    public event Action<Mat>? OnRegionUpdated;
    public event Action<Mat>? OnFullScreenUpdated;

    // --- Portal + GStreamer state ---
    private readonly object _gate = new object();
    private bool _disposed;

    private PortalScreenCastSession? _portal;

    //private SafeHandle _pwFd ;
    private uint _nodeId;

    private nint _pipeline = nint.Zero;
    private nint _appSink = nint.Zero;

    // We keep a buffer for conversion to Mat to avoid re-allocating huge arrays repeatedly.
    private int _frameStrideBytes;
    private int _frameWidth;
    private int _frameHeight;
    private int _pwFdInt = -1;
    private long _lastEmitTicks;
    public int ForceEmitEveryMs { get; set; } = 100;

    // Controls whether CaptureOnce blocks briefly or is purely opportunistic.
    // 0 = don't block; small positive = wait up to N ms for a sample.
    // ReSharper disable once MemberCanBePrivate.Global
    public int PullSampleTimeoutMs { get; set; } = 2;

    public WaylandScreenCaptureProvider(int screenNumber = 0)
    {
        ScreenNumber = screenNumber;
    }
    private Mat? _fullMat; // reusable backing buffer (never given to consumers)
        
    private ulong _lastSig = 0;


    private static unsafe ulong ComputeImageSignature(byte* p, int width, int height, int stride)
    {
        // Sample 16 points distributed in the image (4x4 grid)
        ulong sig = 1469598103934665603UL;

        int gx = 4, gy = 4;
        for (int y = 0; y < gy; y++)
        {
            int py = (height - 1) * y / (gy - 1);
            byte* row = p + py * stride;

            for (int x = 0; x < gx; x++)
            {
                int px = (width - 1) * x / (gx - 1);
                int i = px * 3; // BGR
                byte b0 = row[i + 0];
                byte b1 = row[i + 1];
                byte b2 = row[i + 2];

                sig ^= b0; sig *= 1099511628211UL;
                sig ^= b1; sig *= 1099511628211UL;
                sig ^= b2; sig *= 1099511628211UL;
            }
        }

        sig ^= (ulong)width;  sig *= 1099511628211UL;
        sig ^= (ulong)height; sig *= 1099511628211UL;
        return sig;
    }
    
    
    public void CaptureOnce()
    {
        ThrowIfDisposed();

        // We want CaptureOnce to be sync (your interface), but we need lazy init.
        EnsureStarted();

        // Pull a single frame from appsink.
        if (_appSink == nint.Zero || _pipeline == nint.Zero)
            return;
            
        var timeoutNs = (ulong)Math.Max(0, PullSampleTimeoutMs) * 1_000_000UL;
        nint sample = GstNative.gst_app_sink_try_pull_sample(_appSink, timeoutNs);
            
        if (sample == nint.Zero)
            return;

        try
        {
            // Extract buffer + caps info, map, copy bytes
            nint buffer = GstNative.gst_sample_get_buffer(sample);
            nint caps = GstNative.gst_sample_get_caps(sample);

            if (buffer == nint.Zero || caps == nint.Zero)
                return;

            if (!TryUpdateNegotiatedFormat(caps))
                return;

            if (!GstNative.gst_buffer_map(buffer, out var map, GstNative.GST_MAP_READ))
                return;
   
            try
            {
                    
                // Ensure backing mat exists (reused)
                if (_fullMat == null || _fullMat.Width != _frameWidth || _fullMat.Height != _frameHeight)
                {
                    _fullMat?.Dispose();
                    _fullMat = new Mat(_frameHeight, _frameWidth, MatType.CV_8UC3);
                }
                unsafe
                {
                    int srcStride = _frameStrideBytes;
                    int dstStride = (int)_fullMat.Step();
                    int rowBytes  = _frameWidth * 3;

                    // If we’re tightly packed and strides match, fast bulk copy.
                    if (srcStride == dstStride && (long)map.size >= (long)srcStride * _frameHeight)
                    {
                        int bytes = srcStride * _frameHeight;
                        Buffer.MemoryCopy((void*)map.data, (void*)_fullMat.Data, bytes, bytes);
                    }
                    else
                    {
                        // Safe row copy (handles padding)
                        if ((long)map.size < (long)srcStride * _frameHeight)
                        {
                            Debug.WriteLine($"[WaylandScreenCaptureProvider] Short buffer: map.size={map.size}, need={srcStride * _frameHeight}");
                            return;
                        }

                        byte* src = (byte*)map.data;
                        byte* dst = (byte*)_fullMat.Data;

                        for (int y = 0; y < _frameHeight; y++)
                            Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                    }
                    ulong sig = ComputeImageSignature((byte*)_fullMat.Data, _frameWidth, _frameHeight, (int)_fullMat.Step());
                    if (sig == _lastSig)
                    {
                        if (ForceEmitEveryMs <= 0) return;
                        long now = Environment.TickCount64;
                        // drop a new frame every x Ms to so it isn't just static forever.
                        if (now - _lastEmitTicks < ForceEmitEveryMs) return;
                    }
                    _lastSig = sig;
                    _lastEmitTicks = Environment.TickCount64;
           
                    
                    ScreenWidth = _frameWidth;
                    ScreenHeight = _frameHeight;

                    if (EnableFullScreen)
                    {
                        // consumer-owned copy
                        OnFullScreenUpdated?.Invoke(_fullMat.Clone());
                    }

                    if (EnableRegion)
                    {
                        var r = NormalizeRegion(CaptureRegion, _frameWidth, _frameHeight);
                        if (r is { Width: > 0, Height: > 0 })
                        {
                            using var roi = new Mat(_fullMat, r);
                            // consumer-owned copy
                            OnRegionUpdated?.Invoke(roi.Clone());
                        }
                    }
                }
            }
            finally
            {
                GstNative.gst_buffer_unmap(buffer, ref map);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WaylandScreenCaptureProvider] CaptureOnce error: {ex}");
        }
        finally
        {
            GstNative.gst_sample_unref(sample);
        }
    }

    private void EnsureStarted()
    {
        // Fast path
        lock (_gate)
        {
            if (_pipeline != nint.Zero && _appSink != nint.Zero)
                return;
        }

        // Slow path init (only once)
        lock (_gate)
        {
            if (_pipeline != nint.Zero && _appSink != nint.Zero)
                return;

            StartPortalAndPipeline();
        }
    }

        
    private void StartPortalAndPipeline()
    {
        ThrowIfDisposed();

        // 1) Start portal session (sync-over-async, but only once at startup)
        // This triggers user permission picker.
        _portal ??= PortalScreenCastSession.CreateAndStartAsync(ScreenNumber).GetAwaiter().GetResult();

        //_pwFd = _portal.PipeWireHandle;
        int fdInt = _portal.PipeWireHandle.DangerousGetHandle().ToInt32();
        Debug.WriteLine($"PipeWire fd int={fdInt}");
        _pwFdInt = _portal.PipeWireHandle.DangerousGetHandle().ToInt32();
            
        _nodeId = _portal.NodeId;
        //_sessionHandle = _portal.SessionHandle;

        // 2) Start GStreamer pipeline
        GstNative.gst_init(nint.Zero, nint.Zero);

        // Use pipewiresrc using the fd+node.
        // Note: "path" is used by pipewiresrc to select the node. For portal screencast, passing the numeric node id works.
        // We force BGR to avoid extra conversion later.
        string pipelineDesc =
            $"pipewiresrc fd={_pwFdInt} path={_nodeId} do-timestamp=true ! " +
            $"videoconvert n-threads=1 ! video/x-raw,format=BGR ! " +
            $"appsink name=sink emit-signals=false sync=false max-buffers=1 drop=true";

        _pipeline = GstNative.gst_parse_launch(pipelineDesc, out var err);
        if (_pipeline == nint.Zero)
        {
            string msg = err != nint.Zero ? GstNative.GErrorToStringAndFree(err) : "unknown error";
            throw new InvalidOperationException($"Failed to create GStreamer pipeline: {msg}");
        }

        _appSink = GstNative.gst_bin_get_by_name(_pipeline, "sink");
        if (_appSink == nint.Zero)
        {
            StopPipeline_NoThrow();
            throw new InvalidOperationException("Failed to find appsink named 'sink' in pipeline.");
        }

        var state = GstNative.gst_element_set_state(_pipeline, GstNative.GST_STATE_PLAYING);
        if (state == GstNative.GST_STATE_CHANGE_FAILURE)
        {
            StopPipeline_NoThrow();
            throw new InvalidOperationException("Failed to set pipeline to PLAYING.");
        }
    }

    private bool TryUpdateNegotiatedFormat(nint caps)
    {
        nint s = GstNative.gst_caps_get_structure(caps, 0);
        if (s == nint.Zero) return false;

        int w, h;
        if (!GstNative.gst_structure_get_int(s, "width", out w)) return false;
        if (!GstNative.gst_structure_get_int(s, "height", out h)) return false;

        int stride = w * 3;

        // Some elements expose "stride" or "rowstride" (not always present).
        if (GstNative.gst_structure_get_int(s, "stride", out var capStride) && capStride > 0)
            stride = capStride;
        else if (GstNative.gst_structure_get_int(s, "rowstride", out var capRowStride) && capRowStride > 0)
            stride = capRowStride;

        _frameWidth = w;
        _frameHeight = h;
        _frameStrideBytes = stride;
        return true;
    }
    
    private static OpenCvSharp.Rect NormalizeRegion(Rect r, int maxW, int maxH)
    {
        int x = Clamp((int)r.X, 0, maxW);
        int y = Clamp((int)r.Y, 0, maxH);
        int w = Clamp((int)r.Width, 0, maxW - x);
        int h = Clamp((int)r.Height, 0, maxH - y);
        return new OpenCvSharp.Rect(x, y, w, h);
    }

    private static int Clamp(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private void StopPipeline_NoThrow()
    {
        try
        {
            if (_pipeline != nint.Zero)
            {
                GstNative.gst_element_set_state(_pipeline, GstNative.GST_STATE_NULL);
            }
        }
        catch { /* ignore */ }

        try
        {
            if (_appSink != nint.Zero)
                GstNative.gst_object_unref(_appSink);
        }
        catch { /* ignore */ }

        try
        {
            if (_pipeline != nint.Zero)
                GstNative.gst_object_unref(_pipeline);
        }
        catch { /* ignore */ }

        _appSink = nint.Zero;
        _pipeline = nint.Zero;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        StopPipeline_NoThrow();

        try { _portal?.Dispose(); } catch { /* ignore */ }
        _fullMat?.Dispose();
        _fullMat = null;
        _portal = null;
        //_frameBuffer = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WaylandScreenCaptureProvider));
    }


}

#endif
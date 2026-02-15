#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Tmds.DBus;


namespace RuneReader.Classes.Platform.Linux.Wayland
{
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

        private Connection? _bus;
        private PortalScreenCastSession? _portal;

        private uint _pwFd = uint.MinValue;
        private uint _nodeId;
        private string? _sessionHandle;

        private nint _pipeline = nint.Zero;
        private nint _appsink = nint.Zero;

        // We keep a buffer for conversion to Mat to avoid re-allocating huge arrays repeatedly.
        private byte[]? _frameBuffer;
        private int _frameStrideBytes;
        private int _frameWidth;
        private int _frameHeight;

        // Controls whether CaptureOnce blocks briefly or is purely opportunistic.
        // 0 = don't block; small positive = wait up to N ms for a sample.
        public int PullSampleTimeoutMs { get; set; } = 0;

        public WaylandScreenCaptureProvider(int screenNumber = 0)
        {
            ScreenNumber = screenNumber;
        }

        public void CaptureOnce()
        {
            ThrowIfDisposed();

            // We want CaptureOnce to be sync (your interface), but we need lazy init.
            EnsureStarted();

            // Pull a single frame from appsink.
            if (_appsink == nint.Zero || _pipeline == nint.Zero)
                return;

            var timeoutNs = PullSampleTimeoutMs <= 0 ? 0UL : (ulong)PullSampleTimeoutMs * 1_000_000UL;
            nint sample = timeoutNs == 0
                ? GstNative.gst_app_sink_try_pull_sample(_appsink, 0)
                : GstNative.gst_app_sink_try_pull_sample(_appsink, timeoutNs);

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
                    int size = checked((int)map.size);
                    EnsureFrameBuffer(size);

                    Marshal.Copy(map.data, _frameBuffer!, 0, size);

                    // Construct a Mat from the buffer (BGR)
                    // IMPORTANT: We create consumer-owned Mats for events (Clone/Copy).
                    using var full = new Mat(_frameHeight, _frameWidth, MatType.CV_8UC3);
                    full.SetArray( _frameBuffer!);

                    ScreenWidth = _frameWidth;
                    ScreenHeight = _frameHeight;

                    if (EnableFullScreen)
                    {
                        // consumer-owned copy
                        var outFull = full.Clone();
                        OnFullScreenUpdated?.Invoke(outFull);
                    }

                    if (EnableRegion)
                    {
                        var r = NormalizeRegion(CaptureRegion, _frameWidth, _frameHeight);
                        if (r.Width > 0 && r.Height > 0)
                        {
                            using var roi = new Mat(full, r);
                            var outRegion = roi.Clone(); // consumer-owned
                            OnRegionUpdated?.Invoke(outRegion);
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
                if (_pipeline != nint.Zero && _appsink != nint.Zero)
                    return;
            }

            // Slow path init (only once)
            lock (_gate)
            {
                if (_pipeline != nint.Zero && _appsink != nint.Zero)
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

            _pwFd = _portal.PipeWireFd;
            _nodeId = _portal.NodeId;
            _sessionHandle = _portal.SessionHandle;

            // 2) Start GStreamer pipeline
            GstNative.gst_init(nint.Zero, nint.Zero);

            // Use pipewiresrc using the fd+node.
            // Note: "path" is used by pipewiresrc to select the node. For portal screencast, passing the numeric node id works.
            // We force BGR to avoid extra conversion later.
            string pipelineDesc =
                $"pipewiresrc fd={_pwFd} path={_nodeId} do-timestamp=true ! " +
                $"videoconvert ! video/x-raw,format=BGR ! " +
                $"appsink name=sink emit-signals=false sync=false max-buffers=1 drop=true";

            nint err = nint.Zero;
            _pipeline = GstNative.gst_parse_launch(pipelineDesc, out err);
            if (_pipeline == nint.Zero)
            {
                string msg = err != nint.Zero ? GstNative.GErrorToStringAndFree(err) : "unknown error";
                throw new InvalidOperationException($"Failed to create GStreamer pipeline: {msg}");
            }

            _appsink = GstNative.gst_bin_get_by_name(_pipeline, "sink");
            if (_appsink == nint.Zero)
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
            // caps: video/x-raw, format=BGR, width=..., height=...
            nint s = GstNative.gst_caps_get_structure(caps, 0);
            if (s == nint.Zero)
                return false;

            int w, h;
            if (!GstNative.gst_structure_get_int(s, "width", out w)) return false;
            if (!GstNative.gst_structure_get_int(s, "height", out h)) return false;

            // BGR: 3 bytes per pixel
            int stride = checked(w * 3);

            _frameWidth = w;
            _frameHeight = h;
            _frameStrideBytes = stride;
            return true;
        }

        private void EnsureFrameBuffer(int requiredSize)
        {
            if (_frameBuffer == null || _frameBuffer.Length < requiredSize)
                _frameBuffer = new byte[requiredSize];
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
                if (_appsink != nint.Zero)
                    GstNative.gst_object_unref(_appsink);
            }
            catch { /* ignore */ }

            try
            {
                if (_pipeline != nint.Zero)
                    GstNative.gst_object_unref(_pipeline);
            }
            catch { /* ignore */ }

            _appsink = nint.Zero;
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
            _portal = null;

            // Close fd if we own it
            try
            {
                if (_pwFd > 0)
                    close(_pwFd);
            }
            catch { /* ignore */ }
            _pwFd = uint.MinValue;

            _frameBuffer = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WaylandScreenCaptureProvider));
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int close(uint fd);
    }
}

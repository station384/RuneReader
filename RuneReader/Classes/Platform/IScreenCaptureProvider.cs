using System;
using OpenCvSharp;

namespace RuneReader.Classes.Platform;

public interface IScreenCaptureProvider : IDisposable
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    int ScreenNumber { get; } // The monitor to capture
    
    Rect CaptureRegion { get; set; }  // region to capture (zone1)
    bool EnableRegion { get; set; }
    bool EnableFullScreen { get; set; }
    

    // Trigger a capture of currently enabled channels
    void CaptureOnce();

    // Latest snapshots (provider-owned)
    // Mat LastRegion { get; }
    // Mat LastFullScreen { get; }

    // Fire when a new snapshot is available (consumer-owned)
    event Action<Mat>? OnRegionUpdated;
    event Action<Mat>? OnFullScreenUpdated;
}
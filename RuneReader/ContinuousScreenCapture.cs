// ContinuousScreenCapture.cs
// Task + CancellationToken version (replaces Thread + async void)
//
// Notes:
// - StartCapture() is non-blocking (fires the loop on the ThreadPool).
// - StopCaptureAsync() is the preferred stop (await it from UI if you can).
// - StopCapture() is kept for compatibility and blocks until fully stopped.
// - This class does NOT dispose the platform capture provider by default—
//   keep that in the owner (MainWindow/platform services) unless you want it here.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RuneReader.Classes.Platform; // adjust namespace if yours differs

namespace RuneReader // adjust to your current namespace
{
    public sealed class ContinuousScreenCapture : IDisposable
    {
        private readonly IScreenCaptureProvider _screenCaptureProvider;

        private readonly object _gate = new();

        private CancellationTokenSource? _cts;
        private Task? _captureTask;

        // milliseconds between CaptureOnce() calls
        private int _captureIntervalMs;

        public bool IsCapturing
        {
            get
            {
                lock (_gate)
                    return _captureTask != null && !_captureTask.IsCompleted;
            }
        }

        public int CaptureIntervalMs
        {
            get
            {
                lock (_gate) return _captureIntervalMs;
            }
            set
            {
                lock (_gate) _captureIntervalMs = Math.Max(1, value);
            }
        }

        public ContinuousScreenCapture(IScreenCaptureProvider screenCaptureProvider, int captureIntervalMs = 33)
        {
            _screenCaptureProvider = screenCaptureProvider ?? throw new ArgumentNullException(nameof(screenCaptureProvider));
            _captureIntervalMs = Math.Max(1, captureIntervalMs);
        }

        public void StartCapture()
        {
            lock (_gate)
            {
                if (_captureTask != null && !_captureTask.IsCompleted)
                    return;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                _captureTask = Task.Run(() => CaptureLoopAsync(token), token);
            }
        }

        // Preferred stop: awaitable and non-blocking for UI code
        public async Task StopCaptureAsync()
        {
            CancellationTokenSource? cts;
            Task? task;

            lock (_gate)
            {
                cts = _cts;
                task = _captureTask;
                _cts = null;
                _captureTask = null;
            }

            if (cts == null || task == null)
                return;

            try
            {
                cts.Cancel();
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on cancel
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                cts.Dispose();
            }
        }

        // Compatibility stop: blocks until the loop exits
        public void StopCapture()
        {
            StopCaptureAsync().GetAwaiter().GetResult();
        }

        private async Task CaptureLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _screenCaptureProvider.CaptureOnce();
                }
                catch (Exception ex)
                {
                    // If you want special handling for DXGI wait timeouts, do it inside the provider.
                    Debug.WriteLine(ex);
                }

                int delay;
                lock (_gate) delay = _captureIntervalMs;

                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                StopCapture();
            }
            catch
            {
                // swallow on dispose
            }

            // NOTE: leaving capture provider disposal to the owner.
            // If you want this class to own it, uncomment:
            // _screenCaptureProvider.Dispose();
        }
    }
}




// using OpenCvSharp;
// using System;
// using System.Diagnostics;
// using System.Threading;
// using System.Threading.Tasks;
// using Avalonia.Threading;
// using RuneReader.Classes.Platform;
//
// namespace RuneReader
// {
//     
//     // This now acts as a helper that triggers the capture of images.   It doesn't handle any images directly anymore.  it just triggers the grabbing.
//     // using whatever screen capture provider is provided.
//     public class ContinuousScreenCapture : IDisposable
//     {
//         private Thread _captureThread;
//         private volatile bool _isCapturing;
//         private int _captureInterval; // Interval in milliseconds
//         private readonly IScreenCaptureProvider _screenWindowsCapture; // Instance of CaptureScreen class
//         private readonly object _intervalLock = new();
//         
//
//         public  bool IsCapturing { get { return _isCapturing; } }
//
//         private Thread CreateCaptureThread()
//         {
//             return  new Thread(CaptureLoop)
//             {
//                 IsBackground = false // Set the thread as a background thread
//                 ,Priority = ThreadPriority.Normal
//             };
//
//         }
//         
//         public ContinuousScreenCapture(int interval,  IScreenCaptureProvider screenCapture)
//         {
//             _captureInterval = interval;
//             _screenWindowsCapture = screenCapture;
//             _captureThread = CreateCaptureThread();
//         }
//
//         public int CaptureInterval
//         {
//             get
//             {
//                 lock (_intervalLock)
//                 {
//                     return _captureInterval;
//                 }
//             }
//             set
//             {
//                 lock (_intervalLock)
//                 {
//                     _captureInterval = value;
//                 }
//             }
//         }
//
//         public void StartCapture()
//         {
//             if (!_isCapturing)
//             {
//                 _isCapturing = true;
//                 if (_captureThread.ThreadState == System.Threading.ThreadState.Stopped)
//                 {
//                     _captureThread = CreateCaptureThread();
//                 }
//                 _captureThread.Start();
//             }
//         }
//
//         public void StopCapture()
//         {
//             if (_isCapturing)
//             {
//                 _isCapturing = false;
//                 _captureThread.Join(); // Wait for it to finish.
//             }
//         }
//
//         private async void CaptureLoop()
//         {
//             if (_screenWindowsCapture == null)
//             {
//                 throw new Exception("screenCapture cannot be NULL");
//             }
//
//             while (_isCapturing)
//             {
//      
//                try
//                {
//                    _screenWindowsCapture.CaptureOnce();
//                         // Marshal back to the UI thread when possible.
//                         // if (_uiDispatcher != null)
//                         //     _uiDispatcher.Post(() => UpdateFirstImage?.Invoke(capturedImage));
//                         // else
//                         //     Dispatcher.UIThread.Post(() => UpdateFirstImage?.Invoke(capturedImage));
//                }
//                catch (Exception ex)
//                {
//                    Debug.WriteLine(ex);
//                    _isCapturing = true;
//                }
//          
//                 // Use the latest interval value
//                 int sleepTime;
//                 lock (_intervalLock)
//                 {
//                     sleepTime = _captureInterval;
//                 }
//                 
//                 await Task.Delay(sleepTime);
//             }
//             Debug.WriteLine("Capturing Stopped");
//         }
//
//
//         public void Dispose()
//         {
//             StopCapture();
//
//
//
//         }
//     }
// }

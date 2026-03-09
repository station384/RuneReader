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
    public sealed class ContinuousScreenCapture(
        IScreenCaptureProvider screenCaptureProvider,
        int captureIntervalMs = 33)
        : IDisposable
    {
        private readonly IScreenCaptureProvider _screenCaptureProvider = screenCaptureProvider ?? throw new ArgumentNullException(nameof(screenCaptureProvider));

        private readonly object _gate = new();

        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private int _disposed;
        private bool IsDisposed => Volatile.Read(ref _disposed) == 1;
        // milliseconds between CaptureOnce() calls
        private int _captureIntervalMs = Math.Max(1, captureIntervalMs);

        public bool IsCapturing
        {
            get
            {
                //lock (_gate)
                    return _captureTask != null && !_captureTask.IsCompleted;
            }
        }

        public int CaptureIntervalMs
        {
            get
            {
                //lock (_gate) 
                    return _captureIntervalMs;
            }
            set
            {
                //lock (_gate) 
                    _captureIntervalMs = Math.Max(1, value);
            }
        }

        public void StartCapture()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(ContinuousScreenCapture));
            //lock (_gate)
            {
                if (_captureTask is { IsCompleted: false })
                    return;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;

               // _captureTask = Task.Run(() => CaptureLoopAsync(token), token);
               _captureTask = CaptureLoopAsync(token);
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
                await cts.CancelAsync();
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
            await Task.Yield();
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
                //lock (_gate) 
                    delay = _captureIntervalMs;

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
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            try
            {
                StopCaptureAsync().GetAwaiter().GetResult();
                //StopCapture();
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




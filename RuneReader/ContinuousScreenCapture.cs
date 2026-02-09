using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using RuneReader.Classes.Platform;

namespace RuneReader
{
    
    // This now acts as a helper that triggers the capture of images.   It doesn't handle any images directly anymore.  it just triggers the grabbing.
    // using whatever screen capture provider is provided.
    public class ContinuousScreenCapture : IDisposable
    {
        private Thread _captureThread;
        private volatile bool _isCapturing;
        private int _captureInterval; // Interval in milliseconds
        private readonly IScreenCaptureProvider _screenWindowsCapture; // Instance of CaptureScreen class
        private readonly object _intervalLock = new();
        

        public  bool IsCapturing { get { return _isCapturing; } }

        private Thread CreateCaptureThread()
        {
            return  new Thread(CaptureLoop)
            {
                IsBackground = false // Set the thread as a background thread
                ,Priority = ThreadPriority.Normal
            };

        }
        
        public ContinuousScreenCapture(int interval,  IScreenCaptureProvider screenCapture)
        {
            _captureInterval = interval;
            _screenWindowsCapture = screenCapture;
            _captureThread = CreateCaptureThread();
        }

        public int CaptureInterval
        {
            get
            {
                lock (_intervalLock)
                {
                    return _captureInterval;
                }
            }
            set
            {
                lock (_intervalLock)
                {
                    _captureInterval = value;
                }
            }
        }

        public void StartCapture()
        {
            if (!_isCapturing)
            {
                _isCapturing = true;
                if (_captureThread.ThreadState == System.Threading.ThreadState.Stopped)
                {
                    _captureThread = CreateCaptureThread();
                }
                _captureThread.Start();
            }
        }

        public void StopCapture()
        {
            if (_isCapturing)
            {
                _isCapturing = false;
                _captureThread.Join(); // Wait for it to finish.
            }
        }

        private async void CaptureLoop()
        {
            if (_screenWindowsCapture == null)
            {
                throw new Exception("screenCapture cannot be NULL");
            }

            while (_isCapturing)
            {
     
               try
               {
                   _screenWindowsCapture.CaptureOnce();
                        // Marshal back to the UI thread when possible.
                        // if (_uiDispatcher != null)
                        //     _uiDispatcher.Post(() => UpdateFirstImage?.Invoke(capturedImage));
                        // else
                        //     Dispatcher.UIThread.Post(() => UpdateFirstImage?.Invoke(capturedImage));
               }
               catch (Exception ex)
               {
                   Debug.WriteLine(ex);
                   _isCapturing = true;
               }
         
                // Use the latest interval value
                int sleepTime;
                lock (_intervalLock)
                {
                    sleepTime = _captureInterval;
                }
                
                await Task.Delay(sleepTime);
            }
            Debug.WriteLine("Capturing Stopped");
        }


        public void Dispose()
        {
            StopCapture();



        }
    }
}

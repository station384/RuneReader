using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace RuneReader
{
    public class ContinuousScreenCapture
    {
        private Thread captureThread;
        private volatile bool isCapturing;
        private int captureInterval; // Interval in milliseconds
        private Dispatcher uiDispatcher;
        private CaptureScreen screenCapture; // Instance of CaptureScreen class
        private readonly object intervalLock = new object();

        public delegate void UpdateFirstImageDelegate(Mat image);
        public event UpdateFirstImageDelegate UpdateFirstImage;

        private OpenCvSharp.Rect _captureRegion;
        public OpenCvSharp.Rect CaptureRegion
        {
            get
            {
                return _captureRegion;
                //    screenCapture.CaptureRegion;// _captureRegion;
            }
            set
            {
                screenCapture.CaptureRegion = value;
                _captureRegion = screenCapture.CaptureRegion;
            }
        }
        public  bool IsCapturing { get { return isCapturing; } }

        private Thread CreateCaptureThread()
        {
            return  new Thread(CaptureLoop)
            {
                IsBackground = false // Set the thread as a background thread
,
                Priority = ThreadPriority.AboveNormal
            };

        }
        public ContinuousScreenCapture(int interval, Dispatcher uiDispatcher, CaptureScreen captureScreen)
        {
            this.captureInterval = interval;
            this.uiDispatcher = uiDispatcher;
            this.screenCapture = captureScreen;
            this._captureRegion = captureScreen.CaptureRegion;
            captureThread = CreateCaptureThread();
        }

        public int CaptureInterval
        {
            get
            {
                lock (intervalLock)
                {
                    return captureInterval;
                }
            }
            set
            {
                lock (intervalLock)
                {
                    captureInterval = value;
                }
            }
        }

        public void StartCapture()
        {
            if (isCapturing == false)
            {
                isCapturing = true;
                if (captureThread.ThreadState == System.Threading.ThreadState.Stopped)
                {
                    captureThread = CreateCaptureThread();
                }
                captureThread.Start();
            }
        }

        public void StopCapture()
        {
            if (isCapturing == true)
            {

                isCapturing = false;
               
            }
        }

        private async void CaptureLoop()
        {
            if (screenCapture == null)
            {
                throw new Exception("screenCapture cannot be NULL");
            }

            while (isCapturing)
            {
                var results = await screenCapture.GrabScreen();
                
                Mat capturedImage = screenCapture.CapturedImageFirst; // Implement this to capture the screen
                try
                {
                    uiDispatcher.Invoke(() =>
                    {
                        UpdateFirstImage?.Invoke(capturedImage);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    isCapturing = true;
                }
                // Use the latest interval value
                int sleepTime;
                lock (intervalLock)
                {
                    sleepTime = captureInterval;
                }

                Thread.Sleep(sleepTime);
            }
            Debug.WriteLine("Capturing Stopped");
        }
    }
}

using System;
using System.Drawing;
using System.Threading;
using System.Windows;
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

        public delegate void UpdateFirstImageDelegate(Bitmap image);
        public event UpdateFirstImageDelegate UpdateFirstImage;

        public delegate void UpdateSecondImageDelegate(Bitmap image);
        public event UpdateSecondImageDelegate UpdateSecondImage;


        private Rect[] _captureRegion;
        public Rect[] CaptureRegion { 
            get {
                return _captureRegion;
                //    screenCapture.CaptureRegion;// _captureRegion;
            }
            set
            {
              
                screenCapture.CaptureRegion = new Rect[2]
                {
                    value[0],
                    value[1]
                };
                _captureRegion = screenCapture.CaptureRegion;
            }
            }
        public bool IsCapturing {get { return isCapturing; }}
        
        public ContinuousScreenCapture(int interval, Dispatcher uiDispatcher, CaptureScreen captureScreen)
        {
            this.captureInterval = interval;
            this.uiDispatcher = uiDispatcher;
            this.screenCapture = captureScreen;
            this._captureRegion = captureScreen.CaptureRegion;
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
                captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = false // Set the thread as a background thread
                    , Priority = ThreadPriority.AboveNormal
                    
                   
                };
            captureThread.Start();
            }
        }

        public void StopCapture()
        {
            if (isCapturing == true)
            {

                isCapturing = false;
                if (captureThread != null && captureThread.IsAlive)
                {
                   // captureThread.Join(); // Wait for the thread to finish
                }
            }
        }

        private void CaptureLoop()
        {
      
            while (isCapturing)
            {
                screenCapture.GrabScreen();
                Bitmap capturedImage = screenCapture.CapturedImageFirst; // Implement this to capture the screen
                Bitmap capturedImage2 = screenCapture.CapturedImageSecond; // Implement this to capture the screen

                try
                {
                    uiDispatcher.Invoke(() =>
                    {
                        UpdateFirstImage?.Invoke(capturedImage);
                        UpdateSecondImage?.Invoke(capturedImage2);
                    });
                   

                }
                catch (Exception ex)
                {
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
           // Thread.Sleep(100);
        }
    }
}

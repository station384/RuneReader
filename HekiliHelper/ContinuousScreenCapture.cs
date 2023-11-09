using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HekiliHelper
{
    public class ContinuousScreenCapture
    {
        private Thread captureThread;
        private bool isCapturing;
        private int captureInterval; // Interval in milliseconds
        private Dispatcher uiDispatcher;
        private CaptureScreen screenCapture; // Instance of CaptureScreen class
        private readonly object intervalLock = new object();

        public delegate void UpdateUIImageDelegate(Bitmap image);
        public event UpdateUIImageDelegate UpdateUIImage;

        private Rect _captureRegion;
        public Rect CaptureRegion { get {
                return _captureRegion;
            }
            set
            {
                _captureRegion = value;
                screenCapture.CaptureRegion = value;
                if (isCapturing)
                {
//                    StopCapture();
//                    StartCapture();
                };
            }
            }
        public bool IsCapturing {get { return isCapturing; }}
        
        public ContinuousScreenCapture(int interval, Dispatcher uiDispatcher, CaptureScreen captureScreen)
        {
            this.captureInterval = interval;
            this.uiDispatcher = uiDispatcher;
            this.screenCapture = captureScreen;
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
            isCapturing = true;
            captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true // Set the thread as a background thread
            };
            captureThread.Start();
        }

        public void StopCapture()
        {
            isCapturing = false;
            if (captureThread != null && captureThread.IsAlive)
            {
                captureThread.Join(); // Wait for the thread to finish
            }
        }

        private void CaptureLoop()
        {
            while (isCapturing)
            {
                screenCapture.GrabScreen();
                 Bitmap capturedImage = screenCapture.CapturedImage; // Implement this to capture the screen

                uiDispatcher.Invoke(() =>
                {
                    UpdateUIImage?.Invoke(capturedImage);
                });

                // Use the latest interval value
                int sleepTime;
                lock (intervalLock)
                {
                    sleepTime = captureInterval;
                }
                Thread.Sleep(sleepTime);
            }
        }
    }
}

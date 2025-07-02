using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using OpenCvSharp;
using System.Windows.Media.Media3D;
using System.Runtime.InteropServices;



namespace RuneReader
{

    public class CaptureScreen
    {
        IScreenCaptureService screenCaptureService;
        IEnumerable<GraphicsCard> graphicsCards;
        IEnumerable<Display> displays;
        private System.Windows.Rect _captureRegion = new System.Windows.Rect();
        private int _maxHeight;
        private int _maxWidth;
        IScreenCapture screenCapture;
        ICaptureZone capZone1 = null;
        ICaptureZone capZoneFullScreen = null;


        private volatile Mat _CapturedImageFirst;
        private volatile Mat _CapturedFullScreen;

        public Mat CapturedImageFirst
        {
            get
            {
                return _CapturedImageFirst;
            }
            private set
            {
                // We have to manage the memory for the Mat images.  so its up to us to make sure we detroy the prior one
                // before setting the new one.
                if (_CapturedImageFirst != null)
                {
                    if (!_CapturedImageFirst.IsDisposed) _CapturedImageFirst.Dispose();
                }
                _CapturedImageFirst = value;

            }
        }


        public  Mat CapturedFullScreen
        {
            get
            {
                return  _CapturedFullScreen;
            }
            set
            {
                // We have to manage the memory for the Mat images.  so its up to us to make sure we detroy the prior one
                // before setting the new one.
                if (_CapturedFullScreen != null)
                {
                    if (!_CapturedFullScreen.IsDisposed) _CapturedFullScreen.Dispose();
                    GC.Collect();
                }
                _CapturedFullScreen = value;
            }
        }


        public System.Windows.Rect CaptureRegion
        {
            get => _captureRegion; set
            {
                if (_captureRegion == value) return;
                _captureRegion.X = (value.X >= 0 && value.X <= _maxWidth) ? value.X : 0;
                _captureRegion.Y = (value.Y >= 0 && value.Y <= _maxHeight) ? value.Y : 0;
                _captureRegion.Width = (value.Width >= 0 && value.Width <= _maxWidth) ? value.Width : 0;
                _captureRegion.Height = (value.Height >= 0 && value.Height <= _maxHeight) ? value.Height : 0;
                screenCapture.UpdateCaptureZone(capZone1, (int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
            }
        }





        public CaptureScreen(System.Windows.Rect Regions, int? downscaleLevel)
        {
            // Create a screen-capture service
            if (screenCaptureService == null)
            {
                screenCaptureService = new DX11ScreenCaptureService();
            }
            // Get all available graphics cards
            graphicsCards = screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in
            displays = screenCaptureService.GetDisplays(graphicsCards.First());

            // Create a screen-capture for all screens you want to capture
            screenCapture = screenCaptureService.GetScreenCapture(displays.First());
            _maxHeight = displays.First().Height;
            _maxWidth = displays.First().Width;


            _captureRegion = Regions;
            if (capZone1 == null)
            {
                capZone1 = screenCapture.RegisterCaptureZone((int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
                capZone1.Updated += CapZone1_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                capZone1.AutoUpdate = false;
        
            }

            if (capZoneFullScreen == null)
            {
                capZoneFullScreen = screenCapture.RegisterCaptureZone((int)0, (int)0, (int)_maxWidth, (int)_maxHeight, downscaleLevel: 0);
                capZoneFullScreen.Updated += CapZoneFullScreen_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                capZoneFullScreen.AutoUpdate = false;
            }
            _CapturedImageFirst = new Mat();
            _CapturedFullScreen = new Mat();
            capZone1.RequestUpdate();
        }


        // Holders just to keep track if events have fired.
        private bool _fullscreenUpdated = false;
        private bool _firstImageUpdated = false;

        /// <summary>
        /// Triggers a refresh of the screen grab and stores the image in CapturedImageFirst
        /// </summary>
        /// <returns>
        /// True otherwise exception
        /// </returns>
        public async Task<bool> GrabScreen()
        {
            // Capture the screen
            _firstImageUpdated = false;
            capZone1.RequestUpdate();
            screenCapture.CaptureScreen();
            // Doo dee doo dee doo...  lets wait for the image to be updated.
            while (!_firstImageUpdated)
            {
                await Task.Delay(1);
            }
            _firstImageUpdated = false;
            return true;
        }

        private void CapZoneFullScreen_Updated(object? sender, EventArgs e)
        {
            byte[]? pixelData = null;
            using (capZoneFullScreen.Lock())
            {
                pixelData = capZoneFullScreen.RawBuffer.ToArray();
            }

            CapturedFullScreen = Mat.FromPixelData(capZoneFullScreen.Height, capZoneFullScreen.Width, MatType.CV_8UC4, pixelData);
            _fullscreenUpdated = true;
        }

        private void CapZone1_Updated(object? sender, EventArgs e)
        {
            byte[]? pixelData = null;
            using (capZoneFullScreen.Lock())
            {
                pixelData = capZone1.RawBuffer.ToArray();
            }

            CapturedImageFirst = Mat.FromPixelData(capZone1.Height, capZone1.Width, MatType.CV_8UC4, pixelData);
     
            _firstImageUpdated = true;
        }

        /// <summary>
        /// Note :  YOU MUST DISPOSE OF THE RETURNED MAT
        /// </summary>
        /// <returns>
        /// Mat OpenCV
        /// </returns>
        public async Task GrabFullScreens()
        {
            _fullscreenUpdated = false;
            capZoneFullScreen.RequestUpdate();
            while (_fullscreenUpdated == false)
            {
                await Task.Delay(1);
            }
            _fullscreenUpdated = false;            
        }


    }





}

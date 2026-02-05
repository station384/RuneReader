using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;




namespace RuneReader
{

    public class CaptureScreen
    {
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly IEnumerable<GraphicsCard> _graphicsCards;
        private readonly IEnumerable<Display> _displays;
        private OpenCvSharp.Rect _captureRegion = new OpenCvSharp.Rect(0,0,0,0);
        private readonly int _maxHeight;
        private readonly int _maxWidth;
        private readonly IScreenCapture _screenCapture;
        private readonly ICaptureZone _capZone1 = null;
        private readonly ICaptureZone _capZoneFullScreen = null;


        private volatile Mat _capturedImageFirst;
        private volatile Mat _capturedFullScreen;

        public Mat CapturedImageFirst
        {
            get
            {
                return _capturedImageFirst;
            }
            // private set
            // {
            //     // We have to manage the memory for the Mat images.  so its up to us to make sure we detroy the prior one
            //     // before setting the new one.
            //     if (_capturedImageFirst != null)
            //     {
            //         if (!_capturedImageFirst.IsDisposed) _capturedImageFirst.Dispose();
            //     }
            //     _capturedImageFirst = value;
            //
            // }
        }


        public  Mat CapturedFullScreen
        {
            get
            {
                return  _capturedFullScreen;
            }
            // set
            // {
            //     // We have to manage the memory for the Mat images.  so its up to us to make sure we destroy the prior one
            //     // before setting the new one.
            //     if (_capturedFullScreen != null)
            //     {
            //         if (!_capturedFullScreen.IsDisposed)
            //         {
            //             _capturedFullScreen.Dispose();
            //             GC.Collect();
            //         }
            //     }
            //     _capturedFullScreen = value;
            // }
        }


        public OpenCvSharp.Rect CaptureRegion
        {
            get => _captureRegion; set
            {
                if (_captureRegion == value) return;
                value.X = Math.Clamp(value.X,0,_maxWidth);
                value.Y = Math.Clamp(value.Y, 0, _maxHeight);
                value.Height = Math.Clamp(value.Height,0,_maxHeight);
                value.Width = Math.Clamp(value.Width,0,_maxWidth);
                _captureRegion.X = (value.X >= 0 && value.X <= _maxWidth) ? value.X : 0;
                _captureRegion.Y = (value.Y >= 0 && value.Y <= _maxHeight) ? value.Y : 0;
                if (value.Width + value.X > _maxWidth)
                {
                    _captureRegion.X = _maxWidth - value.Width;
                }
                if (value.Height + value.Y > _maxHeight)
                {
                    _captureRegion.Y = _maxHeight - value.Height;
                }


                _captureRegion.Width = (value.Width >= 0 && value.Width <= _maxWidth) ? value.Width : _maxWidth;
                _captureRegion.Height = (value.Height >= 0 && value.Height <= _maxHeight) ? value.Height : _maxHeight;
                _screenCapture.UpdateCaptureZone(_capZone1, (int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
            }
        }
        

        public CaptureScreen(OpenCvSharp.Rect regions, int? downscaleLevel)
        {
            // Create a screen-capture service
            if (_screenCaptureService == null)
            {
#if WINDOWS
                _screenCaptureService = new DX11ScreenCaptureService();
#elif LINUX
                screenCaptureService = new X11ScreenCaptureService();
#else
                throw new PlatformNotSupportedException("Screen capture is only implemented for Windows (DX11) and Linux (X11) in this build.");
#endif
            }
            // Get all available graphics cards
            _graphicsCards = _screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in
            _displays = _screenCaptureService.GetDisplays(_graphicsCards.First());

            // Create a screen-capture for all screens you want to capture
            _screenCapture = _screenCaptureService.GetScreenCapture(_displays.First());
            _maxHeight = _displays.First().Height;
            _maxWidth = _displays.First().Width;


            _captureRegion = regions;
            if (_capZone1 == null)
            {
                var clampedX = (_captureRegion.X >= 0 && _captureRegion.X <= _maxWidth) ? _captureRegion.X : 0;
                var clampedY = (_captureRegion.Y >= 0 && _captureRegion.Y <= _maxHeight) ? _captureRegion.Y : 0;
                var clampedWidth = (_captureRegion.Width >= 0 && _captureRegion.Width <= _maxWidth) ? _captureRegion.Width : _maxWidth;
                var clampedHeight = (_captureRegion.Height >= 0 && _captureRegion.Height <= _maxHeight) ? _captureRegion.Height : _maxHeight;
                _capZone1 = _screenCapture.RegisterCaptureZone((int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
                _capZone1.Updated += CapZone1_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZone1.AutoUpdate = false;
        
            }

            if (_capZoneFullScreen == null)
            {
                _capZoneFullScreen = _screenCapture.RegisterCaptureZone((int)0, (int)0, (int)_maxWidth, (int)_maxHeight, downscaleLevel: 0);
                _capZoneFullScreen.Updated += CapZoneFullScreen_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZoneFullScreen.AutoUpdate = false;
            }
            _capturedImageFirst = new Mat();
            _capturedFullScreen = new Mat();
            _capZone1.RequestUpdate();
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
            _capZone1.RequestUpdate();
            _screenCapture.CaptureScreen();
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
            using (_capZoneFullScreen.Lock())
            {
                pixelData = _capZoneFullScreen.RawBuffer.ToArray();
            }
            var tMat = Mat.FromPixelData(_capZoneFullScreen.Height, _capZoneFullScreen.Width, MatType.CV_8UC4, pixelData);
            if (_capturedFullScreen != null && _capturedFullScreen.Cols == tMat.Cols &&
                _capturedFullScreen.Rows == tMat.Rows && _capturedFullScreen.Flags == tMat.Flags)
            {
                tMat.CopyTo(_capturedFullScreen);    
            }
            else
            {
                _capturedFullScreen = tMat.Clone();
            }
            tMat.Dispose();
            //CapturedFullScreen = 
            
            _fullscreenUpdated = true;
        }

        private void CapZone1_Updated(object? sender, EventArgs e)
        {
            byte[]? pixelData = null;
            using (_capZoneFullScreen.Lock())
            {
                pixelData = _capZone1.RawBuffer.ToArray();
            }
            var tMat = Mat.FromPixelData(_capZone1.Height, _capZone1.Width, MatType.CV_8UC4, pixelData);
            if (_capturedImageFirst.IsDisposed)
            {
                _capturedImageFirst = tMat.Clone();
            }
            
            if (_capturedImageFirst != null && _capturedImageFirst.Cols == tMat.Cols &&
                _capturedImageFirst.Rows == tMat.Rows && _capturedImageFirst.Flags == tMat.Flags)
            {
                tMat.CopyTo(_capturedImageFirst);    
            }
            else
            {
                _capturedImageFirst = tMat.Clone();
            }
            tMat.Dispose();
            

     
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
            _capZoneFullScreen.RequestUpdate();
            while (_fullscreenUpdated == false)
            {
                await Task.Delay(1);
            }
            _fullscreenUpdated = false;            
        }


    }





}

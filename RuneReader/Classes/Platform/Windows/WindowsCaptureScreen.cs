using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using ScreenCapture.NET;

namespace RuneReader.Classes.Platform.Windows
{

    public class WindowsCaptureScreen : IScreenCaptureProvider
    {
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly IEnumerable<GraphicsCard> _graphicsCards;
        private OpenCvSharp.Rect _captureRegion = new OpenCvSharp.Rect(0,0,0,0);
        private readonly IScreenCapture _screenCapture;
        private readonly ICaptureZone _capZone1;
        private readonly ICaptureZone _capZoneFullScreen;


        private Mat _capturedImageFirst;
        private Mat _capturedFullScreen;




        public int ScreenWidth { get; }
        public int ScreenHeight { get; }
        public int ScreenNumber { get; }

        public OpenCvSharp.Rect CaptureRegion
        {
            get => _captureRegion; 
            set
            {
                if (_captureRegion == value) return;
                value.X = Math.Clamp(value.X,0,ScreenWidth);
                value.Y = Math.Clamp(value.Y, 0, ScreenHeight);
                value.Height = Math.Clamp(value.Height,0,ScreenHeight);
                value.Width = Math.Clamp(value.Width,0,ScreenWidth);
                _captureRegion.X = (value.X >= 0 && value.X <= ScreenWidth) ? value.X : 0;
                _captureRegion.Y = (value.Y >= 0 && value.Y <= ScreenHeight) ? value.Y : 0;
                if (value.Width + value.X > ScreenWidth)
                {
                    _captureRegion.X = ScreenWidth - value.Width;
                }
                if (value.Height + value.Y > ScreenHeight)
                {
                    _captureRegion.Y = ScreenHeight - value.Height;
                }


                _captureRegion.Width = (value.Width >= 0 && value.Width <= ScreenWidth) ? value.Width : ScreenWidth;
                _captureRegion.Height = (value.Height >= 0 && value.Height <= ScreenHeight) ? value.Height : ScreenHeight;
                _screenCapture.UpdateCaptureZone(_capZone1, (int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
            }
        }

        public bool EnableRegion { get; set; }
        public bool EnableFullScreen { get; set; }
        
        
        public Mat LastRegion { get {  return _capturedImageFirst; }  }
        public Mat LastFullScreen { get {return  _capturedFullScreen; }   }
        
        /// <summary>
        /// Subscribers responsibility to dispose of the MAT
        /// </summary>
        public event Action<Mat>? OnRegionUpdated;

        /// <summary>
        /// Subscribers responsibility to dispose of the MAT
        /// </summary>
        public event Action<Mat>? OnFullScreenUpdated;





        /// <summary>
        /// Triggers a refresh of the screen grab 
        /// </summary>
        public  void CaptureOnce()
        {
            // Capture the screen
            if (EnableRegion)
            {
                _capZone1.RequestUpdate();
            }

            if (EnableFullScreen)
            {
                _capZoneFullScreen.RequestUpdate();
            }
            
            try
            {
                _screenCapture.CaptureScreen();
            }
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.Message);
                // benign: no new frame yet
            }
          
        }

        private unsafe void CapZoneFullScreen_Updated(object? sender, EventArgs e)
        {
            Mat tMat;

            
            using (_capZoneFullScreen.Lock())
            {
                int bytesPerPixel = _capZoneFullScreen.ColorFormat.BytesPerPixel;
                int stride = _capZoneFullScreen.Width * bytesPerPixel;
                ReadOnlySpan<byte> span = _capZoneFullScreen.RawBuffer;
                fixed (byte* ptr = span)
                {
                    tMat = Mat.FromPixelData(_capZoneFullScreen.Height, _capZoneFullScreen.Width, MatType.CV_8UC4, (IntPtr)ptr, stride).Clone();
                }
            }
            
            Mat old = Interlocked.Exchange(ref _capturedFullScreen, tMat);
            if (old != null && !old.IsDisposed)
                old.Dispose();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnFullScreenUpdated?.Invoke(_capturedFullScreen);
            });
          

        }

        private unsafe void CapZone1_Updated(object? sender, EventArgs e)
        {
            Mat tMat;

            // copy the data directly
            using (_capZone1.Lock())
            {
                int bytesPerPixel = _capZone1.ColorFormat.BytesPerPixel;
                int stride = _capZone1.Width * bytesPerPixel;
                ReadOnlySpan<byte> span = _capZone1.RawBuffer;
                fixed (byte* ptr = span)
                {
                    tMat = Mat.FromPixelData(_capZone1.Height, _capZone1.Width, MatType.CV_8UC4, (IntPtr)ptr, stride).Clone();
                }
            }
            
            // This results in getting disposed errors.       
            Mat old = Interlocked.Exchange(ref _capturedImageFirst, tMat);
            if (old != null && !old.IsDisposed)
                old.Dispose();
            
            // if (_capturedImageFirst.IsDisposed)
            // {
            //     _capturedImageFirst = tMat.Clone();
            // }
            //
            // if (_capturedImageFirst.Cols == tMat.Cols &&
            //     _capturedImageFirst.Rows == tMat.Rows && _capturedImageFirst.Flags == tMat.Flags)
            // {
            //     tMat.CopyTo(_capturedImageFirst);    
            // }
            // else
            // {
            //     _capturedImageFirst = tMat.Clone();
            // }
            //tMat.Dispose();
        
         
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnRegionUpdated?.Invoke(_capturedImageFirst.Clone());
            });
           // _firstImageUpdated = true;
        }
        

        public WindowsCaptureScreen()
        {
            // Create a screen-capture service
            if (_screenCaptureService == null)
            {
                _screenCaptureService = new DX11ScreenCaptureService();
            }
            // Get all available graphics cards
            _graphicsCards = _screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in (Return as list to avoid multiple enumerations
            IEnumerable<Display> displays = _screenCaptureService.GetDisplays(_graphicsCards.First()).ToList();

            var mainDisplay = displays.First();
            
            // Create a screen-capture for all screens you want to capture
            _screenCapture = _screenCaptureService.GetScreenCapture(mainDisplay);
            ScreenWidth = mainDisplay.Width;
            ScreenHeight = mainDisplay.Height;
            ScreenNumber = mainDisplay.Index;
            


          //  _captureRegion = regions;
            if (_capZone1 == null)
            {
                // We start with the initial capZone1 as fullscreen.   after that the set will adjust it.
                _capZone1 = _screenCapture.RegisterCaptureZone((int)0, (int)0, (int)ScreenWidth, (int)ScreenHeight, downscaleLevel: 0);
                _capZone1.Updated += CapZone1_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZone1.AutoUpdate = false;
            }

            if (_capZoneFullScreen == null)
            {
                // Full Screen is... well Full Screen.
                _capZoneFullScreen = _screenCapture.RegisterCaptureZone((int)0, (int)0, (int)ScreenWidth, (int)ScreenHeight, downscaleLevel: 0);
                _capZoneFullScreen.Updated += CapZoneFullScreen_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZoneFullScreen.AutoUpdate = false;
            }
            _capturedImageFirst = new Mat();
            _capturedFullScreen = new Mat();
        }

        
        public void Dispose()
        {
            _screenCaptureService.Dispose();
            _screenCapture.Dispose();
            _capturedImageFirst.Dispose();
            _capturedFullScreen.Dispose();
        }
    }





}

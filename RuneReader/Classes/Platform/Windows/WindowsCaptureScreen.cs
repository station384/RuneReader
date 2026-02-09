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
        private static bool IsDxgiWaitTimeout(Exception ex)
        {
            // Most reliable: HRESULT 0x887A0027
            const int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);

            // Many exceptions expose HResult via Exception.HResult
            if (ex.HResult == DXGI_ERROR_WAIT_TIMEOUT) return true;

            // Some libraries wrap: check inner chain
            var inner = ex.InnerException;
            while (inner != null)
            {
                if (inner.HResult == DXGI_ERROR_WAIT_TIMEOUT) return true;
                inner = inner.InnerException;
            }

            // Fallback: message check (last resort)
            return ex.Message.Contains("DXGI_ERROR_WAIT_TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("887A0027", StringComparison.OrdinalIgnoreCase);
        }
        
        private readonly IScreenCaptureService _screenCaptureService;
        private OpenCvSharp.Rect _captureRegion = new OpenCvSharp.Rect(0,0,0,0);
        private readonly IScreenCapture _screenCapture;
        private readonly ICaptureZone _capZone1 ; 
        private readonly ICaptureZone _capZoneFullScreen ;
        private int _disposed; // 0 = running, 1 = disposed
        private bool IsDisposed => Volatile.Read(ref _disposed) == 1;
        //private bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;


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
            if (IsDisposed) return;
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
            catch (Exception ex) when (IsDxgiWaitTimeout(ex))
            {
                // benign: no new frame yet
                return;
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"CaptureScreen failed: {ex}");
                // Optional: mark a flag for reinit, or rethrow depending on your policy
            }
          
        }

        private unsafe void CapZoneFullScreen_Updated(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            var handler = OnFullScreenUpdated;
            if (handler != null)
            {
                Mat tMat;


                using (_capZoneFullScreen.Lock())
                {
                    // Screen.net doesn't return the Stride so have to calc it.  It does return the BytesPerPixel.
                    int stride = _capZoneFullScreen.Width * _capZoneFullScreen.ColorFormat.BytesPerPixel;
                    ReadOnlySpan<byte> span = _capZoneFullScreen.RawBuffer;
                    fixed (byte* ptr = span)
                    {
                        tMat = Mat.FromPixelData(_capZoneFullScreen.Height, _capZoneFullScreen.Width, MatType.CV_8UC4,
                            (IntPtr)ptr, stride).Clone();
                    }
                }

                var old = Interlocked.Exchange(ref _capturedFullScreen, tMat);
                if (old is { IsDisposed: false })
                    old.Dispose();
                var matToReturnToCaller = tMat.Clone();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    handler.Invoke(matToReturnToCaller);
                });
            }

        }

        private unsafe void CapZone1_Updated(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            var handler = OnRegionUpdated;
            if (handler != null)
            {

                Mat tMat;

                // copy the data directly
                using (_capZone1.Lock())
                {
                    // Screen.net doesn't return the Stride so have to calc it.  It does return the BytesPerPixel.
                    int stride = _capZone1.Width * _capZone1.ColorFormat.BytesPerPixel;
                    ReadOnlySpan<byte> span = _capZone1.RawBuffer;
                    fixed (byte* ptr = span)
                    {
                        tMat = Mat.FromPixelData(_capZone1.Height, _capZone1.Width, MatType.CV_8UC4, (IntPtr)ptr,
                            stride).Clone();
                    }
                }

                // This results in getting disposed errors.       
                var old = Interlocked.Exchange(ref _capturedImageFirst, tMat);
                if (old is { IsDisposed: false })
                    old.Dispose();

                var matToReturnToCaller = tMat.Clone();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { handler.Invoke(matToReturnToCaller); });
            }
        }
        

        public WindowsCaptureScreen()
        {
            // Create a screen-capture service
            _screenCaptureService = new DX11ScreenCaptureService();

            // Get all available graphics cards
            var graphicsCards = _screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in (Return as list to avoid multiple enumerations
            IEnumerable<Display> displays = _screenCaptureService.GetDisplays(graphicsCards.First()).ToList();

            var mainDisplay = displays.First();
            
            // Create a screen-capture for all screens you want to capture
            _screenCapture = _screenCaptureService.GetScreenCapture(mainDisplay);
            ScreenWidth = mainDisplay.Width;
            ScreenHeight = mainDisplay.Height;
            ScreenNumber = mainDisplay.Index;
            


          //  _captureRegion = regions;

                // We start with the initial capZone1 as fullscreen.   after that the set will adjust it.
                _capZone1 = _screenCapture.RegisterCaptureZone((int)0, (int)0, (int)ScreenWidth, (int)ScreenHeight, downscaleLevel: 0);
                _capZone1.Updated += CapZone1_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZone1.AutoUpdate = false;
     


                // Full Screen is... well Full Screen.
                _capZoneFullScreen = _screenCapture.RegisterCaptureZone((int)0, (int)0, (int)ScreenWidth, (int)ScreenHeight, downscaleLevel: 0);
                _capZoneFullScreen.Updated += CapZoneFullScreen_Updated;
                // We only want to update the zone when we trigger it.  no need for extra CPU cycles
                _capZoneFullScreen.AutoUpdate = false;
  
            _capturedImageFirst = new Mat();
            _capturedFullScreen = new Mat();
        }

        
        public void Dispose()
        {
       
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _disposed = 1;
            try
            {
                _capZone1.Updated -= CapZone1_Updated;
                _capZoneFullScreen.Updated -= CapZoneFullScreen_Updated;
            }
            catch
            {
                 /* ignore */
            }


            _screenCaptureService.Dispose();
            _screenCapture.Dispose();
            _capturedImageFirst.Dispose();
            _capturedFullScreen.Dispose();
        }
    }





}

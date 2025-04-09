using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows;
using HPPH;
using System.Threading.Tasks;
using System.Windows.Controls;
using OpenCvSharp;
using System.Windows.Media.Media3D;
using System.Runtime.InteropServices;



namespace RuneReader
{


    public static class ImageExtension
    {
        public static Bitmap ToBitmap(this IImage image) => image.AsRefImage<ColorBGRA>().ToBitmap();
        public static Bitmap ToBitmap(this Image<ColorBGRA> image) => image.AsRefImage<ColorBGRA>().ToBitmap();

        public static unsafe Bitmap ToBitmap(this RefImage<ColorBGRA> image)
        {
            Bitmap output = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
            System.Drawing.Rectangle rect = new(0, 0, image.Width, image.Height);
            BitmapData bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            nint ptr = bmpData.Scan0;
            foreach (var row in image.Rows)
            {
                row.CopyTo(new Span<ColorBGRA>((void*)ptr, bmpData.Stride));
                ptr += bmpData.Stride;
            }

            output.UnlockBits(bmpData);
            return output;
        }
    }

    public class CaptureScreen
    {
        private bool isCapturing;
        private readonly int captureX, captureY, captureWidth, captureHeight;
        IScreenCaptureService screenCaptureService;
        IEnumerable<GraphicsCard> graphicsCards;
        IEnumerable<Display> displays;
        private System.Windows.Rect _captureRegion = new System.Windows.Rect();
        private int _maxHeight;
        private int _maxWidth;
        IScreenCapture screenCapture;
        ICaptureZone capZone1 = null;
        ICaptureZone capZoneFullScreen = null;


        
        public  Bitmap CapturedImageFirst { get; private set; }
        private volatile Mat _CapturedFullScreen;
        private  Mat CapturedFullScreen { get 
            { 
                return _CapturedFullScreen; 
            }  
            set {
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


        public void GrabScreen()
        {
            // Capture the screen
            // This should be done in a loop on a seperate thread as CaptureScreen blocks if the screen is not updated (still image).
             screenCapture.CaptureScreen();
           
            // Do something with the captured image - e.g. access all pixels (same could be done with topLeft)


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
            }

            if (capZoneFullScreen == null)
            {
                capZoneFullScreen = screenCapture.RegisterCaptureZone((int)0, (int)0, (int)_maxWidth, (int)_maxHeight, downscaleLevel: 0);
                capZoneFullScreen.Updated += CapZoneFullScreen_Updated;
                capZoneFullScreen.AutoUpdate = false;
            }
        }



        private bool _fullscreenUpdated = false;
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
            //Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
            using (capZone1.Lock())
            {
                CapturedImageFirst = ImageExtension.ToBitmap(capZone1.Image);
            }
        }

        public async Task<Mat> GrabFullScreens()
        {
            Mat result = null;

            _fullscreenUpdated = false;
            capZoneFullScreen.RequestUpdate();
            while (_fullscreenUpdated == false)
            {
                await Task.Delay(1);
            }
            _fullscreenUpdated = false; ;
            return result = CapturedFullScreen.Clone();

        }


    }





}

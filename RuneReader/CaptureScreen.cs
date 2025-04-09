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


        public Bitmap CapturedImageFirst { get; private set; }
        public Bitmap CapturedImageSecond { get; private set; }

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

            //Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
            using (capZone1.Lock())
            {

                CapturedImageFirst = ImageExtension.ToBitmap(capZone1.Image);

                //// You have multiple options now:
                //// 1. Access the raw byte-data
                //ReadOnlySpan<byte> rawData = fullscreen.RawBuffer;

                //// 2. Use the provided abstraction to access pixels without having to care about low-level byte handling
                //// Get the image captured for the zone
                //IImage image = fullscreen.Image;

                //// Iterate all pixels of the image
                //foreach (IColor color in image)
                //    Console.WriteLine($"A: {color.A}, R: {color.R}, G: {color.G}, B: {color.B}");

                //// Get the pixel at location (x = 10, y = 20)
                //IColor imageColorExample = image[10, 20];

                //// Get the first row
                //IImage.IImageRow row = image.Rows[0];
                //// Get the 10th pixel of the row
                //IColor rowColorExample = row[10];

                //// Get the first column
                //IImage.IImageColumn column = image.Columns[0];
                //// Get the 10th pixel of the column
                //IColor columnColorExample = column[10];

                //// Cuts a rectangle out of the original image (x = 100, y = 150, width = 400, height = 300)
                //IImage subImage = image[100, 150, 400, 300];

                // All of the things above (rows, columns, sub-images) do NOT allocate new memory so they are fast and memory efficient, but for that reason don't provide raw byte access.
            }



        }
        //int x, int y, int width, int height,
        public CaptureScreen(System.Windows.Rect Regions, int? downscaleLevel)
        {

            _captureRegion = Regions;
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

            // Register the regions you want to capture om the screen
            // Capture the whole screen
            // ICaptureZone fullscreen = screenCapture.RegisterCaptureZone(0, 0, screenCapture.Display.Width, screenCapture.Display.Height);
            // Capture a 100x100 region at the top left and scale it down to 50x50
            capZone1 = screenCapture.RegisterCaptureZone((int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);

        }

        public  Mat GrabFullScreens()
        {
            Mat result = null;
            ICaptureZone capZoneFullScreen = null;
            if (screenCaptureService == null)
            {
                screenCaptureService = new DX11ScreenCaptureService();
            }
            // Get all available graphics cards
            var lgraphicsCards = screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in
            var ldisplays = screenCaptureService.GetDisplays(lgraphicsCards.First());


            // Create a screen-capture for all screens you want to capture
            var lscreenCapture = screenCaptureService.GetScreenCapture(ldisplays.First());
            
            var maxHeight = ldisplays.First().Height;
            var maxWidth = ldisplays.First().Width;

            // Register the regions you want to capture om the screen
            // Capture the whole screen
            // ICaptureZone fullscreen = screenCapture.RegisterCaptureZone(0, 0, screenCapture.Display.Width, screenCapture.Display.Height);
            // Capture a 100x100 region at the top left and scale it down to 50x50
            capZoneFullScreen = lscreenCapture.RegisterCaptureZone((int)0, (int)0, (int)maxWidth, (int)maxHeight, downscaleLevel: 0);
            var screenCap = lscreenCapture.CaptureScreen();
            if (screenCap)
            {
                using (capZoneFullScreen.Lock())
                {
                    //result = ImageExtension.ToBitmap(capZoneFullScreen.Image);
                    Mat image = Mat.FromPixelData(capZoneFullScreen.Height, capZoneFullScreen.Width, MatType.CV_8UC4, capZoneFullScreen.RawBuffer.ToArray());
                    result = image;
                }
            }
            lscreenCapture.UnregisterCaptureZone(capZoneFullScreen);

            
            
            return result;

        }


    }





}

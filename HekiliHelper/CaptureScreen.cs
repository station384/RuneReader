using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;



namespace HekiliHelper
{


    public static class ImageExtension
    {
        public static Bitmap ToBitmap(this IImage image) => image.AsRefImage<ColorBGRA>().ToBitmap();
        public static Bitmap ToBitmap(this Image<ColorBGRA> image) => image.AsRefImage<ColorBGRA>().ToBitmap();

        public static unsafe Bitmap ToBitmap(this RefImage<ColorBGRA> image)
        {
            Bitmap output =   new(image.Width, image.Height, PixelFormat.Format32bppArgb);
            System.Drawing.Rectangle rect = new(0, 0, image.Width, image.Height);
            BitmapData bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            nint ptr = bmpData.Scan0;
            foreach (ReadOnlyRefEnumerable<ColorBGRA> row in image.Rows)
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
        private Rect _captureRegion;
        private int _maxHeight;
        private int _maxWidth;
        public Rect CaptureRegion { get => _captureRegion; set
            {
                if (_captureRegion == value) return;
                _captureRegion.X = (value.X >= 0 && value.X <= _maxWidth)? value.X : 0;
                _captureRegion.Y = (value.Y >= 0 && value.Y <= _maxHeight)? value.Y : 0;
                _captureRegion.Width = (value.Width >= 0 && value.Width <= _maxWidth) ? value.Width : 0;
                _captureRegion.Height = (value.Height >= 0 && value.Height <= _maxHeight) ? value.Height : 0;
                if (topLeft != null)
                {
                    
                    screenCapture.UpdateCaptureZone(topLeft, (int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);
                }
            } 
        }
        IScreenCapture screenCapture;
        ICaptureZone topLeft = null;
        
      //  public Rect capRegion { get; set; }



        public Bitmap CapturedImage { get;private set; }


        public void GrabScreen ()
        {

    

            // Capture the screen
            // This should be done in a loop on a seperate thread as CaptureScreen blocks if the screen is not updated (still image).
            screenCapture.CaptureScreen();

            // Do something with the captured image - e.g. access all pixels (same could be done with topLeft)

            //Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
            using (topLeft.Lock())
            {

                CapturedImage = ImageExtension.ToBitmap(topLeft.Image);

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

        public CaptureScreen(int x, int y, int width, int height, int ?downscaleLevel)
        {
            _captureRegion = new Rect { X = (double)x, Y = (double)y, Width = width, Height = height };

            // Create a screen-capture service
            screenCaptureService = new DX11ScreenCaptureService();

            // Get all available graphics cards
            graphicsCards = screenCaptureService.GetGraphicsCards();

            // Get the displays from the graphics card(s) you are interested in
            displays = screenCaptureService.GetDisplays(graphicsCards.First());

            // Create a screen-capture for all screens you want to capture
            screenCapture = screenCaptureService.GetScreenCapture(displays.First());
            _maxHeight = displays.First().Height ;
            _maxWidth = displays.First().Width ;
            
            

            // Register the regions you want to capture om the screen
            // Capture the whole screen
            // ICaptureZone fullscreen = screenCapture.RegisterCaptureZone(0, 0, screenCapture.Display.Width, screenCapture.Display.Height);
            // Capture a 100x100 region at the top left and scale it down to 50x50
            topLeft = screenCapture.RegisterCaptureZone((int)_captureRegion.X, (int)_captureRegion.Y, (int)_captureRegion.Width, (int)_captureRegion.Height, downscaleLevel: 0);

                //GrabScreen();


            }
    }





}

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
        private Rect[] _captureRegion = {new Rect(), new Rect()};
        private int _maxHeight;
        private int _maxWidth;
        IScreenCapture screenCapture;
        ICaptureZone capZone1 = null;
        ICaptureZone capZone2 = null;
        public Bitmap CapturedImageFirst { get; private set; }
        public Bitmap CapturedImageSecond { get; private set; }

        public Rect[] CaptureRegion { get => _captureRegion; set
            {
                if (_captureRegion == value) return;
                for (var i = 0; i < value.Length; i++)
                {

                    _captureRegion[i].X = (value[i].X >= 0 && value[i].X <= _maxWidth) ? value[i].X : 0;
                    _captureRegion[i].Y = (value[i].Y >= 0 && value[i].Y <= _maxHeight) ? value[i].Y : 0;
                    _captureRegion[i].Width = (value[i].Width >= 0 && value[i].Width <= _maxWidth) ? value[i].Width : 0;
                    _captureRegion[i].Height = (value[i].Height >= 0 && value[i].Height <= _maxHeight) ? value[i].Height : 0;
                    if (capZone1 != null && i == 0)
                    {
                        screenCapture.UpdateCaptureZone(capZone1, (int)_captureRegion[i].X, (int)_captureRegion[i].Y, (int)_captureRegion[i].Width, (int)_captureRegion[i].Height, downscaleLevel: 0);
                    }
                    if (capZone2 != null && i == 1)
                    {
                        screenCapture.UpdateCaptureZone(capZone2, (int)_captureRegion[i].X, (int)_captureRegion[i].Y, (int)_captureRegion[i].Width, (int)_captureRegion[i].Height, downscaleLevel: 0);
                    }


                }
            } 
        }

        





        public void GrabScreen ()
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
            using (capZone2.Lock())
            {
                CapturedImageSecond = ImageExtension.ToBitmap(capZone2.Image);
            }


        }
        //int x, int y, int width, int height,
        public CaptureScreen(Rect[] Regions, int ?downscaleLevel)
        {
            //            _captureRegion[0] = //new Rect { X = (double)x, Y = (double)y, Width = width, Height = height };
            //           _captureRegion[1] = //new Rect { X = (double)x, Y = (double)y, Width = width, Height = height };
            _captureRegion = Regions;
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
            capZone1 = screenCapture.RegisterCaptureZone((int)_captureRegion[0].X, (int)_captureRegion[0].Y, (int)_captureRegion[0].Width, (int)_captureRegion[0].Height, downscaleLevel: 0);
            capZone2 = screenCapture.RegisterCaptureZone((int)_captureRegion[1].X, (int)_captureRegion[1].Y, (int)_captureRegion[1].Width, (int)_captureRegion[1].Height, downscaleLevel: 0);
        }




    }





}

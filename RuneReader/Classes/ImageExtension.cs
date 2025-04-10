using System;
using System.Drawing;
using System.Drawing.Imaging;
using HPPH;



namespace RuneReader.Classes
{
    // This is no longer needed as I have moved to full OpenCV processing.  but keeping it here for now.
    public static class ImageExtension
    {
        public static Bitmap ToBitmap(this IImage image) => image.AsRefImage<ColorBGRA>().ToBitmap();
        public static Bitmap ToBitmap(this Image<ColorBGRA> image) => image.AsRefImage<ColorBGRA>().ToBitmap();

        public static unsafe Bitmap ToBitmap(this RefImage<ColorBGRA> image)
        {
            Bitmap output = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
            Rectangle rect = new(0, 0, image.Width, image.Height);
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





}

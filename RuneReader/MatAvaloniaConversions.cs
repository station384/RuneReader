using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenCvSharp;

namespace RuneReader;


public static class MatAvaloniaExtensions
{
    /// <summary>
    /// Converts an OpenCV Mat to an Avalonia WriteableBitmap.
    /// Supports CV_8UC4 (BGRA) and CV_8UC1 (grayscale).
    /// Grayscale is expanded to BGRA for display.
    /// </summary>
    public static unsafe WriteableBitmap ToWriteableBitmap(this Mat mat, WriteableBitmap? reuse = null)
    {
        if (mat.Empty()) throw new ArgumentException("Mat is empty.");
        if (mat.IsDisposed) return 
            new WriteableBitmap(new PixelSize(80,80),Vector.One);
        // Normalize to BGRA (CV_8UC4) for display
        Mat? temp = null;
        Mat bgra = mat;

        try
        {
            if (mat.Type() == MatType.CV_8UC4)
            {
                // already BGRA
            }
            else if (mat.Type() == MatType.CV_8UC1)
            {
                temp = new Mat();
                // Gray -> BGRA
                Cv2.CvtColor(mat, temp, ColorConversionCodes.GRAY2BGRA);
                bgra = temp;
            }
            else if (mat.Type() == MatType.CV_8UC3)
            {
                temp = new Mat();
                // BGR -> BGRA
                Cv2.CvtColor(mat, temp, ColorConversionCodes.BGR2BGRA);
                bgra = temp;
            }
            else
            {
                throw new ArgumentException($"Unsupported Mat type {mat.Type()}. Expected CV_8UC4, CV_8UC3, or CV_8UC1.");
            }

            int width = bgra.Cols;
            int height = bgra.Rows;

            if (reuse == null || reuse.PixelSize.Width != width || reuse.PixelSize.Height != height)
            {
                reuse = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul); // safe default; can be Opaque if you know alpha=255
            }

            using var fb = reuse.Lock();

            int srcStride = (int)bgra.Step();
            int dstStride = fb.RowBytes;
            int rowBytes = width * 4;

            
            {
                byte* srcBase = (byte*)bgra.DataPointer;
                byte* dstBase = (byte*)fb.Address;

                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(
                        srcBase + (nint)(y * srcStride),
                        dstBase + (nint)(y * dstStride),
                        dstStride,
                        rowBytes);
                }
            }

            return reuse;
        }
        finally
        {
  
            temp?.Dispose(); // only disposes if we allocated a conversion mat
        }
    }
}

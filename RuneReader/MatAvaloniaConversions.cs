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
    public static WriteableBitmap ToWriteableBitmap(this Mat mat, WriteableBitmap? reuse = null)
    {
        if (mat.Empty()) throw new ArgumentException("Mat is empty.");

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

            unsafe
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



// public static class MatAvaloniaExtensions
// {
//     public static WriteableBitmap ToWriteableBitmapBgra(this Mat mat, WriteableBitmap? reuse = null)
//     {
//         if (mat.Empty()) throw new ArgumentException("Mat is empty.");
//         if (mat.Type() != MatType.CV_8UC4)
//             throw new ArgumentException($"Expected CV_8UC4 (BGRA). Got: {mat.Type()}");
//
//         int width = mat.Cols;
//         int height = mat.Rows;
//
//         // Create or reuse a bitmap of the same size
//         if (reuse == null || reuse.PixelSize.Width != width || reuse.PixelSize.Height != height)
//         {
//             reuse = new WriteableBitmap(
//                 new PixelSize(width, height),
//                 new Vector(96, 96),
//                 PixelFormat.Bgra8888,
//                 AlphaFormat.Unpremul); // important for BGRA from capture
//         }
//
//         using var fb = reuse.Lock();
//
//         int srcStride = (int)mat.Step();      // bytes per row in Mat
//         int dstStride = fb.RowBytes;          // bytes per row in bitmap
//         int rowBytes  = width * 4;            // BGRA = 4 bytes per pixel
//
//         unsafe
//         {
//             byte* srcBase = (byte*)mat.DataPointer;
//             byte* dstBase = (byte*)fb.Address;
//
//             // Copy row-by-row (handles differing strides safely)
//             for (int y = 0; y < height; y++)
//             {
//                 Buffer.MemoryCopy(
//                     srcBase + (nint)(y * srcStride),
//                     dstBase + (nint)(y * dstStride),
//                     dstStride,
//                     rowBytes);
//             }
//         }
//
//         return reuse;
//     }
// }
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media.Imaging;

namespace HekiliEcho
{
    public class ImageHelpers
    {
        public  Bitmap CreateBitmap(int width, int height, Color color)
        {
            // Create a new bitmap with the specified size and format.
            Bitmap coloredBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Create a Graphics object for drawing.
            using (Graphics gfx = Graphics.FromImage(coloredBitmap))
            {
                // Create a brush with the specified color.
                using (Brush brush = new SolidBrush(color))
                {
                    // Use the Graphics object to fill the bitmap with the specified color.
                    gfx.FillRectangle(brush, 0, 0, width, height);
                }
            }

            return coloredBitmap;
        }

        public  bool FindColorInFirstQuarter(Bitmap image, Color targetColor, double tolerance)
        {
            // Calculate the tolerance for each color component.
            int toleranceR = (int)(255 * tolerance);
            int toleranceG = (int)(255 * tolerance);
            int toleranceB = (int)(255 * tolerance);

            // Determine the area to search (first quarter of the image).
            Rectangle rect = new Rectangle(0, 0, image.Width / 6, image.Height / 6);
              BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);

            try
            {
                // Declare an array to hold the bytes of the bitmap.
                int bytesPerPixel = Image.GetPixelFormatSize(image.PixelFormat) / 8;
                int byteCount = bmpData.Stride * bmpData.Height;
                byte[] pixels = new byte[byteCount];

                // Copy the RGB values into the array.
                Marshal.Copy(bmpData.Scan0, pixels, 0, byteCount);

                for (int y = 0; y < bmpData.Height; y++)
                {
                    for (int x = 0; x < bmpData.Width; x++)
                    {
                        // Calculate the index of the pixel's byte.
                        int idx = (y * bmpData.Stride) + (x * bytesPerPixel);

                        // Extract the pixel's components. The order of these bytes depends on the PixelFormat.
                        int B = pixels[idx];
                        int G = pixels[idx + 1];
                        int R = pixels[idx + 2];

                        // Check if the color is within the tolerance for each component.
                        if (R-5 + (targetColor.R - toleranceR) >= toleranceR ||
                            G-5 + (targetColor.G - toleranceR) >= toleranceG ||
                            B-5+ (targetColor.B - toleranceR) >= toleranceB)
                        {
                            return true; // The color is within the tolerance range.
                        }

                        if (Math.Abs(R - toleranceR) >= targetColor.R &&
                            Math.Abs(G - toleranceR) >= targetColor.G &&
                            Math.Abs(B - toleranceR) >= targetColor.B )
                        {
                            return true; // The color is within the tolerance range.
                        }
                    }
                }
            }
            finally
            {
                // Ensure to unlock the bits even if an exception occurs.
                image.UnlockBits(bmpData);
            }

            return false; // The color was not found within the tolerance range.
        }
        public  Bitmap RemoveRedComponent(Bitmap original)
        {
            // Lock the bitmap's bits. 
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData bmpData = original.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * original.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every red byte to 0.
            for (int i = 0; i < rgbValues.Length; i += 4)
            {
                //rgbValues[i] = 0;
                //rgbValues[i + 1] = 0;
                rgbValues[i + 2] = 0; // Set the red value to 0.

            }

            // Copy the RGB values back to the bitmap.
            Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            original.UnlockBits(bmpData);

            return original;
        }
        public  Bitmap FilterByColor(Bitmap original, Color targetColor, double tolerance)
            {
                // Calculate the tolerance for each color component based on the target color.
                int toleranceR = (int)(targetColor.R * tolerance);
                int toleranceG = (int)(targetColor.G * tolerance);
                int toleranceB = (int)(targetColor.B * tolerance);

                // Lock the bitmap's bits. 
                Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
                System.Drawing.Imaging.BitmapData bmpData =
                    original.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, original.PixelFormat);

                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;

                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * original.Height;
                byte[] rgbValues = new byte[bytes];

                // Copy the RGB values into the array.
                System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

                for (int i = 0; i < rgbValues.Length; i += 4)
                {
                    int blue = rgbValues[i];
                    int green = rgbValues[i + 1];
                    int red = rgbValues[i + 2];

                    // Check if the color is within the tolerance range.
                    if (Math.Abs(red - targetColor.R) > toleranceR ||
                        Math.Abs(green - targetColor.G) > toleranceG ||
                        Math.Abs(blue - targetColor.B) > toleranceB)
                    {
                        // Set the color to black if it's not within the tolerance.
                        rgbValues[i] = rgbValues[i + 1] = rgbValues[i + 2] = 0;
                    }
                }

                // Copy the RGB values back to the bitmap.
                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

                // Unlock the bits.
                original.UnlockBits(bmpData);

                return original;
            }

        public static Bitmap BumpToBlack(Bitmap original, byte threshold)
        {
            // Create a new bitmap to store the processed image
            Bitmap bumpedBlack = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits
            BitmapData originalData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData bumpedData = bumpedBlack.LockBits(
                new Rectangle(0, 0, bumpedBlack.Width, bumpedBlack.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int byteCount = originalData.Stride * originalData.Height;
            byte[] pixels = new byte[byteCount];

            // Copy the RGB values into the array
            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, pixels, 0, byteCount);
            original.UnlockBits(originalData);

            for (int i = 0; i < byteCount; i += 4)
            {
                // Check if the pixel's color is below the threshold for R, G, and B
                if (pixels[i] <= threshold || pixels[i + 1] <= threshold || pixels[i + 2] <= threshold)
                {
                    // Set the pixel to pure black
                    pixels[i] = pixels[i] <= threshold ? (byte)0 : pixels[i];     // Blue
                    pixels[i + 1] = pixels[i + 1] <= threshold ? (byte)0 : pixels[i]; ; // Green
                    pixels[i + 2] = pixels[i + 1] <= threshold ? (byte)0 : pixels[i]; ; // Red
                }
                // Preserve the alpha channel value
                pixels[i + 3] = pixels[i + 3];
            }

            // Copy the modified pixel data back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bumpedData.Scan0, byteCount);
            bumpedBlack.UnlockBits(bumpedData);

            return bumpedBlack;
        }
        public static Bitmap BumpToWhite(Bitmap original, int threshold)
        {
            // Define the threshold above which pixels will be turned to pure white
             int whiteThreshold = 255 - threshold > 255 ? 255 : threshold;

            // Create a new bitmap to store the processed image
            Bitmap bumpedWhite = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits
            BitmapData originalData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData bumpedData = bumpedWhite.LockBits(
                new Rectangle(0, 0, bumpedWhite.Width, bumpedWhite.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int byteCount = originalData.Stride * originalData.Height;
            byte[] pixels = new byte[byteCount];

            // Copy the RGB values into the array
            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, pixels, 0, byteCount);
            original.UnlockBits(originalData);

            for (int i = 0; i < byteCount; i += 4)
            {
                // Check if the pixel's color is above the threshold for R, G, and B
                if (pixels[i] >= whiteThreshold || pixels[i + 1] >= whiteThreshold || pixels[i + 2] >= whiteThreshold)
                {
                    // Set the pixel to pure white
                    pixels[i] = pixels[i] >= (byte)whiteThreshold ? (byte)255 : pixels[i];     // Blue
                    pixels[i + 1] = pixels[i+1] >= (byte)whiteThreshold ? (byte)255 : pixels[i]; // Green
                    pixels[i + 2] = pixels[i+2] >= (byte)whiteThreshold ? (byte)255 : pixels[i]; // Red
                }
                // Preserve the alpha channel value
                pixels[i + 3] = pixels[i + 3];
            }

            // Copy the modified pixel data back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bumpedData.Scan0, byteCount);
            bumpedWhite.UnlockBits(bumpedData);

            return bumpedWhite;
        }
        public static Bitmap ConvertToGrayscaleAndBumpWhite(Bitmap original, byte threshold)
        {
            // Clone the original image to ensure it's in a pixel format that we can work with and has 32bpp
            Bitmap grayscaleImage = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits for the original and the new grayscale image
            BitmapData originalData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly, original.PixelFormat);
            BitmapData grayscaleData = grayscaleImage.LockBits(
                new Rectangle(0, 0, grayscaleImage.Width, grayscaleImage.Height),
                ImageLockMode.WriteOnly, grayscaleImage.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int byteCount = originalData.Stride * originalData.Height;
            byte[] originalPixels = new byte[byteCount];
            byte[] grayscalePixels = new byte[byteCount];

            // Copy the RGB values into the array
            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, originalPixels, 0, byteCount);

            for (int i = 0; i < byteCount; i += bytesPerPixel)
            {
                // Calculate the grayscale value
                byte grayValue = (byte)(originalPixels[i + 2] * 0.299 + originalPixels[i + 1] * 0.587 + originalPixels[i] * 0.114);

                // Bump pixels close to white up to full white
                if (grayValue >= threshold)
                {
                    grayValue = 255;
                }

                // Set the new pixel value for grayscale image
                grayscalePixels[i] = grayValue;        // Blue component
                grayscalePixels[i + 1] = grayValue;    // Green component
                grayscalePixels[i + 2] = grayValue;    // Red component
                grayscalePixels[i + 3] = originalPixels[i + 3]; // Preserve alpha channel
            }

            // Copy the modified pixel data back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(grayscalePixels, 0, grayscaleData.Scan0, byteCount);

            // Unlock the bits
            original.UnlockBits(originalData);
            grayscaleImage.UnlockBits(grayscaleData);

            return grayscaleImage;
        }
        public static Bitmap RemoveNoise(Bitmap image, int windowSize)
    {
        if (windowSize % 2 == 0) throw new ArgumentException("Window size must be odd.", nameof(windowSize));
        if (image.PixelFormat != PixelFormat.Format32bppArgb)
            throw new ArgumentException("Only 32bppArgb images are supported.", nameof(image.PixelFormat));

        // Create a new bitmap to store the noise-free image
        Bitmap result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

        // Lock the bitmap's bits
        BitmapData originalData = image.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData resultData = result.LockBits(
            new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int bytesPerPixel = 4; // 4 bytes per pixel for 32bpp images
        byte[] pixelBuffer = new byte[originalData.Stride * originalData.Height];
        byte[] resultBuffer = new byte[resultData.Stride * resultData.Height];

        // Copy the pixel values into the buffer
        Marshal.Copy(originalData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
        image.UnlockBits(originalData);

        int filterOffset = (windowSize - 1) / 2;
        byte[] neighbourPixels = new byte[windowSize * windowSize];
        int byteOffset = 0;

        for (int offsetY = filterOffset; offsetY < image.Height - filterOffset; offsetY++)
        {
            for (int offsetX = filterOffset; offsetX < image.Width - filterOffset; offsetX++)
            {
                byteOffset = offsetY * originalData.Stride + offsetX * bytesPerPixel;

                // Create a window of pixels around the current pixel to get the median value
                int windowIndex = 0;
                for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                {
                    for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                    {
                        int calcOffset = byteOffset +
                                         (filterX * bytesPerPixel) +
                                         (filterY * originalData.Stride);

                        neighbourPixels[windowIndex++] = pixelBuffer[calcOffset];
                    }
                }

                Array.Sort(neighbourPixels);
                byte median = neighbourPixels[windowSize * windowSize / 2];

                resultBuffer[byteOffset] = median; // Blue
                resultBuffer[byteOffset + 1] = median; // Green
                resultBuffer[byteOffset + 2] = median; // Red
                resultBuffer[byteOffset + 3] = pixelBuffer[byteOffset + 3]; // Alpha channel should remain unchanged
            }
        }

        // Copy the modified pixel data back into the bitmap
        Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
        result.UnlockBits(resultData);

        return result;
    }


    public Bitmap ConvertToBlackAndWhiteSlow(Bitmap original, byte threshold)
        {
            // Create a new bitmap with the same dimensions as the original.
            Bitmap blackAndWhite = new Bitmap(original.Width, original.Height, original.PixelFormat);
            blackAndWhite.SetResolution(original.VerticalResolution, original.HorizontalResolution);

            // Loop through each pixel in the bitmap.
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    // Get the color of the current pixel.
                    Color originalColor = original.GetPixel(x, y);

                    // Compute the grayscale value of the pixel
                    byte grayScale = (byte)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));

                    // If the grayscale value is above the threshold, set the pixel to white; otherwise, set it to black.
                    Color newColor = grayScale >= threshold ? Color.White : Color.Black;

                    // Set the new pixel color at the current position.
                    blackAndWhite.SetPixel(x, y, newColor);
                }
            }

            // Return the new black and white bitmap.
            return blackAndWhite;
        }

        public static Bitmap ConvertToBlackAndWhite(Bitmap original, byte threshold)
        {
            // Create a new bitmap in grayscale format
            Bitmap blackAndWhite = new Bitmap(original.Width, original.Height, PixelFormat.Format1bppIndexed);

            // Lock the bitmap's bits
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData originalData = original.LockBits(rect, ImageLockMode.ReadOnly, original.PixelFormat);
            BitmapData bwData = blackAndWhite.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);

            // Determine the number of bytes per pixel
            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;

            unsafe
            {
                for (int y = 0; y < original.Height; y++)
                {
                    byte* originalRow = (byte*)originalData.Scan0 + (y * originalData.Stride);
                    for (int x = 0; x < original.Width; x++)
                    {
                        // Compute the index for the pixel
                        int idx = x * bytesPerPixel;
                        // Calculate the grayscale value using the luminance formula (rec. 601)
                        byte luminance = (byte)((originalRow[idx + 2] * 0.299) + (originalRow[idx + 1] * 0.587) + (originalRow[idx] * 0.114));
                        // Determine if the pixel should be black or white based on the threshold
                        bool isWhite = luminance >= threshold;

                        // Set the pixel in the new bitmap
                        if (isWhite)
                        {
                            int index = y * bwData.Stride + (x / 8);
                            byte mask = (byte)(0x80 >> (x % 8));
                            ((byte*)bwData.Scan0)[index] |= mask;
                        }
                    }
                }
            }

            // Unlock the bits
            original.UnlockBits(originalData);
            blackAndWhite.UnlockBits(bwData);

            // Set the palette to 2 colors: black and white
            ColorPalette palette = blackAndWhite.Palette;
            palette.Entries[0] = Color.Black;
            palette.Entries[1] = Color.White;
            blackAndWhite.Palette = palette;

            return blackAndWhite;
        }
        public unsafe Bitmap ConvertToBlackAndWhiteFast(Bitmap original, byte threshold)
        {
            // Create a new bitmap with the same dimensions as the original.
            Bitmap blackAndWhite = new Bitmap(original.Width, original.Height);
            blackAndWhite.SetResolution(original.VerticalResolution, original.HorizontalResolution);

            // Lock the bitmap's bits.
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData originalData = original.LockBits(rect, ImageLockMode.ReadOnly, original.PixelFormat);
            BitmapData newData = blackAndWhite.LockBits(rect, ImageLockMode.WriteOnly, blackAndWhite.PixelFormat);

            // Get the address of the first line.
            byte* originalPtr = (byte*)originalData.Scan0;
            byte* newPtr = (byte*)newData.Scan0;

            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int heightInPixels = originalData.Height;
            int widthInBytes = originalData.Width * bytesPerPixel;
            int width = originalData.Width;

            for (int y = 0; y < heightInPixels; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Compute the offset for the current pixel.
                    int i = y * originalData.Stride + x * bytesPerPixel;

                    // Compute the grayscale value of the pixel.
                    byte grayScale = (byte)((originalPtr[i + 2] * 0.3) + (originalPtr[i + 1] * 0.59) + (originalPtr[i] * 0.11));

                    // If the grayscale value is above the threshold, set the pixel to white; otherwise, set it to black.
                    byte colorValue = grayScale >= threshold ? (byte)255 : (byte)0;

                    // Set the new pixel color at the current position.
                    newPtr[i] = colorValue; // Blue
                    newPtr[i + 1] = colorValue; // Green
                    newPtr[i + 2] = colorValue; // Red
                }
            }

            // Unlock the bits.
            original.UnlockBits(originalData);
            blackAndWhite.UnlockBits(newData);

            // Return the new black and white bitmap.
            return blackAndWhite;
        }

        public Bitmap InvertImageColors(Bitmap original)
        {
            // Lock the bitmap's bits. 
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData bmpData = original.LockBits(rect, ImageLockMode.ReadWrite, original.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * original.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Invert the RGB values.
            for (int i = 0; i < rgbValues.Length; i++)
            {
                rgbValues[i] = (byte)(255 - rgbValues[i]);
            }

            // Copy the RGB values back to the bitmap.
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            original.UnlockBits(bmpData);

            // Return the inverted bitmap.
            return original;
        }

        public  Bitmap ConvertToGrayscaleFast(Bitmap original)
        {
            // Create a blank bitmap with the same dimensions as the original
            Bitmap grayscaleBitmap = new Bitmap(original.Width, original.Height);

            // Lock the original bitmap in memory
            BitmapData originalData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // Lock the new bitmap in memory
            BitmapData grayscaleData = grayscaleBitmap.LockBits(
                new Rectangle(0, 0, grayscaleBitmap.Width, grayscaleBitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            // Create an array to hold the pixel data
            int bytes = originalData.Stride * original.Height;
            byte[] pixelData = new byte[bytes];

            // Copy the pixel data from the original bitmap
            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, pixelData, 0, bytes);

            // Convert the pixels to grayscale
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                // Calculate the gray value
                byte gray = (byte)(.299 * pixelData[i + 2] + .587 * pixelData[i + 1] + .114 * pixelData[i]);

                // Set the gray value to all three color components
                pixelData[i] = gray;        // Blue
                pixelData[i + 1] = gray;    // Green
                pixelData[i + 2] = gray;    // Red
                                            // Alpha value remains unchanged
            }

            // Copy the modified pixel data back to the new bitmap
            System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, grayscaleData.Scan0, bytes);

            // Unlock the bits of both bitmaps
            original.UnlockBits(originalData);
            grayscaleBitmap.UnlockBits(grayscaleData);

            return grayscaleBitmap;
        }



        public  Bitmap UnsharpMask(Bitmap image, int blurRadius, double amount, int threshold)
    {
        // First, create the blurred version of the original image
        Bitmap blurredImage = GaussianBlur(image, blurRadius);

        // Lock bits of the original and the blurred images for faster pixel operations
        BitmapData originalData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                                                 ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData blurredData = blurredImage.LockBits(new Rectangle(0, 0, blurredImage.Width, blurredImage.Height),
                                                       ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        int byteCount = originalData.Stride * originalData.Height;
        byte[] originalPixels = new byte[byteCount];
        byte[] blurredPixels = new byte[byteCount];

        // Copy pixel data to arrays
        System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, originalPixels, 0, byteCount);
        System.Runtime.InteropServices.Marshal.Copy(blurredData.Scan0, blurredPixels, 0, byteCount);

        // Unlock bits for the blurred image, we are done with it
        blurredImage.UnlockBits(blurredData);
        blurredImage.Dispose();

        // Create result image and lock bits
        Bitmap resultImage = new Bitmap(image.Width, image.Height);
        BitmapData resultData = resultImage.LockBits(new Rectangle(0, 0, resultImage.Width, resultImage.Height),
                                                     ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] resultPixels = new byte[byteCount];

        int pixelBufferOffset = 0;
        int calcOffset = 0;

        byte ClampToByte(double color)
        {
            return (byte)(Math.Max(0, Math.Min(255, color)));
        }

        // Iterate through the pixel data
        for (int i = 0; i < byteCount; i += 4)
        {
            // Calculate the color difference
            double blueDifference = originalPixels[i] - blurredPixels[i];
            double greenDifference = originalPixels[i + 1] - blurredPixels[i + 1];
            double redDifference = originalPixels[i + 2] - blurredPixels[i + 2];

            if (Math.Abs(blueDifference) > threshold || Math.Abs(greenDifference) > threshold || Math.Abs(redDifference) > threshold)
            {
                resultPixels[i] = ClampToByte(originalPixels[i] + amount * blueDifference);
                resultPixels[i + 1] = ClampToByte(originalPixels[i + 1] + amount * greenDifference);
                resultPixels[i + 2] = ClampToByte(originalPixels[i + 2] + amount * redDifference);
                resultPixels[i + 3] = originalPixels[i + 3]; // Copy alpha channel
            }
            else
            {
                resultPixels[i] = originalPixels[i];
                resultPixels[i + 1] = originalPixels[i + 1];
                resultPixels[i + 2] = originalPixels[i + 2];
                resultPixels[i + 3] = originalPixels[i + 3]; // Copy alpha channel
            }
        }

        // Copy result pixels back to the result image
        System.Runtime.InteropServices.Marshal.Copy(resultPixels, 0, resultData.Scan0, byteCount);
        resultImage.UnlockBits(resultData);

        // Unlock bits for the original image, we are done with it
        image.UnlockBits(originalData);

        return resultImage;
    }

    // Gaussian Blur function needs to be defined separately as provided earlier



    public Bitmap GaussianBlur(Bitmap image, int radius)
        {
        double[,] CreateGaussianKernel(int size, double weight)
        {
            double[,] kernel = new double[size, size];
            double twoWeightSquare = 2 * weight * weight;
            double sigmaRoot = 2 * Math.PI * weight * weight;
            double total = 0.0;

            int kernelRadius = size / 2;
            for (int y = -kernelRadius; y <= kernelRadius; y++)
            {
                for (int x = -kernelRadius; x <= kernelRadius; x++)
                {
                    double distance = x * x + y * y;
                    kernel[y + kernelRadius, x + kernelRadius] = Math.Exp(-distance / twoWeightSquare) / sigmaRoot;
                    total += kernel[y + kernelRadius, x + kernelRadius];
                }
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] /= total;
                }
            }

            return kernel;
        }

        byte Clamp(double value)
        {
            return (byte)(Math.Max(0, Math.Min(255, value)));
        }

        Bitmap blurred = new Bitmap(image.Width, image.Height,image.PixelFormat);

        using (Graphics graphics = Graphics.FromImage(blurred))
        {
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
        }

        int size = radius * 2 + 1;
        double weight = radius / 3.0;
        double[,] kernel = CreateGaussianKernel(size, weight);

            BitmapData srcData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, image.PixelFormat);// PixelFormat.Format32bppArgb);
            BitmapData dstData = blurred.LockBits(new Rectangle(0, 0, blurred.Width, blurred.Height), ImageLockMode.WriteOnly, blurred.PixelFormat);// PixelFormat.Format32bppArgb);

        int bytes = srcData.Stride * srcData.Height;
        byte[] pixelBuffer = new byte[bytes];
        byte[] resultBuffer = new byte[bytes];

        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, pixelBuffer, 0, bytes);

        image.UnlockBits(srcData);

        int filterOffset = (size - 1) / 2;
        int calcOffset = 0;

        int byteOffset = 0;
        byte blue = 0;
        byte green = 0;
        byte red = 0;

        for (int offsetY = filterOffset; offsetY < image.Height - filterOffset; offsetY++)
        {
            for (int offsetX = filterOffset; offsetX < image.Width - filterOffset; offsetX++)
            {
                blue = green = red = 0;

                byteOffset = offsetY * srcData.Stride + offsetX * 4;

                for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                {
                    for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                    {

                        calcOffset = byteOffset + (filterX * 4) + (filterY * srcData.Stride);

                        blue += (byte)(pixelBuffer[calcOffset] * kernel[filterY + filterOffset, filterX + filterOffset]);
                        green += (byte)(pixelBuffer[calcOffset + 1] * kernel[filterY + filterOffset, filterX + filterOffset]);
                        red += (byte)(pixelBuffer[calcOffset + 2] * kernel[filterY + filterOffset, filterX + filterOffset]);
                    }
                }

                resultBuffer[byteOffset] = Clamp(blue);
                resultBuffer[byteOffset + 1] = Clamp(green);
                resultBuffer[byteOffset + 2] = Clamp(red);
                resultBuffer[byteOffset + 3] = 255; // Alpha channel for 32bpp
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(resultBuffer, 0, dstData.Scan0, bytes);
        blurred.UnlockBits(dstData);

        return blurred;
    }


        public static List<List<Point>> FindContours(Bitmap bitmap, byte threshold )
        {
            // Thresholding constants
            //const byte threshold = 128;
            const byte white = 255;
            const byte black = 0;

            int width = bitmap.Width;
            int height = bitmap.Height;
            var contours = new List<List<Point>>();

            // Convert to grayscale and threshold
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte* row = ptr + (y * bitmapData.Stride);
                        byte color = (byte)((row[x * 3] * 0.11) + (row[x * 3 + 1] * 0.59) + (row[x * 3 + 2] * 0.3));
                        if (color > threshold) // Above the threshold, mark as white
                        {
                            row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = white;
                        }
                        else // Below the threshold, mark as black
                        {
                            row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = black;
                        }
                    }
                }
            }
            bitmap.UnlockBits(bitmapData);

            // Contour tracing
            bool[,] visited = new bool[width, height];
            var directions = new Point[] { new Point(0, 1), new Point(1, 0), new Point(0, -1), new Point(-1, 0) }; // Down, Right, Up, Left
            var currentDirection = 0; // Start by moving down

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (bitmap.GetPixel(x, y).R == black && !visited[x, y])
                    {
                        // Start a new contour
                        List<Point> contour = new List<Point>();
                        Point currentPoint = new Point(x, y);
                        Point startPoint = currentPoint;
                        do
                        {
                            contour.Add(currentPoint);
                            visited[currentPoint.X, currentPoint.Y] = true;

                            // Find the next point in the contour
                            Point nextPoint = Point.Empty;
                            for (int i = 0; i < directions.Length; i++)
                            {
                                Point checkPoint = new Point(currentPoint.X + directions[currentDirection].X, currentPoint.Y + directions[currentDirection].Y);
                                if (checkPoint.X >= 0 && checkPoint.X < width && checkPoint.Y >= 0 && checkPoint.Y < height &&
                                    bitmap.GetPixel(checkPoint.X, checkPoint.Y).R == black && !visited[checkPoint.X, checkPoint.Y])
                                {
                                    nextPoint = checkPoint;
                                    break;
                                }
                                currentDirection = (currentDirection + 1) % directions.Length; // Change direction clockwise
                            }

                            if (nextPoint != Point.Empty)
                            {
                                currentPoint = nextPoint;
                            }
                            else
                            {
                                // No next point found, end of contour
                                break;
                            }

                        } while (currentPoint != startPoint); // End when we have returned to the start

                        if (contour.Count > 0)
                        {
                            contours.Add(contour);
                        }
                    }
                }
            }

            return contours;
        }
        public static List<double> CalculateContourAreas(List<List<Point>> contours)
        {
            var areas = new List<double>();

            foreach (var contour in contours)
            {
                double area = 0.0;
                int j = contour.Count - 1; // The last vertex is the 'previous' one to the first for the area calculation.

                for (int i = 0; i < contour.Count; i++)
                {
                    area += (contour[j].X + contour[i].X) * (contour[j].Y - contour[i].Y);
                    j = i; // j is previous vertex to i
                }

                areas.Add(Math.Abs(area / 2.0)); // The absolute value of area / 2 gives the area.
            }

            return areas;
        }

        public  double CalculateContourArea(List<Point> contour)
        {
            double area = 0.0;

            if (contour.Count > 2) // Need at least three points to form a polygon
            {
                for (int i = 0; i < contour.Count; i++)
                {
                    Point p1 = contour[i];
                    Point p2 = contour[(i + 1) % contour.Count]; // Modulo to wrap around to the first point

             
                        area += (p1.X * p2.Y - p2.X * p1.Y);
                }

                area = Math.Abs(area) * 0.5;
            }

            return area;
        }
        public  Rectangle GetBoundingRect(List<Point> contour)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (Point pt in contour)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
        public  Bitmap DrawRectangle(Bitmap original, Rectangle rect, Color color)
    {
        // Lock the original bitmap in memory
        BitmapData bitmapData = original.LockBits(
            new Rectangle(0, 0, original.Width, original.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        unsafe
        {
            // Get the pointer to the bitmap's pixel data
            byte* ptrFirstPixel = (byte*)bitmapData.Scan0;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    // Calculate the position of the pixel
                    byte* currentLine = ptrFirstPixel + (y * bitmapData.Stride);
                    int position = x * 4; // 4 bytes per pixel for 32bppArgb

                    // Draw only the border of the rectangle
                    if (x == rect.Left || x == rect.Right - 1 || y == rect.Top || y == rect.Bottom - 1)
                    {
                        currentLine[position] = color.B;     // Blue component
                        currentLine[position + 1] = color.G; // Green component
                        currentLine[position + 2] = color.R; // Red component
                        currentLine[position + 3] = color.A; // Alpha component
                    }
                }
            }
        }

        // Unlock the bits
        original.UnlockBits(bitmapData);

        return original;
    }
        public  Bitmap ResizeImage(Bitmap originalImage, int width, int height)
        {
            // Create a new empty bitmap to hold the resized image
            Bitmap resizedImage = new Bitmap(width, height);

            // Use a graphics object to draw the resized image into the bitmap
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                // Set the resize quality modes to high quality
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;//.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.None;

                // Draw the original image into the new bitmap, resizing it
                graphics.DrawImage(originalImage, 0, 0, width, height);
            }

            // Return the resized bitmap
            return resizedImage;
        }





        public  Bitmap IncreaseImageDpi(Bitmap originalImage, int dpi)
        {
            // Create a new bitmap with the same dimensions as the original image
            Bitmap newImage = new Bitmap(originalImage.Width, originalImage.Height, originalImage.PixelFormat);

            // Set the resolution of the new image to 300 DPI
            newImage.SetResolution(dpi, dpi);

            // Use a graphics object to draw the original image onto the new image
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);
            }

            return newImage;
        }




        public static Bitmap RescaleImageToDpi(Bitmap originalImage, int targetDpi)
        {
            // Determine the current DPI of the image.
            float currentDpiX = originalImage.HorizontalResolution;
            float currentDpiY = originalImage.VerticalResolution;

            // Calculate the size of the image in inches.
            float widthInInches = originalImage.Width / currentDpiX;
            float heightInInches = originalImage.Height / currentDpiY;

            // Calculate the new size in pixels based on the desired DPI.
            int newWidth = (int)(widthInInches * targetDpi);
            int newHeight = (int)(heightInInches * targetDpi);

            // Create a new bitmap with the new dimensions and desired DPI.
            Bitmap newImage = new Bitmap(newWidth, newHeight, originalImage.PixelFormat);
            newImage.SetResolution(targetDpi, targetDpi);

            // Use a graphics object to draw the resized image.
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Set high-quality image resizing settings.
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor; //.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.None;//.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                // Draw the original image, resized to the new dimensions.
                graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }

            return newImage;
        }

        /// <summary>
        /// Takes a bitmap and converts it to an image that can be handled by WPF ImageBrush
        /// </summary>
        /// <param name="src">A bitmap image</param>
        /// <returns>The image as a BitmapImage for WPF</returns>
        public static BitmapImage Convert(Bitmap src)
        {
            MemoryStream ms = new MemoryStream();
            ((System.Drawing.Bitmap)src).Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }






    }


}

using OpenCvSharp;
using System;

namespace RuneReader.Classes.OpenCV;

internal class ImageProcessingOpenCv
{


    public static void GammaCorrection(Mat src, Mat dst, double gamma)
    {
        var invGamma = 1.0 / gamma;

        using var table = new Mat(1, 256, MatType.CV_8U);
        for (int i = 0; i < 256; ++i)
        {
            table.Set(0, i, (int)(Math.Pow(i / 255.0f, invGamma) * 255.0f));
        }

        Cv2.LUT(src, table, dst);
    }

    public static void ApplyContrastBrightness(Mat src, Mat dst, double brightness, double contrast)
    {
        // ReSharper disable TooWideLocalVariableScope
        var shadow = 0.0;
        var highlight = 0.0;
        var alphaB = 0.0;
        var gammaB = 0.0;
        // ReSharper enable TooWideLocalVariableScope
        
        if (brightness != 0.0)
        {
            if (brightness > 0.0)
            {
                shadow = brightness;
                highlight = 255.0;
            }
            else
            {
                shadow = 0.0;
                highlight = 255.0 + brightness;
            }
            alphaB = (highlight - shadow) / 255.0;
            gammaB = shadow;
            Cv2.AddWeighted(src, alphaB, src, 0, gammaB, dst);

        }
        else
        {
            dst = src.Clone();
        }

        if (contrast != 0.0)
        {
            var f = 131.0 * (contrast + 127.0) / (127.0 * (131.0 - contrast));
            var alphaC = f;
            var gammaC = 127.0 * (1 - f);

            Cv2.AddWeighted(dst, alphaC, dst, 0, gammaC, dst);

        }



    }

    private static Scalar ConvertRgbToLabRange(Scalar rgbColor, double threshold, bool? isLowerBound)
    {
        using var rgbMat = new Mat(1, 1, MatType.CV_8UC4, rgbColor);
        using var hsvMat = new Mat();
        Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2Lab);
        //Mat test = hsvMat.ExtractChannel(2);
        //test.At<Vec3b>(0, 0);

        if (threshold > 1.0) { threshold = 1.0; }
        if (threshold < 0.0) { threshold = 0.001; }
        var hsvColor = hsvMat.Get<Vec4b>(0, 0);

        // Adjust the HSV range based on the tolerance
        int l = hsvColor[0];
        int a = hsvColor[1];
        int b = hsvColor[2];
        int c = hsvColor[3];
        var lTol = (int)(l * threshold);

        //if (h + hTol > 255) { hTol = 0; }
        //if (s + sTol > 255) { sTol = 255; }
        //if (v + vTol > 255) { vTol = 255; }
        //if (h - hTol < 0) { hTol = 0; }
        //if (s - sTol < 0) { sTol = 0; }
        //if (v - vTol < 0) { vTol = 0; }

        if (isLowerBound == null)
        {
            return new Scalar(
                l,
                a,
                b,
                c);

        }
        else
            return new Scalar(
                //isLowerBound.Value ? h - 10 : h + 10,
                //isLowerBound.Value ? s - 20 : s + 20,
                //isLowerBound.Value ? v - vTol : v + vTol);
                isLowerBound.Value ? l - lTol : l + lTol,
                isLowerBound.Value ? a : a,
                isLowerBound.Value ? b : b,
                isLowerBound.Value ? c : c);

    }

    private static Scalar ConvertRgbToHsvRange(Scalar rgbColor, double threshold, bool? isLowerBound)
    {
        using var rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
        using var hsvMat = new Mat();
        Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2HSV_FULL);

        if (threshold > 1.0) { threshold = 0.7; }
        if (threshold < 0.0) { threshold = 0.0; }
        var hsvColor = hsvMat.Get<Vec4b>(0, 0);

        // Adjust the HSV range based on the tolerance
        int h = hsvColor[0];
        int s = hsvColor[1];
        int v = hsvColor[2];
        var hTol = (double)(h * 0.01);
        var sTol = (double)(s * 0.05);
        var vTol = (double)(v * threshold);

        var constantVarianceHl = 255.0 * 0.01;
        var constantVarianceSl = 255.0 * 0.05;
        var constantVarianceVl = 255.0 * threshold;

        var constantVarianceHh = 255.0 * 0.01;
        var constantVarianceSh = 255.0 * 0.05;
        var constantVarianceVh = 255.0 * threshold;


        //double constantVarianceHL = h * (0.025);
        //double constantVarianceSL = s * (0.13);
        //double constantVarianceVL = v * (Threshold);

        //double constantVarianceHH = h * (0.00);
        //double constantVarianceSH = s * (0.03);
        //double constantVarianceVH = v * (Threshold);


        if (isLowerBound == null)
        {
            return new Scalar(
                h,
                s,
                v);

        }
        else

        {
            byte h1;
            byte s1;
            byte v1;

            if (isLowerBound.Value)
            {
                h1 = (byte)Math.Max(Math.Round(h - constantVarianceHl, 0.0), 0.0);
                s1 = (byte)Math.Max(Math.Round(s - constantVarianceSl, 0.0), 0.0);
                v1 = (byte)Math.Max(Math.Round(v - constantVarianceVl, 0.0), 0.0);
            }
            else
            {
                h1 = (byte)Math.Min(Math.Round(h + constantVarianceHh, 0.0), 255.0);
                s1 = (byte)Math.Min(Math.Round(s + constantVarianceSh, 0.0), 255.0);
                v1 = (byte)Math.Min(Math.Round(v + constantVarianceVh, 0.0), 255.0);
            }

            return new Scalar(h1, s1, v1);

            //return new Scalar(
            //    //isLowerBound.Value ? h - 10 : h + 10,
            //    //isLowerBound.Value ? s - 20 : s + 20,
            //    //isLowerBound.Value ? v - vTol : v + vTol);
            //    isLowerBound.Value ? Math.Floor(h - hTol) : Math.Ceiling(h + hTol),
            //    isLowerBound.Value ? Math.Floor(s - sTol) : Math.Ceiling(s + sTol),
            //    isLowerBound.Value ? Math.Floor(v - vTol) : Math.Ceiling(v + vTol)
            //    );
        }
    }


    private static Scalar ConvertRgbToHlsRange(Scalar rgbColor, double threshold, bool? isLowerBound)
    {
        using var rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
        using var hslMat = new Mat();
        Cv2.CvtColor(rgbMat, hslMat, ColorConversionCodes.BGR2HLS); //.BGR2HLS_FULL
        Vec3b hslColor = hslMat.Get<Vec3b>(0, 0);

        // Adjust the HSL range based on the tolerance
        int h = hslColor[0];
        int l = hslColor[1];
        int s = hslColor[2];

        int hTol = 0;// (int)(h * Threshold);
        int lTol = (int)(l * threshold);
        int sTol = (int)(s * threshold);
        if (h + hTol > 255) { hTol = 0; }
        if (l + lTol > 255) { lTol = 0; }
        if (s + sTol > 255) { sTol = 0; }
        if (h - hTol < 0) { hTol = 0; }
        if (l - lTol < 0) { lTol = 0; }
        if (s - sTol < 0) { sTol = 0; }

        //if (isLowerBound == null)
        //{
        //    return new Scalar(
        //        h ,
        //        l ,
        //        s );

        //} else

        return new Scalar(
            isLowerBound.Value ? h - hTol : h + hTol,
            isLowerBound.Value ? l - lTol : l + lTol,
            isLowerBound.Value ? s - sTol : s + sTol

        );
    }



    private static Scalar ConvertBgrToBgrRange(Scalar rgbColor, double threshold, bool? isLowerBound)
    {
        // Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
        // Mat hslMat = new Mat();
        // Cv2.CvtColor(rgbMat, hslMat, ColorConversionCodes.RGB2BGR); //.BGR2HLS_FULL
        // Vec3b hslColor = rgbMat.Get<Vec3b>(0, 0);

        // Adjust the HSL range based on the tolerance
        byte b = (byte)rgbColor[0];
        byte g = (byte)rgbColor[1];
        byte r = (byte)rgbColor[2];
        int bTol = (int)(b * threshold);// Threshold);
        int gTol = (int)(g * threshold);
        int rTol = (int)(r * threshold);

        if (b + bTol > 255) { bTol = 0; }
        if (g + gTol > 255) { gTol = 0; }
        if (r + rTol > 255) { rTol = 0; }
        if (b - bTol < 0) { bTol = 0; }
        if (g - gTol < 0) { gTol = 0; }
        if (r - rTol < 0) { rTol = 0; }


        if (isLowerBound == null)
        {
            return new Scalar(
                b,
                g,
                r);

        }
        else

            return new Scalar(
                isLowerBound.Value ? b - bTol : b + bTol,
                isLowerBound.Value ? g - gTol : g + gTol,
                isLowerBound.Value ? r - rTol : r + rTol);
    }



    public static Mat IsolateColorLab(Mat src, Scalar rgbColor, double threshold)
    {
        // Convert the RGB color and tolerance to HSV
        var upperBound = ConvertRgbToLabRange(rgbColor, threshold, false);
        var lowerBound = ConvertRgbToLabRange(rgbColor, threshold, true);
        //      Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);

        // Convert the image to HSV color space
        using var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2Lab);

        // Create a mask for the desired color range
        using var mask = new Mat();
        Cv2.InRange(hsv, lowerBound, upperBound, mask);

        // Bitwise-AND mask and original image to isolate the color
        var result = new Mat();
        Cv2.BitwiseAnd(src, src, result, mask);
        mask.Dispose();
        hsv.Dispose();
        //         Cv2.CvtColor(result, result, ColorConversionCodes.Lab2BGR);


        return result;
    }



    public static Mat IsolateColorHsv(Mat src, Scalar rgbColor, double threshold)
    {
        // Convert the RGB color and tolerance to HSV
        var upperBound = ConvertRgbToHsvRange(rgbColor, threshold, false);
        var lowerBound = ConvertRgbToHsvRange(rgbColor, threshold, true);
        //      Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);

        // Works  Uses WAY to much CPU
        using var deNoised = new Mat();
        //Cv2.FastNlMeansDenoisingColored(src, deNoised, 2, 3, 7, 21);
        //       Cv2.Blur(src,deNoised,new Size(3,3));
        //Cv2.GaussianBlur(src, deNoised, new Size(3, 3), 0,0);
        Cv2.MedianBlur(src, deNoised, 5);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 1));

        // Dilate the image
        using var dilated = new Mat();
        Cv2.Dilate(deNoised, dilated, kernel, iterations: 5);



        // Convert the image to HSV color space
        using var hsv = new Mat();
        Cv2.CvtColor(dilated, hsv, ColorConversionCodes.BGR2HSV_FULL);

        // Create a mask for the desired color range
        using var mask = new Mat();
        Cv2.InRange(hsv, lowerBound, upperBound, mask);

        // Bitwise-AND mask and original image to isolate the color
        Mat result = new Mat();
        Cv2.BitwiseNot(mask, result);

        //      Cv2.BitwiseAnd(src, src, result, mask);
        //mask.Dispose();
        //hsv.Dispose();
        //Cv2.CvtColor(result, result, ColorConversionCodes.BGR2HSV);


        return result;
    }



    public static Mat IsolateColorHls(Mat src, Scalar rgbColor, double threshold)
    {
        // Convert the RGB color and tolerance to HSV
        var upperBound = ConvertRgbToHlsRange(rgbColor, threshold, false);
        var lowerBound = ConvertRgbToHlsRange(rgbColor, threshold, true);
        //    Scalar centerBound = ConvertRgbToHlsRange(rgbColor, Threshold, null);

        // Convert the image to HSV color space
        using var hls = new Mat();
        Cv2.CvtColor(src, hls, ColorConversionCodes.BGR2HLS);

        // Create a mask for the desired color range
        using var mask = new Mat();
        Cv2.InRange(hls, lowerBound, upperBound, mask);

        // Bitwise-AND mask and original image to isolate the color
        var result = new Mat();
        Cv2.BitwiseAnd(src, src, result, mask);

        return result;
    }

    public static Mat IsolateColorRgb(Mat src, Scalar rgbColor, double threshold)
    {
        // Convert the RGB color and tolerance to HSV
        var upperBound = ConvertBgrToBgrRange(rgbColor, threshold, false);
        var lowerBound = ConvertBgrToBgrRange(rgbColor, threshold, true);
        //           Scalar centerBound = ConvertBGRToBGRRange(rgbColor, Threshold, null);

        // Convert the image to HSV color space
        //       Mat hsv = new Mat();
        //  Cv2.CvtColor(src, src, ColorConversionCodes.RGB2BGR);
        //            Mat hsv = src.Clone();

        // Create a mask for the desired color range
        using var mask = new Mat();
        Cv2.InRange(src, lowerBound, upperBound, mask);

        // Bitwise-AND mask and original image to isolate the color
        var result = new Mat();
        Cv2.BitwiseAnd(src, src, result, mask);

        return result;
    }


    public static Mat RescaleImageToNewDpi(Mat src, double currentDpi, double newDpi)
    {

        // Calculate the scaling factor
        var scaleFactor = newDpi / currentDpi;

        // Calculate the new dimensions
        var newWidth = (int)(src.Width * scaleFactor);
        var newHeight = (int)(src.Height * scaleFactor);

        // Resize the image
        var resizedImage = new Mat();
        Cv2.Resize(src, resizedImage, new Size(newWidth, newHeight), interpolation: InterpolationFlags.Cubic);

        return resizedImage;
    }


    public static bool IsThereAnImageInTopLeftQuarter(Mat src)
    {

        var x = src.Width / 8 + src.Width / 16;
        var y = src.Height / 16;
        var width = src.Width / 2 - src.Width / 4;
        var height = src.Height / 2 / 2;
        Rect roi = new Rect(x, y, width, height);

        using Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);

        using Mat edges = new Mat();
        var x1 = Cv2.Mean(firstQuarter);
        if (x1.Val0 <= 250)
            return true;
        else
            return false;

    }

    public static bool IsThereAnImageInTopRightQuarter(Mat src)
    {
        var x1 = src.Width / 2 + src.Width / 16;
        var y1 = src.Height / 16;
        var width1 = src.Width / 2 - src.Width / 4;
        var height1 = src.Height / 2 / 2;
        Rect roi1 = new Rect(x1, y1, width1, height1);


        using var firstQuarter = new Mat(src, roi1);

        //using Mat edges = new Mat();

        var x2 = Cv2.Mean(firstQuarter);
        if (x2.Val0 <= 250)
            return true;
        else
            return false;

    }

    public static bool IsThereAnImageInBottomLeftQuarter(Mat src)
    {
        var x = src.Width / 8 + src.Width / 16;
        var y = src.Height / 2 + src.Height / 8;
        var width = src.Width / 2 - src.Width / 4;
        var height = src.Height / 2 / 2;
        var roi = new Rect(x, y, width, height);

        using var firstQuarter = src.Clone(roi);// new Mat(src, roi);

        //using var edges = new Mat();
        var x1 = Cv2.Mean(firstQuarter);
        if (x1.Val0 <= 250)
            return true;
        else
            return false;

    }

    public static bool IsThereAnImageInBottomCenter(Mat src)
    {
        var width = src.Width / 2 - src.Width / 4;
        var height = src.Height / 2 / 2;
        var x = src.Width / 2 - width / 2;
        var y = src.Height / 2 + src.Height / 8;

        var roi = new Rect(x, y, width, height);
        using var firstQuarter = src.Clone(roi);// new Mat(src, roi);
        //using Mat edges = new Mat();
        var x1 = Cv2.Mean(firstQuarter);
        if (x1.Val0 <= 250)
            return true;
        else
            return false;

    }

    public static void FillRectangle(ref Mat src, Rect rect, Scalar color)
    {
        Cv2.Rectangle(src, rect, color, -1);

    }

    public static void DrawMarkers(ref Mat src)
    {
        Cv2.Line(src, src.Width / 2, 0, src.Width / 2, src.Height, Scalar.FromRgb(255, 0, 0), 2, LineTypes.AntiAlias);
        Cv2.Line(src, 0, src.Height / 2, src.Width, src.Height / 2, Scalar.FromRgb(255, 0, 0), 2, LineTypes.AntiAlias);


        //Draw top left sensor
        var x = src.Width / 8 + src.Width / 16;
        var y = src.Height / 16;
        var width = src.Width / 2 - src.Width / 4;
        var height = src.Height / 2 / 2;
        var roi = new Rect(x, y, width, height);
        Cv2.Rectangle(src, roi, Scalar.Red, 2, LineTypes.AntiAlias);

        //Draw top right sensor
        var x1 = src.Width / 2 + src.Width / 16;
        var y1 = src.Height / 16;
        var width1 = src.Width / 2 - src.Width / 4;
        var height1 = src.Height / 2 / 2;
        var roi1 = new Rect(x1, y1, width1, height1);
        Cv2.Rectangle(src, roi1, Scalar.Red, 2, LineTypes.AntiAlias);


        //Draw Left Lower Sensor
        var x2 = src.Width / 8 + src.Width / 16;
        var y2 = src.Height / 2 + src.Height / 8;
        var width2 = src.Width / 2 - src.Width / 4;
        var height2 = src.Height / 2 / 2;
        var roi2 = new Rect(x2, y2, width2, height2);
        Cv2.Rectangle(src, roi2, Scalar.Red, 2, LineTypes.AntiAlias);

        //Draw Bottom Center Sensor
        var width3 = src.Width / 2 - src.Width / 4;
        var height3 = src.Height / 2 / 2;
        var x3 = src.Width / 2 - width3 / 2;
        var y3 = src.Height / 2 + src.Height / 8;

        var roi3 = new Rect(x3, y3, width3, height3);
        Cv2.Rectangle(src, roi3, Scalar.Blue, 2, LineTypes.AntiAlias);

    }

}
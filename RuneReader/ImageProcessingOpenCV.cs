using Microsoft.Windows.Themes;
using OpenCvSharp;
using SharpGen.Runtime;
using System;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace RuneReader
{
    internal class ImageProcessingOpenCV
    {


        public static void gammaCorrection(Mat src, Mat dst, double gamma)
        {
            double invGamma = 1.0 / gamma;

            Mat table = new Mat(1, 256, MatType.CV_8U);
            for (int i = 0; i < 256; ++i)
            {
                table.Set(0, i, (int)(Math.Pow(i / 255.0f, invGamma) * 255.0f));
            }

            Cv2.LUT(src, table, dst);
        }

        public static void applyContrastBrightness(Mat src, Mat dst,  double brightness, double contrast)
        {
            double shadow = 0.0;
            double highlight = 0.0;
            double alpha_b = 0.0;
            double gamma_b = 0.0;
            //Mat result = new Mat();
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
                alpha_b = (highlight - shadow) / 255.0;
                gamma_b = shadow;
                Cv2.AddWeighted(src, alpha_b, src, 0, gamma_b, dst);

            }
            else
            {
                dst = src.Clone();
            }

            if (contrast != 0.0)
            {
                double f = 131.0*(contrast + 127.0)/(127.0*(131.0-contrast));
                double alpha_c = f;
                double gamma_c = 127.0 * (1 - f);

                Cv2.AddWeighted(dst, alpha_c, dst, 0, gamma_c, dst);

            }



        }

        private static Scalar ConvertRgbToLabRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            Mat rgbMat = new Mat(1, 1, MatType.CV_8UC4, rgbColor);
            Mat hsvMat = new Mat();
            Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2Lab);
            //Mat test = hsvMat.ExtractChannel(2);
            //test.At<Vec3b>(0, 0);

            if (Threshold > 1.0) { Threshold = 1.0; }
            if (Threshold < 0.0) { Threshold = 0.001; }
            Vec4b hsvColor = hsvMat.Get<Vec4b>(0, 0);

            // Adjust the HSV range based on the tolerance
            int l = hsvColor[0];
            int a = hsvColor[1];
            int b = hsvColor[2];
            int c = hsvColor[3];
            int lTol = (int)(l * Threshold);

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
                    isLowerBound.Value ? a : a ,
                    isLowerBound.Value ? b : b ,
                    isLowerBound.Value ? c : c);

        }

        private static Scalar ConvertRgbToHsvRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            using Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
            using Mat hsvMat = new Mat();
            Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2HSV_FULL);
            
            if (Threshold > 1.0) { Threshold = 0.9; }
            if (Threshold < 0.0) { Threshold = 0.0; }
            Vec4b hsvColor = hsvMat.Get<Vec4b>(0, 0);

            // Adjust the HSV range based on the tolerance
            int h = hsvColor[0];
            int s = hsvColor[1];
            int v = hsvColor[2];
            double hTol = (double)(h * 0.05);
            double sTol = (double)(s * 0.10);
            double vTol = (double)(v * Threshold) ;

            double constantVarianceHL = 255.0 * (0.01);
            double constantVarianceSL = 255.0 * (0.05);
            double constantVarianceVL = 255.0 * (Threshold);

            double constantVarianceHH = 255.0 * (0.01);
            double constantVarianceSH = 255.0 * (0.05);
            double constantVarianceVH = 255.0 * (Threshold);


            //double constantVarianceHL = h * (0.01);
            //double constantVarianceSL = s * (0.03);
            //double constantVarianceVL = v * (Threshold);

            //double constantVarianceHH = h * (0.01);
            //double constantVarianceSH = s * (0.05);
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
                    h1 = (byte)Math.Max(Math.Round((double)h - constantVarianceHL, 0.0),0.0);
                    s1 = (byte)Math.Max(Math.Round((double)s - constantVarianceSL, 0.0),0.0);
                    v1 = (byte)Math.Max(Math.Round((double)v - constantVarianceVL, 0.0),0.0);
                    return new Scalar(h1, s1, v1);
                }
                else
                {
                    h1 = (byte)Math.Min(Math.Round((double)h + constantVarianceHH, 0.0), 255.0);
                    s1 = (byte)Math.Min(Math.Round((double)s + constantVarianceSH, 0.0), 255.0);
                    v1 = (byte)Math.Min(Math.Round((double)v + constantVarianceVH, 0.0), 255.0);
                    return new Scalar(h1,s1,v1);
                }

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


        public static Scalar ConvertRgbToHlsRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
            Mat hslMat = new Mat();
            Cv2.CvtColor(rgbMat, hslMat, ColorConversionCodes.BGR2HLS); //.BGR2HLS_FULL
            Vec3b hslColor = hslMat.Get<Vec3b>(0, 0);

            // Adjust the HSL range based on the tolerance
            int h = hslColor[0];
            int l = hslColor[1];
            int s = hslColor[2];

            int hTol = 0;// (int)(h * Threshold);
            int lTol = (int)(l * Threshold);
            int sTol = (int)(s * Threshold);
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



        private static Scalar ConvertBGRToBGRRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            // Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
            // Mat hslMat = new Mat();
            // Cv2.CvtColor(rgbMat, hslMat, ColorConversionCodes.RGB2BGR); //.BGR2HLS_FULL
            // Vec3b hslColor = rgbMat.Get<Vec3b>(0, 0);

            // Adjust the HSL range based on the tolerance
            byte b = (byte)rgbColor[0];
            byte g = (byte)rgbColor[1];
            byte r = (byte)rgbColor[2];
            int bTol = (int)(b * Threshold);// Threshold);
            int gTol = (int)(g * Threshold);
            int rTol = (int)(r * Threshold);

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



        public static Mat IsolateColorLab(Mat src, Scalar rgbColor, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertRgbToLabRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertRgbToLabRange(rgbColor, Threshold, true);
            //      Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);

            // Convert the image to HSV color space
            Mat hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2Lab);

            // Create a mask for the desired color range
            Mat mask = new Mat();
            Cv2.InRange(hsv, lowerBound, upperBound, mask);

            // Bitwise-AND mask and original image to isolate the color
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);
            mask.Dispose();
            hsv.Dispose();
   //         Cv2.CvtColor(result, result, ColorConversionCodes.Lab2BGR);


            return result;
        }



        public static Mat IsolateColorHSV(Mat src, Scalar rgbColor, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertRgbToHsvRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertRgbToHsvRange(rgbColor, Threshold, true);
            //      Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);
        
            // Works  Uses WAY to much CPU
            using Mat deNoised = new Mat();
            //Cv2.FastNlMeansDenoisingColored(src, deNoised, 2, 3, 7, 21);
     //       Cv2.Blur(src,deNoised,new Size(3,3));
            //Cv2.GaussianBlur(src, deNoised, new Size(3, 3), 0,0);
            Cv2.MedianBlur(src, deNoised, 5);

            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 1));

            // Dilate the image
            Mat dilated = new Mat();
            Cv2.Dilate(deNoised, dilated, kernel, iterations:5);

            

            // Convert the image to HSV color space
           using Mat hsv = new Mat();
            Cv2.CvtColor(dilated, hsv, ColorConversionCodes.BGR2HSV_FULL);

            // Create a mask for the desired color range
            using Mat mask = new Mat();
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



        public static Mat IsolateColorHLS(Mat src, Scalar rgbColor, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertRgbToHlsRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertRgbToHlsRange(rgbColor, Threshold, true);
            //    Scalar centerBound = ConvertRgbToHlsRange(rgbColor, Threshold, null);

            // Convert the image to HSV color space
            Mat hls = new Mat();
            Cv2.CvtColor(src, hls, ColorConversionCodes.BGR2HLS);

            // Create a mask for the desired color range
            Mat mask = new Mat();
            Cv2.InRange(hls, lowerBound, upperBound, mask);

            // Bitwise-AND mask and original image to isolate the color
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);

            return result;
        }

        public static Mat IsolateColorRGB(Mat src, Scalar rgbColor, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertBGRToBGRRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertBGRToBGRRange(rgbColor, Threshold, true);
            //           Scalar centerBound = ConvertBGRToBGRRange(rgbColor, Threshold, null);

            // Convert the image to HSV color space
            //       Mat hsv = new Mat();
            //  Cv2.CvtColor(src, src, ColorConversionCodes.RGB2BGR);
            //            Mat hsv = src.Clone();

            // Create a mask for the desired color range
            Mat mask = new Mat();
            Cv2.InRange(src, lowerBound, upperBound, mask);

            // Bitwise-AND mask and original image to isolate the color
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);

            return result;
        }


        public static Mat RescaleImageToNewDpi(Mat src, double currentDpi, double newDpi)
        {

            // Calculate the scaling factor
            double scaleFactor = newDpi / currentDpi;

            // Calculate the new dimensions
            int newWidth = (int)(src.Width * scaleFactor);
            int newHeight = (int)(src.Height * scaleFactor);

            // Resize the image
            Mat resizedImage = new Mat();
            Cv2.Resize(src, resizedImage, new OpenCvSharp.Size(newWidth, newHeight), interpolation: InterpolationFlags.Cubic);

            return resizedImage;
        }


        public static bool IsThereAnImageInTopLeftQuarter(Mat src)
        {

            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 16);
            var width = (src.Width / 2) - (src.Width / 4);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);

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
            var x1 = (src.Width / 2) + (src.Width / 16);
            var y1 = (src.Height / 16);
            var width1 = (src.Width / 2) - (src.Width / 4);
            var height1 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(x1, y1, width1, height1);


            Mat firstQuarter = new Mat(src, roi1);

           using Mat edges = new Mat();

            var x2 = Cv2.Mean(firstQuarter);
            if (x2.Val0 <= 250)
                return true;
            else
                return false;

        }

        public static bool IsThereAnImageInBottomLeftQuarter(Mat src)
        {
            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 2) + (src.Height / 8);
            var width = (src.Width / 2) - (src.Width / 4);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);

            using Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);

            using Mat edges = new Mat();
            var x1 = Cv2.Mean(firstQuarter);
            if (x1.Val0 <= 250)
                return true;
            else
                return false;

        }

        public static bool IsThereAnImageInBottomCenter(Mat src)
        {
            var width = (src.Width / 2) - (src.Width / 4);
            var height = (src.Height / 2) / 2;
            var x = (src.Width / 2) - (width / 2);
            var y = (src.Height / 2) + (src.Height / 8);

            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);
            using Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);
            using Mat edges = new Mat();
            var x1 = Cv2.Mean(firstQuarter);
            if (x1.Val0 <= 250)
                return true;
            else
                return false;

        }

        public static void FillRectangle(ref Mat src, OpenCvSharp.Rect rect, Scalar color)
        {
            Cv2.Rectangle(src, rect, color, -1);

        }

        public static void DrawMarkers(ref Mat src)
        {
            Cv2.Line(src, (int)(src.Width / 2), 0, (int)(src.Width / 2), src.Height, Scalar.FromRgb(255, 0, 0), 2, LineTypes.AntiAlias);
            Cv2.Line(src, 0, (int)(src.Height / 2), src.Width, (int)(src.Height / 2), Scalar.FromRgb(255, 0, 0), 2, LineTypes.AntiAlias);


            //Draw top left sensor
            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 16);
            var width = (src.Width / 2) - (src.Width / 4);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);
            Cv2.Rectangle(src, roi, Scalar.Red, 2, LineTypes.AntiAlias);

            //Draw top right sensor
            var x1 = (src.Width / 2) + (src.Width / 16);
            var y1 = (src.Height / 16);
            var width1 = (src.Width / 2) - (src.Width / 4);
            var height1 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(x1, y1, width1, height1);
            Cv2.Rectangle(src, roi1, Scalar.Red, 2, LineTypes.AntiAlias);


            //Draw Left Lower Sensor
            var x2 = (src.Width / 8) + (src.Width / 16);
            var y2 = (src.Height / 2) + (src.Height / 8);
            var width2 = (src.Width / 2) - (src.Width / 4);
            var height2 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi2 = new OpenCvSharp.Rect(x2, y2, width2, height2);
            Cv2.Rectangle(src, roi2, Scalar.Red, 2, LineTypes.AntiAlias);

            //Draw Bottom Center Sensor
            var width3 = (src.Width / 2) - (src.Width / 4);
            var height3 = (src.Height / 2) / 2;
            var x3 = (src.Width / 2) - (width3 / 2);
            var y3 = (src.Height / 2) + (src.Height / 8);

            OpenCvSharp.Rect roi3 = new OpenCvSharp.Rect(x3, y3, width3, height3);
            Cv2.Rectangle(src, roi3, Scalar.Blue, 2, LineTypes.AntiAlias);

        }

    }
}

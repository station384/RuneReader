using ControlzEx.Standard;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;


namespace RuneReader
{
    public class BarcodeDecode
    {
        //  private static BarcodeReaderGeneric BarcodeReaderEngine = new BarcodeReaderGeneric();
        
        public class BarcodeFindResult
        {
            public int screenID { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public class BarcodeResult
        {
            public bool BarcodeFound { get; set; }
            public String DetectedText { get; set; }
            public String DecodedTextValue { get; set; }
            public int WaitTime { get; set; }
            public bool InCombat { get; set; }
            public bool HasTarget { get; set; }

            public BarcodeResult()
            {
                DetectedText = String.Empty;
                DecodedTextValue = String.Empty;
                BarcodeFound = false;
                WaitTime = 0;
            }
        }
        
        // Calculate check digit (returns 0-9)
        public static int CalculateCheckDigit(string input)
        {
            int sum = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (!char.IsDigit(input[i]))
                    throw new ArgumentException("Input contains non-numeric characters.");

                int digit = input[i] - '0';
                int weight = (i % 2 == 0) ? 3 : 1;  // Match Lua's odd/even weighting
                sum += digit * weight;
            }
            return (10 - (sum % 10)) % 10;
        }

        // Validate full string with check digit
        public static bool ValidateWithCheckDigit(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 2)
                return false;

            string basePart = input.Substring(0, input.Length - 1);
            char checkChar = input[input.Length - 1];

            if (!char.IsDigit(checkChar))
                return false;

            int expected = checkChar - '0';
            int actual = CalculateCheckDigit(basePart);

            return expected == actual;
        }


        private static string DecodeTextValue(string s)
        {
            string result = string.Empty;
            string segment = string.Empty;
            if (s.Length >= 5)
            {
                segment = s.Substring(0, 2);
                int tInt = 0;
                if (int.TryParse(segment, out tInt))
                {
                    switch (tInt)
                    {
                        case 1:
                            result = "1";
                            break;
                        case 2:
                            result = "2";
                            break;
                        case 3:
                            result = "3";
                            break;
                        case 4:
                            result = "4";
                            break;
                        case 5:
                            result = "5";
                            break;
                        case 6:
                            result = "6";
                            break;
                        case 7:
                            result = "7";
                            break;
                        case 8:
                            result = "8";
                            break;
                        case 9:
                            result = "9";
                            break;
                        case 10:
                            result = "0";
                            break;
                        case 11:
                            result = "-";
                            break;
                        case 12:
                            result = "=";
                            break;

                        case 21:
                            result = "CF1";
                            break;
                        case 22:
                            result = "CF2";
                            break;
                        case 23:
                            result = "CF3";
                            break;
                        case 24:
                            result = "CF4";
                            break;
                        case 25:
                            result = "CF5";
                            break;
                        case 26:
                            result = "CF6";
                            break;
                        case 27:
                            result = "CF7";
                            break;
                        case 28:
                            result = "CF8";
                            break;
                        case 29:
                            result = "CF9";
                            break;
                        case 30:
                            result = "CF10";
                            break;
                        case 31:
                            result = "CF11";
                            break;
                        case 32:
                            result = "CF12";
                            break;

                        case 41:
                            result = "AF1";
                            break;
                        case 42:
                            result = "AF2";
                            break;
                        case 43:
                            result = "AF3";
                            break;
                        case 44:
                            result = "AF4";
                            break;
                        case 45:
                            result = "AF5";
                            break;
                        case 46:
                            result = "AF6";
                            break;
                        case 47:
                            result = "AF7";
                            break;
                        case 48:
                            result = "AF8";
                            break;
                        case 49:
                            result = "AF9";
                            break;
                        case 50:
                            result = "AF10";
                            break;
                        case 51:
                            result = "AF11";
                            break;
                        case 52:
                            result = "AF12";
                            break;


                        case 61:
                            result = "F1";
                            break;
                        case 62:
                            result = "F2";
                            break;
                        case 63:
                            result = "F3";
                            break;
                        case 64:
                            result = "F4";
                            break;
                        case 65:
                            result = "F5";
                            break;
                        case 66:
                            result = "F6";
                            break;
                        case 67:
                            result = "F7";
                            break;
                        case 68:
                            result = "F8";
                            break;
                        case 69:
                            result = "F9";
                            break;
                        case 70:
                            result = "F10";
                            break;
                        case 71:
                            result = "F11";
                            break;
                        case 72:
                            result = "F12";
                            break;


                        default:
                            result = string.Empty;
                            break;
                    }
                };

            }



            return result;
        }

        private static int DecodeWaitValue(string s)
        {
            int result = 0;
            string segment = string.Empty;
            if (s.Length >= 5)
            {
                segment = s.Substring(2, 3);
                if (int.TryParse(segment , out result))
                {
                    //     result = result;
                    //result = result ;
                };
                result = result * 10;
            }

            return result;
        }

        private static byte DecodeConditionsBits(string s)
        {
            byte result = 0;
            string segment = string.Empty;
            if (s.Length >= 6)
            {
                segment = s.Substring(5, 1);
                if (byte.TryParse(segment, out result))
                {
                    //     result = result;
                    //result = result ;
                };

            }

            return result;
        }

// make this static,  no need to create the objects more than once.
        private static readonly DecodingOptions hints = new DecodingOptions
        {
            PureBarcode = true, // the capture should be just the barcode and no extras
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.CODE_39 },
            TryHarder = true,
            TryInverted = false,
            AssumeCode39CheckDigit =false,
            UseCode39ExtendedMode = false
        };

        // make this static,  no need to create the objects more than once.
        private static readonly BarcodeReaderGeneric BarcodeReaderEngine = new BarcodeReaderGeneric()
        {
            AutoRotate = false,
            Options = hints
        };

        public static BarcodeResult DecodeBarcode(Mat imageMat)
        {
            BarcodeResult result = new BarcodeResult();
            
            // Decode barcode using ZXing
            ZXing.Result decodeResult = null;

            var luminanceSource = new RuneReader.Classes.OpenCV.OpenCvLuminanceSource(imageMat);

            decodeResult = BarcodeReaderEngine.Decode(luminanceSource);

            if (decodeResult != null && ValidateWithCheckDigit(decodeResult.Text))
            {
                result.BarcodeFound = true;
                result.DetectedText = decodeResult.Text;
                result.DecodedTextValue = DecodeTextValue(decodeResult.Text);
                result.WaitTime = DecodeWaitValue(decodeResult.Text);
                // Bit0 hasTarget Bit1 inCombat Bit3 NotUsed
                var conditions = DecodeConditionsBits(decodeResult.Text);
                result.HasTarget = (conditions & (1 << 0)) != 0;
                result.InCombat = (conditions & (1 << 1)) != 0;

            } else
            {
                result.DecodedTextValue = "";
                result.DetectedText = "brr";
                result.WaitTime = 0;
                result.BarcodeFound = false;
                result.HasTarget = false;
                result.InCombat = false;
            }

            return result;
        }

        public static BarcodeFindResult DecodeFind(ref Mat imageMat) 
        {
            var result = new BarcodeFindResult();

            // Convert the image to grayscale.
            Mat srcGray = imageMat.Clone();
            try
            {
                Cv2.CvtColor(srcGray, srcGray, ColorConversionCodes.BGR2GRAY);

                // Create a Mat to hold the binary (thresholded) image.

                // Set a fixed threshold value.
                // We invert the image here becase the barcode is blended with grey and we want the Blacks to pop out
                // So with iverting blacks become whites and its becomes easier to filter for white values.
                // But we cant detect a barcode that is inverted so we have to invert it back.  the result is pure black and white barcode
                // which is easier to detect and won't get messed up by ZXings binaryizer.
                double thresholdValue = 220;
                double maxValue = 255;
                Cv2.BitwiseNot(srcGray, srcGray);
                Cv2.Threshold(srcGray, srcGray, thresholdValue, maxValue, ThresholdTypes.Binary);
                Cv2.BitwiseNot(srcGray, srcGray);

                var luminanceSource = new Classes.OpenCV.OpenCvLuminanceSource(srcGray);
                var decodeResult = BarcodeReaderEngine.Decode(luminanceSource);

                if (decodeResult != null)
                {
                    float minX = float.MaxValue;
                    float minY = float.MaxValue;
                    float maxX = float.MinValue;
                    float maxY = float.MinValue;

                    foreach (var point in decodeResult.ResultPoints)
                    {
                        if (point.X < minX) minX = point.X;
                        if (point.Y < minY) minY = point.Y;
                        if (point.X > maxX) maxX = point.X;
                        if (point.Y > maxY) maxY = point.Y;
                    }

                    // Have to pad out the values as the region that is reported is not always exact but close enuf
                    int paddingW = 30;
                    int paddingH = 10;
                    var rac = new System.Drawing.Rectangle(
                        (int)(minX - paddingW),
                        (int)(minY - paddingH),
                        (int)((maxX - minX) + 2 * paddingW),
                        (int)((maxY - minY) + 2 * paddingH)
                    );

                    // the screenID should be the actual screenID the barcode is found on,  but that code is not implmeneted 
                    // yet so just report it as 1, the value is irealavent right now it just has to be above -1
                    result.screenID = 1;
                    result.X = rac.X;
                    result.Y = rac.Y;
                    result.Width = rac.Width;
                    result.Height = rac.Height;
                }
                else
                {
                    // this should be null to follow the pattern.  but don't feel like putting the check code.
                    result.screenID = -1;
                    result.X = 0;
                    result.Y = 0;
                    result.Width = 100;
                    result.Height = 100;
                }
            }
            finally 
            {
                srcGray.Dispose();
            }
            return result;
        }
    }
}


using OpenCvSharp;
using System;
using System.Collections.Generic;

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
            public byte mode { get; set; }
            public bool BarcodeFound { get; set; }
            public String DetectedText { get; set; }
            public String DecodedTextValue { get; set; }
            public int WaitTime { get; set; }
            public bool InCombat { get; set; }
            public bool HasTarget { get; set; }
            public int GCD { get; set; }
            public int Latency { get; set; }
            public int Delay { get; set; }
            public int SpellID { get; set; }
            public string KeyValue { get; set; }
            public byte BitValue { get; set; }

            public BarcodeResult()
            {
                mode = 0;
                DetectedText = String.Empty;
                DecodedTextValue = String.Empty;
                BarcodeFound = false;
                WaitTime = 0;
                KeyValue = "";
                BitValue = 0;
                SpellID = 0;
                Delay = 0;
                Latency = 0;


            }
        }

        public class BarcodeResultV2 : BarcodeResult
        {
            public int Mode { get; set; }
            public int CastTime { get; set; }
            public int CoolDown { get; set; }
            public int Targets { get; set; }

            public BarcodeResultV2()
            {
                Mode = 0;
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

        public static int CalculateCheckDigitASCII(string input)
        {
            int sum = 0;
            for (int i = 0; i < input.Length; i++)
            {
                int asciiValue = (int)input[i]; // Get byte value of character (0–255)

                int weight = (i % 2 == 0) ? 3 : 1;
                sum += asciiValue * weight;
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

        public static bool ValidateWithCheckDigitASCII(string input)
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

        private static byte DecodeMode (string input)
        {
            byte result = 0;
            string holder = string.Empty;
            if (input.Length >= 1)
            {
                holder = input.Substring(0, 1);

            }
            byte.TryParse(holder, out result);
            return result;
        }

    // Optimized DecodeTextValue using a Dictionary for fast lookup and reduced code size
    private static readonly Dictionary<int, string> TextValueMap = new()
    {
        // 1-9
        [1] = "1",
        [2] = "2",
        [3] = "3",
        [4] = "4",
        [5] = "5",
        [6] = "6",
        [7] = "7",
        [8] = "8",
        [9] = "9",
        // 10-12
        [10] = "0",
        [11] = "-",
        [12] = "=",
        // CF1-CF12 (21-32)
        [21] = "CF1",
        [22] = "CF2",
        [23] = "CF3",
        [24] = "CF4",
        [25] = "CF5",
        [26] = "CF6",
        [27] = "CF7",
        [28] = "CF8",
        [29] = "CF9",
        [30] = "CF10",
        [31] = "CF11",
        [32] = "CF12",
        // AF1-AF12 (41-52)
        [41] = "AF1",
        [42] = "AF2",
        [43] = "AF3",
        [44] = "AF4",
        [45] = "AF5",
        [46] = "AF6",
        [47] = "AF7",
        [48] = "AF8",
        [49] = "AF9",
        [50] = "AF10",
        [51] = "AF11",
        [52] = "AF12",
        // F1-F12 (61-72)
        [61] = "F1",
        [62] = "F2",
        [63] = "F3",
        [64] = "F4",
        [65] = "F5",
        [66] = "F6",
        [67] = "F7",
        [68] = "F8",
        [69] = "F9",
        [70] = "F10",
        [71] = "F11",
        [72] = "F12"
    };

        private static string DecodeTextValue(string s)
        {
            if (s.Length < 1) return string.Empty;
            if (int.TryParse(s, out int tInt) && TextValueMap.TryGetValue(tInt, out var result))
                return result;
            return string.Empty;
        }


        private static int DecodeWaitValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0; // Handle null or empty string scenario

            if (!int.TryParse(s, out var result))
                return 0; // Return 0 if parsing fails

            return result * 10;
        }

        //private static byte DecodeConditionsBits(string s)
        //{
        //    byte result = 0;
        //    string segment = string.Empty;

        //        if (byte.TryParse(s,  out result))
        //        {
               

        //            }
        //    ;

        //    return result;
        //}
        private static byte DecodeConditionsBits(ReadOnlySpan<char> s)
        {
            if (s.IsEmpty || !byte.TryParse(s, out var result)) return default; // Handle empty string or parsing failure scenarios

            return result;
        }
        // make this static,  no need to create the objects more than once.
        private static readonly DecodingOptions hints = new DecodingOptions
        {
            PureBarcode = false, // the capture should be just the barcode and no extras
            PossibleFormats = new List<BarcodeFormat> {  BarcodeFormat.QR_CODE, BarcodeFormat.CODE_39},
            
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

            if (decodeResult != null)//&& ValidateWithCheckDigit(decodeResult.Text))
            {
                if (decodeResult.Text.StartsWith('1'))  //QR Encoded format
                {
                    var items = decodeResult.Text.Split('/');
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (item.StartsWith('1') || item.StartsWith('0'))
                            {
                                if (byte.TryParse(item, out var ti))
                                {
                                    result.mode = ti;
                                }

                            }
                            if (item.StartsWith('B')) //Bit Encoded Values
                            {
                             //   var conditions = DecodeConditionsBits(item.Substring(1));
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.HasTarget = (ti & (1 << 0)) != 0;
                                    result.InCombat = (ti & (1 << 1)) != 0;
                                }
                            }
                            if (item.StartsWith('W')) //Bit Encoded Values
                            {
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.WaitTime = ti;
                                }
                            }
                            if (item.StartsWith('K')) //Bit Encoded Values
                            {
                                result.DecodedTextValue = DecodeTextValue(item.Substring(1));
                            }
                            if (item.StartsWith('D')) //Bit Encoded Values
                            {
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.Delay = ti;

                                }
                            }
                            if (item.StartsWith('G')) //Bit Encoded Values
                            {
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.GCD = ti;
                                }
                            }
                            if (item.StartsWith('A')) //Bit Encoded Values
                            {
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.SpellID = ti;
                                }
                            }
                            if (item.StartsWith('L')) //Bit Encoded Values
                            {
                                if (int.TryParse(item.Substring(1), out var ti))
                                {
                                    result.Latency = ti;
                                }
                            }





                        }
                        result.BarcodeFound = true;
                    }

                }
                else
                {
                    result.mode = DecodeMode(decodeResult.Text);
                    result.BarcodeFound = true;
                    result.DetectedText = decodeResult.Text;
                    result.DecodedTextValue = DecodeTextValue(decodeResult.Text);
                    result.WaitTime = DecodeWaitValue(decodeResult.Text);
                    // Bit0 hasTarget Bit1 inCombat Bit3 NotUsed
                    var conditions = DecodeConditionsBits(decodeResult.Text);
                    result.HasTarget = (conditions & (1 << 0)) != 0;
                    result.InCombat = (conditions & (1 << 1)) != 0;
                }
            }
            else
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


                //       Cv2.ImShow("Peek", srcGray);
                var luminanceSource = new Classes.OpenCV.OpenCvLuminanceSource(srcGray);
                var decodeResult = BarcodeReaderEngine.Decode(luminanceSource);

                if (decodeResult != null)
                {
                   
                    int minX = int.MaxValue;
                    int minY = int.MaxValue;
                    int maxX = int.MinValue;
                    int maxY = int.MinValue;

                    foreach (var point in decodeResult.ResultPoints)
                    {
                        if (point.X < minX) minX = (int)point.X;
                        if (point.Y < minY) minY = (int)point.Y;
                        if (point.X > maxX) maxX = (int)point.X;
                        if (point.Y > maxY) maxY = (int)point.Y;
                    }

                    // Have to pad out the values as the region that is reported is not always exact but close enuf
                    int paddingW = 0;
                    int paddingH = 0;

                    var rac = new OpenCvSharp.Rect(0, 0, 0, 0);

                    if (decodeResult.BarcodeFormat == BarcodeFormat.QR_CODE)
                    {
                            rac = new OpenCvSharp.Rect(
                            minX - (Math.Max(1, maxX - minX + 1) / 2),
                            minY - (Math.Max(1, maxY - minY + 1) / 2),
                             Math.Max(1, maxX - minX + 1) * 2,
                             Math.Max(1, maxY - minY + 1) * 2
                        );
                    }
                    if (decodeResult.BarcodeFormat == BarcodeFormat.CODE_39)
                    {
                        rac = new OpenCvSharp.Rect(
                        minX - (Math.Max(1, maxX - minX + 1) / 2),
                        minY - (Math.Max(1, maxY - minY + 1) / 2),
                         Math.Max(1, maxX - minX + 1) * 2,
                         Math.Max(1, maxY - minY + 1) * 2 );
                        // pad 40 pixels on each side too help the decoder find the start and to bars.
                        rac.Width = rac.Width - (rac.Width/ 2)+40;
                        rac.X = rac.X + (rac.Width / 2)-40;

                        rac.Height = rac.Height +  5;
                    }

                    // the screenID should be the actual screenID the barcode is found on,  but that code is not implmeneted 
                    // yet so just report it as 1, the value is irealavent right now it just has to be above -1
                    result.screenID = 1;
                    result.X = rac.X ;
                    result.Y = rac.Y ;
                    result.Width = rac.Width ;
                    result.Height = rac.Height ;
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

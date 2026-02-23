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
            public bool BarcodeFound { get; set; } = false;
            public String DetectedText { get; set; } = String.Empty;
            public String DecodedTextValue { get; set; } = String.Empty;
            public int WaitTime { get; set; } = 0;
            public bool InCombat { get; set; } = false;
            public bool HasTarget { get; set; } = false;
            public int GCD { get; set; } = 0;
            public bool GcdActive { get; set; } = false;
            public bool MultiTarget { get; set; } = false;
            public int Latency { get; set; } = 0;
            public int Delay { get; set; } = 0;
            public int SpellID { get; set; } = 0;
            public string KeyValue { get; set; } = "";
            public byte BitValue { get; set; } = 0;
            public bool holder1 { get; set; } = false;  // this will get renamed when I find a use for it.
            public bool holder2 { get; set; } = false; // this will get renamed when I find a use for it.
            public bool holder3 { get; set; } = false; // this will get renamed when I find a use for it.
            public int TStampAddon { get; set; } = 0;
            
            public int TStampApp  { get; set; } = 0;

            public int TDiff { get; set; } = 0;

        }

        public class BarcodeResultV2 : BarcodeResult
        {
            public int Mode { get; set; } = 0;
            public int CastTime { get; set; }
            public int CoolDown { get; set; }
            public int Targets { get; set; }
        }

        public static int FromBase36(string input)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int num = 0;
            foreach (char c in input)
            {
                int digit = chars.IndexOf(char.ToUpperInvariant(c));
                if (digit < 0)
                    throw new ArgumentException("Invalid Base36 character: " + c);
                num = num * 36 + digit;
            }

            return num;
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
                int weight = (i % 2 == 0) ? 3 : 1; // Match Lua's odd/even weighting
                sum += digit * weight;
            }

            return (10 - (sum % 10)) % 10;
        }

        public static int CalculateCheckDigitAscii(string input)
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

        public static bool ValidateWithCheckDigitAscii(string input)
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

        private static byte DecodeMode(string input)
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

        private static byte DecodeConditionsBits(ReadOnlySpan<char> s)
        {
            if (s.IsEmpty || !byte.TryParse(s, out var result))
                return 0; // Handle empty string or parsing failure scenarios

            return result;
        }

        // make this static,  no need to create the objects more than once.
        private static readonly DecodingOptions Hints = new DecodingOptions
        {
            PureBarcode = false, // the capture should be just the barcode and no extras
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE, BarcodeFormat.CODE_39 },

            TryHarder = true,
            TryInverted = false,
            AssumeCode39CheckDigit = false,
            UseCode39ExtendedMode = false
        };

        // make this static,  no need to create the objects more than once.
        private static readonly BarcodeReaderGeneric BarcodeReaderEngine = new BarcodeReaderGeneric()
        {
            AutoRotate = false,
            Options = Hints
        };
        static int NowMsXs() => (int)(Environment.TickCount64 % 500); // -0..1000
        
        static int DiffWrap(int sent, int recv, int wrap)
        {
            int d = recv - sent;
            int half = wrap / 2;
            if (d >  half) d -= wrap;
            if (d < -half) d += wrap;
            return d; // ms
        }
        
        public static BarcodeResult DecodeBarcode(Mat imageMat)
        {
            BarcodeResult result = new BarcodeResult();

            // Decode barcode using ZXing
            ZXing.Result decodeResult = null;

            var luminanceSource = new RuneReader.Classes.OpenCV.OpenCvLuminanceSource(imageMat);
            try
            {
                decodeResult = BarcodeReaderEngine.Decode(luminanceSource);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
          
            if (decodeResult != null)
            {
                var items = decodeResult.Text.Split('/');
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item.StartsWith('B')) //Bit Encoded Values
                        {
                           // var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.HasTarget = (ti & (1 << 0)) != 0;
                                result.InCombat = (ti & (1 << 1)) != 0;
                                result.MultiTarget = (ti & (1 << 2)) != 0;
                                result.GcdActive =  (ti & (1 << 3)) != 0; // not longer used.
                                result.holder1 =  (ti & (1 << 3)) != 0; // not  used.
                                result.holder2 =  (ti & (1 << 3)) != 0; // not  used.
                            }
                        }

                        if (item.StartsWith('W')) //Time to wait (includes GCD, and Delay (Charge time and Channeled)
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.WaitTime = ti;
                            }
                        }

                        if (item.StartsWith('K')) // This is our encoded key value
                        {
                            var backToBase10 = item.Substring(1);
                            result.DecodedTextValue = DecodeTextValue(backToBase10.ToString());
                        }

                        if (item.StartsWith('D')) // Delay Time (Charge time and Channeled) 
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.Delay = ti;
                            }
                        }

                        if (item.StartsWith('G')) // GCD time
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.GCD = ti;
                            }
                        }

                        if (item.StartsWith('A')) //Our SpellID
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.SpellID = ti;
                            }
                        }

                        if (item.StartsWith('L')) //Current Latency
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.Latency = ti;
                            }
                        }

                        if (item.StartsWith('T')) //Current Latency
                        {
                            //var backToBase10 = FromBase36(item.Substring(1));
                            if (int.TryParse(item.Substring(1), out var ti))
                            {
                                result.TStampAddon = ti;
                                result.TStampApp =  NowMsXs();
                                 // #if WINDOWS
                                 //result.TDiff =  result.TStampAddon - result.TStampApp ;
                                result.TDiff = DiffWrap(result.TStampApp, result.TStampAddon, 500);
                                //  #else
                                // // this is a hack to compensate for the resolution difference between linux time being filtered thru wine and native calls.
                                // result.TDiff = DiffWrap(result.TStampAddon, result.TStampApp, 5000) - 100; 
                                // if ( result.TDiff < 100)  result.TDiff = 100;
                                // if ( result.TDiff > 250)  result.TDiff = 250;
                                //
                                // #endif   
                            }
                        }

                    }

                    result.BarcodeFound = true;
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
                result.TStampAddon = 0;
                result.TStampApp =  0;
                result.TDiff = 0;

            }

            return result;
        }

        public static BarcodeFindResult DecodeFind(Mat imageMat)
        {
            var result = new BarcodeFindResult();

            // Convert the image to grayscale.
            Mat srcGray = new Mat();
            try
            {
                Cv2.CvtColor(imageMat, srcGray, ColorConversionCodes.BGR2GRAY);

                // Create a Mat to hold the binary (thresholded) image.

                // Set a fixed threshold value.
                // We invert the image here because the barcode is blended with grey and we want the Blacks to pop out
                // So with inverting blacks become whites and its becomes easier to filter for white values.
                // But we cant detect a barcode that is inverted so we have to invert it back.  the result is pure black and white barcode
                // which is easier to detect and won't get messed up by ZXing's binaryizer.
                double thresholdValue = 30;
                double maxValue = 255;
                // Cv2.BitwiseNot(srcGray, srcGray);
                Cv2.Threshold(srcGray, srcGray, thresholdValue, maxValue, ThresholdTypes.Binary);
                // Cv2.BitwiseNot(srcGray, srcGray);


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
                            Math.Max(1, maxY - minY + 1) * 2);
                        // pad 50 pixels on each side too help the decoder find the start and to bars.
                        rac.Width = rac.Width - (rac.Width / 2) + 50;
                        rac.X = rac.X + (rac.Width / 2) - 50;

                        rac.Height = rac.Height ;
                        
                        // ============================================================
                        // NEW: CODE_39 is often detected as a thin strip near the bottom
                        // of a horizontal barcode. We want to pad "upwards" by 10px,
                        // but KEEP the bottom edge where it was.
                        //
                        // Example:
                        //   oldY=200, oldH=2  => bottom=202
                        //   newY=max(0,190)=190
                        //   newH=bottom-newY=202-190=12
                        //
                        // If oldY < 10, newY becomes 0, and height expands accordingly.
                        // ============================================================
                        int bottom = rac.Y + rac.Height;      // preserve original bottom edge
                        int newY = Math.Max(0, rac.Y - 10);   // move top up (clamped at 0)
                        rac.Y = newY;
                        rac.Height = Math.Max(1, bottom - newY);

                        // Optional but recommended safety clamp:
                        // If your earlier math makes X negative, clamp it.
                        if (rac.X < 0) rac.X = 0;
                        if (rac.Y < 0) rac.Y = 0;
                        
                    }

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
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
        public class BarcodeResult
        {
            public bool BarcodeFound { get; set; }

            public String DetectedText { get; set; }
            public String DecodedTextValue { get; set; }
            public int WaitTime { get; set; }
            public BarcodeResult()
            {
                //Regions = new System.Windows.Rect[0];
                DetectedText = String.Empty;
                DecodedTextValue = String.Empty;
                BarcodeFound = false;
                WaitTime = 0;
            }
        }

        private static string DecodeTextValue (string s)
        {
            string result = string.Empty;
            string segment = string.Empty;
            if (s.Length == 5)
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
            if (s.Length == 5)
            {
                segment = s.Substring(2,3);
                if (int.TryParse(segment+"00", out result))
                    {
                    //     result = result;
                    //result = result ;
                    };
            }
            
            return result;
        }

        public static BarcodeResult DecodeBarcode(Mat imageMat)
        {
            BarcodeResult result = new BarcodeResult();

            // Preprocessing with OpenCvSharp (e.g., grayscale, thresholding, etc.)

      //      Mat grayMat = new Mat();
            //Cv2.CvtColor(imageMat, grayMat, ColorConversionCodes.BGR2GRAY);
      


            // Optional: Additional preprocessing steps like thresholding or blurring can be done here

            // Decode barcode using ZXing with OpenCV bindings

            // Convert Mat to Bitmap for ZXing
            // Bitmap bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayMat);
            // Decode barcode using ZXing

            ZXing.Result decodeResult = null;

            var hints = new DecodingOptions
            {
                PureBarcode = true,
                 
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.CODE_39},
                TryHarder = true
            };
            hints.Hints[DecodeHintType.ASSUME_CODE_39_CHECK_DIGIT] = false;
            hints.Hints[DecodeHintType.TRY_HARDER] = false;
            hints.Hints[DecodeHintType.USE_CODE_39_EXTENDED_MODE] = true;
            //hints.Hints[DecodeHintType.ALLOWED_LENGTHS] = 255;

            var BarcodeReaderEngine =  new BarcodeReaderGeneric()
            {
                AutoRotate = false,
                TryInverted = false,
                Options = hints
            };
            BarcodeReaderEngine.AutoRotate = true;
            //BarcodeReaderEngine.TryInverted = true;


                 //    Cv2.ImShow("WithDelays", imageMat);
            var luminanceSource = new OpenCvLuminanceSource(imageMat);
            // var binarizer = new ZXing.Common.HybridBinarizer(luminanceSource);
            //  var binaryBitmap = new BinaryBitmap(binarizer);


             decodeResult = BarcodeReaderEngine.Decode(luminanceSource);
            if (decodeResult != null)
            {
                result.BarcodeFound = true;
                result.DetectedText = decodeResult.Text;
                result.DecodedTextValue = DecodeTextValue(decodeResult.Text);
                result.WaitTime = DecodeWaitValue(decodeResult.Text);

            }
            return result;
        }

    }
}

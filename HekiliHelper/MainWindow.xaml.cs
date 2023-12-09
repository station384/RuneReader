using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Controls;
using System.Linq;
using System.Collections.ObjectModel;
using Vortice.Mathematics;
using Tesseract;

namespace HekiliHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public static class StringExtensions
    {
        public static string Extract(this string input, int len)
        {
            return input[0..Math.Min(input.Length, len)];
        }
    }


    public static class ActivationKeyCodeMapper
    {

        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
            {"1", (int)Key.D1 },
            {"2", (int)Key.D2 },
            {"3", (int)Key.D3 },
            {"'", (int)Key.Oem3 },
            {"W", (int)Key.D},
            {"Q", (int)Key.Q},
            {"E", (int)Key.E},
            
                // ... add additional key mappings as needed
        };

        public static int GetVirtualKeyCode(string key)
        {
            if (KeyMappings.TryGetValue(key, out int vkCode))
            {
                return vkCode;
            }
            throw new ArgumentException("Key not found.", nameof(key));
        }

        public static bool HasKey(string key)
        {
            return KeyMappings.ContainsKey(key);
        }
    }

    // This is the list of acceptable keys we can send to the game and the associated Windows virtual key to send.
    // We can use this for comparison or use it for looking up the matching key
    public static class VirtualKeyCodeMapper
    {


        private static readonly Dictionary<string, int> KeyMappingsExclude = new Dictionary<string, int>
        {
            {"1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
            {"2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
            {"3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
            {"4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
            {"5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
            {"6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
            {"7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
            {"8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
            {"9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
            {"0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
            // Had to remove these keys as that can't be detected using OCR very well.   only about a 30% accuarcy
            {"-", (int)VirtualKeyCodes.VirtualKeyStates.VK_OEM_MINUS},
            {"=", 187}, // This key can be different depending on country, i.e.  US its the = key,  Spanish is the ? (upside down)
        };



        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
        {
           // {"1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
           // {"2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
           // {"3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
          //  {"4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
          //  {"5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
          //  {"6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
          //  {"7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
          //  {"8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
          //  {"9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
          //  {"0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
            // Had to remove these keys as that can't be detected using OCR very well.   only about a 30% accuarcy
            // {"-", (int)VirtualKeyCodes.VirtualKeyStates.VK_OEM_MINUS},
          //    {"=", 187}, // This key can be different depending on country, i.e.  US its the = key,  Spanish is the ? (upside down)
            {"F1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"F2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"F3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
            {"F4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"F5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"F6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"F7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"F8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"F9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"F10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
            {"F11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
            {"F12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},
        
            // This is here just for future,  to accually use these key the value in the key value pair of the diction would need to be an object 
            // to store the CTRL, ALT, SHIFT states
            {"CF1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"CF2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"CF3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
            {"CF4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"CF5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"CF6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"CF7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"CF8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"CF9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"CF10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
            {"CF11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
                        {"CF12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},
            {"AF1", (int)VirtualKeyCodes.VirtualKeyStates.VK_F1},
            {"AF2", (int)VirtualKeyCodes.VirtualKeyStates.VK_F2},
            {"AF3", (int)VirtualKeyCodes.VirtualKeyStates.VK_F3},
            {"AF4", (int)VirtualKeyCodes.VirtualKeyStates.VK_F4},
            {"AF5", (int)VirtualKeyCodes.VirtualKeyStates.VK_F5},
            {"AF6", (int)VirtualKeyCodes.VirtualKeyStates.VK_F6},
            {"AF7", (int)VirtualKeyCodes.VirtualKeyStates.VK_F7},
            {"AF8", (int)VirtualKeyCodes.VirtualKeyStates.VK_F8},
            {"AF9", (int)VirtualKeyCodes.VirtualKeyStates.VK_F9},
            {"AF10", (int)VirtualKeyCodes.VirtualKeyStates.VK_F10},
                        {"AF11", (int)VirtualKeyCodes.VirtualKeyStates.VK_F11},
                                    {"AF12", (int)VirtualKeyCodes.VirtualKeyStates.VK_F12},
                // ... add additional key mappings as needed
    };

        public static int GetVirtualKeyCode(string key)
        {
            if (KeyMappings.TryGetValue(key, out int vkCode))
            {
                return vkCode;
            }
            throw new ArgumentException("Key not found.", nameof(key));
        }

        public static bool HasExcludeKey (string key)
        {
            return KeyMappingsExclude.ContainsKey(key);
        }

        public static bool HasKey(string key)
        {
            return KeyMappings.ContainsKey(key);
        }

    }

    public partial class MainWindow : System.Windows.Window
    {


        #region Win32 Calls
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("USER32.dll")]
        static extern short GetKeyState(VirtualKeyCodes.VirtualKeyStates nVirtKey);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        private const uint OCR_NORMAL = 32512;
        private const int IDC_HAND = 32649;

        // Windows message constants
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12; // Alt key

        // Virtual-Key codes for numeric keys "1" to "0"
        const int VK_1 = 0x31;
        const int VK_2 = 0x32;
        const int VK_3 = 0x33;
        const int VK_4 = 0x34;
        const int VK_5 = 0x35;
        const int VK_6 = 0x36;
        const int VK_7 = 0x37;
        const int VK_8 = 0x38;
        const int VK_9 = 0x39;
        const int VK_0 = 0x30; // Virtual-Key code for the "0" key
        private const int WH_KEYBOARD_LL = 13;



        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, WindowsMessageProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        private struct POINT
        {
            public int x;
            public int y;
        }

        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        #endregion

        private string CurrentKeyToPress { get; set; }
        private volatile string _currentKeyToSend = string.Empty; // Default key to send, can be changed dynamically
        private volatile string _lastKeyToSend = string.Empty; // Default key to send, can be changed dynamically
        private volatile string _DetectedValue = string.Empty;
        private volatile int _DetectedSameCount = 0;
        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _MouseHookID = IntPtr.Zero;
        private WindowsMessageProc _proc;
        private WindowsMessageProc _mouseProc ;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;
        private ImageHelpers ImageHelpers = new ImageHelpers();
        private delegate IntPtr WindowsMessageProc(int nCode, IntPtr wParam, IntPtr lParam);
        private OcrModule ocr = new OcrModule();
     
        private int CurrentR = 25;
        private int CurrentG = 255;
        private int CurrentB = 255;
        private int CurrentA = 255;
        private double CurrentThreshold = 0.3;
        private int CurrentCaptureRateMS = 100;
        private int CurrentKeyPressSpeedMS = 125;
        private int CurrentKeyDownDelayMS = 25;


        private volatile bool _keyPressMode = false;

        private  bool keyPressMode { 
            get { return _keyPressMode; }
            set { _keyPressMode = value; } 
        }


        public string GetActiveWindowTitle()
        {
            IntPtr hwnd = GetForegroundWindow();

            if (hwnd == null)  return null;

            int length = GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public  bool IsCurrentWindowWithTitle(string title)
        {
            var currentTitle = GetActiveWindowTitle();
            return currentTitle?.Equals(title, StringComparison.OrdinalIgnoreCase) ?? false;
        }




        private IntPtr SetHookActionKey(WindowsMessageProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }



        private IntPtr HookCallbackActionKey(int nCode, IntPtr wParam, IntPtr lParam)
        {
            bool handled = false;
      
            if (nCode >= 0 )
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                // We don't want to send key repeats if the app is not in focus
                if (!IsCurrentWindowWithTitle("World of Warcraft"))
                {
                    _timer.Stop();
                    _lastKeyToSend = string.Empty; 
                    // Let the key event go thru so the new focused app can handle it
                    handled = false;
                }
                else
                {
                    var item = ActivationKeyCodeMapper.GetVirtualKeyCode(Properties.Settings.Default.ActivationKey);
                    if (wParam == (IntPtr)WM_KEYDOWN && (int)key == item) // Replace SomeCapturedKey with the actual captured key
                    {
                        // Find the window with the title "wow" only if we haven't already found it
                        if (_wowWindowHandle == IntPtr.Zero)
                        {
                            _wowWindowHandle = FindWindow(null, "wow");
                        }
                        if (_wowWindowHandle != IntPtr.Zero && !_timer.IsEnabled)
                        {
                            _timer.Start();
                            // Don't let the message go thru.  this blocks the game from seeing the key press
                            handled = true;
                        }

                    }
                    else if (wParam == (IntPtr)WM_KEYUP && (int)key == item) // Replace SomeCapturedKey with the actual captured key
                    {
                        _timer.Stop();
                        handled = true;
                    }
                }
            }


            // If the keypress has been handled, return a non-zero value.
            // Otherwise, call the next hook in the chain.
            // return handled ? (IntPtr)0:CallNextHookEx(_hookID, nCode, wParam, lParam); // Locks explorer
             return CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer
          //   return handled ? (IntPtr)1:CallNextHookEx(_hookID, nCode, wParam, lParam); // Blocks input to game does not block windowss

        }




        /// <summary>
        /// Takes a bitmap and converts it to an image that can be handled by WPF ImageBrush
        /// </summary>
        /// <param name="src">A bitmap image</param>
        /// <returns>The image as a BitmapImage for WPF</returns>
        public BitmapImage Convert(Bitmap src)
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

        private string OCRProcess(Bitmap b)
        {
            string Result = "";
            string s = ocr.PerformOcr(b).Replace("\n", "");
            if (VirtualKeyCodeMapper.HasKey(s) && (!VirtualKeyCodeMapper.HasExcludeKey(s)) )
            {
                CurrentKeyToPress = StringExtensions.Extract(s, 4);
                if (!string.IsNullOrEmpty(CurrentKeyToPress.Trim()))
                {
                    _currentKeyToSend = CurrentKeyToPress;
                    Result = CurrentKeyToPress;
                }
            }
         return Result;

        }


        private void ProcessImageLocal(Bitmap image)
        {
            // This only works with non HDR,  for now.

            Bitmap b = image;


            var origWidth = b.Width;
            var origHeight = b.Height;

            //Remember this is running in the background and every CPU cycle counts!!
            //This has to be FAST it is executing every 250 miliseconds 4 times a second
            //The faster this is the more times per second we can evaluate and react faster




            // It is expected that in the game the font on the hotkey text will be set to R:25 B:255 G:255 The font set to mica, and the size set to 40.
            // We filter out everying that isn't close to the color we want.
            // Doing it this way because it wwwas FAST.  This could be doing by doing a find conture and area but that takes alot more caculation than just a simple color filter

            b = ImageHelpers.FilterByColor(b, System.Drawing.Color.FromArgb(CurrentA, CurrentR, CurrentG, CurrentB), CurrentThreshold);
            b = ImageHelpers.RescaleImageToDpi(b, 300);
            //UpdateImageControl(Convert(b));
            // Bring the levels to somthing predictable, to simplify we convert it to greyscale
            b = ImageHelpers.ConvertToGrayscaleFast(b);
            b = ImageHelpers.BumpToBlack(b, 160);

            if (ImageHelpers.FindColorInFirstQuarter(b, System.Drawing.Color.White, CurrentThreshold))
            {
                b = ImageHelpers.BumpToWhite(b, 180);

                // For tesseract it doesn't like HUGE text so we bring it back down to the original size
                b = ImageHelpers.ResizeImage(b, origWidth, origHeight);

                // Bitmap DisplayImage = b;


                // Work Contourse later to find the main text and crop it out
                // Just leaving the code here  just incase I can come up with a fast way of doing this
                //var points = ImageHelpers.FindContours(b,128);
                //foreach (var contour in points)
                //{
                //    System.Console.WriteLine("Contour found with points:");
                //    var area = ImageHelpers.CalculateContourArea(contour);
                //    var BoundingRect = ImageHelpers.GetBoundingRect(contour);
                //    var ar = BoundingRect.Width / (float)(BoundingRect.Height);
                //    if (area > 200 & ar > .25 & ar < 1.2)
                //    {
                //        DisplayImage = ImageHelpers.DrawRectangle(b, BoundingRect, System.Drawing.Color.Red);
                //    }
                //}


                UpdateImageControl(Convert(b));

                string s = OCRProcess(b);
                lDetectedValue.Content = s;
            }
            else
            {
                // nothing found
                UpdateImageControl(Convert(_holderBitmap));
                lDetectedValue.Content = "";

            }

        }

        private Scalar ConvertRgbToHsvRange(Scalar rgbColor, Scalar rgbColorTolerance, bool? isLowerBound)
        {
            Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
            Mat hsvMat = new Mat();
            Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2HSV_FULL);
            Vec3b hsvColor = hsvMat.Get<Vec3b>(0, 0);

            // Adjust the HSV range based on the tolerance
            int h = hsvColor[0];
            int s = hsvColor[1];
            int v = hsvColor[2];
            int hTol = (int)rgbColorTolerance[0];
            int sTol = (int)rgbColorTolerance[1];
            int vTol = (int)rgbColorTolerance[2];
            if (isLowerBound == null)
            {
                return new Scalar(
                    h ,
                    s ,
                    v );

            } else

            return new Scalar(
                isLowerBound.Value ? h : h + hTol,
                isLowerBound.Value ? s - sTol : s + sTol,
                isLowerBound.Value ? v - vTol : v + vTol);
        }


        private Scalar ConvertRgbToHsvRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, rgbColor);
            Mat hsvMat = new Mat();
            Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2HSV_FULL);
            Vec3b hsvColor = hsvMat.Get<Vec3b>(0, 0);

            // Adjust the HSV range based on the tolerance
            int h = hsvColor[0];
            int s = hsvColor[1];
            int v = hsvColor[2];
            int hTol = (int)(h * Threshold);
            int sTol = (int)(s * Threshold);
            int vTol = (int)(v * Threshold);
            if (isLowerBound == null)
            {
                return new Scalar(
                    h,
                    s,
                    v);

            }
            else

                return new Scalar(
                    isLowerBound.Value ? h - hTol : h + hTol,
                    isLowerBound.Value ? s - sTol : s + sTol,
                    isLowerBound.Value ? v - vTol : v + vTol);
        }

        public Mat IsolateColor(Mat src, Scalar rgbColor, Scalar rgbColorTolerance, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertRgbToHsvRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertRgbToHsvRange(rgbColor, Threshold, true);
            Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);
            //Scalar lowerBound = new Scalar(
            //    centerBound.Val0,
            //    centerBound.Val1 - 10,
            //   centerBound.Val2
            //    );


            // Convert the image to HSV color space
            Mat hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV_FULL);



            // Create a mask for the desired color range
            Mat mask = new Mat();
            Cv2.InRange(hsv, lowerBound, upperBound, mask);

            // Bitwise-AND mask and original image to isolate the color
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);

            return result;
        }

        public Mat RescaleImageToNewDpi(Mat src, double currentDpi, double newDpi)
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


        public bool IsThereAnImageInTopLeftQuarter(Mat src)
        {
            // Define the region of interest (ROI) as the first quarter of the image
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect(0, 0, (src.Width / 3), (src.Height / 3));
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect( (int)((src.Width / 2) / 2.5), 0, (int)((src.Width / 2) / 1.2), (src.Height / 3) );


            //       Cv2.Rectangle(resizedMat,
            //new OpenCvSharp.Point((resizedMat.Width / 4), 0),
            //new OpenCvSharp.Point((resizedMat.Width / 8) + (resizedMat.Width / 4), (resizedMat.Height / 3)),

            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 16);
            var width = (src.Width / 2) - (src.Width / 5);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(
                x, y, width, height 
    );

            Mat firstQuarter = src.Clone( roi);// new Mat(src, roi);

            // Convert to grayscale
            //Mat gray = new Mat();
            //Cv2.CvtColor(firstQuarter, gray, ColorConversionCodes.BGR2GRAY);

            // Apply edge detection (e.g., using Canny)
            Mat edges = new Mat();
            //        Cv2.BitwiseNot(firstQuarter, edges);
            var x1 = Cv2.Mean(firstQuarter);
            if (x1.Val0 <= 250)
                return true;
            else
                return false;

        }

        public bool IsThereAnImageInTopRightQuarter(Mat src)
        {
            // Define the region of interest (ROI) as the first quarter of the image
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect((src.Width / 3), (src.Height / 3), src.Width - (src.Width / 3), src.Height - (src.Height / 3));

            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect(

            //    (int)(src.Width / 2.3), 
            //    0,

            //    (int)(src.Width / 1.3), 
            //    src.Height / 3 

            //    );

            var x1 = (src.Width / 2) + (src.Width / 32);
            var y1 = (src.Height / 16);
            var width1 = (src.Width / 2) - (src.Width / 5);
            var height1 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(x1, y1, width1, height1);


            Mat firstQuarter = new Mat(src, roi1);

            // Convert to grayscale
            //Mat gray = new Mat();
            //Cv2.CvtColor(firstQuarter, gray, ColorConversionCodes.BGR2GRAY);

            // Apply edge detection (e.g., using Canny)
            Mat edges = new Mat();

            var x2 = Cv2.Mean(firstQuarter);
            if (x2.Val0 <= 250)
                return true;
            else
                return false;

        }

        private void DrawMarkers (ref Mat src)
        {
            Cv2.Line(src, (int)(src.Width / 2), 0, (int)(src.Width / 2), src.Height, Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8);
            Cv2.Line(src, 0, (int)(src.Height / 2), src.Width, (int)(src.Height / 2), Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8);


            //Draw top left sensor
            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 16);
            var width = (src.Width / 2) - (src.Width / 5);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);
            Cv2.Rectangle(src, roi, Scalar.Red, 1, LineTypes.Link8);

            //Draw top right sensor
            var x1 = (src.Width / 2) + (src.Width / 32);
            var y1 = (src.Height / 16);
            var width1 = (src.Width / 2) - (src.Width / 5);
            var height1 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(x1, y1, width1, height1);
            Cv2.Rectangle(src, roi1, Scalar.Red, 1, LineTypes.Link8);

        }
        private void ProcessImageOpenCV (Bitmap image)
        {
            var origWidth = image.Width;
            var origHeight = image.Height;
            double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
            int Rscale =  ((int)(CurrentR * ((CurrentR * trasThreshold) / CurrentR)));
            int Gscale =  ((int)(CurrentG * ((CurrentG * trasThreshold) / CurrentG)));
            int Bscale =  ((int)(CurrentB * ((CurrentB * trasThreshold) / CurrentB)));

      
            var CVMat = BitmapSourceConverter.ToMat(Convert(image));
            Mat resizedMat;
            resizedMat = RescaleImageToNewDpi(CVMat, image.HorizontalResolution, 300);



            var IsolatedColor = IsolateColor(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Scalar.FromRgb(Rscale, Gscale, Bscale), trasThreshold);

           
            Mat gray = new Mat();
            Cv2.CvtColor(IsolatedColor, gray, ColorConversionCodes.BGR2GRAY);
        
            // Apply Otsu's thresholding
            Cv2.Threshold(gray, gray, 1, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //

            //Mat invertedMask = new Mat();
            //Cv2.BitwiseNot(gray, invertedMask);
            if (IsThereAnImageInTopRightQuarter(gray) ) 
            if ( !IsThereAnImageInTopLeftQuarter(gray) && Properties.Settings.Default.QuickDecode == false )
            {
                Cv2.CvtColor(gray, gray, ColorConversionCodes.BayerBG2RGB);
                    DrawMarkers(ref  gray);

                      var OutImageSource = BitmapSourceConverter.ToBitmapSource(gray);

                    UpdateImageControl(OutImageSource);
                lDetectedValue.Content = "";
                return;
            }
             resizedMat = gray;
           // resizedMat = RescaleImageToNewDpi(gray, image.HorizontalResolution, 300);

         

            //This  currently not working and just taking up CPU cycles.  Not sure what is going on.
            //Will figure this out later.

            // Dilation
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Mat dilation = new Mat();
            Cv2.Dilate(resizedMat, dilation, kernel);
            //var OutImageSource = BitmapSourceConverter.ToBitmapSource(dilation);
      

            // Find contours
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(dilation, out  contours, out  hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxNone);

            foreach (var contour in contours)
            {
                OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);
               // Cv2.Rectangle(CVMat, rect, new Scalar(0, 255, 0), 2);

                // Crop and OCR
                Mat cropped = new Mat(resizedMat, rect);
                var OutImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(cropped);
    
   
                string s = OCRProcess(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(resizedMat));
                Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);

                DrawMarkers(ref resizedMat);

                //Cv2.Line(resizedMat, (int)(resizedMat.Width / 2), 0, (int)(resizedMat.Width / 2), resizedMat.Height, Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8);
                //Cv2.Line(resizedMat, 0, (int)(resizedMat.Height / 2), resizedMat.Width, (int)(resizedMat.Height / 2), Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8);

                ////Draw top left sensor
                //var x = (resizedMat.Width / 8) + (resizedMat.Width / 16);
                //var y = (resizedMat.Height / 16);
                //var width = (resizedMat.Width / 2) - (resizedMat.Width / 5);
                //var height = (resizedMat.Height / 2) / 2;
                //OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);
                //Cv2.Rectangle(resizedMat, roi, Scalar.Red, 1, LineTypes.Link8);

                ////Draw top right sensor
                //var x1 = (resizedMat.Width / 2) + (resizedMat.Width / 32);
                //var y1 = (resizedMat.Height / 16);
                //var width1 = (resizedMat.Width / 2) - (resizedMat.Width / 5);
                //var height1 = (resizedMat.Height / 2) / 2;
                //OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(x1, y1, width1, height1);
                //Cv2.Rectangle(resizedMat, roi1, Scalar.Red, 1, LineTypes.Link8);
                
                
                //Cv2.Rectangle(resizedMat,
                //    new OpenCvSharp.Point((resizedMat.Width / 4), 0),
                //    new OpenCvSharp.Point((resizedMat.Width / 8)+(resizedMat.Width / 4), (resizedMat.Height / 3)),

                //    Scalar.Red, 1, LineTypes.Link8);

                //Cv2.Rectangle(resizedMat,
                //         new OpenCvSharp.Point((resizedMat.Width / 2) + (resizedMat.Width / 8), 0),
                //         new OpenCvSharp.Point((resizedMat.Width / 2) + (resizedMat.Width / 4), (resizedMat.Height / 3)),
                //         Scalar.Red, 1, LineTypes.Link8);


                var OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);

                UpdateImageControl(OutImageSource);
                //if ( _DetectedSameCount >= (int)(Properties.Settings.Default.CaptureRateMS * 0.05))
                if ( _DetectedSameCount >= 1)
                {
                    lDetectedValue.Content = s;
                    _DetectedValue = s;
                    _DetectedSameCount = 0;
                }
                else
                {
                    if (lDetectedValue.Content.ToString() != s)
                    lDetectedValue.Content = "";
                    _DetectedSameCount++;
                }
                
            }



           // var OutImage = BitmapSourceConverter.ToBitmapSource(gray);

            
            //string s = OCRProcess(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(gray));
            //lDetectedValue.Content = s;
        }

        public void StartCaptureProcess()
        {
            // Define the area of the screen you want to capture
            int x = (int)magnifier.Left, 
                y = (int)magnifier.Top, 
                width = (int)magnifier.Width, 
                height = (int)magnifier.Height;

            // Initialize CaptureScreen with the dispatcher and the UI update action
            captureScreen = new CaptureScreen(x, y, width, height,0);
            //  image.Source = Convert(captureScreen.CapturedImage);

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                CurrentCaptureRateMS,
                Dispatcher,
                captureScreen
            );

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateUIImage += (Bitmap image) =>
            {
                //ProcessImageLocal(image);
                ProcessImageOpenCV(image);
            };
        }

        private System.Windows.Threading.DispatcherTimer _timer;

        private MagnifierWindow magnifier;
        // Method to open the MagnifierWindow
        private void OpenMagnifierWindow()
        {
            magnifier.Show();
        }





        // Method to retrieve properties from the MagnifierWindow
        private void RetrieveMagnifierProperties()
        {
            if (magnifier != null)
            {
                double x = magnifier.ScaledX;
                double y = magnifier.ScaledY;
                double width = magnifier.ScaledWidth;
                double height = magnifier.ScaledHeight;

                // Do something with the properties, e.g., display them
                MessageBox.Show($"Magnified Position: ({x}, {y})\n" +
                                $"Magnified Size: {width} x {height}");
            }
        }

        private void CloseMagnifierWindow()
        {
            if (magnifier != null)
            {
                magnifier.Close();
                // May want to destroy the window on close to free up the resources and everything tied to it
                // but have to update the code that reads the chords directly from the magnifier so use the last values stored local
            }
        }


        Bitmap _holderBitmap;
        public MainWindow()
        {
            InitializeComponent();

            magnifier = new MagnifierWindow();
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;


            magnifier.Left = Properties.Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Properties.Settings.Default.CapX;
            magnifier.Top = Properties.Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Properties.Settings.Default.CapY;
            magnifier.Width = Properties.Settings.Default.CapWidth;
            magnifier.Height = Properties.Settings.Default.CapHeight;

            //TargetColorPicker.ColorState =  new ColorPicker.Models.ColorState();
            TargetColorPicker.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Properties.Settings.Default.TargetA, (byte)Properties.Settings.Default.TargetR, (byte)Properties.Settings.Default.TargetG, (byte)Properties.Settings.Default.TargetB);
            CurrentR = Properties.Settings.Default.TargetR;
            CurrentG = Properties.Settings.Default.TargetG;
            CurrentB = Properties.Settings.Default.TargetB;
            CurrentA = Properties.Settings.Default.TargetA; 

            _holderBitmap = ImageHelpers.CreateBitmap(60, 60, System.Drawing.Color.Black);

            tbVariance.Text = Properties.Settings.Default.VariancePercent.ToString();
            sliderColorVariancePercent.Value = Properties.Settings.Default.VariancePercent;

            tbCaptureRateMS.Text = Properties.Settings.Default.CaptureRateMS.ToString();
            sliderCaptureRateMS.Value = Properties.Settings.Default.CaptureRateMS;

            tbKeyRateMS.Text = Properties.Settings.Default.KeyPressSpeedMS.ToString();
            sliderKeyRateMS.Value = Properties.Settings.Default.KeyPressSpeedMS;

            cbPushRelease.IsChecked = Properties.Settings.Default.PushAndRelease;
            cbQuickDecode.IsChecked = Properties.Settings.Default.QuickDecode;
            cbStayOnTop.IsChecked = Properties.Settings.Default.KeepOnTop;
            //Properties.Settings.Default.ActivationKey

                this.Topmost = Properties.Settings.Default.KeepOnTop;


            foreach (var x in cbActivationKey.Items)
            {

               if ( ((ComboBoxItem)x).Content.ToString() == Properties.Settings.Default.ActivationKey)
                    {
                    cbActivationKey.SelectedItem = x;
                }
            }

            OpenMagnifierWindow();

            this.Left = Properties.Settings.Default.AppStartX;
            this.Top = Properties.Settings.Default.AppStartY;

            CurrentKeyToPress = "";
            _proc = HookCallbackActionKey;

            _mouseProc = MouseHookCallback;


        _wowWindowHandle = FindWindow(null, "World of Warcraft");


            StartCaptureProcess();


            // This timer handles the key sending
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += async (sender, args) =>
            {
                // Check the key dictionary if the key is one we should handle
        
                if ((!VirtualKeyCodeMapper.HasKey(_currentKeyToSend)) || (VirtualKeyCodeMapper.HasExcludeKey(_currentKeyToSend) )) return;
               // _wowWindowHandle = FindWindow(null, "World of Warcraft");
                var l_currentKeyToSend = _currentKeyToSend;
                int vkCode = 0;
                // Tranlate the char to the virtual Key Code
                vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(l_currentKeyToSend);
               // int vkCode = _currentKeyToSend + 0x30; // 0x30 is the virtual-key code for "0"
                //KeyInterop.VirtualKeyFromKey(e.Key)
                if (_wowWindowHandle != IntPtr.Zero)
                {
                    // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                    // It fails if the next key to press is the same.
                    // There would have to some logic in the capture to say its a new detection
                    // if (_lastKeyToSend != _currentKeyToSend)
                    {
                        if (l_currentKeyToSend[0] == 'C')
                        {
                            PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_CONTROL, 0);
                        }
                        if (l_currentKeyToSend[0] == 'A')
                        {
                            PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_MENU, 0);
                        }

                        PostMessage(_wowWindowHandle, WM_KEYDOWN, vkCode, 0);


                        while (
                            (
                                (
                                 (!VirtualKeyCodeMapper.HasKey(_currentKeyToSend)) || VirtualKeyCodeMapper.HasExcludeKey(_currentKeyToSend)
                                )
                                || _currentKeyToSend == ""
                             )

                            && _keyPressMode
                        )
                        {

                        }

                        // It may not be necessary to send WM_KEYUP immediately after WM_KEYDOWN
                        // because it simulates a very quick key tap rather than a sustained key press.

                        if (!_keyPressMode)
                        {
                            await Task.Delay(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS);
                        }
                            PostMessage(_wowWindowHandle, WM_KEYUP, vkCode, 0);
                        if (l_currentKeyToSend[0] == 'C')
                        {
                            PostMessage(_wowWindowHandle, WM_KEYUP, VK_CONTROL, 0);
                        }
                        if (l_currentKeyToSend[0] == 'A')
                        {
                            PostMessage(_wowWindowHandle, WM_KEYUP, VK_MENU, 0);
                        }

                        if (_keyPressMode)
                        {
                            await Task.Delay(150);
                        }



                        _lastKeyToSend = l_currentKeyToSend;

                        // this stops the sending of the key till the timer is almost up.  
                        // it takes advantage of the cooldown visual cue in the game that darkens the font (changes the color)
                        // the OCR doesn't see a new char until it is almost times out, at that point it can be pressed and would be added to the action queue
                        _currentKeyToSend = "";
                        _DetectedValue = "";



                    }
                }
            };

            


        }

        #region UI Event handlers
        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (cbStayOnTop.IsChecked == true)
            {
                this.Topmost = true;
                Properties.Settings.Default.KeepOnTop = true;
            }
            else
            {
                this.Topmost = false;
                Properties.Settings.Default.KeepOnTop = false;

            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Start the continuous capturing
            _wowWindowHandle = FindWindow(null, "World of Warcraft");
            if (_wowWindowHandle != IntPtr.Zero)
            {
                if (!screenCapture.IsCapturing)
                {
                    Magnifier_LocationChanged(sender, e);
                    screenCapture.StartCapture();

                    _hookID = _hookID == 0 ? SetHookActionKey(_proc) : 0; 
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = true;
                }
            }
 
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // ... When you want to stop capturing:
            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
                if (_hookID == 0) { 
                    UnhookWindowsHookEx(_hookID);
                    _hookID = 0;
                }
                button_Start.IsEnabled = true;
                button_Stop.IsEnabled = false;
            }
        }

        private void UpdateImageControl(BitmapSource bitmapSource)
        {
   
            imageCap.Source = bitmapSource;
        }



        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            var filePath = ".\\captures\\Cap" + DateTime.Now.ToBinary().ToString() +".tif";


            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create( ((BitmapImage)imageCap.Source) ));
                encoder.Save(fileStream);
            }
        }
        private void OpenMagnifierButton_Click(object sender, RoutedEventArgs e)
        {
            OpenMagnifierWindow();
        }

        private void GetPropertiesButton_Click(object sender, RoutedEventArgs e)
        {
            RetrieveMagnifierProperties();
        }

        private void CloseMagnifierButton_Click(object sender, RoutedEventArgs e)
        {
            CloseMagnifierWindow();
        }

        private void bToggleMagBorder_Click(object sender, RoutedEventArgs e)
        {
            if (magnifier.Visibility == Visibility.Visible)
            {
                magnifier.Visibility = Visibility.Hidden;
            }
        else
            {
                magnifier.Visibility = Visibility.Visible;
            }
        }
        private void setMagnifierPosition (double x, double y, double width, double height)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var dpiX = source.CompositionTarget.TransformToDevice.M11;
                var dpiY = source.CompositionTarget.TransformToDevice.M22;

                // Get the window's current location
                var left = x;
                var top = y;
                var widthh = width;
                var heightt = height;

                // Adjust for DPI scaling
                var scaledLeft = left * dpiX;
                var scaledTop = top * dpiY;
                var scaledWidth = widthh * dpiX;
                var scaledHeight = heightt * dpiY;

                magnifier.Left = scaledLeft;
                magnifier.Top = scaledTop;
                magnifier.Width = scaledWidth;
                magnifier.Height = scaledHeight;


                screenCapture.CaptureRegion = new System.Windows.Rect(scaledLeft, scaledTop, scaledWidth, scaledHeight);
                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }

        }

        private void Magnifier_LocationChanged(object? sender, EventArgs e)
        {
            //            if (screenCapture == null) return;
            //            screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;
            if (screenCapture == null) return;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var dpiX = source.CompositionTarget.TransformToDevice.M11;
                var dpiY = source.CompositionTarget.TransformToDevice.M22;

                // Get the window's current location
                var left = magnifier.CurrrentLocationValue.X;
                var top = magnifier.CurrrentLocationValue.Y;
                var width = magnifier.CurrrentLocationValue.Width;
                var height = magnifier.CurrrentLocationValue.Height;

                // Adjust for DPI scaling
                var scaledLeft = left * dpiX;
                var scaledTop = top * dpiY;
                var scaledWidth = width * dpiX;
                var scaledHeight = height * dpiY;

                screenCapture.CaptureRegion = new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1);
                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }

        }

        private void Magnifier_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (screenCapture == null) return;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var dpiX = source.CompositionTarget.TransformToDevice.M11;
                var dpiY = source.CompositionTarget.TransformToDevice.M22;

                // Get the window's current location
                var left = magnifier.CurrrentLocationValue.X;
                var top = magnifier.CurrrentLocationValue.Y;
                var width = magnifier.CurrrentLocationValue.Width;
                var height = magnifier.CurrrentLocationValue.Height;

                // Adjust for DPI scaling
                var scaledLeft = (left * dpiX) + 1;
                var scaledTop = (top * dpiY) + 1;
                var scaledWidth = (width * dpiX) - 1;
                var scaledHeight = (height * dpiY) - 15;

                scaledWidth = scaledWidth < 0 ? 1 : scaledWidth;
                scaledHeight = scaledHeight < 0 ? 1 : scaledHeight;
                
                

                screenCapture.CaptureRegion = new System.Windows.Rect(scaledLeft , scaledTop, scaledWidth , scaledHeight);
                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {



            Properties.Settings.Default.CapX = magnifier.Left;
            Properties.Settings.Default.CapY = magnifier.Top;
            Properties.Settings.Default.CapWidth = magnifier.Width;
            Properties.Settings.Default.CapHeight = magnifier.Height;
            Properties.Settings.Default.AppStartX = this.Left;
            Properties.Settings.Default.AppStartY = this.Top;
            Properties.Settings.Default.TargetR = CurrentR;
            Properties.Settings.Default.TargetG = CurrentG;
            Properties.Settings.Default.TargetB = CurrentB;
            Properties.Settings.Default.TargetA = 255;

            Properties.Settings.Default.Save();

            magnifier.Close();  
            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
            }
            if (_hookID != 0) { 
            UnhookWindowsHookEx(_hookID);
            _hookID = 0;
            }
            
  

            if (_MouseHookID != IntPtr.Zero)
            {

                UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;
            }
            // Make sure we stop trapping the keyboard
            // UnhookWindowsHookEx(_hookID);
        }
        #endregion

        private void sliderTargetR_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void sliderTargetG_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void sliderTargetB_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
 
        private void buPicker_Click(object sender, RoutedEventArgs e)
        {
            _MouseHookID = MouseSetHook(_mouseProc);
            ChangeCursor();
            // Other application logic
        }



        public static void ChangeCursor()
        {
            // Load the custom cursor
            IntPtr customCursor = LoadCursor(IntPtr.Zero, IDC_HAND);

            // Set the system cursor to the custom cursor
           // SetSystemCursor(customCursor, OCR_NORMAL);
        }

        public static void RestoreCursor()
        {
            // Load the default arrow cursor
            IntPtr defaultCursor = LoadCursor(IntPtr.Zero, 32512); // 32512 is the ID for the standard arrow

            // Restore the system cursor to the default
            SetSystemCursor(defaultCursor, OCR_NORMAL);
        }

        private static IntPtr MouseSetHook(WindowsMessageProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private  IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                Console.WriteLine($"{hookStruct.pt.x}, {hookStruct.pt.y}");
                int x = hookStruct.pt.x;
                int y = hookStruct.pt.y;

                using (Bitmap bmp = new Bitmap(1, 1))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        // Copy the pixel's color into the bitmap
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(1, 1));
                    }

                    // Get the color of the pixel
                    System.Drawing.Color pixelColor = bmp.GetPixel(0, 0);

                    // Convert System.Drawing.Color to System.Windows.Media.Color
                    this.TargetColorPicker.SelectedColor = System.Windows.Media.Color.FromArgb(pixelColor.A, pixelColor.R, pixelColor.G, pixelColor.B);
                    Properties.Settings.Default.TargetR = pixelColor.R;
                    Properties.Settings.Default.TargetG = pixelColor.G;
                    Properties.Settings.Default.TargetB = pixelColor.B;
                    Properties.Settings.Default.TargetA = pixelColor.A;
                    CurrentR = pixelColor.R;
                    CurrentG = pixelColor.G;
                    CurrentB = pixelColor.B;


                }

        

      

                UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;

            }
            return CallNextHookEx(_MouseHookID, nCode, wParam, lParam);
        }

        private void sliderColorVariance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.VariancePercent = (int)sliderColorVariancePercent.Value;
            CurrentThreshold = (int)sliderColorVariancePercent.Value;
            if (tbVariance != null)
                tbVariance.Text = ((int)sliderColorVariancePercent.Value).ToString();

            
        }

        private void sliderCaptureRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.CaptureRateMS = (int)sliderCaptureRateMS.Value;
            CurrentCaptureRateMS = (int)sliderCaptureRateMS.Value;
            if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
            if (screenCapture != null)
            screenCapture.CaptureInterval = (int)sliderCaptureRateMS.Value;
       
        }

        private void sliderKeyRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.KeyPressSpeedMS = (int)sliderKeyRateMS.Value;
            CurrentKeyDownDelayMS = (int)sliderKeyRateMS.Value;
            if (tbKeyRateMS != null)
            tbKeyRateMS.Text = ((int)sliderKeyRateMS.Value).ToString();

        }

        private static readonly Regex _regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
        private static bool IsTextAllowed(string text)
        {
            return !_regex.IsMatch(text);
        }

        private void PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _regex.IsMatch(e.Text);
        }
        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void tbKeyRateMS_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            sliderKeyRateMS.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void tbCaptureRateMS_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            sliderCaptureRateMS.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void tbVariance_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            sliderColorVariancePercent.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void cbActivationKey_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.ActivationKey = ((ComboBoxItem)cbActivationKey.SelectedItem).Content.ToString();
        }

        private void bResetMagPosition_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.CapX = 50;
            Properties.Settings.Default.CapY = 50;
            Properties.Settings.Default.CapWidth = 100;
            Properties.Settings.Default.CapHeight = 100 ;

            magnifier.Left = Properties.Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Properties.Settings.Default.CapX;
            magnifier.Top = Properties.Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Properties.Settings.Default.CapY;
            magnifier.Width = Properties.Settings.Default.CapWidth;
            magnifier.Height = Properties.Settings.Default.CapHeight;

        }

        private void cbPushRelease_Checked(object sender, RoutedEventArgs e)
        {

            _keyPressMode = true;
            Properties.Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbPushRelease_Unchecked(object sender, RoutedEventArgs e)
        {
            _keyPressMode = false;
            Properties.Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbQuickDecode_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.QuickDecode = true;
        }

        private void cbQuickDecode_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.QuickDecode = false;
        }
    }
}

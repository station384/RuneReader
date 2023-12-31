using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Tesseract;
using HekiliHelper.Properties;
using System.Windows.Threading;
using HekiliHelper;
using System.Reflection.Emit;

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
    public struct DetectionRegions
    {
        public bool TopLeft = false;
        public bool TopRight = false;
        public bool BottomLeft = false;
        public bool BottomCenter = false;

        public DetectionRegions()
        {
            TopLeft = false;
            TopRight = false;
            BottomLeft = false;
            BottomCenter = false;
        }
    }
    public class ImageRegions
    {
        public DetectionRegions FirstImageRegions;
        public DetectionRegions SecondImageRegions;

        public ImageRegions() 
        {
            FirstImageRegions = new DetectionRegions();
            SecondImageRegions = new DetectionRegions();
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

        private volatile string[] _currentKeyToSend = new string[] { string.Empty, string.Empty }; // Default key to send, can be changed dynamically

        private volatile string _lastKeyToSend = string.Empty; // Default key to send, can be changed dynamically

        private volatile string _DetectedValueFirst = string.Empty;
        private volatile string _DetectedValueSecond = string.Empty;

        private volatile bool keyProcessing = false;
        private volatile bool keyProcessing2 = false;
        private volatile bool key1Pressed = false;
        private volatile bool key2Pressed = false;


        private volatile int[] _DetectedSameCount = new int[2] { 0, 0 };

        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _MouseHookID = IntPtr.Zero;
        private WindowsMessageProc _proc;
        private WindowsMessageProc _mouseProc;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;
        private ImageHelpers ImageHelpers = new ImageHelpers();
        private delegate IntPtr WindowsMessageProc(int nCode, IntPtr wParam, IntPtr lParam);
        private OcrModule ocr = new OcrModule();
        private MagnifierWindow magnifier;
        private MagnifierWindow magnifier2;
        private  ImageRegions CurrentImageRegions = new ImageRegions();
        private System.Windows.Threading.DispatcherTimer _timer;



        private int CurrentR = 25;
        private int CurrentG = 255;
        private int CurrentB = 255;
        private int CurrentA = 255;
        private int CurrentH = 255;
        private int CurrentS = 255;
        private int CurrentV = 255;
        private double CurrentThreshold = 0.3;
        private int CurrentCaptureRateMS = 100;
        private int CurrentKeyPressSpeedMS = 125;
        private int CurrentKeyDownDelayMS = 25;
        private Dispatcher mainWindowDispatcher;


        private volatile bool _keyPressMode = false;

        private bool keyPressMode {
            get { return _keyPressMode; }
            set { _keyPressMode = value; }
        }


        public string GetActiveWindowTitle()
        {
            IntPtr hwnd = GetForegroundWindow();

            if (hwnd == null) return null;

            int length = GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public bool IsCurrentWindowWithTitle(string title)
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

        private bool AltPressed = false;
        private bool CtrlPressed = false;


        private IntPtr HookCallbackActionKey(int nCode, IntPtr wParam, IntPtr lParam)
        {
            bool handled = false;

            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                // We don't want to send key repeats if the app is not in focus
                if (!IsCurrentWindowWithTitle("World of Warcraft"))
                {
                    _timer.Stop();
      


                    // Let the key event go thru so the new focused app can handle it
                    keyProcessing = false;
                    keyProcessing2 = false;
                    handled = false;
                }
                else
                {
                    var item = ActivationKeyCodeMapper.GetVirtualKeyCode(Properties.Settings.Default.ActivationKey);
                    if (keyProcessing == false || keyProcessing2 == false)
                        if (wParam == (IntPtr)WM_KEYDOWN && (int)key == item)
                        {
                            // Find the window with the title "wow" only if we haven't already found it
                            if (_wowWindowHandle == IntPtr.Zero)
                            {
                                _wowWindowHandle = FindWindow(null, "wow");
                            }
                            if (_wowWindowHandle != IntPtr.Zero && !_timer.IsEnabled && keyProcessing == false)  // Assume timer2 is tied to timer 1
                            {
                                _timer.Start();
                                mainTimerTick(this, new EventArgs());

      

                                // Don't let the message go thru.  this blocks the game from seeing the key press
                                handled = true;
                            }


                        }
                    if (wParam == (IntPtr)WM_KEYUP && (int)key == item)
                    {
                        _timer.Stop();
  
                        keyProcessing = false;
                        keyProcessing2 = false;
                        handled = true;
                    }
                    if (wParam == (IntPtr)WM_KEYDOWN && key == System.Windows.Input.Key.LeftCtrl)
                    {
                        CtrlPressed = true;
                    }
                    if (wParam == (IntPtr)WM_KEYDOWN && key == System.Windows.Input.Key.LeftAlt)
                    {
                        AltPressed = true;
                    }

                    if (wParam == (IntPtr)WM_KEYUP && key == System.Windows.Input.Key.LeftCtrl)
                    {
                        CtrlPressed = false;
                    }
                    if (wParam == (IntPtr)WM_KEYUP && key == System.Windows.Input.Key.LeftAlt)
                    {
                        AltPressed = false;
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
            if (VirtualKeyCodeMapper.HasKey(s) && (!VirtualKeyCodeMapper.HasExcludeKey(s)))
            {
                CurrentKeyToPress = StringExtensions.Extract(s, 4);
                if (!string.IsNullOrEmpty(CurrentKeyToPress.Trim()))
                {
                    // _currentKeyToSend[0] = CurrentKeyToPress;
                    Result = CurrentKeyToPress;
                }

            }
            return Result;

        }


        //private void ProcessImageLocal(Bitmap image)
        //{
        //    // This only works with non HDR,  for now.

        //    Bitmap b = image;


        //    var origWidth = b.Width;
        //    var origHeight = b.Height;

        //    //Remember this is running in the background and every CPU cycle counts!!
        //    //This has to be FAST it is executing every 250 miliseconds 4 times a second
        //    //The faster this is the more times per second we can evaluate and react faster




        //    // It is expected that in the game the font on the hotkey text will be set to R:25 B:255 G:255 The font set to mica, and the size set to 40.
        //    // We filter out everying that isn't close to the color we want.
        //    // Doing it this way because it wwwas FAST.  This could be doing by doing a find conture and area but that takes alot more caculation than just a simple color filter

        //    b = ImageHelpers.FilterByColor(b, System.Drawing.Color.FromArgb(CurrentA, CurrentR, CurrentG, CurrentB), CurrentThreshold);
        //    b = ImageHelpers.RescaleImageToDpi(b, 300);
        //    //UpdateImageControl(Convert(b));
        //    // Bring the levels to somthing predictable, to simplify we convert it to greyscale
        //    b = ImageHelpers.ConvertToGrayscaleFast(b);
        //    b = ImageHelpers.BumpToBlack(b, 160);

        //    if (ImageHelpers.FindColorInFirstQuarter(b, System.Drawing.Color.White, CurrentThreshold))
        //    {
        //        b = ImageHelpers.BumpToWhite(b, 180);

        //        // For tesseract it doesn't like HUGE text so we bring it back down to the original size
        //        b = ImageHelpers.ResizeImage(b, origWidth, origHeight);

        //        // Bitmap DisplayImage = b;


        //        // Work Contourse later to find the main text and crop it out
        //        // Just leaving the code here  just incase I can come up with a fast way of doing this
        //        //var points = ImageHelpers.FindContours(b,128);
        //        //foreach (var contour in points)
        //        //{
        //        //    System.Console.WriteLine("Contour found with points:");
        //        //    var area = ImageHelpers.CalculateContourArea(contour);
        //        //    var BoundingRect = ImageHelpers.GetBoundingRect(contour);
        //        //    var ar = BoundingRect.Width / (float)(BoundingRect.Height);
        //        //    if (area > 200 & ar > .25 & ar < 1.2)
        //        //    {
        //        //        DisplayImage = ImageHelpers.DrawRectangle(b, BoundingRect, System.Drawing.Color.Red);
        //        //    }
        //        //}


        //        UpdateImageControl(Convert(b));

        //        string s = OCRProcess(b);
        //        lDetectedValue.Content = s;
        //    }
        //    else
        //    {
        //        // nothing found
        //        UpdateImageControl(Convert(_holderBitmap));
        //        lDetectedValue.Content = "";

        //    }

        //}

        private Scalar ConvertRgbToHsvRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
        {
            Mat rgbMat = new Mat(1, 1, MatType.CV_8UC4, rgbColor);
            Mat hsvMat = new Mat();
            Cv2.CvtColor(rgbMat, hsvMat, ColorConversionCodes.BGR2HSV_FULL);


            Vec4b hsvColor = hsvMat.Get<Vec4b>(0, 0);

            // Adjust the HSV range based on the tolerance
            int h = hsvColor[0];
            int s = hsvColor[1];
            int v = hsvColor[2];
            int hTol = (int)(h * Threshold);
            int sTol = (int)(s * Threshold);
            int vTol = (int)(v * Threshold);

            //if (h + hTol > 255) { hTol = 0; }
            //if (s + sTol > 255) { sTol = 0; }
            //if (v + vTol > 255) { vTol = 0; }
            //if (h - hTol < 0) { hTol = 0; }
            //if (s - sTol < 0) { sTol = 0; }
            //if (v - vTol < 0) { vTol = 0; }

            if (isLowerBound == null)
            {
                return new Scalar(
                    h,
                    s,
                    v);

            }
            else
                return new Scalar(
                    isLowerBound.Value ? h - 10 : h + 10,
                    isLowerBound.Value ? s - 20 : s + 20,
                    isLowerBound.Value ? v - vTol : v + vTol);
        }


        private Scalar ConvertRgbToHlsRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
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



        private Scalar ConvertBGRToBGRRange(Scalar rgbColor, double Threshold, bool? isLowerBound)
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


        public Mat IsolateColorHSV(Mat src, Scalar rgbColor, double Threshold)
        {
            // Convert the RGB color and tolerance to HSV
            Scalar upperBound = ConvertRgbToHsvRange(rgbColor, Threshold, false);
            Scalar lowerBound = ConvertRgbToHsvRange(rgbColor, Threshold, true);
            //      Scalar centerBound = ConvertRgbToHsvRange(rgbColor, Threshold, null);

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

        public Mat IsolateColorHLS(Mat src, Scalar rgbColor, double Threshold)
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

        public Mat IsolateColorRGB(Mat src, Scalar rgbColor, double Threshold)
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

            Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);

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

        public bool IsThereAnImageInBottomLeftQuarter(Mat src)
        {
            // Define the region of interest (ROI) as the first quarter of the image
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect(0, 0, (src.Width / 3), (src.Height / 3));
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect( (int)((src.Width / 2) / 2.5), 0, (int)((src.Width / 2) / 1.2), (src.Height / 3) );


            //       Cv2.Rectangle(resizedMat,
            //new OpenCvSharp.Point((resizedMat.Width / 4), 0),
            //new OpenCvSharp.Point((resizedMat.Width / 8) + (resizedMat.Width / 4), (resizedMat.Height / 3)),

            var x = (src.Width / 8) + (src.Width / 16);
            var y = (src.Height / 2);
            var width = (src.Width / 2) - (src.Width / 5);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);

            Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);

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

        public bool IsThereAnImageInBottomCenter(Mat src)
        {
            // Define the region of interest (ROI) as the first quarter of the image
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect(0, 0, (src.Width / 3), (src.Height / 3));
            //OpenCvSharp.Rect roi = new OpenCvSharp.Rect( (int)((src.Width / 2) / 2.5), 0, (int)((src.Width / 2) / 1.2), (src.Height / 3) );


            //       Cv2.Rectangle(resizedMat,
            //new OpenCvSharp.Point((resizedMat.Width / 4), 0),
            //new OpenCvSharp.Point((resizedMat.Width / 8) + (resizedMat.Width / 4), (resizedMat.Height / 3)),

            var x = (src.Width / 4) + (src.Width / 16);
            var y = (src.Height / 2);
            var width = (src.Width / 2) - (src.Width / 5);
            var height = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, width, height);

            Mat firstQuarter = src.Clone(roi);// new Mat(src, roi);

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


        private void DrawMarkers(ref Mat src)
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


            //Draw Left Lower Sensor
            var x2 = (src.Width / 8) + (src.Width / 16);
            var y2 = ((src.Height / 2));
            var width2 = (src.Width / 2) - (src.Width / 5);
            var height2 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi2 = new OpenCvSharp.Rect(x2, y2, width2, height2);
            Cv2.Rectangle(src, roi2, Scalar.Red, 1, LineTypes.Link8);

            //Draw Bottom Center Sensor
            var x3 = ((src.Width / 4) + (src.Width / 16)) ;
            var y3 = ((src.Height / 2));
            var width3 = (src.Width / 2) - (src.Width / 5);
            var height3 = (src.Height / 2) / 2;
            OpenCvSharp.Rect roi3 = new OpenCvSharp.Rect(x3, y3, width3, height3);
            Cv2.Rectangle(src, roi3, Scalar.Blue, 1, LineTypes.Link8);

        }



        private string ProcessImageOpenCV(Bitmap image, ref System.Windows.Controls.Label label, ref string _DetectedValue, ref int _DetectedSameCount, ref string CurrentKeyToSend, ref System.Windows.Controls.Image DisplayControl, double Threshold, ref DetectionRegions regions)
        {
            var origWidth = image.Width;
            var origHeight = image.Height;

            int Rscale = ((int)(CurrentR * ((CurrentR * Threshold) / CurrentR)));
            int Gscale = ((int)(CurrentG * ((CurrentG * Threshold) / CurrentG)));
            int Bscale = ((int)(CurrentB * ((CurrentB * Threshold) / CurrentB)));

            string result = "";
            BitmapSource? OutImageSource;
            var CVMat = BitmapSourceConverter.ToMat(Convert(image));
            Mat resizedMat;


            resizedMat = RescaleImageToNewDpi(CVMat, image.HorizontalResolution, 300);

            var IsolatedColor = IsolateColorHSV(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Threshold);


            Mat gray = new Mat();
            Cv2.CvtColor(IsolatedColor, gray, ColorConversionCodes.BGR2GRAY);

            // Apply Otsu's thresholding
            Cv2.Threshold(gray, gray, 1, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //
            resizedMat = gray.Clone();
            regions.TopLeft = IsThereAnImageInTopLeftQuarter(gray);
            regions.TopRight = IsThereAnImageInTopRightQuarter(gray);
            regions.BottomLeft = IsThereAnImageInBottomLeftQuarter(gray);
            regions.BottomCenter = IsThereAnImageInBottomCenter(gray);


            if (regions.TopRight)
            {
                if (!regions.TopLeft && Properties.Settings.Default.QuickDecode == false)
                {
                    Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);
                    DrawMarkers(ref resizedMat);

                    OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);
                    DisplayControl.Source = OutImageSource;
                    label.Content = "";
                    result = "";
                    return result;
                }
                if (!regions.BottomLeft && Properties.Settings.Default.QuickDecode == true)
                {
                    Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);
                    DrawMarkers(ref resizedMat);

                    OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);
                    DisplayControl.Source = OutImageSource;
                    label.Content = "";
                    result = "";
                    return result;
                }


            }
  


            string s = OCRProcess(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(gray));
            CurrentKeyToSend = s;
            Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);

            DrawMarkers(ref resizedMat);

            OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);
            DisplayControl.Source = OutImageSource;

            if (_DetectedSameCount >= 2)
            {
                label.Content = s;
                _DetectedValue = s;
                _DetectedSameCount = 0;
            }
            else
            {
                if (label.Content.ToString() != s)
                    lDetectedValue.Content = "";
                _DetectedSameCount++;
            }

            result = _DetectedValue;
            return result;
        }


        public void StartCaptureProcess()
        {
            // Define the area of the screen you want to capture
            int x = (int)magnifier.Left,
                y = (int)magnifier.Top,
                width = (int)magnifier.Width,
                height = (int)magnifier.Height;

            int x2 = (int)magnifier2.Left,
                y2 = (int)magnifier2.Top,
                width2 = (int)magnifier2.Width,
                height2 = (int)magnifier2.Height;


            // Initialize CaptureScreen with the dispatcher and the UI update action
            System.Windows.Rect[] regions = new System.Windows.Rect[2];
            regions[0] = new System.Windows.Rect { X = (double)x, Y = (double)y, Width = width, Height = height };
            regions[1] = new System.Windows.Rect { X = (double)x2, Y = (double)y2, Width = width2, Height = height2 };
            captureScreen = new CaptureScreen(regions, 0);
            //  image.Source = Convert(captureScreen.CapturedImage);

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                CurrentCaptureRateMS,
                Dispatcher,
                captureScreen
            );


            // Only process the 2nd image if it is active.  The image will still be captured behind the scenes,  but no OCR will be done on it.

            screenCapture.UpdateSecondImage += (Bitmap image) =>
            {
                if (Properties.Settings.Default.Use2ndImageDetection)
                {
                    //ProcessImageLocal(image);
                    double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
                    ProcessImageOpenCV(image, ref lDetectedValue2, ref _DetectedValueSecond, ref _DetectedSameCount[1], ref _currentKeyToSend[1], ref imageCap2, trasThreshold, ref CurrentImageRegions.SecondImageRegions);
                }
                else
                {
                    // Not capturing so set values back to 0-state
                    lDetectedValue2.Content = "";
                    _DetectedValueSecond = "";
                    _DetectedSameCount[1] = 0;
                    _currentKeyToSend[1] = "";
                }
            };

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateFirstImage += (Bitmap image) =>
            {
                //ProcessImageLocal(image);
                double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
                ProcessImageOpenCV(image, ref lDetectedValue, ref _DetectedValueFirst, ref _DetectedSameCount[0], ref _currentKeyToSend[0], ref imageCap, trasThreshold, ref CurrentImageRegions.FirstImageRegions);
            };


            


        }




        // Method to open the MagnifierWindow
        private void OpenMagnifierWindow()
        {
            magnifier.Show();
            magnifier2.Show();
        }

        string lastKey = "";
        private async void mainTimerTick(object? sender, EventArgs args)
        {
            // If key is already processing skip this tick
            if (keyProcessing || keyProcessing2 || key1Pressed)
                return;

            if (_currentKeyToSend[0] == "")
                return;

            if (lastKey == _currentKeyToSend[0])
            {
                await Task.Delay(200);
                lastKey = "";
            }


            if (CurrentImageRegions.FirstImageRegions.TopLeft == false)
            {
                _currentKeyToSend[0] = "";
                return;
            }
            

            var currentKeyToSend = _currentKeyToSend[0];
            var currentKeyToSend1 = _currentKeyToSend[1];


            // THis is a brute force way of trying to keep a key from being rapidly pressed

            // Check the key dictionary if the key is one we should handle
            if ((!VirtualKeyCodeMapper.HasKey(currentKeyToSend)) || (VirtualKeyCodeMapper.HasExcludeKey(currentKeyToSend)))
            {
                keyProcessing = false;
                _currentKeyToSend[0] = "";
                return;
            }

            keyProcessing = true;




            int vkCode = 0;


            if (_wowWindowHandle != nint.Zero)
            {
                //assuming we got here means we can do anything we want with the regions settings as they will update to the true values in the background
                //and we know what keys we want to send
                CurrentImageRegions.FirstImageRegions.TopLeft = false;
                CurrentImageRegions.FirstImageRegions.TopRight = false;
                CurrentImageRegions.FirstImageRegions.BottomLeft = false;
                CurrentImageRegions.FirstImageRegions.BottomCenter = false;





                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Red;
                ImageCap2Border.BorderBrush = System.Windows.Media.Brushes.Black;

                // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                // It fails if the next key to press is the same.
                // There would have to some logic in the capture to say its a new detection

                // Tranlate the char to the virtual Key Code
                vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(currentKeyToSend);

                // command is tied to CTRL or ALT So have to press them
                if (currentKeyToSend[0] == 'C') //&& CtrlPressed == false
                    PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_CONTROL, 0);
                else
                //    // Command isn't tied to CTRL so send a CTRL Up.
                //    // This should really be peeking in the message buffer to see if the the key is really pressed or not. and only send the up if it is. 
                //    // This could also be accomlished buy storing off the value in the message processor and storing a flag local if it saw one or not.
                //    // keyboards are global so that may work.
                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_CONTROL, 0);  
                if (currentKeyToSend[0] == 'A') // && AltPressed == false
                    PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_MENU, 0);
                else
                    // See Notes on CTRL.
                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_MENU, 0);

                // Press the command Key Down
                PostMessage(_wowWindowHandle, WM_KEYDOWN, vkCode, 0);
                
                
                // CTRL and ALT do not need to be held down just only pressed initally for the command to be interpeted correctly
                if (currentKeyToSend[0] == 'C' ) PostMessage(_wowWindowHandle, WM_KEYUP, VK_CONTROL, 0); //&& CtrlPressed == true
                if (currentKeyToSend[0] == 'A' ) PostMessage(_wowWindowHandle, WM_KEYUP, VK_MENU, 0); //&& AltPressed == true
                //     await Task.Delay((int)sliderCaptureRateMS.Value); // Give some time for hekili to refresh


                // I want atleast 1 cycle to go thru
                while (CurrentImageRegions.FirstImageRegions.TopRight == false)
                {
                    await Task.Delay(1);
                    await Task.Yield();
                }
                _currentKeyToSend[0] = "";
       

                if (_keyPressMode)
                {
                    await Task.Yield();
                    while (CurrentImageRegions.FirstImageRegions.TopLeft == false && button_Start.IsEnabled == false)
                    {


                        //await Task.Delay(5);
                        await Task.Yield();
                        // Lets explore some second options while this is on cooldown
                        if (Properties.Settings.Default.Use2ndImageDetection == true )
                        {
                      //      var currentKeyToSend1 = _currentKeyToSend[1];
                            if (currentKeyToSend1 == "")
                                continue;

                            // This is to avoid duplicate keypresses.  not sure if blocking it is helpful or not, in theory it should just pop to the primary,
                            // but allowing it to press early should make it fire a little faster.   unsure...  skipping it avoids the question.  
                            if (currentKeyToSend1 == currentKeyToSend)
                                continue;





                            #region 2nd Key Options

                            keyProcessing2 = true;

                            //if (CurrentImageRegions.FirstImageRegions.TopLeft == false)
                            //{
                            //    keyProcessing2 = false;
                            //    _currentKeyToSend[1] = "";
                            //    continue;
                            //}

                         //   if (CurrentImageRegions.SecondImageRegions.TopLeft == false)
                            //{
                            //    keyProcessing2 = false;
                            //    _currentKeyToSend[1] = "";
                            //    continue;
                            //}

                            if ((!VirtualKeyCodeMapper.HasKey(currentKeyToSend1)) || (VirtualKeyCodeMapper.HasExcludeKey(currentKeyToSend1))
                            )
                            {
                                keyProcessing2 = false;
                                _currentKeyToSend[1] = "";
                                break;
                            }
                            CurrentImageRegions.SecondImageRegions.TopLeft = false;
                            CurrentImageRegions.SecondImageRegions.TopRight = false;
                            CurrentImageRegions.SecondImageRegions.BottomLeft = false;
                            CurrentImageRegions.SecondImageRegions.BottomCenter = false;
                            _currentKeyToSend[1] = "";

                            int vkCode2 = 0;
                            if (_wowWindowHandle != nint.Zero)
                            {

             

                                ImageCap2Border.BorderBrush = System.Windows.Media.Brushes.Red;
                                // Let up on the 1st command key
                                PostMessage(_wowWindowHandle, WM_KEYUP, vkCode, 0);
                                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;


                                // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                                // It fails if the next key to press is the same.
                                // There would have to some logic in the capture to say its a new detection


                                await Task.Yield();

                                // Handle the if command is tied to CTRL or ALT
                                if (currentKeyToSend1[1] == 'C') //&& CtrlPressed == false
                                    PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_CONTROL, 0);
                                else
                                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_CONTROL, 0);

                                if (currentKeyToSend1[1] == 'A') //&& AltPressed == false
                                    PostMessage(_wowWindowHandle, WM_KEYDOWN, VK_MENU, 0);
                                else
                                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_MENU, 0);

                         
                                // Tranlate the char to the virtual Key Code
                                vkCode2 = VirtualKeyCodeMapper.GetVirtualKeyCode(currentKeyToSend1);
                                _currentKeyToSend[0] = "";
                                PostMessage(_wowWindowHandle, WM_KEYDOWN, vkCode2, 0);
                                // CTRL and ALT do not need to be held down just only pressed initally for the command to be interpeted correctly
                                if (currentKeyToSend1[1] == 'C') // && CtrlPressed == true
                                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_CONTROL, 0);

                                if (currentKeyToSend1[1] == 'A') // && AltPressed == true
                                    PostMessage(_wowWindowHandle, WM_KEYUP, VK_MENU, 0);


                                if (_keyPressMode)
                                {
                                    // Now we pause until top is filled then we release the key that should queue the command.
                                    while (CurrentImageRegions.FirstImageRegions.TopLeft == false && button_Start.IsEnabled == false)
                                    {
                                        await Task.Yield();
                                       
                                    }
                                }

                                PostMessage(_wowWindowHandle, WM_KEYUP, vkCode2, 0);

                                // this stops the sending of the key till the timer is almost up.  
                                // it takes advantage of the cooldown visual cue in the game that darkens the font (changes the color)
                                // the OCR doesn't see a new char until it is almost times out, at that point it can be pressed and would be added to the action queue

                                keyProcessing2 = false;
        
                                #endregion


                            }

                        }
                    }
                }



                // If where not watching for when things time out, we insert a hard delay
                if (!_keyPressMode)
                {
                    await Task.Delay(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS);
                }

                // Let up on the command key
                PostMessage(_wowWindowHandle, WM_KEYUP, vkCode, 0);

                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
                keyProcessing = false;
           

                // this stops the sending of the key till the timer is almost up.  
                // it takes advantage of the cooldown visual cue in the game that darkens the font (changes the color)
                // the OCR doesn't see a new char until it is almost times out, at that point it can be pressed and would be added to the action queue
                _DetectedValueFirst = "";
                }

            

            ImageCap2Border.BorderBrush = System.Windows.Media.Brushes.Black;

            keyProcessing = false;
        }
    


//        Bitmap _holderBitmap;
        public MainWindow()
        {
            InitializeComponent();
            mainWindowDispatcher = this.Dispatcher;


            magnifier = new MagnifierWindow();
            magnifier.Left = Properties.Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Properties.Settings.Default.CapX;
            magnifier.Top = Properties.Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Properties.Settings.Default.CapY;
            magnifier.Width = Properties.Settings.Default.CapWidth;
            magnifier.Height = Properties.Settings.Default.CapHeight;
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;




            magnifier2 = new MagnifierWindow();
            magnifier2.border.BorderBrush = BorderBrush = System.Windows.Media.Brushes.Blue;
            magnifier2.Left = Properties.Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Properties.Settings.Default.Cap2X;
            magnifier2.Top = Properties.Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Properties.Settings.Default.Cap2Y;
            magnifier2.Width = Properties.Settings.Default.Cap2Width;
            magnifier2.Height = Properties.Settings.Default.Cap2Height;
            magnifier2.SizeChanged += Magnifier2_SizeChanged;
            magnifier2.LocationChanged += Magnifier2_LocationChanged;






            //TargetColorPicker.ColorState =  new ColorPicker.Models.ColorState();
            TargetColorPicker.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Properties.Settings.Default.TargetA, (byte)Properties.Settings.Default.TargetR, (byte)Properties.Settings.Default.TargetG, (byte)Properties.Settings.Default.TargetB);
            CurrentR = Properties.Settings.Default.TargetR;
            CurrentG = Properties.Settings.Default.TargetG;
            CurrentB = Properties.Settings.Default.TargetB;
            CurrentA = Properties.Settings.Default.TargetA; 

  //          _holderBitmap = ImageHelpers.CreateBitmap(60, 60, System.Drawing.Color.Black);

            tbVariance.Text = Properties.Settings.Default.VariancePercent.ToString();
            sliderColorVariancePercent.Value = Properties.Settings.Default.VariancePercent;

            tbCaptureRateMS.Text = Properties.Settings.Default.CaptureRateMS.ToString();
            sliderCaptureRateMS.Value = Properties.Settings.Default.CaptureRateMS;

            tbKeyRateMS.Text = Properties.Settings.Default.KeyPressSpeedMS.ToString();
            sliderKeyRateMS.Value = Properties.Settings.Default.KeyPressSpeedMS;

            cbPushRelease.IsChecked = Properties.Settings.Default.PushAndRelease;
            cbQuickDecode.IsChecked = Properties.Settings.Default.QuickDecode;
            cbStayOnTop.IsChecked = Properties.Settings.Default.KeepOnTop;

            cbUse2ndImage.IsChecked = Properties.Settings.Default.Use2ndImageDetection;
            
            ImageCap2Border.Visibility = cbUse2ndImage.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            lDetectedValue2.Visibility = ImageCap2Border.Visibility; // no need to reeval the vars, we already know.  (yeah this can be done in xaml bindings..  but right now I don't know how and don't feel like looking it up.)

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
          
            _timer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += mainTimerTick;






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
                    Magnifier2_LocationChanged(sender, e);
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

            if (magnifier2.Visibility == Visibility.Visible)
            {
                magnifier2.Visibility = Visibility.Hidden;
            }
            else
            {
                magnifier2.Visibility = Visibility.Visible;
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
                //     if (screenCapture.CaptureRegion != null ) 
                screenCapture.CaptureRegion = 

                    new System.Windows.Rect[2] 
                    { new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1),
                      screenCapture.CaptureRegion == null ? new System.Windows.Rect() : screenCapture.CaptureRegion[1]
                     };
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


                //    if (screenCapture.CaptureRegion != null)
                screenCapture.CaptureRegion = 
                    new System.Windows.Rect[2]
                    { 
                        new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1),
                      screenCapture.CaptureRegion == null ? new System.Windows.Rect() : screenCapture.CaptureRegion[1]
                     };                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }
        }

        private void Magnifier2_LocationChanged(object? sender, EventArgs e)
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
                var left = magnifier2.CurrrentLocationValue.X;
                var top = magnifier2.CurrrentLocationValue.Y;
                var width = magnifier2.CurrrentLocationValue.Width;
                var height = magnifier2.CurrrentLocationValue.Height;

                // Adjust for DPI scaling
                var scaledLeft = left * dpiX;
                var scaledTop = top * dpiY;
                var scaledWidth = width * dpiX;
                var scaledHeight = height * dpiY;
                // if (screenCapture.CaptureRegion != null)
                screenCapture.CaptureRegion = //new System.Windows.Rect(scaledLeft + 1, scaledTop + 1, scaledWidth - 1, scaledHeight - 1);
                    new System.Windows.Rect[2]
                    {
                      screenCapture.CaptureRegion == null ? new System.Windows.Rect() : screenCapture.CaptureRegion[0],
                     new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1)
                     };                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }

        }

        private void Magnifier2_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (screenCapture == null) return;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var dpiX = source.CompositionTarget.TransformToDevice.M11;
                var dpiY = source.CompositionTarget.TransformToDevice.M22;

                // Get the window's current location
                var left = magnifier2.CurrrentLocationValue.X;
                var top = magnifier2.CurrrentLocationValue.Y;
                var width = magnifier2.CurrrentLocationValue.Width;
                var height = magnifier2.CurrrentLocationValue.Height;

                // Adjust for DPI scaling
                var scaledLeft = (left * dpiX) + 1;
                var scaledTop = (top * dpiY) + 1;
                var scaledWidth = (width * dpiX) - 1;
                var scaledHeight = (height * dpiY) - 15;

                scaledWidth = scaledWidth < 0 ? 1 : scaledWidth;
                scaledHeight = scaledHeight < 0 ? 1 : scaledHeight;


                //     if (screenCapture.CaptureRegion != null)
                screenCapture.CaptureRegion = //new System.Windows.Rect(scaledLeft + 1, scaledTop + 1, scaledWidth - 1, scaledHeight - 1);
                    new System.Windows.Rect[2]
                    {
                      screenCapture.CaptureRegion == null ? new System.Windows.Rect() : screenCapture.CaptureRegion[0],
                     new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1)
                     };                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }
        }


        private void Window_Closed(object sender, EventArgs e)
        {



            Properties.Settings.Default.CapX = magnifier.Left;
            Properties.Settings.Default.CapY = magnifier.Top;
            Properties.Settings.Default.CapWidth = magnifier.Width;
            Properties.Settings.Default.CapHeight = magnifier.Height;
            Properties.Settings.Default.Cap2X = magnifier2.Left;
            Properties.Settings.Default.Cap2Y = magnifier2.Top;
            Properties.Settings.Default.Cap2Width = magnifier2.Width;
            Properties.Settings.Default.Cap2Height = magnifier2.Height;
            Properties.Settings.Default.AppStartX = this.Left;
            Properties.Settings.Default.AppStartY = this.Top;
            Properties.Settings.Default.TargetR = CurrentR;
            Properties.Settings.Default.TargetG = CurrentG;
            Properties.Settings.Default.TargetB = CurrentB;
            Properties.Settings.Default.TargetA = 255;

            Properties.Settings.Default.Save();

            magnifier.Close();
            magnifier2.Close();

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

 
        private void buPicker_Click(object sender, RoutedEventArgs e)
        {
            _MouseHookID = MouseSetHook(_mouseProc);
            //ChangeCursor();
            this.TargetColorPicker.SelectedColor = System.Windows.Media.Color.FromArgb(255, 0, 0, 0);
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

            magnifier2.Left = Properties.Settings.Default.Cap2X > SystemParameters.PrimaryScreenWidth ? 100 : Properties.Settings.Default.CapX;
            magnifier2.Top = Properties.Settings.Default.Cap2Y > SystemParameters.PrimaryScreenHeight ? 100 : Properties.Settings.Default.CapY;
            magnifier2.Width = Properties.Settings.Default.Cap2Width;
            magnifier2.Height = Properties.Settings.Default.Cap2Height;

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

        private void TargetColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
          
            Properties.Settings.Default.TargetR = TargetColorPicker.SelectedColor.R;
            Properties.Settings.Default.TargetG = TargetColorPicker.SelectedColor.G; 
            Properties.Settings.Default.TargetB = TargetColorPicker.SelectedColor.B;
            Properties.Settings.Default.TargetA = TargetColorPicker.SelectedColor.A;
            CurrentR = TargetColorPicker.SelectedColor.R;
            CurrentG = TargetColorPicker.SelectedColor.G;
            CurrentB = TargetColorPicker.SelectedColor.B;
        }

        private void cbUse2ndImage_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Use2ndImageDetection = true;
            ImageCap2Border.Visibility = Visibility.Visible;
            lDetectedValue2.Visibility = Visibility.Visible;

        }

        private void cbUse2ndImage_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Use2ndImageDetection = false;
            ImageCap2Border.Visibility = Visibility.Collapsed;
            lDetectedValue2.Visibility = Visibility.Collapsed;

        }
    }
}

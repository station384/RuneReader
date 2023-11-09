using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;


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


    // This is the list of acceptable keys we can send to the game and the associated Windows virtual key to send.
    // We can use this for comparison or use it for looking up the matching key
    public static class VirtualKeyCodeMapper
    {

        private static readonly Dictionary<string, int> KeyMappings = new Dictionary<string, int>
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
        // {"-", (int)VirtualKeyCodes.VirtualKeyStates.VK_OEM_MINUS},
        //  {"=", 187}, // This key can be different depending on country, i.e.  US its the = key,  Spanish is the ? (upside down)
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
        {"C1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
        {"C2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
        {"C3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
        {"C4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
        {"C5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
        {"C6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
        {"C7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
        {"C8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
        {"C9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
        {"C0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
        {"A1", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_1},
        {"A2", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_2},
        {"A3", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_3},
        {"A4", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_4},
        {"A5", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_5},
        {"A6", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_6},
        {"A7", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_7},
        {"A8", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_8},
        {"A9", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_9},
        {"A0", (int)VirtualKeyCodes.VirtualKeyStates.VK_Alphanumeric_0},
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

    public partial class MainWindow : Window
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


        // Windows message constants
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;

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
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion

        private string CurrentKeyToPress { get; set; }
        private volatile string _currentKeyToSend = string.Empty; // Default key to send, can be changed dynamically
        private volatile string _lastKeyToSend = string.Empty; // Default key to send, can be changed dynamically
        private IntPtr _hookID = IntPtr.Zero;
        private KeyboardHookProc _proc;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;
        private ImageHelpers ImageHelpers = new ImageHelpers();
        private delegate IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam);


    

        public  string GetActiveWindowTitle()
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




        private IntPtr SetHook(KeyboardHookProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }



        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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
                    _lastKeyToSend = string.Empty; 
                    // Let the key event go thru so the new focused app can handle it
                    handled = false;
                }
                else
                {

                    if (wParam == (IntPtr)WM_KEYDOWN && key == Key.D1) // Replace SomeCapturedKey with the actual captured key
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
                    else if (wParam == (IntPtr)WM_KEYUP && key == Key.D1) // Replace SomeCapturedKey with the actual captured key
                    {
                        _timer.Stop();
                        handled = true;
                    }
                }
            }


            // If the keypress has been handled, return a non-zero value.
            // Otherwise, call the next hook in the chain.
            return handled ? (IntPtr)1 : CallNextHookEx(_hookID, nCode, wParam, lParam);
        

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
       


        public void StartCaptureProcess()
        {
            // Define the area of the screen you want to capture
            int x = (int)magnifier.Left, 
                y = (int)magnifier.Top, 
                width = (int)magnifier.Width, 
                height = (int)magnifier.Height;

            // Initialize CaptureScreen with the dispatcher and the UI update action
            captureScreen = new CaptureScreen(x, y, width, height,0);
            var ocr = new OcrModule();
            //  image.Source = Convert(captureScreen.CapturedImage);

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                250,
                Dispatcher,
                captureScreen
            );

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateUIImage += (Bitmap image) =>
            {
                Bitmap b = image;
                double BlurRadius = sliderBlur.Value;
                double UnsharpPower = sliderAmount.Value;
                double Threshold = sliderThreshold.Value;

                var origWidth = b.Width;
                var origHeight = b.Height;

                //Remember this is running in the background and every CPU cycle counts!!
                //This has to be FAST it is executing every 250 miliseconds 4 times a second
                //The faster this is the more times per second we can evaluate and react faster
                
                


                // It is expected that in the game the font on the hotkey text will be set to R:25 B:255 G:255 The font set to mica, and the size set to 40.
                // We filter out everying that isn't close to the color we want.
                // Doing it this way because it was FAST.  This could be doing by doing a find conture and area but that takes alot more caculation than just a simple color filter
                b = ImageHelpers.FilterByColor(b, System.Drawing.Color.FromArgb(25, 255, 255), 0.95);
                b = ImageHelpers.RescaleImageToDpi(b, 300);

                // Bring the levels to somthing predictable, to simplify we convert it to greyscale
                b = ImageHelpers.ConvertToGrayscaleFast(b);
                b = ImageHelpers.BumpToBlack(b, 170);
                b = ImageHelpers.BumpToWhite(b, 180);
      
    
                // For tesseract it doesn't like HUGE text so we bring it back down to the original size
                b = ImageHelpers.ResizeImage(b, origWidth, origHeight);
  



                Bitmap DisplayImage = b;


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



                imageCap.Source = Convert(DisplayImage);

                string s = ocr.PerformOcr(b).Replace("\n", "");
                if (VirtualKeyCodeMapper.HasKey(s))
                {
                    CurrentKeyToPress = StringExtensions.Extract(s,3) ;
                    if (!string.IsNullOrEmpty(CurrentKeyToPress.Trim()))
                    {
                        _currentKeyToSend = CurrentKeyToPress;
                    } else
                    {
                        _currentKeyToSend = "";
                    }
                   // Console.WriteLine(s);
                   
                }
                lDetectedValue.Content = s;
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



        public MainWindow()
        {
            InitializeComponent();

            magnifier = new MagnifierWindow();
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;
            magnifier.Left = Properties.Settings.Default.CapX;
            magnifier.Top = Properties.Settings.Default.CapY;
            magnifier.Width = Properties.Settings.Default.CapWidth;
            magnifier.Height = Properties.Settings.Default.CapHeight;


            OpenMagnifierWindow();

            this.Left = Properties.Settings.Default.AppStartX;
            this.Top = Properties.Settings.Default.AppStartY;

            CurrentKeyToPress = "";
            _proc = HookCallback;


            sliderBlur.Value = 100;
            sliderAmount.Value = 1;
            sliderThreshold.Value = 1;

            _wowWindowHandle = FindWindow(null, "World of Warcraft");


            StartCaptureProcess();


            // This timer handles the key sending
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(250);
            _timer.Tick += async (sender, args) =>
            {
                // Check the key dictionary if the key is one we should handle
                if (!VirtualKeyCodeMapper.HasKey(_currentKeyToSend)) return;
                int vkCode = 0;
                // Tranlate the char to the virtual Key Code
                vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(_currentKeyToSend);
               // int vkCode = _currentKeyToSend + 0x30; // 0x30 is the virtual-key code for "0"
                //KeyInterop.VirtualKeyFromKey(e.Key)
                if (_wowWindowHandle != IntPtr.Zero)
                {
                    // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                    // It fails if the next key to press is the same.
                    // There would have to some logic in the capture to say its a new detection
                   // if (_lastKeyToSend != _currentKeyToSend)
                    {
                        _lastKeyToSend = _currentKeyToSend;
                        PostMessage(_wowWindowHandle, WM_KEYDOWN, vkCode, 0);
                        // It may not be necessary to send WM_KEYUP immediately after WM_KEYDOWN
                        // because it simulates a very quick key tap rather than a sustained key press.
                        await Task.Delay(Random.Shared.Next() % 15 + 8); 
                        PostMessage(_wowWindowHandle, WM_KEYUP, vkCode, 0);
                        _lastKeyToSend =  _currentKeyToSend;

                        // this stops the sending of the key till the timer is almost up.  
                        // it takes advantage of the cooldown visual cue in the game that darkens the font (changes the color)
                        // the OCR doesn't see a new char until it is almost times out, at that point it can be pressed and would be added to the action queue
                        _currentKeyToSend = ""; 
                    
                    }
                }
            };

            


        }

        #region UI Event handlers
        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Start the continuous capturing
            _wowWindowHandle = FindWindow(null, "World of Warcraft");
            if (_wowWindowHandle != IntPtr.Zero)
            {
                if (!screenCapture.IsCapturing)
                {
                    screenCapture.StartCapture();

                    _hookID = _hookID == 0 ? SetHook(_proc) : 0; 
                }
            }
 
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // ... When you want to stop capturing:
            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
                UnhookWindowsHookEx(_hookID);
                _hookID = 0;
            }
        }

        private void UpdateImageControl(BitmapSource bitmapSource)
        {
            // Assuming there's an Image control named 'imageControl' in the XAML
            imageCap.Source = bitmapSource;
        }

        private void sliderBlur_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            textBoxBlur.Text = e.NewValue.ToString();
        }

        private void sliderAmount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            textBoxAmount.Text = e.NewValue.ToString();
        }

        private void sliderThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            textBoxThreshold.Text = e.NewValue.ToString();
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

        private void Magnifier_LocationChanged(object? sender, EventArgs e)
        {
            if (screenCapture == null) return;
            screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;
        }

        private void Magnifier_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (screenCapture == null) return;
            screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;
        }

        private void Window_Closed(object sender, EventArgs e)
        {



            Properties.Settings.Default.CapX = magnifier.Left;
            Properties.Settings.Default.CapY = magnifier.Top;
            Properties.Settings.Default.CapWidth = magnifier.Width;
            Properties.Settings.Default.CapHeight = magnifier.Height;
            Properties.Settings.Default.AppStartX = this.Left;
            Properties.Settings.Default.AppStartY = this.Top;
            Properties.Settings.Default.Save();


            CloseMagnifierWindow();

            // Make sure we stop trapping the keyboard
            UnhookWindowsHookEx(_hookID);
        }
        #endregion
    }
}

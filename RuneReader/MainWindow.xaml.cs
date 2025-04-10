using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Controls;
using RuneReader.Properties;
using System.Windows.Threading;
using System.Linq;
using MahApps.Metro.Controls;
using System.Runtime.CompilerServices;
using static RuneReader.BarcodeDecode;
using System.Threading;
using ZXing;
using RuneReader.Classes;
using RuneReader.Classes.Utilities;
using System.Timers;



namespace RuneReader
{

    public partial class MainWindow : MetroWindow
    {
        public static UserSettings AppSettings { get; private set; }

        private volatile Stack<KeyCommand> KeyCommandStack = new Stack<KeyCommand> ();

        private volatile string _currentKeyToSend =  string.Empty ; // Default key to send, can be changed dynamically

        private volatile bool keyProcessingFirst = false;
        private volatile bool activationKeyPressed = false;

        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _MouseHookID = IntPtr.Zero;
        private WindowsAPICalls.WindowsMessageProc _proc;
        private WindowsAPICalls.WindowsMessageProc _mouseProc;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;


        private bool BarCodeFound = false;

        
  

        private MagnifierWindow magnifier;
        private volatile ImageRegions CurrentImageRegions = new ImageRegions();
        private DispatcherTimer _timer;
        private DispatcherTimer _TimerWowWindowMonitor; // This timer is here just incase the game closes and is reopened, this will catch the new window ID.
        private DispatcherTimer _TimerBarcodeMonitor; // This timer is here to attempt to find and set the barcode location automaticly.


        private int CurrentR = 25;
        private int CurrentG = 255;
        private int CurrentB = 255;
        private int CurrentA = 255;

        private double CurrentThreshold = 0.3;
        private int CurrentCaptureRateMS = 100;
        private int CurrentKeyPressSpeedMS = 125;
        private int CurrentKeyDownDelayMS = 25;
        private Dispatcher mainWindowDispatcher;
        private int PetKeyVKCode = 0;

        private volatile bool _keyPressMode = false;
        private volatile float WowGamma = 1.0f;
        private volatile bool ProcessingKey = false;

        private bool Initalizing = true;  // To prevent events from firing as the xaml defaults are applied

        

        private IntPtr SetHookActionKey(WindowsAPICalls.WindowsMessageProc proc)
        {
            IntPtr result = IntPtr.Zero;
            using (Process curProcess = Process.GetCurrentProcess())
            {
                if (curProcess.MainModule != null)
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    result = WindowsAPICalls.SetWindowsHookEx(WindowsAPICalls.WH_KEYBOARD_LL, proc, WindowsAPICalls.GetModuleHandle(curModule.ModuleName), 0);
                }
            }
            return result;
        }

        private bool AltPressed = false;
        private bool CtrlPressed = false;


        private IntPtr HookCallbackActionKey(int nCode, IntPtr wParam, IntPtr lParam)
        {
   

            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                // We don't want to send key repeats if the app is not in focus
                if (!WindowsAPICalls.IsCurrentWindowWithTitle("World of Warcraft"))
                {
                    _timer.Stop();

                    // Let the key event go thru so the new focused app can handle it
                    keyProcessingFirst = false;
                    activationKeyPressed = false;
                }
                else
                {
                    var item = ActivationKeyCodeMapper.GetVirtualKeyCode(AppSettings.ActivationKey);
                    if (keyProcessingFirst == false)
                    {
                        if (wParam == (IntPtr)WindowsAPICalls.WM_KEYDOWN && (int)key == item)
                        {
                            // Find the window with the title "wow" only if we haven't already found it
                            if (_wowWindowHandle == IntPtr.Zero)
                            {
                                _wowWindowHandle =  WindowsAPICalls.FindWowWindow("World of Warcraft");
                            }
                            if (_wowWindowHandle != IntPtr.Zero)
                            {
                                activationKeyPressed = true;
                                keyProcessingFirst = true;

                                _timer.Start();
                 
                            }


                        }
                    }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYUP && (int)key == item)
                    {
                        activationKeyPressed = false;
                        keyProcessingFirst = false;
                        _timer.Stop();
                
                    }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYDOWN && key == System.Windows.Input.Key.LeftCtrl)
                    {
                        CtrlPressed = true;
                    }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYDOWN && key == System.Windows.Input.Key.LeftAlt)
                    {
                        AltPressed = true;
                    }

                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYUP && key == System.Windows.Input.Key.LeftCtrl)
                    {
                        CtrlPressed = false;
                    }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYUP && key == System.Windows.Input.Key.LeftAlt)
                    {
                        AltPressed = false;
                    }


                }
            }

                return WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
            
        }


        private struct ProcessImageResult
        {
            public string CurrentKeyToSend;
            public int WaitTime;
            public bool HasTarget;
            public DetectionRegions regions;
        }

        /// <summary>
        /// Used to find the delays and text in the image 
        /// </summary>
        /// <param name="image">Bitmap we are going to process</param>
        /// <param name="lKeyVal">Label control we will update the text of</param>
        /// <param name="lWait">Label control we will update the text of</param>
        /// <param name="DisplayControl">Image used for OCR refence to USER no delays</param>
        /// <param name="Threshold">0.0 -> 1.0 How much variance of color are we going to call the same</param>
        /// <returns>ProcessImageResult</returns>
        private ProcessImageResult ProcessImageOpenCV(ref Mat image,  ref System.Windows.Controls.Label lKeyVal, ref Label lWait,    ref System.Windows.Controls.Image DisplayControl,  double Threshold)
        {
            
            var origWidth = image.Width;
            var origHeight = image.Height;
            var CurrentKeyToSend = string.Empty;
            double xMin = 0;
            double yMin = 0;
            double xMax = 0;
            double yMax = 0;
            int Rscale = ((int)(CurrentR * ((CurrentR * Threshold) / CurrentR)));
            int Gscale = ((int)(CurrentG * ((CurrentG * Threshold) / CurrentG)));
            int Bscale = ((int)(CurrentB * ((CurrentB * Threshold) / CurrentB)));


            var result = new ProcessImageResult { CurrentKeyToSend = "", HasTarget=false, WaitTime = 0, regions = new DetectionRegions { HasTarget=false, WaitTime = 0, BottomCenter=false, BottomLeft=false, TopLeft=false, TopRight=false} };
            BitmapSource? OutImageSource;

            var CVMat = image.Clone();
            double wowGammaSetting = WowGamma;


            Mat srcGray = new Mat();
            Cv2.CvtColor(CVMat, srcGray, ColorConversionCodes.BGR2GRAY);
            double thresholdValue = 220;
            double maxValue = 255;
            Cv2.BitwiseNot(srcGray, srcGray);
            Cv2.Threshold(srcGray, srcGray, thresholdValue, maxValue, ThresholdTypes.Binary);
            Cv2.BitwiseNot(srcGray, srcGray);


            var barcodeResult = BarcodeDecode.DecodeBarcode(srcGray);
                if (barcodeResult.BarcodeFound)
                {

                    result = new ProcessImageResult { 
                       CurrentKeyToSend = "", 
                       HasTarget = barcodeResult.HasTarget, 
                       WaitTime = barcodeResult.WaitTime, 
                       regions = new DetectionRegions { 
                           HasTarget = barcodeResult.HasTarget, 
                           WaitTime = barcodeResult.WaitTime, 
                           BottomCenter = (barcodeResult.WaitTime <= 500), 
                           BottomLeft = (barcodeResult.WaitTime <= 300), 
                           TopLeft = (barcodeResult.WaitTime <= 0), 
                           TopRight = (barcodeResult.WaitTime < 1000)
                       } 
                    };

                    BarCodeFound = true;
                    if (barcodeResult.HasTarget == true || cbIgnoreTargetInfo.IsChecked == true)
                    {
                        CurrentKeyToSend = barcodeResult.DecodedTextValue;
                    }
                    else
                    {
                        CurrentKeyToSend = "";
                    }
                }
                else
                {
                    BarCodeFound = false;
                }
            

            // Push the new image out the the first image,  this has the markers and delays
            if (BarCodeFound)
                OutImageSource = BitmapSourceConverter.ToBitmapSource(srcGray);
            else
                OutImageSource = BitmapSourceConverter.ToBitmapSource(CVMat);


            DisplayControl.Source = OutImageSource;

            // Push the image that doesn't have delays out to the second display.   This image is what is OCRed on.
            if (BarCodeFound)
                OutImageSource = BitmapSourceConverter.ToBitmapSource(srcGray);
            else
                OutImageSource = BitmapSourceConverter.ToBitmapSource(CVMat);
         

            // Update the label
            lKeyVal.Content = CurrentKeyToSend;
            lWait.Content = result.WaitTime.ToString();
            result.CurrentKeyToSend = CurrentKeyToSend;
            
            CVMat.Dispose();
            image.Dispose();
            srcGray.Dispose();

            return result;
        }


        private void StartCaptureProcess()
        {
            // Define the area of the screen you want to capture
            int x = (int)magnifier.Left,
                y = (int)magnifier.Top,
                width = (int)magnifier.Width,
                height = (int)magnifier.Height;

   


            // Initialize CaptureScreen with the dispatcher and the UI update action
            System.Windows.Rect regions = new System.Windows.Rect();
            regions = new System.Windows.Rect { X = (double)x, Y = (double)y, Width = width, Height = height };

            captureScreen = new CaptureScreen(regions, 0);

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                CurrentCaptureRateMS,
                Dispatcher,
                captureScreen
            );

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateFirstImage +=  (Mat image) =>
            {
                double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
                var capResult = ProcessImageOpenCV(ref image, ref lDetectedValue, ref lDetectedTime, ref imageCap, trasThreshold);
                CurrentImageRegions.FirstImageRegions.TopRight = capResult.regions.TopRight;
                CurrentImageRegions.FirstImageRegions.TopLeft = capResult.regions.TopLeft;
                CurrentImageRegions.FirstImageRegions.BottomLeft = capResult.regions.BottomLeft;
                CurrentImageRegions.FirstImageRegions.BottomCenter = capResult.regions.BottomCenter;
                CurrentImageRegions.FirstImageRegions.HasTarget = capResult.regions.HasTarget;
                CurrentImageRegions.FirstImageRegions.WaitTime = capResult.regions.WaitTime;
                _currentKeyToSend = capResult.CurrentKeyToSend;
                image.Dispose();
            };

        }

        private async void mainTimerTick(object? sender, EventArgs args)
        {
            if (activationKeyPressed == true && ProcessingKey == false)
                await ProcessBarCodeKey();  
        }

        private async Task ProcessKey()
        {
           
            if (KeyCommandStack.Count == 0 || ProcessingKey == true) return;
            ProcessingKey = true;
            KeyCommand currentKey = KeyCommandStack.Pop();
 
            if (_wowWindowHandle != nint.Zero)
            {
                DateTime currentD = DateTime.Now;

                if (currentKey.Alt == true && currentKey.Key == "F4")  // Some how AF4 got thru and killed wow.   so I want to Explictly ignore it.  I will never allow ALT-F4
                {
                    ProcessingKey = false;
                    return;
                }
                if (WindowsAPICalls.IsKeyPressed(WindowsAPICalls.VK_MENU) && currentKey.Key == "F4")  // Alt key was pressed so don't want that
                {
                    ProcessingKey = false;
                    return;
                }
 
                // Tranlate the char to the virtual Key Code
                var vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(currentKey.Key);

                // command is tied to CTRL or ALT So have to press them
                if (currentKey.Ctrl) 
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_CONTROL, 0);
                else
                    // Command isn't tied to CTRL so send a CTRL Up.
                    // This should really be peeking in the message buffer to see if the the key is really pressed or not. and only send the up if it is. 
                    // This could also be accomlished buy storing off the value in the message processor and storing a flag local if it saw one or not.
                    // keyboards are global so that may work.
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0);
             
                if (currentKey.Alt) 
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_MENU, 0);
                else
                    // See Notes on CTRL.
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_MENU, 0);



                // Press the command Key Down
                WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, vkCode, 0);
          


                // CTRL and ALT do not need to be held down just only pressed initally for the command to be interpeted correctly
                if (currentKey.Ctrl) WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0); //&& CtrlPressed == true
                if (currentKey.Alt) WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_MENU, 0); //&& AltPressed == true

                //Add the keypress delay while monitoring that the activationkey is still pressed (allows interrupting the delay)
                DateTime currentMS = DateTime.Now.Add(new TimeSpan(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS) * 1000);
                while ((currentMS >= DateTime.Now ) && activationKeyPressed == true)
                {
                    await Task.Delay(1);
                }
                
               
                
                if (_keyPressMode )
                {
                    await Task.Delay(CurrentCaptureRateMS == 0 ? 2 : CurrentCaptureRateMS / 2); // Try and wait for a capture refresh
                     currentMS = DateTime.Now;
                    currentKey.MaxWaitTime = 5000;
                    currentMS = DateTime.Now.AddMilliseconds(currentKey.MaxWaitTime);
                    DateTime MaxWaitTime = DateTime.Now.AddSeconds(8);

                    while ((currentMS >= DateTime.Now && currentKey.MaxWaitTime >= 350 ) && activationKeyPressed == true )
                    {
                        await Task.Delay(1);
                        currentKey.MaxWaitTime = CurrentImageRegions.FirstImageRegions.WaitTime;
                        if (DateTime.Now > MaxWaitTime)
                        {
                            goto alldone;
                        }
                    }               
                }

                // If where not watching for when things time out, we insert a hard delay
                // This is no longer need as were putting a hard pause above
                //if (!_keyPressMode)
                //{
                //   await Task.Delay(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS);//.ConfigureAwait(true);
                //}
            alldone:
                WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, vkCode, 0);
                ProcessingKey = false;
            }

            return;
       
        }




        private async Task ProcessBarCodeKey()
        {
            if (activationKeyPressed == false)
            {
                return;
            }

            if (ProcessingKey == true)
            {
                return;
            }

            var keyToSendFirst = string.Empty;
            var keyToSendSecond = string.Empty;
            int vkCode = 0;
            DateTime currentD = DateTime.Now;

  

            keyToSendFirst = string.Empty;
            vkCode = 0;

            #region WaitFor a Key to show up


            // lets just hang out here till we have a key
            currentD = DateTime.Now;
            keyToSendFirst = _currentKeyToSend;
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true )
            {
                await Task.Delay(1);
                keyToSendFirst = _currentKeyToSend;
                if (currentD.AddMilliseconds(5000) < DateTime.Now)
                {
                    goto allDone;
                }
            }

            
            if (  !VirtualKeyCodeMapper.HasKey(keyToSendFirst) )
            {
                 goto allDone;
            }


            #endregion

            KeyCommandStack.Push(new KeyCommand(keyToSendFirst, CurrentImageRegions.FirstImageRegions.WaitTime, CurrentImageRegions.FirstImageRegions.HasTarget));
            await ProcessKey();

        allDone:
            ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
        }



        public MainWindow()
        {
            InitializeComponent();
            Initalizing = false;
            mainWindowDispatcher = this.Dispatcher;
            AppSettings = SettingsManager.LoadSettings();

            magnifier = new MagnifierWindow();
            magnifier.Left = AppSettings.CapX > SystemParameters.PrimaryScreenWidth ? 100 : AppSettings.CapX;
            magnifier.Left = AppSettings.CapX < 0 ? 0 : AppSettings.CapX;
            magnifier.Top = AppSettings.CapY > SystemParameters.PrimaryScreenHeight ? 100 : AppSettings.CapY;
            magnifier.Width = AppSettings.CapWidth;
            magnifier.Height = AppSettings.CapHeight;
            magnifier.ShowInTaskbar = false;
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;

            cbPetAttackKey.SelectedValue = cbPetAttackKey.Items[AppSettings.PetKey]; 
            if (AppSettings.PetKeyEnables == true)
            {
                lPet.IsEnabled = true;
                cbPetAttackKey.IsEnabled = true;
                cbPetKeyEnabled.IsChecked = true;
            }
            else
            {
                lPet.IsEnabled = false;
                cbPetAttackKey.IsEnabled = false;
                cbPetKeyEnabled.IsChecked = false;
            }

            sliderWowGamma.Value = AppSettings.WowGamma;
            tbWowGamma.Text = AppSettings.WowGamma.ToString("0.0");
            tbCaptureRateMS.Text = AppSettings.CaptureRateMS.ToString();
            sliderCaptureRateMS.Value = AppSettings.CaptureRateMS;
            tbKeyRateMS.Text = AppSettings.KeyPressSpeedMS.ToString();
            sliderKeyRateMS.Value = AppSettings.KeyPressSpeedMS;
            cbPushRelease.IsChecked = AppSettings.PushAndRelease;
            cbStayOnTop.IsChecked = AppSettings.KeepOnTop;


            if (AppSettings.IgnoreTargetingInfo == true)
            {
                cbIgnoreTargetInfo.IsChecked =  true;
            }
            else
            {
                cbIgnoreTargetInfo.IsChecked = false;

            }

            this.Topmost = AppSettings.KeepOnTop;


            foreach (var x in cbActivationKey.Items)
            {

               if ( ((ComboBoxItem)x).Content.ToString() == AppSettings.ActivationKey)
                    {
                    cbActivationKey.SelectedItem = x;
                }
            }

            OpenMagnifierWindow();

            this.Left = AppSettings.AppStartX;
            this.Top = AppSettings.AppStartY;

            _proc = HookCallbackActionKey;

            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft");

 
            StartCaptureProcess();

            magnifier.Visibility = Visibility.Hidden;

            //This timer watches for the wow window
            _TimerWowWindowMonitor = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Background);
            _TimerWowWindowMonitor.Interval = TimeSpan.FromSeconds(5);
            _TimerWowWindowMonitor.Tick += _TimerWowWindowMonitor_Tick;
            _TimerWowWindowMonitor.Stop();

            //This timer handles sending of the key commands
            _timer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Normal);
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Tick += mainTimerTick;
            _timer.Stop();

            //This timer will run every 5 seconds to try and find the barcode.
            _TimerBarcodeMonitor = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Background);
            _TimerBarcodeMonitor.Interval = TimeSpan.FromSeconds(5);
            _TimerBarcodeMonitor.Tick += _TimerBarcodeMonitor_Tick; ;
            _TimerBarcodeMonitor.Stop();


            //_timer.IsEnabled = false ;

          
        }

        private async void _TimerBarcodeMonitor_Tick(object? sender, EventArgs e)
        {
            if (!BarCodeFound & screenCapture.IsCapturing)
            {
                await AttemptToFindBarcode();
            }
        }

        private void _TimerWowWindowMonitor_Tick(object? sender, EventArgs e)
        {
            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft");
        }

        private async Task AttemptToFindBarcode()
        {
            Mat image = (await captureScreen.GrabFullScreens()).Clone();

            try
            {
                var r = DecodeFind(ref image);
                if (r.screenID >= 1)
                {
                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget != null)
                    {
                        var dpiX = source.CompositionTarget.TransformToDevice.M11;
                        var dpiY = source.CompositionTarget.TransformToDevice.M22;

                        // Get the window's current location
                        magnifier.Left = r.X / dpiX;
                        magnifier.Top = r.Y / dpiY;
                        magnifier.Width = r.Width / dpiX;
                        magnifier.Height = r.Height / dpiY;

                    }
                }
            }
            finally
            { 
                //We could do this as a using.   But I like the try, it helps me visualize when we destroy and what context.
                image.Dispose();
            }
        }



        #region UI Event handlers
        private void buClickKeepMagOnTop(object sender, RoutedEventArgs e)
        {
            if (cbStayOnTop.IsChecked == true)
            {
                this.Topmost = true;
                AppSettings.KeepOnTop = true;
            }
            else
            {
                this.Topmost = false;
                AppSettings.KeepOnTop = false;

            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Start the continuous capturing
            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft"); //WindowsAPICalls.FindWindow(null, "World of Warcraft");
            if (_wowWindowHandle != IntPtr.Zero)
            {
                if (!screenCapture.IsCapturing)
                {
                    Magnifier_LocationChanged(sender, e);
        
                    _currentKeyToSend = "";
                
                    screenCapture.StartCapture();

                    _hookID = _hookID == IntPtr.Zero ? SetHookActionKey(_proc) : IntPtr.Zero; 
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = true;
                    _TimerWowWindowMonitor.Start();
                    _TimerBarcodeMonitor.Start();
                    _timer.Start();

                }
            }
 
        }
       
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // ... When you want to stop capturing:
            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
                if (_hookID != IntPtr.Zero) {
                    WindowsAPICalls.UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }
                button_Start.IsEnabled = true;
                button_Stop.IsEnabled = false;
                _timer.Stop(); 
                _TimerWowWindowMonitor.Stop();
                _TimerBarcodeMonitor.Stop();

            }
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
        }

        private void Magnifier_LocationChanged(object? sender, EventArgs e)
        {
            if (Initalizing ) return;
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
                var scaledLeft = left * dpiX +1;
                var scaledTop = top * dpiY +1;
                var scaledWidth = width * dpiX -1;
                var scaledHeight = height * dpiY -1;
                //     if (screenCapture.CaptureRegion != null ) 
                screenCapture.CaptureRegion = new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1);
                //screenCapture.CaptureRegion = magnifier.CurrrentLocationValue;

            }

        }

        private void Magnifier_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Initalizing) return;
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
                var scaledHeight = (height * dpiY) - 1;

                scaledWidth = scaledWidth < 0 ? 1 : scaledWidth;
                scaledHeight = scaledHeight < 0 ? 1 : scaledHeight;


 
                screenCapture.CaptureRegion =  new System.Windows.Rect(scaledLeft+1, scaledTop+1, scaledWidth-1, scaledHeight-1);               


            }
        }

        // Method to open the MagnifierWindow
        private void OpenMagnifierWindow()
        {
            magnifier.Show();
        }

        private void Window_Closed(object sender, EventArgs e)
        {

            AppSettings.CapX = magnifier.Left;
            AppSettings.CapY = magnifier.Top;
            AppSettings.CapWidth = magnifier.Width;
            AppSettings.CapHeight = magnifier.Height;
            AppSettings.AppStartX = this.Left;
            AppSettings.AppStartY = this.Top;


            SettingsManager.SaveSettings(AppSettings);

            _timer.Stop();
            _TimerWowWindowMonitor.Stop();


            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
            }
           
            
            if (_hookID != IntPtr.Zero) {
                // Make sure we stop trapping the keyboard
                WindowsAPICalls.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }


            if (_MouseHookID != IntPtr.Zero)
            {

                // Make sure we stop trapping the mouse if its active
                WindowsAPICalls.UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;
            }
            magnifier.Close();



        }

        private void sliderCaptureRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
            AppSettings.CaptureRateMS = (int)sliderCaptureRateMS.Value;
            CurrentCaptureRateMS = (int)sliderCaptureRateMS.Value;
            if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
            if (screenCapture != null)
            screenCapture.CaptureInterval = (int)sliderCaptureRateMS.Value;
       
        }


        private void sliderWowGamma_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
            AppSettings.WowGamma = (float)Math.Round(sliderWowGamma.Value,1);
            WowGamma = (float)AppSettings.WowGamma;
            if (tbWowGamma != null)
                tbWowGamma.Text = ((float)AppSettings.WowGamma).ToString("0.0");

        }

        private void sliderKeyRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
            AppSettings.KeyPressSpeedMS = (int)sliderKeyRateMS.Value;
            CurrentKeyDownDelayMS = (int)sliderKeyRateMS.Value;
            if (tbKeyRateMS != null)
            tbKeyRateMS.Text = ((int)sliderKeyRateMS.Value).ToString();

        }

        private new void PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            
            e.Handled = !StringExtensions.IsTextAllowed(e.Text);
        }

        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!StringExtensions.IsTextAllowed(text))
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
            if (Initalizing) return;
            sliderKeyRateMS.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void tbCaptureRateMS_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Initalizing) return;
            sliderCaptureRateMS.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void tbWowGamme_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Initalizing) return;
            //  sliderWowGamma.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }



        private void cbActivationKey_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Initalizing) return;
            AppSettings.ActivationKey = ((ComboBoxItem)cbActivationKey.SelectedItem).Content.ToString();
        }

        private void bResetMagPosition_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.CapX = 50;
            AppSettings.CapY = 50;
            AppSettings.CapWidth = 100;
            AppSettings.CapHeight = 100 ;

            magnifier.Left = AppSettings.CapX > SystemParameters.PrimaryScreenWidth ? 100 : AppSettings.CapX;
            magnifier.Top = AppSettings.CapY > SystemParameters.PrimaryScreenHeight ? 100 : AppSettings.CapY;
            magnifier.Width = AppSettings.CapWidth;
            magnifier.Height = AppSettings.CapHeight;
        }

        private void cbPushRelease_Checked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            _keyPressMode = true;
            AppSettings.PushAndRelease = _keyPressMode;
        }

        private void cbPushRelease_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            _keyPressMode = false;
            AppSettings.PushAndRelease = _keyPressMode;
        }

        private void cbPetAttackKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            AppSettings.PetKey = ((ComboBox)e.Source).SelectedIndex;
            if (e.Source != null)
            {
                if (((ComboBox)e.Source).SelectedItem != null)
                {
                    PetKeyVKCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((ComboBoxItem)((ComboBox)e.Source).SelectedItem).Content.ToString());
                }
            }
        }

        private void cbPetKeyEnabled_Checked(object sender, RoutedEventArgs e)
        {
            lPet.IsEnabled = true;
            cbPetAttackKey.IsEnabled = true;
            AppSettings.PetKeyEnables = lPet.IsEnabled;
        }

        private void cbPetKeyEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            lPet.IsEnabled = false;
            cbPetAttackKey.IsEnabled = false;
            AppSettings.PetKeyEnables = lPet.IsEnabled;
        }

        private void cbIgnoreTargetInfo_Click(object sender, RoutedEventArgs e)
        {
            if (cbIgnoreTargetInfo.IsChecked == null)
            {
                AppSettings.IgnoreTargetingInfo = true;
            }
            else
            {
                if (cbIgnoreTargetInfo.IsChecked.Value)
                {
                    AppSettings.IgnoreTargetingInfo = true;
                }
                else
                {
                    AppSettings.IgnoreTargetingInfo = false;
                }
            }

        }

        private async void bFindBarcode_Click(object sender, RoutedEventArgs e)
        {
            await AttemptToFindBarcode();
        }



        #endregion

    }
}

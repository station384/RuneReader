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
using HekiliEcho.Properties;
using System.Windows.Threading;
using HekiliEcho;
using System.Reflection.Emit;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Vortice.Direct3D11;

namespace HekiliEcho
{

    public partial class MainWindow : System.Windows.Window
    {


        private  string[] _currentKeyToSend = new string[] { string.Empty, string.Empty }; // Default key to send, can be changed dynamically

        private volatile bool keyProcessingFirst = false;
        private volatile bool keyProcessingSecond = false;
        private volatile bool activationKeyPressed = false;


        private volatile int[] _DetectedSameCount = new int[2] { 0, 0 };

        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _MouseHookID = IntPtr.Zero;
        private WindowsAPICalls.WindowsMessageProc _proc;
        private WindowsAPICalls.WindowsMessageProc _mouseProc;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;


        private OcrModule ocr = new OcrModule();
        private MagnifierWindow magnifier;
        private MagnifierWindow magnifier2;
        private ImageRegions CurrentImageRegions = new ImageRegions();
        private DispatcherTimer _timer;



        private int CurrentR = 25;
        private int CurrentG = 255;
        private int CurrentB = 255;
        private int CurrentA = 255;

        private double CurrentThreshold = 0.3;
        private int CurrentCaptureRateMS = 100;
        private int CurrentKeyPressSpeedMS = 125;
        private int CurrentKeyDownDelayMS = 25;
        private Dispatcher mainWindowDispatcher;


        private volatile bool _keyPressMode = false;



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
            bool handled = false;

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
                    keyProcessingSecond = false;
                    activationKeyPressed = false;

                    handled = false;
                }
                else
                {
                    var item = ActivationKeyCodeMapper.GetVirtualKeyCode(Settings.Default.ActivationKey);
                    if (keyProcessingFirst == false || keyProcessingSecond == false)
                        if (wParam == (IntPtr)WindowsAPICalls.WM_KEYDOWN && (int)key == item)
                        {
                            // Find the window with the title "wow" only if we haven't already found it
                            if (_wowWindowHandle == IntPtr.Zero)
                            {
                                _wowWindowHandle = WindowsAPICalls.FindWindow(null, "wow");
                            }
                            if (_wowWindowHandle != IntPtr.Zero && !_timer.IsEnabled )  
                            {
                                activationKeyPressed = true;


                                _timer.Start();
                              //  mainTimerTick(this, new EventArgs());

                                // Don't let the message go thru.  this blocks the game from seeing the key press
                                handled = true;
                            }


                        }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYUP && (int)key == item)
                    {
                        activationKeyPressed = false;

                        _timer.Stop();
  

                        handled = true;
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


            // If the keypress has been handled, return a non-zero value.
            // Otherwise, call the next hook in the chain.

            return WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer
            // return handled ? (IntPtr)0:CallNextHookEx(_hookID, nCode, wParam, lParam); // Locks explorer
            // return handled ? (IntPtr)1:CallNextHookEx(_hookID, nCode, wParam, lParam); // Blocks input to game does not block windows
        }

        private string OCRProcess(Bitmap b, System.Windows.Rect region)
        {
            string Result = "";
            var ocrResult = ocr.PerformPointOcr(b, region);

            string s = ocrResult.Replace("\n", "");
            if (VirtualKeyCodeMapper.HasKey(s) && (!VirtualKeyCodeMapper.HasExcludeKey(s)))
            {
                var CurrentKeyToPress = StringExtensions.Extract(s, 4);
                if (!string.IsNullOrEmpty(CurrentKeyToPress.Trim()))
                {
                    Result = CurrentKeyToPress;
                }
            }
            return Result;

        }


        private string OCRProcess(Bitmap b)
        {

            string Result = "";
            OcrResult ocrResult;
            ocrResult = ocr.PerformFullOcr(b);

            string s = ocrResult.DetectedText.Replace("\n", "");
            if (VirtualKeyCodeMapper.HasKey(s) && (!VirtualKeyCodeMapper.HasExcludeKey(s)))
            {
                var CurrentKeyToPress = StringExtensions.Extract(s, 4);
                if (!string.IsNullOrEmpty(CurrentKeyToPress.Trim()))
                {
                    Result = CurrentKeyToPress;
                }

            }
            return Result;

        }



        private  int ProcessImageOpenCV(Bitmap image, ref System.Windows.Controls.Label label, ref string _DetectedValue, ref int _DetectedSameCount,  ref System.Windows.Controls.Image DisplayControl, ref System.Windows.Controls.Image DisplayControlDelays, double Threshold, ref DetectionRegions regions)
        {
            var origWidth = image.Width;
            var origHeight = image.Height;
            var CurrentKeyToSend = string.Empty;
            int Rscale = ((int)(CurrentR * ((CurrentR * Threshold) / CurrentR)));
            int Gscale = ((int)(CurrentG * ((CurrentG * Threshold) / CurrentG)));
            int Bscale = ((int)(CurrentB * ((CurrentB * Threshold) / CurrentB)));


            int result = -1;
            BitmapSource? OutImageSource;
            var CVMat = BitmapSourceConverter.ToMat(ImageHelpers.Convert(image));
            Mat resizedMat;


            resizedMat = ImageProcessingOpenCV.RescaleImageToNewDpi(CVMat, image.HorizontalResolution, 300);

            var IsolatedColorWithDelays = ImageProcessingOpenCV.IsolateColorHSV(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Threshold);
            var IsolatedColorWithoutDelays = ImageProcessingOpenCV.IsolateColorHSV(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Threshold +1  );

            Mat grayWithDelays = new Mat();
            Mat grayWithoutDelays = new Mat();
            Cv2.CvtColor(IsolatedColorWithDelays, grayWithDelays, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(IsolatedColorWithoutDelays, grayWithoutDelays, ColorConversionCodes.BGR2GRAY);

            // Apply Otsu's thresholding
            Cv2.Threshold(grayWithDelays, grayWithDelays, 250, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //
            Cv2.Threshold(grayWithoutDelays, grayWithoutDelays, 250, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //

            // Find the current bounding boxes, and try and get rid of the useless ones
            System.Windows.Rect[] ocrRegionsWithDelays = ocr.GetRegions(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithDelays));
            System.Windows.Rect[] ocrRegionsWithoutDelays = ocr.GetRegions(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithoutDelays));

            if (ocrRegionsWithDelays == null)
                ocrRegionsWithDelays = new System.Windows.Rect[] {new System.Windows.Rect(0,0,grayWithDelays.Width, grayWithDelays.Height) };


            if (ocrRegionsWithoutDelays == null)
                ocrRegionsWithoutDelays = new System.Windows.Rect[] { new System.Windows.Rect(0, 0, grayWithoutDelays.Width, grayWithoutDelays.Height) };

            List<System.Windows.Rect> usefulRegionsWithDelays = new List<System.Windows.Rect>();
            List<System.Windows.Rect> usefulRegionsWithoutDelays = new List<System.Windows.Rect>();
            if (ocrRegionsWithDelays.Length >= 1 )
            {
                for (int i = 0; i < ocrRegionsWithDelays.Length && i  <= 20; i++)
                {
                    if (ocrRegionsWithDelays[i].Height * ocrRegionsWithDelays[i].Width < 1000)
                    {
                            ImageProcessingOpenCV.FillRectangle(ref grayWithDelays, new OpenCvSharp.Rect((int)ocrRegionsWithDelays[i].X, (int)ocrRegionsWithDelays[i].Y, (int)ocrRegionsWithDelays[i].Width, (int)ocrRegionsWithDelays[i].Width), Scalar.FromRgb(255, 255, 255) );
                    } else
                    {
                        usefulRegionsWithDelays.Add(ocrRegionsWithDelays[i]);
                    }
                }
                   // Not sure if we need to yield to other threads here. Put this here just incase the for loop is huge.  
            } 
            else
            {
                usefulRegionsWithDelays.Add(ocrRegionsWithDelays[0]);
            }



            // Find the total region size of all the regions that were detected
            double xMin = 0;
            double yMin = 0;
            double xMax = 0;
            double yMax = 0;


            xMin = usefulRegionsWithDelays.Min(s => s.X);
            yMin = usefulRegionsWithDelays.Min(s => s.Y);
            xMax = usefulRegionsWithDelays.Max(s => s.X + s.Width);
            yMax = usefulRegionsWithDelays.Max(s => s.Y + s.Height);

            var int32Rect = new Int32Rect((int)xMin, (int)yMin, (int)xMax - (int)xMin, (int)yMax - (int)yMin);
            System.Windows.Rect finalRegionWithDelays = new System.Windows.Rect(int32Rect.X, int32Rect.Y, int32Rect.Width, int32Rect.Height);


            if (ocrRegionsWithoutDelays.Length > 1 )
            {
                for (int i = 0; i < ocrRegionsWithoutDelays.Length &&  i <= 20; i++)
                {
                    if (ocrRegionsWithoutDelays[i].Height * ocrRegionsWithoutDelays[i].Width < 1000)
                    {
                        ImageProcessingOpenCV.FillRectangle(ref grayWithoutDelays, new OpenCvSharp.Rect((int)ocrRegionsWithoutDelays[i].X, (int)ocrRegionsWithoutDelays[i].Y, (int)ocrRegionsWithoutDelays[i].Width, (int)ocrRegionsWithoutDelays[i].Width), Scalar.FromRgb(255, 255, 255));
                    }
                    else
                    {
                        usefulRegionsWithoutDelays.Add(ocrRegionsWithoutDelays[i]);
                    }
                }
            }
            else
            {
                usefulRegionsWithoutDelays.Add(ocrRegionsWithoutDelays[0]);
            }

            //if (usefulRegionsWithoutDelays == null)
            //    return -1;
            // Find the total region size of all the regions that were detected
            xMin = usefulRegionsWithoutDelays.Min(s => s.X);
            yMin = usefulRegionsWithoutDelays.Min(s => s.Y);
            xMax = usefulRegionsWithoutDelays.Max(s => s.X + s.Width);
            yMax = usefulRegionsWithoutDelays.Max(s => s.Y + s.Height);
            int32Rect = new Int32Rect((int)xMin, (int)yMin, (int)xMax - (int)xMin, (int)yMax - (int)yMin);
            System.Windows.Rect finalRegionWithoutDelays = new System.Windows.Rect(int32Rect.X, int32Rect.Y, int32Rect.Width, int32Rect.Height);













            resizedMat = grayWithDelays.Clone();
            resizedMat = ImageProcessingOpenCV.RescaleImageToNewDpi(resizedMat, image.HorizontalResolution, 96);

            regions.TopLeft = ImageProcessingOpenCV.IsThereAnImageInTopLeftQuarter(grayWithDelays);
            regions.TopRight = ImageProcessingOpenCV.IsThereAnImageInTopRightQuarter(grayWithDelays);
            regions.BottomLeft = ImageProcessingOpenCV.IsThereAnImageInBottomLeftQuarter(grayWithDelays);
            regions.BottomCenter = ImageProcessingOpenCV.IsThereAnImageInBottomCenter(grayWithDelays);
    



            string s = OCRProcess(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithoutDelays), finalRegionWithoutDelays);

            CurrentKeyToSend = s;
            Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);

            ImageProcessingOpenCV.DrawMarkers(ref resizedMat);

            OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);
            DisplayControl.Source = OutImageSource;

            OutImageSource = BitmapSourceConverter.ToBitmapSource(grayWithoutDelays);
            DisplayControlDelays.Source = OutImageSource;




            label.Content = s;
            _DetectedValue = s;

            return result;
        }


        private void StartCaptureProcess()
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
            //  image.Source = Convert(captureScreen.CapturedImage); //debuging

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                CurrentCaptureRateMS,
                Dispatcher,
                captureScreen
            );


            // Only process the 2nd image if it is active.  The image will still be captured behind the scenes,  but no OCR will be done on it.

            //screenCapture.UpdateSecondImage += (Bitmap image) =>
            //{
            //    if (Settings.Default.Use2ndImageDetection)
            //    {
            //        double trasThreshold = (CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100) ;
            //        ProcessImageOpenCV(image, ref lDetectedValue2, ref  _currentKeyToSend[1], ref _DetectedSameCount[1], ref imageCap2, trasThreshold , ref CurrentImageRegions.SecondImageRegions);
            //       Task.Yield();

            //    }
            //    else
            //    {
            //        // Not capturing so set values back to 0-state
            //        lDetectedValue2.Content = "";
            //        _DetectedSameCount[1] = 0;
            //        _currentKeyToSend[1] = "";
            //    }
            //};

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateFirstImage +=  (Bitmap image) =>
            {
                double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
                 ProcessImageOpenCV(image, ref lDetectedValue, ref _currentKeyToSend[0], ref _DetectedSameCount[0], ref imageCap, ref imageCap2, trasThreshold, ref CurrentImageRegions.FirstImageRegions);
       

            };

        }





        private async void mainTimerTick(object? sender, EventArgs args)
        {
 
          //  _timer.Stop();
      
            // If key is already processing skip this tick
            if (keyProcessingFirst || keyProcessingSecond)
            {
                return;
            }
            var keyToSendFirst = string.Empty;
            var keyToSendSecond = string.Empty;
            int vkCode = 0;
            DateTime currentD = DateTime.Now;

        repeatWithoutDelay:
            keyToSendFirst = string.Empty;
            keyToSendSecond = string.Empty;
            vkCode = 0;

            if (CurrentImageRegions.FirstImageRegions.BottomLeft == false && keyProcessingFirst == true)  // First Image is almost done processing
            {
                keyProcessingFirst = false;
                keyProcessingSecond = false;
                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
                return;
            }

            // lets just hang out here till we have a key
            currentD = DateTime.Now;

            keyToSendFirst = _currentKeyToSend[0];
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true)
            {
                await Task.Delay(1) ;
                keyToSendFirst = _currentKeyToSend[0];
                if ( currentD.AddMilliseconds(1000) < DateTime.Now)
                { keyProcessingFirst = false;
                 keyProcessingSecond = false ;
                    return;
                }
            }













            currentD = DateTime.Now;
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true && !VirtualKeyCodeMapper.HasExcludeKey(keyToSendFirst) && VirtualKeyCodeMapper.HasKey(keyToSendFirst))
            {
                await Task.Delay(1);
                keyToSendFirst = _currentKeyToSend[0];
                if (currentD.AddMilliseconds(1000) < DateTime.Now)
                {
                    keyProcessingFirst = false;
                    keyProcessingSecond = false;
                    return;
                }
            }

            if (keyToSendFirst == "")
            {
                keyProcessingFirst = false;
                keyProcessingSecond = false;
                return;
            }

            keyProcessingFirst = true;


       


            if (_wowWindowHandle != nint.Zero)
            {
      
                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Red;

                CurrentImageRegions.FirstImageRegions.TopLeft = false;
                CurrentImageRegions.FirstImageRegions.BottomLeft = false;
                CurrentImageRegions.FirstImageRegions.BottomCenter = false;
                // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                // It fails if the next key to press is the same.
                // There would have to some logic in the capture to say its a new detection

                // Tranlate the char to the virtual Key Code
                vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(keyToSendFirst);

                // command is tied to CTRL or ALT So have to press them
                if (keyToSendFirst[0] == 'C') //&& CtrlPressed == false
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_CONTROL, 0);
                else
                    // Command isn't tied to CTRL so send a CTRL Up.
                    // This should really be peeking in the message buffer to see if the the key is really pressed or not. and only send the up if it is. 
                    // This could also be accomlished buy storing off the value in the message processor and storing a flag local if it saw one or not.
                    // keyboards are global so that may work.
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0);  
                
                if (keyToSendFirst[0] == 'A') // && AltPressed == false
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_MENU, 0);
                else
                    // See Notes on CTRL.
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_MENU, 0);

                // Press the command Key Down
                WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, vkCode, 0);
             //   tbCommandLog.Text = tbCommandLog.Text + "I1-"+keyToSendFirst + "\r";


                // CTRL and ALT do not need to be held down just only pressed initally for the command to be interpeted correctly
                if (keyToSendFirst[0] == 'C' ) WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0); //&& CtrlPressed == true
                if (keyToSendFirst[0] == 'A' ) WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_MENU, 0); //&& AltPressed == true
                _currentKeyToSend[0] = "";
                await Task.Delay(500) ;  // we want atleast a 150ms delay when pressing and releasing the key. Wow cooldown can be no less that 500 accept for instant not GCD.  we will just have to suffer with those.





                if (_keyPressMode)
                {

                    currentD = DateTime.Now;
                    while (CurrentImageRegions.FirstImageRegions.TopLeft == false && button_Start.IsEnabled == false && activationKeyPressed == true)  // Do this loop till we have see we have a value starting to appear
                    {
           
                        await Task.Delay(1);
                        if (currentD.AddMilliseconds(3000) < DateTime.Now)  // Max of a 3 second channel  or wait

                        {
                            keyProcessingFirst = false;
                            keyProcessingSecond = false;
                            break;
                        }

                    }

                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, vkCode, 0);
                }
         
              
         



                // If where not watching for when things time out, we insert a hard delay
                if (!_keyPressMode)
                {
                    await Task.Delay(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS).ConfigureAwait(true);
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, vkCode, 0);
                }


                if (_currentKeyToSend[0] != "" && button_Start.IsEnabled == false && activationKeyPressed == true)
                {
                    goto repeatWithoutDelay;
                }



                // Let up on the command key
                keyProcessingFirst = false;
                keyProcessingSecond = false;
            }

            keyProcessingFirst = false;
            keyProcessingSecond = false;
            //   if (activationKeyPressed) _timer.Start();
            ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;

        }

        // Method to open the MagnifierWindow
        private void OpenMagnifierWindow()
        {
            magnifier.Show();
            magnifier2.Show();
        }


        public MainWindow()
        {
            InitializeComponent();
            mainWindowDispatcher = this.Dispatcher;


            magnifier = new MagnifierWindow();
            magnifier.Left = Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.CapX;
            magnifier.Top = Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.CapY;
            magnifier.Width = Settings.Default.CapWidth;
            magnifier.Height = Settings.Default.CapHeight;
            magnifier.ShowInTaskbar = false;
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;

            magnifier2 = new MagnifierWindow();
            magnifier2.border.BorderBrush = BorderBrush = System.Windows.Media.Brushes.Blue;
            magnifier2.Left = Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.Cap2X;
            magnifier2.Top = Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.Cap2Y;
            magnifier2.Width = Settings.Default.Cap2Width;
            magnifier2.Height = Settings.Default.Cap2Height;
            magnifier2.ShowInTaskbar = false;
            magnifier2.SizeChanged += Magnifier2_SizeChanged;
            magnifier2.LocationChanged += Magnifier2_LocationChanged;

            magnifier2.Visibility = Visibility.Hidden;


            ColorPicker.PortableColorPicker cp;
            cp = (ColorPicker.PortableColorPicker)cbColorDruid.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.DruidTargetA, (byte)Settings.Default.DruidTargetR, (byte)Settings.Default.DruidTargetG, (byte)Settings.Default.DruidTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorPaladin.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.PaladinTargetA, (byte)Settings.Default.PaladinTargetR, (byte)Settings.Default.PaladinTargetG, (byte)Settings.Default.PaladinTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorWarlock.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.WarlockTargetA, (byte)Settings.Default.WarlockTargetR, (byte)Settings.Default.WarlockTargetG, (byte)Settings.Default.WarlockTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorShaman.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.ShamanTargetA, (byte)Settings.Default.ShamanTargetR, (byte)Settings.Default.ShamanTargetG, (byte)Settings.Default.ShamanTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorRogue.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.RogueTargetA, (byte)Settings.Default.RogueTargetR, (byte)Settings.Default.RogueTargetG, (byte)Settings.Default.RogueTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorWarrior.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.WarriorTargetA, (byte)Settings.Default.WarriorTargetR, (byte)Settings.Default.WarriorTargetG, (byte)Settings.Default.WarriorTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorEvoker.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.EvokerTargetA, (byte)Settings.Default.EvokerTargetR, (byte)Settings.Default.EvokerTargetG, (byte)Settings.Default.EvokerTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorHunter.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.HunterTargetA, (byte)Settings.Default.HunterTargetR, (byte)Settings.Default.HunterTargetG, (byte)Settings.Default.HunterTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorMage.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.MageTargetA, (byte)Settings.Default.MageTargetR, (byte)Settings.Default.MageTargetG, (byte)Settings.Default.MageTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorPriest.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.PriestTargetA, (byte)Settings.Default.PriestTargetR, (byte)Settings.Default.PriestTargetG, (byte)Settings.Default.PriestTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorMonk.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.MonkTargetA, (byte)Settings.Default.MonkTargetR, (byte)Settings.Default.MonkTargetG, (byte)Settings.Default.MonkTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorDemonHunter.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.DemonHunterTargetA, (byte)Settings.Default.DemonHunterTargetR, (byte)Settings.Default.DemonHunterTargetG, (byte)Settings.Default.DemonHunterTargetB);
            cp = (ColorPicker.PortableColorPicker)cbColorDefault.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.TargetA, (byte)Settings.Default.TargetR, (byte)Settings.Default.TargetG, (byte)Settings.Default.TargetB);



            RadioButton cb =  GetSelectedCheckBox();
            cp = (ColorPicker.PortableColorPicker)cb.Content;
            CurrentR = cp.SelectedColor.R;
            CurrentG = cp.SelectedColor.G;
            CurrentB = cp.SelectedColor.B;
            CurrentA = cp.SelectedColor.A;

            RadioButton rb = GetSelectedCheckBox();


            if ((string)rb.Tag == "default")
            {
                tbVariance.Text = Settings.Default.VariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.VariancePercent;
            }
            if ((string)rb.Tag == "druid")
            {
                tbVariance.Text = Settings.Default.DruidVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.DruidVariancePercent;
            }
            if ((string)rb.Tag == "paladin")
            {
                tbVariance.Text = Settings.Default.PaladinVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.PaladinVariancePercent;
            }
            if ((string)rb.Tag == "warlock")
            {
                tbVariance.Text = Settings.Default.WarlockVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.WarlockVariancePercent;
            }
            if ((string)rb.Tag == "shaman")
            {
                tbVariance.Text = Settings.Default.ShamanVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.ShamanVariancePercent;
            }
            if ((string)rb.Tag == "rogue")
            {
                tbVariance.Text = Settings.Default.RogueVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.RogueVariancePercent;
            }
            if ((string)rb.Tag == "warrior")
            {
                tbVariance.Text = Settings.Default.WarriorVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.WarriorVariancePercent;
            }
            if ((string)rb.Tag == "evoker")
            {
                tbVariance.Text = Settings.Default.EvokerVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.EvokerVariancePercent;
            }
            if ((string)rb.Tag == "hunter")
            {
                tbVariance.Text = Settings.Default.HunterVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.HunterVariancePercent;
            }
            if ((string)rb.Tag == "mage")
            {
                tbVariance.Text = Settings.Default.MageVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.MageVariancePercent;
            }
            if ((string)rb.Tag == "priest")
            {
                tbVariance.Text = Settings.Default.PriestVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.PriestVariancePercent;
            }
            if ((string)rb.Tag == "monk")
            {
                tbVariance.Text = Settings.Default.MonkVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.MonkVariancePercent;
            }
            if ((string)rb.Tag == "demonhunter")
            {
                tbVariance.Text = Settings.Default.DemonHunterVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.DemonHunterVariancePercent;
            }



            tbCaptureRateMS.Text = Settings.Default.CaptureRateMS.ToString();
            sliderCaptureRateMS.Value = Settings.Default.CaptureRateMS;

            tbKeyRateMS.Text = Settings.Default.KeyPressSpeedMS.ToString();
            sliderKeyRateMS.Value = Settings.Default.KeyPressSpeedMS;

            cbPushRelease.IsChecked = Settings.Default.PushAndRelease;
            cbQuickDecode.IsChecked = Settings.Default.QuickDecode;
            cbStayOnTop.IsChecked = Settings.Default.KeepOnTop;

            // I figured out a way of knowing the next command before the cooldown.  no longer need the 2nd image.
            cbUse2ndImage.IsChecked = false;// Settings.Default.Use2ndImageDetection; 
            
            ImageCap2Border.Visibility = cbUse2ndImage.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            lDetectedValue2.Visibility = ImageCap2Border.Visibility; // no need to reeval the vars, we already know.  (yeah this can be done in xaml bindings..  but right now I don't know how and don't feel like looking it up.)
            ImageCap2Border.Visibility = Visibility.Visible;

            this.Topmost = Settings.Default.KeepOnTop;


            foreach (var x in cbActivationKey.Items)
            {

               if ( ((ComboBoxItem)x).Content.ToString() == Settings.Default.ActivationKey)
                    {
                    cbActivationKey.SelectedItem = x;
                }
            }

            OpenMagnifierWindow();

            this.Left = Settings.Default.AppStartX;
            this.Top = Settings.Default.AppStartY;

          
            
            _proc = HookCallbackActionKey;

            _mouseProc = MouseHookCallback;


            _wowWindowHandle = WindowsAPICalls.FindWindow(null, "World of Warcraft");

 
            StartCaptureProcess();


            // This timer handles the key sending
          
            _timer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromMilliseconds(25);
            _timer.Tick += mainTimerTick;

        }

  
        #region UI Event handlers
        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (cbStayOnTop.IsChecked == true)
            {
                this.Topmost = true;
                Settings.Default.KeepOnTop = true;
            }
            else
            {
                this.Topmost = false;
                Settings.Default.KeepOnTop = false;

            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Start the continuous capturing
            _wowWindowHandle = WindowsAPICalls.FindWindow(null, "World of Warcraft");
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
                    WindowsAPICalls.UnhookWindowsHookEx(_hookID);
                    _hookID = 0;
                }
                button_Start.IsEnabled = true;
                button_Stop.IsEnabled = false;
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

            //if (magnifier2.Visibility == Visibility.Visible)
            //{
            //    magnifier2.Visibility = Visibility.Hidden;
            //}
            //else
            //{
            //    magnifier2.Visibility = Visibility.Visible;
            //}



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
                var scaledHeight = height * dpiY - 15;
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
                var scaledHeight = height * dpiY -15;
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



            Settings.Default.CapX = magnifier.Left;
            Settings.Default.CapY = magnifier.Top;
            Settings.Default.CapWidth = magnifier.Width;
            Settings.Default.CapHeight = magnifier.Height;
            Settings.Default.Cap2X = magnifier2.Left;
            Settings.Default.Cap2Y = magnifier2.Top;
            Settings.Default.Cap2Width = magnifier2.Width;
            Settings.Default.Cap2Height = magnifier2.Height;
            Settings.Default.AppStartX = this.Left;
            Settings.Default.AppStartY = this.Top;


            Settings.Default.Save();

            magnifier.Close();
            magnifier2.Close();

            if (screenCapture.IsCapturing)
            {
                screenCapture.StopCapture();
            }
            if (_hookID != 0) {
                // Make sure we stop trapping the keyboard
                WindowsAPICalls.UnhookWindowsHookEx(_hookID);
            _hookID = 0;
            }
            
  

            if (_MouseHookID != IntPtr.Zero)
            {

                // Make sure we stop trapping the mouse if its active
                WindowsAPICalls.UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;
            }

        }


 

        private RadioButton GetSelectedCheckBox ()
        {
            // Im tired so I'm just bruteforcing all of this.  Its not flexable and I know I will regert it later when a new one is added.
            if (cbColorDruid.IsChecked == true)
            {
                return cbColorDruid;
            }
            if (cbColorPaladin.IsChecked == true)
            {
                return cbColorPaladin;
            }
            if (cbColorWarlock.IsChecked == true)
            {
                return cbColorWarlock;
            }
            if (cbColorShaman.IsChecked == true)
            {
                return cbColorShaman;
            }
            if (cbColorRogue.IsChecked == true)
            {
                return cbColorRogue;
            }
            if (cbColorWarrior.IsChecked == true)
            {
                return cbColorWarrior;
            }
            if (cbColorEvoker.IsChecked == true)
            {
                return cbColorEvoker;
            }
            if (cbColorHunter.IsChecked == true)
            {
                return cbColorHunter;
            }
            if (cbColorMage.IsChecked == true)
            {
                return cbColorMage;
            }
            if (cbColorPriest.IsChecked == true)
            {
                return cbColorPriest;
            }
            if (cbColorMonk.IsChecked == true)
            {
                return cbColorMonk;
            }
            if (cbColorDemonHunter.IsChecked == true)
            {
                return cbColorDemonHunter;
            }
            cbColorDefault.IsChecked = true;
            cbColorDefault.Tag = "default";
            return cbColorDefault;
        }

        private void SetAssociatedSetting (RadioButton SelectedCheckbox, byte R, byte G, byte B, byte A)
        {
            RadioButton item = SelectedCheckbox;
            ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)item.Content;
            //cp.SelectedColor = System.Windows.Media.Color.FromArgb(R, G, B, A);
            cp.SelectedColor = System.Windows.Media.Color.FromArgb(A, R, G, B);



            if ((string)item.Tag == "default")
            {
                Settings.Default.TargetR = R;
                Settings.Default.TargetG = G;
                Settings.Default.TargetB = B;
                Settings.Default.TargetA = A;
            }
            if ((string)item.Tag == "druid")
            {
                Settings.Default.DruidTargetR = R;
                Settings.Default.DruidTargetG = G;
                Settings.Default.DruidTargetB = B;
                Settings.Default.DruidTargetA = A;
            }
            if ((string)item.Tag == "paladin") {
                Settings.Default.PaladinTargetR = R;
                Settings.Default.PaladinTargetG = G;
                Settings.Default.PaladinTargetB = B;
                Settings.Default.PaladinTargetA = A;
            }
            if ((string)item.Tag == "warlock")
            {
                Settings.Default.WarlockTargetR = R;
                Settings.Default.WarlockTargetG = G;
                Settings.Default.WarlockTargetB = B;
                Settings.Default.WarlockTargetA = A;
            }
            if ((string)item.Tag == "shaman")
            {
                Settings.Default.ShamanTargetR = R;
                Settings.Default.ShamanTargetG = G;
                Settings.Default.ShamanTargetB = B;
                Settings.Default.ShamanTargetA = A;
            }
            if ((string)item.Tag == "rogue")
            {
                Settings.Default.RogueTargetR = R;
                Settings.Default.RogueTargetG = G;
                Settings.Default.RogueTargetB = B;
                Settings.Default.RogueTargetA = A;
            }
            if ((string)item.Tag == "warrior")
            {
                Settings.Default.WarriorTargetR = R;
                Settings.Default.WarriorTargetG = G;
                Settings.Default.WarriorTargetB = B;
                Settings.Default.WarriorTargetA = A;
            }
            if ((string)item.Tag == "evoker")
            {
                Settings.Default.EvokerTargetR = R;
                Settings.Default.EvokerTargetG = G;
                Settings.Default.EvokerTargetB = B;
                Settings.Default.EvokerTargetA = A;
            }
            if ((string)item.Tag == "hunter")
            {
                Settings.Default.HunterTargetR = R;
                Settings.Default.HunterTargetG = G;
                Settings.Default.HunterTargetB = B;
                Settings.Default.HunterTargetA = A;
            }
            if ((string)item.Tag == "mage")
            {
                Settings.Default.MageTargetR = R;
                Settings.Default.MageTargetG = G;
                Settings.Default.MageTargetB = B;
                Settings.Default.MageTargetA = A;
            }
            if ((string)item.Tag == "priest")
            {
                Settings.Default.PriestTargetR = R;
                Settings.Default.PriestTargetG = G;
                Settings.Default.PriestTargetB = B;
                Settings.Default.PriestTargetA = A;
            }
            if ((string)item.Tag == "monk")
            {
                Settings.Default.MonkTargetR = R;
                Settings.Default.MonkTargetG = G;
                Settings.Default.MonkTargetB = B;
                Settings.Default.MonkTargetA = A;
            }
            if ((string)item.Tag == "demonhunter")
            {
                Settings.Default.DemonHunterTargetR = R;
                Settings.Default.DemonHunterTargetG = G;
                Settings.Default.DemonHunterTargetB = B;
                Settings.Default.DemonHunterTargetA = A;

            }
            CurrentR = R;
            CurrentG = G;
            CurrentB = B;

        




        }

        private void buPicker_Click(object sender, RoutedEventArgs e)
        {
            _MouseHookID = MouseSetHook(_mouseProc);
            RadioButton item = GetSelectedCheckBox();
            ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)item.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);
        }



        public static void ChangeCursor()
        {
            // Load the custom cursor
            IntPtr customCursor = WindowsAPICalls.LoadCursor(IntPtr.Zero, WindowsAPICalls.IDC_HAND);

            // Set the system cursor to the custom cursor
           // SetSystemCursor(customCursor, OCR_NORMAL);
        }

        public static void RestoreCursor()
        {
            // Load the default arrow cursor
            IntPtr defaultCursor = WindowsAPICalls.LoadCursor(IntPtr.Zero, 32512); // 32512 is the ID for the standard arrow

            // Restore the system cursor to the default
            WindowsAPICalls.SetSystemCursor(defaultCursor, WindowsAPICalls.OCR_NORMAL);
        }

        private static IntPtr MouseSetHook(WindowsAPICalls.WindowsMessageProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return WindowsAPICalls.SetWindowsHookEx(WindowsAPICalls.WH_MOUSE_LL, proc, WindowsAPICalls.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private  IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WindowsAPICalls.WM_LBUTTONDOWN)
            {
                WindowsAPICalls.MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<WindowsAPICalls.MSLLHOOKSTRUCT>(lParam);
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

                    RadioButton item = GetSelectedCheckBox();
                    ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)item.Content;
                


                    // Convert System.Drawing.Color to System.Windows.Media.Color
                    SetAssociatedSetting(item, pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A);



                }





                WindowsAPICalls.UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;

            }
            return WindowsAPICalls.CallNextHookEx(_MouseHookID, nCode, wParam, lParam);
        }

        private void sliderColorVariance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            CurrentThreshold = (int)sliderColorVariancePercent.Value;
            if (tbVariance != null)
                tbVariance.Text = ((int)sliderColorVariancePercent.Value).ToString();

            RadioButton rb = GetSelectedCheckBox();


            if ((string)rb.Tag == "default")
            {
                Settings.Default.VariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "druid")
            {
                Settings.Default.DruidVariancePercent = (int)sliderColorVariancePercent.Value;

            }
            if ((string)rb.Tag == "paladin")
            {
                Settings.Default.PaladinVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "warlock")
            {
                Settings.Default.WarlockVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "shaman")
            {
                Settings.Default.ShamanVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "rogue")
            {
                Settings.Default.RogueVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "warrior")
            {
                Settings.Default.WarriorVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "evoker")
            {
                Settings.Default.EvokerVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "hunter")
            {
                Settings.Default.HunterVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "mage")
            {
                Settings.Default.MageVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "priest")
            {
                Settings.Default.PriestVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "monk")
            {
                Settings.Default.MonkVariancePercent = (int)sliderColorVariancePercent.Value;
            }
            if ((string)rb.Tag == "demonhunter")
            {
                Settings.Default.DemonHunterVariancePercent = (int)sliderColorVariancePercent.Value;
            }



        }

        private void sliderCaptureRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Settings.Default.CaptureRateMS = (int)sliderCaptureRateMS.Value;
            CurrentCaptureRateMS = (int)sliderCaptureRateMS.Value;
            if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
            if (screenCapture != null)
            screenCapture.CaptureInterval = (int)sliderCaptureRateMS.Value;
       
        }

        private void sliderKeyRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Settings.Default.KeyPressSpeedMS = (int)sliderKeyRateMS.Value;
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
            Settings.Default.ActivationKey = ((ComboBoxItem)cbActivationKey.SelectedItem).Content.ToString();
        }

        private void bResetMagPosition_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.CapX = 50;
            Settings.Default.CapY = 50;
            Settings.Default.CapWidth = 100;
            Settings.Default.CapHeight = 100 ;

            magnifier.Left = Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.CapX;
            magnifier.Top = Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.CapY;
            magnifier.Width = Settings.Default.CapWidth;
            magnifier.Height = Settings.Default.CapHeight;

            magnifier2.Left = Settings.Default.Cap2X > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.CapX;
            magnifier2.Top = Settings.Default.Cap2Y > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.CapY;
            magnifier2.Width = Settings.Default.Cap2Width;
            magnifier2.Height = Settings.Default.Cap2Height;

        }

        private void cbPushRelease_Checked(object sender, RoutedEventArgs e)
        {

            _keyPressMode = true;
            Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbPushRelease_Unchecked(object sender, RoutedEventArgs e)
        {
            _keyPressMode = false;
            Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbQuickDecode_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.QuickDecode = true;
        }

        private void cbQuickDecode_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.QuickDecode = false;
        }

        private void TargetColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
           // todo: make stil worl with the new multi color storage
            Settings.Default.TargetR = TargetColorPicker.SelectedColor.R;
            Settings.Default.TargetG = TargetColorPicker.SelectedColor.G; 
            Settings.Default.TargetB = TargetColorPicker.SelectedColor.B;
            Settings.Default.TargetA = TargetColorPicker.SelectedColor.A;
            CurrentR = TargetColorPicker.SelectedColor.R;
            CurrentG = TargetColorPicker.SelectedColor.G;
            CurrentB = TargetColorPicker.SelectedColor.B;
        }

        private void cbUse2ndImage_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.Use2ndImageDetection = true;
            ImageCap2Border.Visibility = Visibility.Visible;
            lDetectedValue2.Visibility = Visibility.Visible;

        }

        private void cbUse2ndImage_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.Use2ndImageDetection = false;
            ImageCap2Border.Visibility = Visibility.Collapsed;
            lDetectedValue2.Visibility = Visibility.Collapsed;

        }

        private void cbColorDruid_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton cb = (RadioButton)sender;
            if (cb.IsChecked is null) return;
            if (cb.Tag is null) return;

            ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)cb.Content;
            CurrentA = cp.SelectedColor.A;
            CurrentR = cp.SelectedColor.R;
            CurrentG = cp.SelectedColor.G;
            CurrentB = cp.SelectedColor.B;

            if ((string)cb.Tag == "default")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.VariancePercent;
            }
            if ((string)cb.Tag == "druid")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.DruidVariancePercent;

            }
            if ((string)cb.Tag == "paladin")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.PaladinVariancePercent;
            }
            if ((string)cb.Tag == "warlock")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.WarlockVariancePercent;
            }
            if ((string)cb.Tag == "shaman")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.ShamanVariancePercent;
            }
            if ((string)cb.Tag == "rogue")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.RogueVariancePercent;
            }
            if ((string)cb.Tag == "warrior")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.WarriorVariancePercent;
            }
            if ((string)cb.Tag == "evoker")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.EvokerVariancePercent;
            }
            if ((string)cb.Tag == "hunter")
            {
                sliderColorVariancePercent.Value= (int)Settings.Default.HunterVariancePercent;
            }
            if ((string)cb.Tag == "mage")
            {
                sliderColorVariancePercent.Value= (int)Settings.Default.MageVariancePercent;
            }
            if ((string)cb.Tag == "priest")
            {
                sliderColorVariancePercent.Value= (int)Settings.Default.PriestVariancePercent;
            }
            if ((string)cb.Tag == "monk")
            {
                sliderColorVariancePercent.Value= (int)Settings.Default.MonkVariancePercent;
            }
            if ((string)cb.Tag == "demonhunter")
            {
                sliderColorVariancePercent.Value= (int)Settings.Default.DemonHunterVariancePercent;
            }




        }
        #endregion

    }
}

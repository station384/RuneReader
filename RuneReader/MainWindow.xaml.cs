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



namespace RuneReader
{

    public class KeyCommand
    {
        public bool Alt { get; private set; } = false;
        public bool Ctrl { get; private set; } = false;
        public bool Shift { get; private set; } = false;
        public string Key { get; private set; } = string.Empty;
        public int MaxWaitTime { get;  set; } = 0;
        public bool HasTarget { get; set; } = false;

        public KeyCommand(string key, int maxWaitTime, bool hasTarget)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (key[0] == 'C') { Ctrl = true; }
                if (key[0] == 'A') { Alt = true; }
                if (key[0] == 'S') { Shift = true; }
                MaxWaitTime = maxWaitTime;
                HasTarget = hasTarget;
                Key = key;
            }
        }
    }

    public partial class MainWindow : MetroWindow
    {


        private  Stack<KeyCommand> KeyCommandStack = new Stack<KeyCommand> ();

        private  string[] _currentKeyToSend = new string[] { string.Empty, string.Empty }; // Default key to send, can be changed dynamically

        private volatile bool keyProcessingFirst = false;
        private  bool activationKeyPressed = false;


        private volatile int[] _DetectedSameCount = new int[2] { 0, 0 };

        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _MouseHookID = IntPtr.Zero;
        private WindowsAPICalls.WindowsMessageProc _proc;
        private WindowsAPICalls.WindowsMessageProc _mouseProc;
        private IntPtr _wowWindowHandle = IntPtr.Zero;
        private CaptureScreen captureScreen;
        private ContinuousScreenCapture screenCapture;


        private OcrModule ocr = new OcrModule();
        private bool BarCodeFound = false;

        
  

        private MagnifierWindow magnifier;
        private MagnifierWindow magnifier2;
        private volatile ImageRegions CurrentImageRegions = new ImageRegions();
        private DispatcherTimer _timer;
        private DispatcherTimer _TimerWowWindowMonitor; // This timer is here just incase the game closes and is reopened, this will catch the new window ID.



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

        private DateTime keypressStart = DateTime.MinValue;
        private DateTime keypressEnd = DateTime.MinValue;
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
                    activationKeyPressed = false;

                    handled = false;
                }
                else
                {
                    var item = ActivationKeyCodeMapper.GetVirtualKeyCode(Settings.Default.ActivationKey);
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

                                // we want the timer to react NOW.   
                                // mainTimerTick(this, new EventArgs());
                                keypressStart = DateTime.Now;
                                keypressEnd = DateTime.MinValue; 



                                // Don't let the message go thru.  this blocks the game from seeing the key press
                                handled = true;
                            }


                        }
                    }
                    if (wParam == (IntPtr)WindowsAPICalls.WM_KEYUP && (int)key == item)
                    {
                        activationKeyPressed = false;
                        keyProcessingFirst = false;
                        _timer.Stop();
                        keypressEnd = DateTime.Now;

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
            //if (_currentKeyToSend[0] != "" )
            //    return handled ? (IntPtr)1: WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Blocks input to game does not block windows
            //else
            //                return WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
            // return handled ? (IntPtr)0:CallNextHookEx(_hookID, nCode, wParam, lParam); // Locks explorer

            if (KeyCommandStack.Count > 0)
            {
                return handled ? (IntPtr)1 : WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Blocks input to game does not block windows
            }
            if (keypressEnd != DateTime.MinValue && keypressEnd.Subtract(keypressStart).Milliseconds < 100)
            {
                return handled ? (IntPtr)1 : WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Blocks input to game does not block windows
            }
            else
            {
                return WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
            }
        }

        private string OCRProcess(Bitmap b, System.Windows.Rect region)
        {
            string Result = String.Empty;
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




        /// <summary>
        /// Used to find the delays and text in the image 
        /// </summary>
        /// <param name="image">Bitmap we are going to process</param>
        /// <param name="label">Label control we will update the text of</param>
        /// <param name="_DetectedValue">Updates to value detected</param>
        /// <param name="_DetectedSameCount">Times the same value was detected</param>
        /// <param name="DisplayControl">Image used for OCR refence to USER no delays</param>
        /// <param name="DisplayControlDelays">image used for showing the delays</param>
        /// <param name="Threshold">0.0 -> 1.0 How much variance of color are we going to call the same</param>
        /// <param name="regions">Updates this refrence with the current detected reguions </param>
        /// <returns>always returns -1 for now.</returns>
        private  int ProcessImageOpenCV(Bitmap image, ref System.Windows.Controls.Label label, ref string _DetectedValue, ref int _DetectedSameCount,  ref System.Windows.Controls.Image DisplayControl, ref System.Windows.Controls.Image DisplayControlDelays, double Threshold, ref DetectionRegions regions)
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


            int result = -1;
            BitmapSource? OutImageSource;
            var CVMat = BitmapSourceConverter.ToMat(ImageHelpers.Convert(image));
            Mat resizedMat;


            resizedMat = ImageProcessingOpenCV.RescaleImageToNewDpi(CVMat, image.HorizontalResolution, 300);
          
            double gammaAdjust;
            double contrastAdjust;
            double brightnessAdjust;
            double wowGammaSetting = WowGamma;
            if (cbColorCustom.IsChecked.Value == true)
            {
                gammaAdjust = 1.0;
                contrastAdjust = 0.0;
                brightnessAdjust = 0.0;
            }
            else
            { 
                gammaAdjust = ( 1-(WowGamma - 1)  );
            }






            ImageProcessingOpenCV.gammaCorrection(resizedMat, resizedMat, gammaAdjust);
          //  Cv2.ImShow("test", resizedMat);
            using var IsolatedColorWithDelays = ImageProcessingOpenCV.IsolateColorHSV(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Threshold);
            using var IsolatedColorWithoutDelays = ImageProcessingOpenCV.IsolateColorHSV(resizedMat, Scalar.FromRgb(CurrentR, CurrentG, CurrentB), Threshold +1  );


            Mat grayWithDelays = IsolatedColorWithDelays.Clone();
            Mat grayWithoutDelays = IsolatedColorWithoutDelays.Clone();
            //  Cv2.ImShow("WithDelays", grayWithDelays);
            //  Cv2.ImShow("WithoutDelays", grayWithoutDelays);


            //// Apply Otsu's thresholding
            ////Cv2.Threshold(grayWithDelays, grayWithDelays, 250, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //
            ////Cv2.Threshold(grayWithoutDelays, grayWithoutDelays, 250, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv); //

            //      Cv2.Threshold(grayWithDelays, grayWithDelays, 0, 255,  ThresholdTypes.BinaryInv); //
            //      Cv2.Threshold(grayWithoutDelays, grayWithoutDelays, 0, 255, ThresholdTypes.BinaryInv); //

            // grab a copy of the image with delays, and resize it to 96 dpi (standard size), this will be used to display to the user
            resizedMat = grayWithDelays.Clone();
            resizedMat = ImageProcessingOpenCV.RescaleImageToNewDpi(resizedMat, image.HorizontalResolution, 96);
            if (Settings.Default.UseOCR)
            {


                // Find the current bounding boxes, and try and get rid of the useless ones
                System.Windows.Rect[] ocrRegionsWithDelays = ocr.GetRegions(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithDelays));
                System.Windows.Rect[] ocrRegionsWithoutDelays = ocr.GetRegions(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithoutDelays));

                // If no bounding boxes were found create one that encompasess the entire image
                if (ocrRegionsWithDelays == null)
                    ocrRegionsWithDelays = new System.Windows.Rect[] { new System.Windows.Rect(0, 0, grayWithDelays.Width, grayWithDelays.Height) };
                if (ocrRegionsWithoutDelays == null)
                    ocrRegionsWithoutDelays = new System.Windows.Rect[] { new System.Windows.Rect(0, 0, grayWithoutDelays.Width, grayWithoutDelays.Height) };

                // Used to keep a list of the rational bounding boxes
                List<System.Windows.Rect> usefulRegionsWithDelays = new List<System.Windows.Rect>();
                List<System.Windows.Rect> usefulRegionsWithoutDelays = new List<System.Windows.Rect>();

                //Sort thru a max of 50 and find all the ones that are a reasonable size and add those to the list with Delays
                if (ocrRegionsWithDelays.Length >= 1)
                {
                    for (int i = 0; i < ocrRegionsWithDelays.Length && i <= 50; i++)
                    {
                        if (ocrRegionsWithDelays[i].Height * ocrRegionsWithDelays[i].Width < 1500)
                        {
                            ImageProcessingOpenCV.FillRectangle(ref grayWithDelays, new OpenCvSharp.Rect((int)ocrRegionsWithDelays[i].X, (int)ocrRegionsWithDelays[i].Y, (int)ocrRegionsWithDelays[i].Width, (int)ocrRegionsWithDelays[i].Width), Scalar.FromRgb(255, 255, 255));
                        }
                        else
                        {
                            usefulRegionsWithDelays.Add(ocrRegionsWithDelays[i]);
                        }
                    }

                }

                if (usefulRegionsWithDelays.Count == 0)
                {
                    usefulRegionsWithDelays.Add(new System.Windows.Rect(0, 0, grayWithDelays.Width, grayWithDelays.Height));
                }



                // Find the total region size of all the regions that were detected for the image used with delays
                xMin = usefulRegionsWithDelays.Min(s => s.X);
                yMin = usefulRegionsWithDelays.Min(s => s.Y);
                xMax = usefulRegionsWithDelays.Max(s => s.X + s.Width);
                yMax = usefulRegionsWithDelays.Max(s => s.Y + s.Height);

                var int32Rect = new Int32Rect((int)xMin, (int)yMin, (int)xMax - (int)xMin, (int)yMax - (int)yMin);
                System.Windows.Rect finalRegionWithDelays = new System.Windows.Rect(int32Rect.X, int32Rect.Y, int32Rect.Width, int32Rect.Height);



                //Sort thru a max of 50 and find all the ones that are a reasonable size and add those to the list (Without delays in the image)
                if (ocrRegionsWithoutDelays.Length > 1)
                {
                    for (int i = 0; i < ocrRegionsWithoutDelays.Length && i <= 50; i++)
                    {
                        if (ocrRegionsWithoutDelays[i].Height * ocrRegionsWithoutDelays[i].Width < 1500)
                        {
                            ImageProcessingOpenCV.FillRectangle(ref grayWithoutDelays, new OpenCvSharp.Rect((int)ocrRegionsWithoutDelays[i].X, (int)ocrRegionsWithoutDelays[i].Y, (int)ocrRegionsWithoutDelays[i].Width, (int)ocrRegionsWithoutDelays[i].Width), Scalar.FromRgb(255, 255, 255));
                        }
                        else
                        {
                            usefulRegionsWithoutDelays.Add(ocrRegionsWithoutDelays[i]);
                        }
                    }
                }

                if (usefulRegionsWithoutDelays.Count == 0)
                {
                    usefulRegionsWithoutDelays.Add(new System.Windows.Rect(0, 0, grayWithoutDelays.Width, grayWithoutDelays.Height));
                }
                // Find the total region size of all the regions that were detected for the image with out delays
                xMin = usefulRegionsWithoutDelays.Min(s => s.X);
                yMin = usefulRegionsWithoutDelays.Min(s => s.Y);
                xMax = usefulRegionsWithoutDelays.Max(s => s.X + s.Width);
                yMax = usefulRegionsWithoutDelays.Max(s => s.Y + s.Height);
                int32Rect = new Int32Rect((int)xMin, (int)yMin, (int)xMax - (int)xMin, (int)yMax - (int)yMin);
                System.Windows.Rect finalRegionWithoutDelays = new System.Windows.Rect(int32Rect.X, int32Rect.Y, int32Rect.Width, int32Rect.Height);





                // This is where we detect if there are pixels in a certain region of the image.   This is used to detect command delays
                regions.TopLeft = ImageProcessingOpenCV.IsThereAnImageInTopLeftQuarter(grayWithDelays);
                regions.TopRight = ImageProcessingOpenCV.IsThereAnImageInTopRightQuarter(grayWithDelays);
                regions.BottomLeft = ImageProcessingOpenCV.IsThereAnImageInBottomLeftQuarter(grayWithDelays);
                regions.BottomCenter = ImageProcessingOpenCV.IsThereAnImageInBottomCenter(grayWithDelays);



                // The heart of the detection  OCR
                string s = OCRProcess(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(grayWithoutDelays), finalRegionWithoutDelays);

                // Unnecessary assignment to a new var.  but have it split for possible logic if nothing is detected, and debuging.
                CurrentKeyToSend = s;
            }  // End of OCR Processing


            if (Settings.Default.UseBarCode)
            {

                var barcodeResult = BarcodeDecode.DecodeBarcode(CVMat);
                if (barcodeResult.BarcodeFound)
                {
                    regions.TopRight = (barcodeResult.WaitTime < 1000);
                    regions.BottomCenter = (barcodeResult.WaitTime <= 500);
                    regions.BottomLeft = (barcodeResult.WaitTime <= 300);
                    regions.TopLeft = (barcodeResult.WaitTime <= 0);
                    regions.WaitTime = barcodeResult.WaitTime;
                    regions.HasTarget = barcodeResult.HasTarget;
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
            } 

            // Convert the Image for binay to RGB so we can draw some colored markers on the image
//                Cv2.CvtColor(CVMat, resizedMat, ColorConversionCodes.BayerBG2RGB);
            Cv2.CvtColor(resizedMat, resizedMat, ColorConversionCodes.BayerBG2RGB);

            // Draw the colored markers
            ImageProcessingOpenCV.DrawMarkers(ref resizedMat);

            // Push the new image out the the first image,  this has the markers and delays
            if (BarCodeFound)

                OutImageSource = BitmapSourceConverter.ToBitmapSource(CVMat);
         
            
            else
                OutImageSource = BitmapSourceConverter.ToBitmapSource(resizedMat);


            DisplayControl.Source = OutImageSource;

            // Push the image that doesn't have delays out to the second display.   This image is what is OCRed on.
            if (BarCodeFound)

                OutImageSource = BitmapSourceConverter.ToBitmapSource(CVMat);
            else
            OutImageSource = BitmapSourceConverter.ToBitmapSource(grayWithoutDelays);
            DisplayControlDelays.Source = OutImageSource;



            // Update the label
            label.Content = CurrentKeyToSend;
            
            // Update the detected value object that was passed in.
            _DetectedValue = CurrentKeyToSend;

            resizedMat.Dispose();
            grayWithDelays.Dispose();
            grayWithoutDelays.Dispose();


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

            // Create an instance of ContinuousScreenCapture with the CaptureScreen object
            screenCapture = new ContinuousScreenCapture(
                CurrentCaptureRateMS,
                Dispatcher,
                captureScreen
            );

            // Assign a handler to the UpdateUIImage event
            screenCapture.UpdateFirstImage +=  (Bitmap image) =>
            {
                double trasThreshold = CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
                 ProcessImageOpenCV(image, ref lDetectedValue, ref _currentKeyToSend[0], ref _DetectedSameCount[0], ref imageCap, ref imageCap2, trasThreshold, ref CurrentImageRegions.FirstImageRegions);
       

            };

        }





        private async void mainTimerTick(object? sender, EventArgs args)
        {
     
            //_timer.IsEnabled = false;
            if (activationKeyPressed == true)
            if (Settings.Default.UseBarCode )
            {
                
                    await ProcessBarCodeKey();  
              
            } 
            else
            {
                    keyProcessingFirst = false;
                    await ProcessOCRKey();
            }
            //if (activationKeyPressed == true)
            //    _timer.IsEnabled = true;
        }

        
        
        
        
        private bool ProcessingKey = false;
        private async Task ProcessKey()
        {
           
            if (KeyCommandStack.Count == 0 || ProcessingKey == true) return;
        
            KeyCommand currentKey = currentKey = KeyCommandStack.Pop();
            ProcessingKey = true;
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
 
                // I keep poking at this trying to figure out how to only send the key press again if a new key is to me pressed.
                // It fails if the next key to press is the same.
                // There would have to some logic in the capture to say its a new detection

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
        





                if (_keyPressMode )
                {

                    DateTime  currentMS = DateTime.Now;

                    currentMS = DateTime.Now.AddMilliseconds(500);

                    while (currentKey.MaxWaitTime == 0 && activationKeyPressed == true )
                    {
                        await Task.Delay(1);
                        currentKey.MaxWaitTime = CurrentImageRegions.FirstImageRegions.WaitTime;
                        if (DateTime.Now >= currentMS )
                        {
                            goto alldone;
                        }
                    }

                    currentMS = DateTime.Now.AddMilliseconds(currentKey.MaxWaitTime);
                    DateTime MaxWaitTime = DateTime.Now.AddSeconds(8);

                    while ((currentMS >= DateTime.Now && currentKey.MaxWaitTime != 0) && activationKeyPressed == true )
                    {

                        await Task.Delay(1);
                        currentKey.MaxWaitTime = CurrentImageRegions.FirstImageRegions.WaitTime;
                        if (currentKey.MaxWaitTime <= 100)
                        {
                            //This is so we can exit early and start the next key.   BUT we still need to finish this one.  

                            goto alldone;
                        }
                        //else
                        //{
                        //if (KeyCommandStack.Count == 0)
                        //{
                        //    await Task.Delay(currentKey.MaxWaitTime);
                        //}
                        //else
                        //{
                        //    if (KeyCommandStack.Peek().Key == currentKey.Key)
                        //    {
                        //        await Task.Delay(50);
                        //    }
                        //}
                        //}
                        if (DateTime.Now > MaxWaitTime)
                        {
                            goto alldone;
                        }
                    }

               
                }






                // If where not watching for when things time out, we insert a hard delay
                if (!_keyPressMode)
                {
                    await Task.Delay(Random.Shared.Next() % 5 + CurrentKeyDownDelayMS);//.ConfigureAwait(true);
                }
            alldone:
               
                WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, vkCode, 0);
           
                await Task.Delay(1);
                ProcessingKey = false;

            }


            //   ProcessingKey = false;
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
            keyToSendFirst = _currentKeyToSend[0];
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true )
            {
                await Task.Delay(1);
                keyToSendFirst = _currentKeyToSend[0];
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
            //while (ProcessingKey)
            //{
            //    await Task.Delay(1);
            //}
            KeyCommandStack.Push(new KeyCommand(keyToSendFirst, CurrentImageRegions.FirstImageRegions.WaitTime, CurrentImageRegions.FirstImageRegions.HasTarget));
            //ProcessingKey = true;
            await ProcessKey();
         











        allDone:
     
           
            ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
        }


        private async Task ProcessOCRKey()
        {

            // If key is already processing skip this tick
            if (keyProcessingFirst)
            {
                return;
            }
            if (activationKeyPressed == false)
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

            if (CurrentImageRegions.FirstImageRegions.TopRight == false && keyProcessingFirst == true)  // First Image is almost done processing
            {
                keyProcessingFirst = false;
                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
                goto allDone;
                //return;
            }

            // lets just hang out here till we have a key
            currentD = DateTime.Now;

            keyToSendFirst = _currentKeyToSend[0];
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true)
            {
                await Task.Delay(1);
                keyToSendFirst = _currentKeyToSend[0];
                if (currentD.AddMilliseconds(1000) < DateTime.Now)
                {
                    keyProcessingFirst = false;
                    goto allDone;
                    //return;
                }
            }


            currentD = DateTime.Now;
            while (keyToSendFirst == "" && button_Start.IsEnabled == false && activationKeyPressed == true && (!(VirtualKeyCodeMapper.HasExcludeKey(keyToSendFirst) && BarCodeFound == false) && VirtualKeyCodeMapper.HasKey(keyToSendFirst)))
            {
                await Task.Delay(1);
                keyToSendFirst = _currentKeyToSend[0];
                if (currentD.AddMilliseconds(1000) < DateTime.Now)
                {
                    keyProcessingFirst = false;
                    //return;
                    goto allDone;
                }
            }

            if (keyToSendFirst == "")
            {
                keyProcessingFirst = false;
                // return;
                goto allDone;
            }

            currentD = DateTime.Now;
            while (CurrentImageRegions.FirstImageRegions.TopLeft == false && button_Start.IsEnabled == false && activationKeyPressed == true && (!(VirtualKeyCodeMapper.HasExcludeKey(keyToSendFirst) && BarCodeFound == false) && VirtualKeyCodeMapper.HasKey(keyToSendFirst)))
            {
                await Task.Delay(1);
                // keyToSendFirst = _currentKeyToSend[0];
                if (Settings.Default.PetKeyEnables == true)
                {
                    if (PetKeyVKCode >= 112) { WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_CONTROL, 0); }

                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, PetKeyVKCode, 0);
                    if (PetKeyVKCode >= 112) { WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0); }

                    await Task.Delay(50);
                    WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, PetKeyVKCode, 0);

                }

                if (currentD.AddMilliseconds(3000) < DateTime.Now)
                {
                    keyProcessingFirst = false;
                    //return;
                    goto allDone;
                }
            }


            keyProcessingFirst = true;





            if (_wowWindowHandle != nint.Zero)
            {

                ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Red;

                //CurrentImageRegions.FirstImageRegions.TopLeft = false;
                //CurrentImageRegions.FirstImageRegions.BottomLeft = false;
                //CurrentImageRegions.FirstImageRegions.BottomCenter = false;
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
                if (keyToSendFirst[0] == 'C') WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0); //&& CtrlPressed == true
                if (keyToSendFirst[0] == 'A') WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_MENU, 0); //&& AltPressed == true
                //_currentKeyToSend[0] = "";
                await Task.Delay(50);  // we want atleast a 150ms delay when pressing and releasing the key. Wow cooldown can be no less that 500 accept for instant not GCD.  we will just have to suffer with those.




                await Task.Delay(1);
                if (_keyPressMode)
                {

                    currentD = DateTime.Now;

                    await Task.Delay(50);
                    while (CurrentImageRegions.FirstImageRegions.BottomLeft == false && button_Start.IsEnabled == false && activationKeyPressed == true)  // Do this loop till we have see we have a value starting to appear
                    {

                        await Task.Delay(1);

                        if (Settings.Default.PetKeyEnables == true)
                        {
                            if (PetKeyVKCode >= 112)
                            {
                                WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, WindowsAPICalls.VK_CONTROL, 0);
                            }

                            WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYDOWN, PetKeyVKCode, 0);
                            if (PetKeyVKCode >= 112)
                            { WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, WindowsAPICalls.VK_CONTROL, 0); }

                            await Task.Delay(50);
                            WindowsAPICalls.PostMessage(_wowWindowHandle, WindowsAPICalls.WM_KEYUP, PetKeyVKCode, 0);

                        }

                        if (currentD.AddMilliseconds(7000) < DateTime.Now)  // Max of a 3 second channel  or wait
                        {
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
            }


        allDone:
            keyProcessingFirst = false;
            ImageCapBorder.BorderBrush = System.Windows.Media.Brushes.Black;
        }

        public MainWindow()
        {
            InitializeComponent();
            Initalizing = false;
            mainWindowDispatcher = this.Dispatcher;


            magnifier = new MagnifierWindow();
            magnifier.Left = Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.CapX;
            magnifier.Left = Settings.Default.CapX < 0 ? 0 : Settings.Default.CapX;
            magnifier.Top = Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.CapY;
            magnifier.Width = Settings.Default.CapWidth;
            magnifier.Height = Settings.Default.CapHeight;
            magnifier.ShowInTaskbar = false;
            magnifier.SizeChanged += Magnifier_SizeChanged;
            magnifier.LocationChanged += Magnifier_LocationChanged;

            magnifier2 = new MagnifierWindow();
            magnifier2.border.BorderBrush = BorderBrush = System.Windows.Media.Brushes.Blue;
            magnifier2.Left = Settings.Default.CapX > SystemParameters.PrimaryScreenWidth ? 100 : Settings.Default.Cap2X;
            magnifier2.Left = Settings.Default.CapX < 0 ? 0 : Settings.Default.Cap2X;

            magnifier2.Top = Settings.Default.CapY > SystemParameters.PrimaryScreenHeight ? 100 : Settings.Default.Cap2Y;
            magnifier2.Width = Settings.Default.Cap2Width;
            magnifier2.Height = Settings.Default.Cap2Height;
            magnifier2.ShowInTaskbar = false;
            magnifier2.SizeChanged += Magnifier2_SizeChanged;
            magnifier2.LocationChanged += Magnifier2_LocationChanged;

            magnifier2.Visibility = Visibility.Hidden;

            cbPetAttackKey.SelectedValue = cbPetAttackKey.Items[Settings.Default.PetKey]; 
            if (Settings.Default.PetKeyEnables == true)
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

            cbUseOCR.IsChecked = Settings.Default.UseOCR;
            cbUseBarcode.IsChecked = Settings.Default.UseBarCode;

            ColorPicker.PortableColorPicker cp;
            cp = (ColorPicker.PortableColorPicker)cbColorCustom.Content;
            cp.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.CustomTargetA, (byte)Settings.Default.CustomTargetR, (byte)Settings.Default.CustomTargetG, (byte)Settings.Default.CustomTargetB);

            

            RadioButton cb =  GetSelectedCheckBox();
            cp = (ColorPicker.PortableColorPicker)cb.Content;
            CurrentR = cp.SelectedColor.R;
            CurrentG = cp.SelectedColor.G;
            CurrentB = cp.SelectedColor.B;
            CurrentA = cp.SelectedColor.A;

            RadioButton rb = GetSelectedCheckBox();

            if ((string)rb.Tag == "custom")
            {
                tbVariance.Text = Settings.Default.CustomVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.CustomVariancePercent;

                

                tbHexColors.Text = string.Concat(Settings.Default.CustomTargetR.ToString("X2"), Settings.Default.CustomTargetG.ToString("X2"), Settings.Default.CustomTargetB.ToString("X2"));

            }


            if ((string)rb.Tag == "default")
            {
                tbVariance.Text = Settings.Default.VariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.VariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.TargetR.ToString("X2"), Settings.Default.TargetG.ToString("X2"), Settings.Default.TargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "druid")
            {
                tbVariance.Text = Settings.Default.DruidVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.DruidVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.DruidTargetR.ToString("X2"), Settings.Default.DruidTargetG.ToString("X2"), Settings.Default.DruidTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "paladin")
            {
                tbVariance.Text = Settings.Default.PaladinVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.PaladinVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.PaladinTargetR.ToString("X2"), Settings.Default.PaladinTargetG.ToString("X2"), Settings.Default.PaladinTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "warlock")
            {
                tbVariance.Text = Settings.Default.WarlockVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.WarlockVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.WarlockTargetR.ToString("X2"), Settings.Default.WarlockTargetG.ToString("X2"), Settings.Default.WarlockTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "shaman")
            {
                tbVariance.Text = Settings.Default.ShamanVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.ShamanVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.ShamanTargetR.ToString("X2"), Settings.Default.ShamanTargetG.ToString("X2"), Settings.Default.ShamanTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "rogue")
            {
                tbVariance.Text = Settings.Default.RogueVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.RogueVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.RogueTargetR.ToString("X2"), Settings.Default.RogueTargetG.ToString("X2"), Settings.Default.RogueTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "warrior")
            {
                tbVariance.Text = Settings.Default.WarriorVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.WarriorVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.WarriorTargetR.ToString("X2"), Settings.Default.WarriorTargetG.ToString("X2"), Settings.Default.WarriorTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "evoker")
            {
                tbVariance.Text = Settings.Default.EvokerVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.EvokerVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.EvokerTargetR.ToString("X2"), Settings.Default.EvokerTargetG.ToString("X2"), Settings.Default.EvokerTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "hunter")
            {
                tbVariance.Text = Settings.Default.HunterVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.HunterVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.HunterTargetR.ToString("X2"), Settings.Default.HunterTargetG.ToString("X2"), Settings.Default.HunterTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "mage")
            {

                tbVariance.Text = Settings.Default.MageVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.MageVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.MageTargetR.ToString("X2"), Settings.Default.MageTargetG.ToString("X2"), Settings.Default.MageTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "priest")
            {
                tbVariance.Text = Settings.Default.PriestVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.PriestVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.PriestTargetR.ToString("X2"), Settings.Default.PriestTargetG.ToString("X2"), Settings.Default.PriestTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "monk")
            {
                tbVariance.Text = Settings.Default.MonkVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.MonkVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.MonkTargetR.ToString("X2"), Settings.Default.MonkTargetG.ToString("X2"), Settings.Default.MonkTargetB.ToString("X2"));

            }
            if ((string)rb.Tag == "demonhunter")
            {
                tbVariance.Text = Settings.Default.DemonHunterVariancePercent.ToString();
                sliderColorVariancePercent.Value = Settings.Default.DemonHunterVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.DemonHunterTargetR.ToString("X2"), Settings.Default.DemonHunterTargetG.ToString("X2"), Settings.Default.DemonHunterTargetB.ToString("X2"));

            }

            sliderWowGamma.Value = Settings.Default.WowGamma;
            tbWowGamma.Text = Settings.Default.WowGamma.ToString("0.0");
          

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

            if (Settings.Default.IgnoreTargetingInfo == true)
            {
                cbIgnoreTargetInfo.IsChecked =  true;
            }
            else
            {
                cbIgnoreTargetInfo.IsChecked = false;

            }

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


            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft");

 
            StartCaptureProcess();

            magnifier.Visibility = Visibility.Hidden;

            _TimerWowWindowMonitor = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Background);
            _TimerWowWindowMonitor.Interval = TimeSpan.FromSeconds(5);
            _TimerWowWindowMonitor.Tick += _TimerWowWindowMonitor_Tick;
            _TimerWowWindowMonitor.Stop();


            // This timer handles the key sending


            //This timer needs to go away and be converted into a thread.
            _timer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Normal);
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Tick += mainTimerTick;

            _timer.IsEnabled = false ;

          
        }

        private void _TimerWowWindowMonitor_Tick(object? sender, EventArgs e)
        {
            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft");
        }


        #region UI Event handlers
        private void buClickKeepMagOnTop(object sender, RoutedEventArgs e)
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
            _wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft"); //WindowsAPICalls.FindWindow(null, "World of Warcraft");
            if (_wowWindowHandle != IntPtr.Zero)
            {
                if (!screenCapture.IsCapturing)
                {
                    Magnifier_LocationChanged(sender, e);
                    Magnifier2_LocationChanged(sender, e);
                    _currentKeyToSend[0] = "";
                    _currentKeyToSend[1] = "";
                    screenCapture.StartCapture();

                    _hookID = _hookID == IntPtr.Zero ? SetHookActionKey(_proc) : IntPtr.Zero; 
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = true;
                    _TimerWowWindowMonitor.Start();
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
            if (Initalizing) return;
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
            if (Initalizing) return;
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

        // Method to open the MagnifierWindow
        private void OpenMagnifierWindow()
        {
            magnifier.Show();
            magnifier2.Show();
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
            magnifier2.Close();


        }




        private RadioButton GetSelectedCheckBox ()
        {
            // Im tired so I'm just bruteforcing all of this.  Its not flexable and I know I will regert it later when a new one is added.

            if (cbColorCustom.IsChecked == true)
            {
                return cbColorCustom;
            }
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
            _wowWindowHandle = WindowsAPICalls.FindWindow(null, "World of Warcraft");
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


            if ((string)item.Tag == "custom")
            {
                Settings.Default.CustomTargetR = R;
                Settings.Default.CustomTargetG = G;
                Settings.Default.CustomTargetB = B;
                Settings.Default.CustomTargetA = A;
            }
            //if ((string)item.Tag == "default")
            //{
            //    Settings.Default.TargetR = R;
            //    Settings.Default.TargetG = G;
            //    Settings.Default.TargetB = B;
            //    Settings.Default.TargetA = A;
            //}
            //if ((string)item.Tag == "druid")
            //{
            //    Settings.Default.DruidTargetR = R;
            //    Settings.Default.DruidTargetG = G;
            //    Settings.Default.DruidTargetB = B;
            //    Settings.Default.DruidTargetA = A;
            //}
            //if ((string)item.Tag == "paladin") {
            //    Settings.Default.PaladinTargetR = R;
            //    Settings.Default.PaladinTargetG = G;
            //    Settings.Default.PaladinTargetB = B;
            //    Settings.Default.PaladinTargetA = A;
            //}
            //if ((string)item.Tag == "warlock")
            //{
            //    Settings.Default.WarlockTargetR = R;
            //    Settings.Default.WarlockTargetG = G;
            //    Settings.Default.WarlockTargetB = B;
            //    Settings.Default.WarlockTargetA = A;
            //}
            //if ((string)item.Tag == "shaman")
            //{
            //    Settings.Default.ShamanTargetR = R;
            //    Settings.Default.ShamanTargetG = G;
            //    Settings.Default.ShamanTargetB = B;
            //    Settings.Default.ShamanTargetA = A;
            //}
            //if ((string)item.Tag == "rogue")
            //{
            //    Settings.Default.RogueTargetR = R;
            //    Settings.Default.RogueTargetG = G;
            //    Settings.Default.RogueTargetB = B;
            //    Settings.Default.RogueTargetA = A;
            //}
            //if ((string)item.Tag == "warrior")
            //{
            //    Settings.Default.WarriorTargetR = R;
            //    Settings.Default.WarriorTargetG = G;
            //    Settings.Default.WarriorTargetB = B;
            //    Settings.Default.WarriorTargetA = A;
            //}
            //if ((string)item.Tag == "evoker")
            //{
            //    Settings.Default.EvokerTargetR = R;
            //    Settings.Default.EvokerTargetG = G;
            //    Settings.Default.EvokerTargetB = B;
            //    Settings.Default.EvokerTargetA = A;
            //}
            //if ((string)item.Tag == "hunter")
            //{
            //    Settings.Default.HunterTargetR = R;
            //    Settings.Default.HunterTargetG = G;
            //    Settings.Default.HunterTargetB = B;
            //    Settings.Default.HunterTargetA = A;
            //}
            //if ((string)item.Tag == "mage")
            //{
            //    Settings.Default.MageTargetR = R;
            //    Settings.Default.MageTargetG = G;
            //    Settings.Default.MageTargetB = B;
            //    Settings.Default.MageTargetA = A;
            //}
            //if ((string)item.Tag == "priest")
            //{
            //    Settings.Default.PriestTargetR = R;
            //    Settings.Default.PriestTargetG = G;
            //    Settings.Default.PriestTargetB = B;
            //    Settings.Default.PriestTargetA = A;
            //}
            //if ((string)item.Tag == "monk")
            //{
            //    Settings.Default.MonkTargetR = R;
            //    Settings.Default.MonkTargetG = G;
            //    Settings.Default.MonkTargetB = B;
            //    Settings.Default.MonkTargetA = A;
            //}
            //if ((string)item.Tag == "demonhunter")
            //{
            //    Settings.Default.DemonHunterTargetR = R;
            //    Settings.Default.DemonHunterTargetG = G;
            //    Settings.Default.DemonHunterTargetB = B;
            //    Settings.Default.DemonHunterTargetA = A;

            //}
            CurrentR = R;
            CurrentG = G;
            CurrentB = B;

        




        }

        private void buPicker_Click(object sender, RoutedEventArgs e)
        {
      
            RadioButton item = GetSelectedCheckBox();

            
            if ((string)item.Tag == "custom")
            {
                _MouseHookID = MouseSetHook(_mouseProc);
                ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)item.Content;
                cp.SelectedColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);
               

            } else
            {
                _MouseHookID = IntPtr.Zero;

            }
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
                    if ((string)item.Tag == "custom")
                    {
                        ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)item.Content;


  
                        // Convert System.Drawing.Color to System.Windows.Media.Color
                        SetAssociatedSetting(item, pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A);
                    }


                }




                if (_MouseHookID != IntPtr.Zero)
                { 
                WindowsAPICalls.UnhookWindowsHookEx(_MouseHookID);
                _MouseHookID = IntPtr.Zero;
                }
            }
            return WindowsAPICalls.CallNextHookEx(_MouseHookID, nCode, wParam, lParam);
        }

        private void sliderColorVariance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
            CurrentThreshold = (int)sliderColorVariancePercent.Value;
            if (tbVariance != null)
                tbVariance.Text = ((int)sliderColorVariancePercent.Value).ToString();

            RadioButton rb = GetSelectedCheckBox();


            if ((string)rb.Tag == "custom")
            {
                Settings.Default.CustomVariancePercent = (int)sliderColorVariancePercent.Value;
            }

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
            if (Initalizing) return;
            Settings.Default.CaptureRateMS = (int)sliderCaptureRateMS.Value;
            CurrentCaptureRateMS = (int)sliderCaptureRateMS.Value;
            if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
            if (screenCapture != null)
            screenCapture.CaptureInterval = (int)sliderCaptureRateMS.Value;
       
        }


        private void sliderWowGamma_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
            Settings.Default.WowGamma = (float)Math.Round(sliderWowGamma.Value,1);
            WowGamma = (float)Settings.Default.WowGamma;
            if (tbWowGamma != null)
                tbWowGamma.Text = ((float)Settings.Default.WowGamma).ToString("0.0");

        }

        private void sliderKeyRateMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Initalizing) return;
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

        private void tbVariance_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Initalizing) return;
            sliderColorVariancePercent.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
        }

        private void cbActivationKey_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Initalizing) return;
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
            if (Initalizing) return;
            _keyPressMode = true;
            Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbPushRelease_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            _keyPressMode = false;
            Settings.Default.PushAndRelease = _keyPressMode;

        }

        private void cbQuickDecode_Checked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            Settings.Default.QuickDecode = true;
        }

        private void cbQuickDecode_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            Settings.Default.QuickDecode = false;
        }

        private void TargetColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
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
            if (Initalizing) return;
            Settings.Default.Use2ndImageDetection = true;
            ImageCap2Border.Visibility = Visibility.Visible;
            lDetectedValue2.Visibility = Visibility.Visible;

        }

        private void cbUse2ndImage_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            Settings.Default.Use2ndImageDetection = false;
            ImageCap2Border.Visibility = Visibility.Collapsed;
            lDetectedValue2.Visibility = Visibility.Collapsed;

        }

        private void cbColorDruid_Checked(object sender, RoutedEventArgs e)
        {
            if (Initalizing) return;
            RadioButton cb = (RadioButton)sender;
            if (cb.IsChecked is null) return;
            if (cb.Tag is null) return;

            ColorPicker.PortableColorPicker cp = (ColorPicker.PortableColorPicker)cb.Content;
            CurrentA = cp.SelectedColor.A;
            CurrentR = cp.SelectedColor.R;
            CurrentG = cp.SelectedColor.G;
            CurrentB = cp.SelectedColor.B;

            if ((string)cb.Tag == "custom")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.CustomVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.CustomTargetR.ToString("X2"), Settings.Default.CustomTargetG.ToString("X2"), Settings.Default.CustomTargetB.ToString("X2"));
            }
            if ((string)cb.Tag == "default")
            {
                sliderColorVariancePercent.Value = (int)Settings.Default.VariancePercent;

                Settings.Default.TargetR = TargetColorPicker.SelectedColor.R;
                Settings.Default.TargetG = TargetColorPicker.SelectedColor.G;
                Settings.Default.TargetB = TargetColorPicker.SelectedColor.B;

                tbHexColors.Text = string.Concat(Settings.Default.TargetR.ToString("X2"), Settings.Default.TargetG.ToString("X2"), Settings.Default.TargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "druid")
            {
                Settings.Default.DruidTargetR = cpDruid.SelectedColor.R;
                Settings.Default.DruidTargetG = cpDruid.SelectedColor.G;
                Settings.Default.DruidTargetB = cpDruid.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.DruidVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.DruidTargetR.ToString("X2"), Settings.Default.DruidTargetG.ToString("X2"), Settings.Default.DruidTargetB.ToString("X2"));
            }
            if ((string)cb.Tag == "paladin")
            {
                Settings.Default.PaladinTargetR = cpColorPaladin.SelectedColor.R;
                Settings.Default.PaladinTargetG = cpColorPaladin.SelectedColor.G;
                Settings.Default.PaladinTargetB = cpColorPaladin.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.PaladinVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.PaladinTargetR.ToString("X2"), Settings.Default.PaladinTargetG.ToString("X2"), Settings.Default.PaladinTargetB.ToString("X2"));
            }
            if ((string)cb.Tag == "warlock")
            {
                Settings.Default.WarlockTargetR = cpWarlock.SelectedColor.R;
                Settings.Default.WarlockTargetG = cpWarlock.SelectedColor.G;
                Settings.Default.WarlockTargetB = cpWarlock.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.WarlockVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.WarlockTargetR.ToString("X2"), Settings.Default.WarlockTargetG.ToString("X2"), Settings.Default.WarlockTargetB.ToString("X2"));
            }
            if ((string)cb.Tag == "shaman")
            {

                Settings.Default.ShamanTargetR = cpShamam.SelectedColor.R;
                Settings.Default.ShamanTargetG = cpShamam.SelectedColor.G;
                Settings.Default.ShamanTargetB = cpShamam.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.ShamanVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.ShamanTargetR.ToString("X2"), Settings.Default.ShamanTargetG.ToString("X2"), Settings.Default.ShamanTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "rogue")
            {
                Settings.Default.RogueTargetR = cpRogue.SelectedColor.R;
                Settings.Default.RogueTargetG = cpRogue.SelectedColor.G;
                Settings.Default.RogueTargetB = cpRogue.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.RogueVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.RogueTargetR.ToString("X2"), Settings.Default.RogueTargetG.ToString("X2"), Settings.Default.RogueTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "warrior")
            {
                Settings.Default.WarriorTargetR = cpWarrior.SelectedColor.R;
                Settings.Default.WarriorTargetG = cpWarrior.SelectedColor.G;
                Settings.Default.WarriorTargetB = cpWarrior.SelectedColor.B;

                sliderColorVariancePercent.Value = (int)Settings.Default.WarriorVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.WarriorTargetR.ToString("X2"), Settings.Default.WarriorTargetG.ToString("X2"), Settings.Default.WarriorTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "evoker")
            {
                Settings.Default.EvokerTargetR = cpEvoker.SelectedColor.R;
                Settings.Default.EvokerTargetG = cpEvoker.SelectedColor.G;
                Settings.Default.EvokerTargetB = cpEvoker.SelectedColor.B;


                sliderColorVariancePercent.Value = (int)Settings.Default.EvokerVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.EvokerTargetR.ToString("X2"), Settings.Default.EvokerTargetG.ToString("X2"), Settings.Default.EvokerTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "hunter")
            {

                Settings.Default.HunterTargetR = cpHunter.SelectedColor.R;
                Settings.Default.HunterTargetG = cpHunter.SelectedColor.G;
                Settings.Default.HunterTargetB = cpHunter.SelectedColor.B;

                sliderColorVariancePercent.Value= (int)Settings.Default.HunterVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.HunterTargetR.ToString("X2"), Settings.Default.HunterTargetG.ToString("X2"), Settings.Default.HunterTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "mage")
            {

                Settings.Default.MageTargetR = cpMage.SelectedColor.R;
                Settings.Default.MageTargetG = cpMage.SelectedColor.G;
                Settings.Default.MageTargetB = cpMage.SelectedColor.B;
                
                sliderColorVariancePercent.Value= (int)Settings.Default.MageVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.MageTargetR.ToString("X2"), Settings.Default.MageTargetG.ToString("X2"), Settings.Default.MageTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "priest")
            {

                Settings.Default.PriestTargetR = cpPriest.SelectedColor.R;
                Settings.Default.PriestTargetG = cpPriest.SelectedColor.G;
                Settings.Default.PriestTargetB = cpPriest.SelectedColor.B;

                sliderColorVariancePercent.Value= (int)Settings.Default.PriestVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.PriestTargetR.ToString("X2"), Settings.Default.PriestTargetG.ToString("X2"), Settings.Default.PriestTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "monk")
            {
                Settings.Default.MonkTargetR = cpMonk.SelectedColor.R;
                Settings.Default.MonkTargetG = cpMonk.SelectedColor.G;
                Settings.Default.MonkTargetB = cpMonk.SelectedColor.B;

                sliderColorVariancePercent.Value= (int)Settings.Default.MonkVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.MonkTargetR.ToString("X2"), Settings.Default.MonkTargetG.ToString("X2"), Settings.Default.MonkTargetB.ToString("X2"));

            }
            if ((string)cb.Tag == "demonhunter")
            {

                Settings.Default.DemonHunterTargetR = cpDemonHunter.SelectedColor.R;
                Settings.Default.DemonHunterTargetG = cpDemonHunter.SelectedColor.G;
                Settings.Default.DemonHunterTargetB = cpDemonHunter.SelectedColor.B;

                sliderColorVariancePercent.Value= (int)Settings.Default.DemonHunterVariancePercent;
                tbHexColors.Text = string.Concat(Settings.Default.DemonHunterTargetR.ToString("X2"), Settings.Default.DemonHunterTargetG.ToString("X2"), Settings.Default.DemonHunterTargetB.ToString("X2"));

            }




        }
        private void cbPetAttackKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            Settings.Default.PetKey = ((ComboBox)e.Source).SelectedIndex;
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
            Settings.Default.PetKeyEnables = lPet.IsEnabled;
        }

        private void cbPetKeyEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            lPet.IsEnabled = false;
            cbPetAttackKey.IsEnabled = false;
            Settings.Default.PetKeyEnables = lPet.IsEnabled;
        }

        private void cbUseBarcode_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.UseBarCode = true;
        }

        private void cbUseOCR_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.UseOCR = true;
        }

        private void cbUseBarcode_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.UseBarCode = false;
        }

        private void cbUseOCR_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.UseOCR = false;
        }

        private void cbIgnoreTargetInfo_Click(object sender, RoutedEventArgs e)
        {
            if (cbIgnoreTargetInfo.IsChecked == null)
            {
                Settings.Default.IgnoreTargetingInfo = true;
            }
            else
            {
                if (cbIgnoreTargetInfo.IsChecked.Value)
                {
                    Settings.Default.IgnoreTargetingInfo = true;
                }
                else
                {
                    Settings.Default.IgnoreTargetingInfo = false;
                }
            }

        }
        #endregion


    }
}

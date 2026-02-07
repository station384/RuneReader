using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OpenCvSharp;
using RuneReader.Classes;
using RuneReader.Classes.Utilities;
using ScreenCapture.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RuneReader.Classes.Platform;
using RuneReader.Classes.Platform.Windows;
using static RuneReader.BarcodeDecode;
using Window = Avalonia.Controls.Window;


namespace RuneReader;

public partial class MainWindow : Avalonia.Controls.Window
{
    private static UserSettings AppSettings { get; set; } = new UserSettings();

    private volatile Stack<KeyCommand> _keyCommandStack = new Stack<KeyCommand>();

    private volatile string _currentKeyToSend = string.Empty; // Default key to send, can be changed dynamically

    private volatile bool _keyProcessingFirst = false;
    private volatile bool _activationKeyPressed = false;
#if WINDOWS
    private static IntPtr _hookId = IntPtr.Zero;
    private static IntPtr _mouseHookId = IntPtr.Zero;
    private WindowsApiCalls.WindowsMessageProc _proc;
    private WindowsApiCalls.WindowsMessageProc _mouseProc;
    private IntPtr _wowWindowHandle = IntPtr.Zero;
#else
        // Global low-level hooks are Windows-only.
        private readonly object? _proc = null;
        private readonly object? _mouseProc = null;
#endif
    private CaptureScreen _captureScreen;
    private ContinuousScreenCapture _screenCapture;


    private bool _barCodeFound = false;




    //private MagnifierWindow magnifier;
    private OpenCvSharp.Rect _capRegion;
    private volatile ImageRegions _currentImageRegions = new ImageRegions();
    private DispatcherTimer _timer;
#if WINDOWS
    private DispatcherTimer _timerWowWindowMonitor; // Windows-only: monitors WoW window handle.
#endif
    private DispatcherTimer _timerBarcodeMonitor; // This timer is here to attempt to find and set the barcode location automatically.


    private int _currentR = 25;
    private int _currentG = 255;
    private int _currentB = 255;
    private int _currentA = 255;

    private double _currentThreshold = 0.3;
    private int _currentCaptureRateMs = 100;
    private int _currentKeyPressSpeedMs = 125;
    private int _currentKeyDownDelayMs = 25;
    private Dispatcher? _mainWindowDispatcher;
    private int _petKeyVkCode = 0;
    private int _gseMtVkKeyCode = 0;
    private int _gseStVkKeyCode = 0;
    private bool _useGse = false;


    
    private volatile bool _keyPressMode = false;
    private volatile float _wowGamma = 1.0f;
    private volatile bool _processingKey = false;

    private bool _initializing = true;  // To prevent events from firing as the xaml defaults are applied
    private int _maxHeight = 0;
    private int _maxWidth = 0;
        
    private static bool IsDesigner => Design.IsDesignMode;

    private bool _altPressed = false; 
    private bool _ctrlPressed = false;
        
#if WINDOWS
    private IntPtr SetHookActionKey(WindowsApiCalls.WindowsMessageProc proc)
    {
        var result = IntPtr.Zero;
        using Process curProcess = Process.GetCurrentProcess();
        if (curProcess.MainModule == null) return result;
        using ProcessModule curModule = curProcess.MainModule;
        result = WindowsApiCalls.SetWindowsHookEx(WindowsApiCalls.WH_KEYBOARD_LL, proc, WindowsApiCalls.GetModuleHandle(curModule.ModuleName), 0);

        return result;
    }




    private IntPtr HookCallbackActionKey(int nCode, IntPtr wParam, IntPtr lParam)
    {

        nint result = 0;
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            // We don't want to send key repeats if the app is not in focus
            if (!WindowsApiCalls.IsCurrentWindowWithTitle("World of Warcraft"))
            {
                _timer.Stop();

                // Let the key event go through so the new focused app can handle it
                _keyProcessingFirst = false;
                _activationKeyPressed = false;
                result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
            }
            else
            {
                var item = ActivationKeyCodeMapper.GetVirtualKeyCode(AppSettings.ActivationKey);
                if (_keyProcessingFirst == false)
                {
                    if (wParam == (IntPtr)WindowsApiCalls.WM_KEYDOWN && vkCode == item)
                    {
                        // Find the window with the title "wow" only if we haven't already found it
                        if (_wowWindowHandle == IntPtr.Zero)
                        {
                            _wowWindowHandle = WindowsApiCalls.FindWowWindow("World of Warcraft");
                        }
                        if (_wowWindowHandle != IntPtr.Zero)
                        {
                            _activationKeyPressed = true;
                            _keyProcessingFirst = true;

                            _timer.Start();

                        }


                    }
                }
                if (wParam == (IntPtr)WindowsApiCalls.WM_KEYUP && vkCode == item)
                {
                    _activationKeyPressed = false;
                    _keyProcessingFirst = false;
                    _timer.Stop();

                }
                if (wParam == (IntPtr)WindowsApiCalls.WM_KEYDOWN && vkCode == WindowsApiCalls.VK_CONTROL)
                {
                    _ctrlPressed = true;
                }
                if (wParam == (IntPtr)WindowsApiCalls.WM_KEYDOWN && vkCode == WindowsApiCalls.VK_MENU)
                {
                    _altPressed = true;
                }

                if (wParam == (IntPtr)WindowsApiCalls.WM_KEYUP && vkCode == WindowsApiCalls.VK_CONTROL)
                {
                    _ctrlPressed = false;
                }
                if (wParam == (IntPtr)WindowsApiCalls.WM_KEYUP && vkCode == WindowsApiCalls.VK_MENU)
                {
                    _altPressed = false;
                }

                result = WindowsApiCalls.CallNextHookEx(0, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
            }
        }

        //  var result = WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.
        //  var result = WindowsAPICalls.CallNextHookEx(_hookID, nCode, wParam, lParam); // Doesn't lock explorer but does not consume the event.

        return result;

    }

#else
        // Non-Windows builds: no global key hooks.
        private void EnsureWindowsHookSetup()
        {
            // Intentionally empty.
        }
#endif
        
    private struct ProcessImageResult
    {
        public string CurrentKeyToSend;
        public int WaitTime;
        public bool HasTarget;
        public DetectionRegions regions;
        public bool BarcodeFound;
    }
        
    private static int _forceGcCollect = 1;

    private static void CollectGarbage()
    {
        if (_forceGcCollect % 60 == 0)
        {
            _forceGcCollect = 1;
            GC.Collect();
        }
        _forceGcCollect++;
    }
    private WriteableBitmap? _frameBitmap;

    private void UpdatePreview(Mat frame)
    {
        _frameBitmap = frame.ToWriteableBitmap(_frameBitmap);
        void Apply()
        {
            ImageCap.Source = _frameBitmap;
            // Force redraw in cases where Source object is the same instance
            ImageCap.InvalidateVisual();
            // optional: if inside a Border and it caches, invalidate parent too
            ImageCapBorder?.InvalidateVisual();
            CollectGarbage();
        }
        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);

    }

    
    private readonly Mat _grayMat = new Mat();  // This is reused. We will let OpenCV manage the disposal of the heap when reassign it
    private readonly Mat _binMat = new Mat();   // This is reused. We will let OpenCV manage the disposal of the heap when reassign it

    /// <summary>
    /// Used to find the delays and text in the image 
    /// </summary>
    /// <param name="image">OpenCV Mat we are going to process</param>
    /// <param name="threshold">0.0 -> 1.0 How much variance of color are we going to call the same</param>
    /// <returns>ProcessImageResult</returns>
    private ProcessImageResult ProcessImageOpenCv( Mat image,   double threshold)
    {

        var origWidth = image.Width;
        var origHeight = image.Height;
        var currentKeyToSend = string.Empty;

        int redScale = ((int)(_currentR * ((_currentR * threshold) / _currentR)));
        int greenScale = ((int)(_currentG * ((_currentG * threshold) / _currentG)));
        int blueScale = ((int)(_currentB * ((_currentB * threshold) / _currentB)));


        var result = new ProcessImageResult { CurrentKeyToSend = "", HasTarget = false, WaitTime = 0, regions = new DetectionRegions { HasTarget = false, WaitTime = 0, BottomCenter = false, BottomLeft = false, TopLeft = false, TopRight = false } };
        
        double wowGammaSetting = _wowGamma;


  
        Cv2.CvtColor(image, _grayMat, ColorConversionCodes.BGR2GRAY);

        double maxValue = 255;
        double thresholdValue = 20;
        // This is what filters out the background so we can measure the gaps between the bars.
        Cv2.Threshold(_grayMat, _binMat, thresholdValue, maxValue, ThresholdTypes.Binary);



        var barcodeResult = DecodeBarcode(_binMat);
        if (barcodeResult.BarcodeFound)
        {

            result = new ProcessImageResult
            {
                CurrentKeyToSend = "",
                HasTarget = barcodeResult.HasTarget,
                WaitTime = barcodeResult.WaitTime + barcodeResult.Delay,
                regions = new DetectionRegions
                {
                    HasMultiTarget = barcodeResult.HasTarget,
                    HasTarget = barcodeResult.HasTarget,
                    WaitTime = barcodeResult.WaitTime + barcodeResult.Delay,
                    BottomCenter = (barcodeResult.WaitTime <= 500),
                    BottomLeft = (barcodeResult.WaitTime <= 300),
                    TopLeft = (barcodeResult.WaitTime <= 0),
                    TopRight = (barcodeResult.WaitTime < 1000)
                }
            };

            _barCodeFound = true;
            if (barcodeResult.HasTarget == true || CbIgnoreTargetInfo.IsChecked == true)
            {
                currentKeyToSend = barcodeResult.DecodedTextValue;
            }
            else
            {
                currentKeyToSend = "";
            }
        }
        else
        {
            _barCodeFound = false;
        }

        result.BarcodeFound = _barCodeFound;
        result.CurrentKeyToSend = currentKeyToSend;


        return result;
    }

    private void StartCaptureProcess()
    {
        // Define the area of the screen you want to capture
        int x = _capRegion.Left,
            y = _capRegion.Top,
            width = _capRegion.Width,
            height = _capRegion.Height;




        // Initialize CaptureScreen with the dispatcher and the UI update action
        OpenCvSharp.Rect regions = new OpenCvSharp.Rect { X = x, Y = y, Width = width, Height = height };
        if (regions.X + regions.Width > _maxWidth || regions.Y + regions.Height > _maxHeight)
        {
            regions = new OpenCvSharp.Rect(0, 0, 10, 10);

        }

        _captureScreen = new CaptureScreen(regions, 0);

        // Create an instance of ContinuousScreenCapture with the CaptureScreen object
        _screenCapture = new ContinuousScreenCapture(
            _currentCaptureRateMs,
            Dispatcher.UIThread,
            _captureScreen
        );

        // Assign a handler to the UpdateUIImage event
        _screenCapture.UpdateFirstImage += image =>
        {
            double threshold = _currentThreshold == 0 ? 0.0 : _currentThreshold / 100;
            var capResult = ProcessImageOpenCv( image,    threshold);
            _currentImageRegions.FirstImageRegions.TopRight = capResult.regions.TopRight;
            _currentImageRegions.FirstImageRegions.TopLeft = capResult.regions.TopLeft;
            _currentImageRegions.FirstImageRegions.BottomLeft = capResult.regions.BottomLeft;
            _currentImageRegions.FirstImageRegions.BottomCenter = capResult.regions.BottomCenter;
            _currentImageRegions.FirstImageRegions.HasTarget = capResult.regions.HasTarget;
            _currentImageRegions.FirstImageRegions.WaitTime = capResult.regions.WaitTime;
            _currentKeyToSend = capResult.CurrentKeyToSend;
            // Push the new image out the first image,  this has the markers and delays
            if (capResult.BarcodeFound)
            {
                UpdatePreview(_binMat);
                // Update the label
                LDetectedValue.Text = capResult.CurrentKeyToSend;
                LDetectedTime.Text = capResult.WaitTime.ToString();
            }
            else
            {
                UpdatePreview(image);
                LDetectedValue.Text = "N/A";
                LDetectedTime.Text = "N/A";
            }


        };

    }

    private async void MainTimerTick(object? sender, EventArgs args)
    {
        try
        {
            if (_activationKeyPressed && !_processingKey)
                await ProcessBarCodeKey();
        }
        catch (Exception ex)
        {
            // force things to come to a close.
            _activationKeyPressed = false;
        }
    }

    private async Task ProcessKey()
    {

        if (_keyCommandStack.Count == 0 || _processingKey) return;
        _processingKey = true;
        KeyCommand currentKey = _keyCommandStack.Peek();
        if (_useGse)
        {
            _keyCommandStack.Clear();
            if (!_currentImageRegions.FirstImageRegions.HasMultiTarget)
            {
                currentKey = new KeyCommand(VirtualKeyCodeMapper.GetKeyFromVKCode(_gseStVkKeyCode), currentKey.MaxWaitTime, currentKey.HasTarget)
                {
                    Alt = false,
                    Ctrl = false,
                    Shift = false
                };
            }
            else
            {
                currentKey = new KeyCommand(VirtualKeyCodeMapper.GetKeyFromVKCode(_gseMtVkKeyCode), currentKey.MaxWaitTime, currentKey.HasTarget)
                {
                    Alt = false,
                    Ctrl = false,
                    Shift = false
                };
            }
        }
        else
        {
            currentKey = _keyCommandStack.Pop();
        }

        if (_wowWindowHandle == nint.Zero)
        {
            return;
        }

        var currentD = DateTime.Now;

        if (currentKey.Alt == true && currentKey.Key == "F4")  // Somehow AF4 got through and killed wow.   so I want to Explicitly ignore it.  I will never allow ALT-F4
        {
            _processingKey = false;
            return;
        }
        
        if (WindowsApiCalls.IsKeyPressed(WindowsApiCalls.VK_MENU) && currentKey.Key == "F4")  // Alt key was pressed so don't want that
        {
            _processingKey = false;
            return;
        }

        // Translate the char to the virtual Key Code
        var vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(currentKey.Key);

        // Wows Default Key behavior is to activate as soon as the key is pressed.   So lets make sure we do not press anything till we have a 0 wait…
        // Pre-pressing is built into the addon calc  so we don't have to worry about command queuing here.
        while (_currentImageRegions.FirstImageRegions.WaitTime != 0 && _activationKeyPressed)
        {
            await Task.Delay(16);
        }

        // command is tied to CTRL or ALT So have to press them
        if (currentKey.Ctrl)
            WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYDOWN, WindowsApiCalls.VK_CONTROL, 0);
        else
            // Command isn't tied to CTRL so send a CTRL Up.
            // This should really be peeking in the message buffer to see if the key is really pressed or not. and only send the up if it is. 
            // This could also be accomplished by storing off the value in the message processor and storing a flag local if it saw one or not.
            // keyboards are global so that may work.
            WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_CONTROL, 0);

        if (currentKey.Alt)
            WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYDOWN, WindowsApiCalls.VK_MENU, 0);
        else
            // See Notes on CTRL.
            WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_MENU, 0);



        // Press the command Key Down
        WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYDOWN, vkCode, 0);



        // CTRL and ALT do not need to be held down just only pressed initially for the command to be interpreted correctly
        if (currentKey.Ctrl) WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_CONTROL, 0); //&& CtrlPressed == true
        if (currentKey.Alt) WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYUP, WindowsApiCalls.VK_MENU, 0); //&& AltPressed == true



        //Add the keypress delay while monitoring that the activation key is still pressed (allows interrupting the delay)
        // Note:  There are 10000 ticks in a millisecond
        var currentMs = DateTime.Now.Add(new TimeSpan((_currentKeyDownDelayMs) * 10000));

                

                

        if (_keyPressMode)
        {
            // This is the actual time we hold the key down.  This is used in keypress mode and Key hold mode when it is monitoring.
            await Task.Delay(_currentKeyPressSpeedMs); 
                    
            await Task.Delay(_currentCaptureRateMs == 0 ? 2 : _currentCaptureRateMs / 2); // Try and wait for a capture refresh
            currentKey.MaxWaitTime = 6000;
            currentMs = DateTime.Now.AddMilliseconds(currentKey.MaxWaitTime);
            var maxWaitTime = DateTime.Now.AddSeconds(8);
            var anticipateWait = currentKey.MaxWaitTime;


            // Wait time may be out of sync here.  this re-syncs the wait time.
            while ((currentMs >= DateTime.Now && currentKey.MaxWaitTime >= 5000) && _activationKeyPressed == true)
            {
                await Task.Delay(16);
                currentKey.MaxWaitTime = _currentImageRegions.FirstImageRegions.WaitTime;
            }


            while (currentMs >= DateTime.Now && currentKey.MaxWaitTime >= anticipateWait && _activationKeyPressed == true)
            {
                await Task.Delay(16);
                currentKey.MaxWaitTime = _currentImageRegions.FirstImageRegions.WaitTime;

                if (currentKey.MaxWaitTime <= 250)
                {
                    goto allDone;
                }
            }
        }


        // If where not watching for when things time out, we insert a hard delay
        // This is no longer need as were putting a hard pause above
        if (!_keyPressMode)
        {
            // add some randomness to the keypress rate,  just in case of throttling for evenly repeated times
            await Task.Delay(Random.Shared.Next() % 50 + _currentCaptureRateMs);
        }
        allDone:
        WindowsApiCalls.PostMessage(_wowWindowHandle, WindowsApiCalls.WM_KEYUP, vkCode, 0);
        _processingKey = false;

        return;

    }




    private async Task ProcessBarCodeKey()
    {
        if (!_activationKeyPressed)
        {
            return;
        }

        if (_processingKey)
        {
            return;
        }
        var keyToSendFirst = string.Empty;


        #region WaitFor a Key to show up

        // let's just hang out here till we have a key
        var currentD = DateTime.Now;
        keyToSendFirst = _currentKeyToSend;
        while (String.IsNullOrEmpty(keyToSendFirst) && !BStart.IsEnabled && _activationKeyPressed)
        {
            await Task.Delay(5);
            keyToSendFirst = _currentKeyToSend;
            if (currentD.AddMilliseconds(15000) < DateTime.Now)
            {
                goto allDone;
            }
        }


        if (!VirtualKeyCodeMapper.HasKey(keyToSendFirst))
        {
            goto allDone;
        }


        #endregion

        _keyCommandStack.Push(new KeyCommand(keyToSendFirst, _currentImageRegions.FirstImageRegions.WaitTime, _currentImageRegions.FirstImageRegions.HasTarget));
            
        // todo:  move this into its own thread.    the process key should only monitor if things are on the stack not be called from here.
        await ProcessKey();

        allDone:
       // ImageCapBorder.BorderBrush = Brushes.Black;
        await Task.Delay(1);
    }







    private void OnStartup ()
    {

        _mainWindowDispatcher = Dispatcher.UIThread;
        AppSettings = SettingsManager.LoadSettings();


        //Get Screen Metrics
        var screenCaptureService = new DX11ScreenCaptureService();
        var graphicsCards = screenCaptureService.GetGraphicsCards();

        // Convert to a List so it moves data local, and we don't accidentally re-enumerate when we don't have to.
        var displays = screenCaptureService.GetDisplays(graphicsCards.First()).ToList();
        _maxHeight = displays.First().Height;
        _maxWidth = displays.First().Width;
           
        // todo:  Figure out why I am disposing here.   No clue why I put this is.  Instinct tells me I did it for a reason, but that may not exist now.
        screenCaptureService.Dispose();

        _capRegion.Left = (int)(AppSettings.CapX > _maxWidth ? 100 : Math.Abs(AppSettings.CapX));
        _capRegion.Left = (int)(AppSettings.CapX < 0 ? 0 : Math.Abs(AppSettings.CapX));
        _capRegion.Top = (int)(AppSettings.CapY > _maxHeight ? 100 : Math.Abs(AppSettings.CapY));
        _capRegion.Width = (int)AppSettings.CapWidth;
        _capRegion.Height = (int)AppSettings.CapHeight;




        CbUseGse.IsChecked = AppSettings.UseGse;
        CbGseKeyBindSt.SelectedValue = CbGseKeyBindSt.Items[AppSettings.GseStKey];
        CbGseKeyBindMt.SelectedValue = CbGseKeyBindMt.Items[AppSettings.GseMtKey];
        
        
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
        tbCaptureRateMS.Text = AppSettings.CaptureRateMs.ToString();
        sliderCaptureRateMS.Value = AppSettings.CaptureRateMs;
        tbKeyRateMS.Text = AppSettings.KeyPressSpeedMs.ToString();
        sliderKeyRateMS.Value = AppSettings.KeyPressSpeedMs;
        cbPushRelease.IsChecked = AppSettings.PushAndRelease;
        CbStayOnTop.IsChecked = AppSettings.KeepOnTop;
            


        if (AppSettings.IgnoreTargetingInfo)
        {
            CbIgnoreTargetInfo.IsChecked = true;
        }
        else
        {
            CbIgnoreTargetInfo.IsChecked = false;

        }

        this.Topmost = AppSettings.KeepOnTop;


        foreach (var x in cbActivationKey.Items)
        {

            if (((ComboBoxItem)x).Content.ToString() == AppSettings.ActivationKey)
            {
                cbActivationKey.SelectedItem = x;
            }
        }


        Position = new PixelPoint((int)AppSettings.AppStartX, (int)AppSettings.AppStartY);

        _proc = HookCallbackActionKey;

        _wowWindowHandle = WindowsApiCalls.FindWowWindow("World of Warcraft");

        PositionChanged += (_, __) =>
            {
                // On Wayland this may stay 0,0 (compositor limitation)
                var p = Position;
                AppSettings.AppStartX = p.X;
                AppSettings.AppStartY = p.Y;
            };


        StartCaptureProcess();


#if WINDOWS
        // This timer watches for the WoW window (Windows-only).
        _timerWowWindowMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timerWowWindowMonitor.Tick += _TimerWowWindowMonitor_Tick;
        _timerWowWindowMonitor.Stop();
#endif

        //This timer handles sending of the key commands
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1)
        };
        _timer.Tick += MainTimerTick;
        _timer.Stop();

        //This timer will run every 5 seconds to try and find the barcode.
        _timerBarcodeMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timerBarcodeMonitor.Tick += _TimerBarcodeMonitor_Tick; ;
        _timerBarcodeMonitor.Stop();

        _initializing = false;
    }



    private async void _TimerBarcodeMonitor_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (!_barCodeFound & _screenCapture.IsCapturing)
            {
                await AttemptToFindBarcode();
            }
        }
        catch (Exception ex)
        {
            _barCodeFound = false;
        }
    }
        
#if WINDOWS
    private void _TimerWowWindowMonitor_Tick(object? sender, EventArgs e)
    {
        _wowWindowHandle = WindowsApiCalls.FindWowWindow("World of Warcraft");
    }
#endif

    private async Task AttemptToFindBarcode()
    {
        await _captureScreen.GrabFullScreens();
        Mat image = _captureScreen.CapturedFullScreen;

        try
        {
            var r = DecodeFind(image);
            if (r.screenID >= 1)
            {

                // Get the window's current location
                _capRegion.Left = r.X;// / dpiX;
                _capRegion.Top = r.Y;// / dpiY;
                _capRegion.Width = r.Width;// / dpiX;
                _capRegion.Height = r.Height;// / dpiY;
                _screenCapture.CaptureRegion = _capRegion;//(scaledLeft + 1, scaledTop + 1, scaledWidth - 1, scaledHeight - 1);
                _barCodeFound = true;
            }
        }
            
        finally
        {
            //We could do this as a using.   But I like the try, it helps me visualize when we destroy and what context.
            //image.Dispose();
            GC.Collect();
        }
    }



    #region UI Event handlers


    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        // ... When you want to stop capturing:
        if (_screenCapture.IsCapturing)
        {
            _screenCapture.StopCapture();
            if (_hookId != IntPtr.Zero)
            {
#if WINDOWS
                WindowsApiCalls.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
#endif
            }
            BStart.IsEnabled = true;
            BStop.IsEnabled = false;
            _timer.Stop();
#if WINDOWS

            _timerWowWindowMonitor.Stop();
#endif
            _timerBarcodeMonitor.Stop();

        }
    }

    private void Capture_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        // Optional debug capture. The UI button is hidden by default.
        var filePath = ".\\captures\\Cap" + DateTime.Now.ToBinary() + ".png";
            
        //todo:  this needs to use the OpenCV save not the control.   This should just grab the current capture frame and save it as a PNG.
        if (ImageCap.Source is Bitmap bmp)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            using var fileStream = new FileStream(filePath, FileMode.Create);
            bmp.Save(fileStream);
        }
    }
        


    private void sliderCaptureRateMS_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsDesigner) return;

        AppSettings.CaptureRateMs = (int)sliderCaptureRateMS.Value;
        _currentCaptureRateMs = (int)sliderCaptureRateMS.Value;
        if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
        if (_screenCapture != null)
            _screenCapture.CaptureInterval = (int)sliderCaptureRateMS.Value;

    }


    private void sliderWowGamma_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsDesigner) return;

        if (sliderWowGamma is null) return;
        AppSettings.WowGamma = (float)Math.Round(sliderWowGamma.Value, 1);
        _wowGamma = (float)AppSettings.WowGamma;
        if (tbWowGamma != null)
            tbWowGamma.Text = ((float)AppSettings.WowGamma).ToString("0.0");

    }

    private void sliderKeyRateMS_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsDesigner) return;

        AppSettings.KeyPressSpeedMs = (int)sliderKeyRateMS.Value;
        _currentKeyDownDelayMs = (int)sliderKeyRateMS.Value;
        if (tbKeyRateMS != null)
            tbKeyRateMS.Text = ((int)sliderKeyRateMS.Value).ToString();

    }

    private void tbKeyRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        if (sender is not TextBox tb || !int.TryParse(tb.Text, out var v)) return;
        sliderKeyRateMS.Value = v;
            
    }

    private void tbCaptureRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        if (sender is TextBox tb && int.TryParse(tb.Text, out var v))
            sliderCaptureRateMS.Value = v;
    }

    private void tbWowGamme_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        //  sliderWowGamma.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
    }



    private void cbActivationKey_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        AppSettings.ActivationKey = ((ComboBoxItem)cbActivationKey.SelectedItem).Content.ToString();
    }

    private void bResetMagPosition_Click(object? sender, RoutedEventArgs e)
    {
        AppSettings.CapX = 50;
        AppSettings.CapY = 50;
        AppSettings.CapWidth = 100;
        AppSettings.CapHeight = 100;

        //magnifier.Left = AppSettings.CapX > SystemParameters.PrimaryScreenWidth ? 100 : AppSettings.CapX;
        //magnifier.Top = AppSettings.CapY > SystemParameters.PrimaryScreenHeight ? 100 : AppSettings.CapY;
        //magnifier.Width = AppSettings.CapWidth;
        //magnifier.Height = AppSettings.CapHeight;
    }



    private void cbPetAttackKey_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {

        AppSettings.PetKey = ((ComboBox)e.Source).SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                _petKeyVkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((ComboBoxItem)((ComboBox)e.Source).SelectedItem).Content.ToString());
            }
        }
    }


    private async void bFindBarcode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (IsDesigner) return;
            await AttemptToFindBarcode();
        }
        catch (Exception ex)
        {
            _barCodeFound = false;
        }
    }
        

    private void cbPetKeyEnabled_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (((CheckBox)e.Source).IsChecked == true)
        {
            lPet.IsEnabled = true;
            cbPetAttackKey.IsEnabled = true;
            AppSettings.PetKeyEnables = lPet.IsEnabled;
        }
        else
        {
            lPet.IsEnabled = false;
            cbPetAttackKey.IsEnabled = false;
            AppSettings.PetKeyEnables = lPet.IsEnabled;
        }
    }

    private void cbPushRelease_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (IsDesigner) return;
        if (((CheckBox)e.Source).IsChecked == true)
        {
            _keyPressMode = true;
            AppSettings.PushAndRelease = _keyPressMode;
        }
        else
        {
            _keyPressMode = false;
            AppSettings.PushAndRelease = _keyPressMode;
        }
    }

    private void cbStayOnTop_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (((CheckBox)e.Source).IsChecked == true)
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

    private void cbIgnoreTargetInfo_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
       
        if (((CheckBox)e.Source).IsChecked == null)
        {
            AppSettings.IgnoreTargetingInfo = true;
        }
        else
        {
            if (((CheckBox)e.Source).IsChecked == true )
            {
                AppSettings.IgnoreTargetingInfo = true;
            }
            else
            {
                AppSettings.IgnoreTargetingInfo = false;
            }
        }

    }
    #endregion
        

    private void Button_Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        // Start the continuous capturing
        //_wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft"); //WindowsAPICalls.FindWindow(null, "World of Warcraft");
        if (!_screenCapture.IsCapturing)
        {

            _currentKeyToSend = "";

            _screenCapture.StartCapture();

#if WINDOWS
            _hookId = _hookId == IntPtr.Zero ? SetHookActionKey(_proc) : IntPtr.Zero;
#else
                    // No-op on non-Windows.
#endif
            BStart.IsEnabled = false;
            BStop.IsEnabled = true;
#if WINDOWS

            _timerWowWindowMonitor.Start();
#endif
            _timerBarcodeMonitor.Start();
            _timer.Start();

        }

    }

        
        
        
    private bool _started;
    public MainWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_started) return;
                _started = true;
                OnStartup();
            }, DispatcherPriority.Loaded);
        }

      
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            Cleanup();
        }
        finally
        {
            base.OnClosed(e);
        }
    }
    
    private void Cleanup()
    {
        if (Design.IsDesignMode) return;




        
        _grayMat.Dispose();
        _binMat.Dispose();
        AppSettings.CapX = _capRegion.Left;
        AppSettings.CapY = _capRegion.Top;
        AppSettings.CapWidth = _capRegion.Width;
        AppSettings.CapHeight = _capRegion.Height;
        
        


        SettingsManager.SaveSettings(AppSettings);

        _timer.Stop();
#if WINDOWS

        _timerWowWindowMonitor.Stop();
#endif



        if (_screenCapture.IsCapturing)
        {
            _screenCapture.StopCapture();
        }


        if (_hookId != IntPtr.Zero)
        {
            // Make sure we stop trapping the keyboard
#if WINDOWS
            WindowsApiCalls.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
#endif
        }


        //if (_MouseHookID != IntPtr.Zero)
        //{

        //    // Make sure we stop trapping the mouse if its active
        //    WindowsAPICalls.UnhookWindowsHookEx(_MouseHookID);
        //    _MouseHookID = IntPtr.Zero;
        //}
        //magnifier.Close();
    }

    private void CbGseKeyBindMt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AppSettings.GseMtKey = ((ComboBox)e.Source).SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                _gseMtVkKeyCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((ComboBoxItem)((ComboBox)e.Source).SelectedItem).Content.ToString());
            }
        }
    }

    private void CbGseKeyBindSt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AppSettings.GseStKey = ((ComboBox)e.Source).SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                _gseStVkKeyCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((ComboBoxItem)((ComboBox)e.Source).SelectedItem).Content.ToString());
            }
        }
    }

    private void CbUseGse_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (IsDesigner) return;
        if (((CheckBox)e.Source).IsChecked == true)
        {
            _useGse = true;
            AppSettings.UseGse = true;
        }
        else
        {
            _useGse = false;
            AppSettings.UseGse = false;
        }
    }
}
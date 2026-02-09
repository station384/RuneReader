using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
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
using System.Threading;
using System.Threading.Tasks;
using RuneReader.Classes.Platform;
using static RuneReader.BarcodeDecode;



namespace RuneReader;

public partial class MainWindow : Avalonia.Controls.Window
{
    private static UserSettings AppSettings { get; set; } = new();

    private volatile Stack<KeyCommand> _keyCommandStack = new();

    private volatile string _currentKeyToSend = string.Empty; // Default key to send, can be changed dynamically
    //private IntPtr _wowWindowHandle = IntPtr.Zero;

   // private bool KeyProcessingFirst { get; set; } = false;
    private bool ActivationKeyPressed { get; set; }
    private ContinuousScreenCapture? _continuousScreenCaptureProcess;


    private bool _barCodeFound;

    private IPlatformServices? _platform;


    //private MagnifierWindow magnifier;
    private OpenCvSharp.Rect _capRegion;
    private volatile ImageRegions _currentImageRegions = new();
    private DispatcherTimer? _timer;
    private DispatcherTimer? _timerWowWindowMonitor; // Windows-only: monitors WoW window handle.
    private DispatcherTimer? _timerBarcodeMonitor; // This timer is here to attempt to find and set the barcode location automatically.

    
    private int CurrentCaptureRateMs { get; set; }= 100;
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    private int CurrentKeyPressSpeedMs { get; set; }= 125;
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    private int CurrentKeyDownDelayMs { get; set; }= 25;
  //  private Dispatcher? _mainWindowDispatcher;
    private int PetKeyVkCode { get; set; }
    private int GseMtVkKeyCode { get; set; }
    private int GseStVkKeyCode { get; set; }
    private bool UseGse { get; set; }


    
    private volatile bool _keyPressMode;
    // ReSharper disable once NotAccessedField.Local
    private volatile float _wowGamma = 1.0f;
    private volatile bool _processingKey;

    private bool Initializing { get; set; }= true;  // To prevent events from firing as the xaml defaults are applied
    private int ScreenMaxHeight { get; set; }
    private int ScreenMaxWidth { get; set; }
        
    private static bool IsDesigner => Design.IsDesignMode;

    private bool AltPressed { get; set; }
    private bool CtrlPressed { get; set; }
    private bool ShiftPressed { get; set; }
    private string ProcessActivateKey { get; set; } = "1";

    private Mat _capRegionMat = new Mat();
    private Mat _fullScreenMat = new Mat();
    
        
    private struct ProcessImageResult
    {
        public string CurrentKeyToSend;
        public int WaitTime;
        // ReSharper disable once NotAccessedField.Local
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

    
    private Mat GrayMatHolder { get; }= new();  // This is reused. We will let OpenCV manage the disposal of the heap when reassign it
    private Mat BinaryMatHoler { get; }= new();   // This is reused. We will let OpenCV manage the disposal of the heap when reassign it

    /// <summary>
    /// Used to find the delays and text in the image 
    /// </summary>
    /// <param name="image">OpenCV Mat we are going to process</param>
    /// <param name="threshold">0.0 -> 1.0 How much variance of color are we going to call the same</param>
    /// <returns>ProcessImageResult</returns>
    private ProcessImageResult ProcessImageOpenCv( Mat image,   double threshold)
    {
        
        var currentKeyToSend = string.Empty;

        var result = new ProcessImageResult { CurrentKeyToSend = "", HasTarget = false, WaitTime = 0, regions = new DetectionRegions { HasTarget = false, WaitTime = 0, BottomCenter = false, BottomLeft = false, TopLeft = false, TopRight = false } };
        
        Cv2.CvtColor(image, GrayMatHolder, ColorConversionCodes.BGR2GRAY);

        double maxValue = 255;
        double thresholdValue = threshold;
        // This is what filters out the background so we can measure the gaps between the bars.
        Cv2.Threshold(GrayMatHolder, BinaryMatHoler, thresholdValue, maxValue, ThresholdTypes.Binary);



        var barcodeResult = DecodeBarcode(BinaryMatHoler);
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
            if (barcodeResult.HasTarget || CbIgnoreTargetInfo.IsChecked == true)
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

    private void InitializeContinuousCaptureProcess()
    {
        // Define the area of the screen you want to capture
        int x = _capRegion.Left,
            y = _capRegion.Top,
            width = _capRegion.Width,
            height = _capRegion.Height;




        // Initialize CaptureScreen with the dispatcher and the UI update action
        OpenCvSharp.Rect regions = new OpenCvSharp.Rect { X = x, Y = y, Width = width, Height = height };
        if (regions.X + regions.Width > ScreenMaxWidth || regions.Y + regions.Height > ScreenMaxHeight)
        {
            regions = new OpenCvSharp.Rect(0, 0, 10, 10);

        }
        
        
        // Create an instance of ContinuousScreenCapture with the CaptureScreen object
        if (_platform is not null)
        {
            _platform.ScreenCapture.CaptureRegion = regions;
            _continuousScreenCaptureProcess = new ContinuousScreenCapture(
                CurrentCaptureRateMs,
                _platform.ScreenCapture
            );
        }
        
    }

    private async void MainTimerTick(object? sender, EventArgs args)
    {
        try
        {
            if (ActivationKeyPressed && !_processingKey)
                await ProcessBarCodeKey();
        }
        catch (Exception ex)
        {
            // force things to come to a close.
            Debug.WriteLine(ex.Message);
            ActivationKeyPressed = false;
        }
    }

    private async Task ProcessKey()
    {

        if (_keyCommandStack.Count == 0 || _processingKey) return;
        _processingKey = true;
        KeyCommand currentKey = _keyCommandStack.Peek();
        if (UseGse)
        {
            _keyCommandStack.Clear();
            if (!_currentImageRegions.FirstImageRegions.HasMultiTarget)
            {
                currentKey = new KeyCommand(VirtualKeyCodeMapper.GetKeyFromVKCode(GseStVkKeyCode), currentKey.MaxWaitTime, currentKey.HasTarget)
                {
                    Alt = false,
                    Ctrl = false,
                    Shift = false
                };
            }
            else
            {
                currentKey = new KeyCommand(VirtualKeyCodeMapper.GetKeyFromVKCode(GseMtVkKeyCode), currentKey.MaxWaitTime, currentKey.HasTarget)
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

        if (!(_platform!).ForegroundWindow.IsActiveWindow())
        {
            _processingKey = false;
            return;
        }
        

        if (AltPressed && currentKey.Key == "F4")  // Somehow AF4 got through and killed wow.   so I want to Explicitly ignore it.  I will never allow ALT-F4
        {
            _processingKey = false;
            return;
        }
        
        if (AltPressed && currentKey.Key == "F4")  // Alt key was pressed so don't want that
        {
            _processingKey = false;
            return;
        }

        // Translate the char to the virtual Key Code
        var vkCode = VirtualKeyCodeMapper.GetVirtualKeyCode(currentKey.Key);

        // Wows Default Key behavior is to activate as soon as the key is pressed.   So lets make sure we do not press anything till we have a 0 wait…
        // Pre-pressing is built into the addon calc  so we don't have to worry about command queuing here.
        while (_currentImageRegions.FirstImageRegions.WaitTime != 0 && ActivationKeyPressed)
        {
            await Task.Delay(16);
        }

        // command is tied to CTRL or ALT So have to press them
        if (currentKey.Ctrl)
        { 
            _platform.Input.TrySendCtrlKey(true); 
        }
        else
        {
            // Command isn't tied to CTRL so send a CTRL Up.
            // This should really be peeking in the message buffer to see if the key is really pressed or not. and only send the up if it is. 
            // This could also be accomplished by storing off the value in the message processor and storing a flag local if it saw one or not.
            // keyboards are global so that may work.
            _platform.Input.TrySendCtrlKey(false); 
        }

        if (currentKey.Alt)
        {
            _platform.Input.TrySendAltKey(true);
        }
        else
        {
            // See Notes on CTRL.
            _platform.Input.TrySendAltKey(false);
        }

        if (currentKey.Shift)
        {
            _platform.Input.TrySendShiftKey(true); 
        }
        else
        {
            // See Notes on CTRL.
            _platform.Input.TrySendShiftKey(false);
        }


        // Press the command Key Down
        _platform.Input.TrySendKey(vkCode,true);



        // CTRL and ALT do not need to be held down just only pressed initially for the command to be interpreted correctly
        if (currentKey.Ctrl)
        {
            _platform.Input.TrySendCtrlKey(false);
        }

        if (currentKey.Alt)
        {
            _platform.Input.TrySendAltKey(false);
        }

        if (currentKey.Shift)
        {
            _platform.Input.TrySendShiftKey(false);
        }

        //Add the keypress delay while monitoring that the activation key is still pressed (allows interrupting the delay)
        // Note:  There are 10000 ticks in a millisecond

        if (_keyPressMode)
        {
            // This is the actual time we hold the key down.  This is used in keypress mode and Key hold mode when it is monitoring.
            await Task.Delay(CurrentKeyPressSpeedMs); 
                    
            await Task.Delay(CurrentCaptureRateMs == 0 ? 2 : CurrentCaptureRateMs / 2); // Try and wait for a capture refresh
            currentKey.MaxWaitTime = 6000;
            var currentMs = DateTime.Now.AddMilliseconds(currentKey.MaxWaitTime);
           // var maxWaitTime = DateTime.Now.AddSeconds(8);
            var anticipateWait = currentKey.MaxWaitTime;


            // Wait time may be out of sync here.  this re-syncs the wait time.
            while ((currentMs >= DateTime.Now && currentKey.MaxWaitTime >= 5000) && ActivationKeyPressed)
            {
                await Task.Delay(16);
                currentKey.MaxWaitTime = _currentImageRegions.FirstImageRegions.WaitTime;
            }


            while (currentMs >= DateTime.Now && currentKey.MaxWaitTime >= anticipateWait && ActivationKeyPressed)
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
            await Task.Delay(Random.Shared.Next() % 50 + CurrentCaptureRateMs);
        }
        allDone:
        _platform.Input.TrySendKey(vkCode, false);
        _processingKey = false;
    }




    private async Task ProcessBarCodeKey()
    {
        if (!ActivationKeyPressed)
        {
            return;
        }

        if (_processingKey)
        {
            return;
        }


        #region WaitFor a Key to show up

        // let's just hang out here till we have a key
        var currentD = DateTime.Now;
        var keyToSendFirst = _currentKeyToSend;
        while (String.IsNullOrEmpty(keyToSendFirst) && !BStart.IsEnabled && ActivationKeyPressed)
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



    private void InitializePlatform(string? activationKey)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_platform != null)
        {
            if (_platform.Hotkeys.isStarted())
            {
                _platform.Hotkeys.Stop();
            }

            _platform.Hotkeys.Dispose();
        }

        _platform = PlatformFactory.Create(activationKey);
        //_platform.Hotkeys.ActivateKeyChanged += HotkeysOnActivateKeyChanged;
        _platform.Hotkeys.ActivateKeyChangedAsync += HotkeysOnActivateKeyChangedAsync;
        //_platform.Hotkeys.ShiftChanged += HotkeysOnShiftChanged;
        _platform.Hotkeys.ShiftChangedAsync += HotkeysOnShiftChangedAsync;
        //_platform.Hotkeys.CtrlChanged += HotkeysOnCtrlChanged;
        _platform.Hotkeys.CtrlChangedAsync += HotkeysOnCtrlChangedAsync;
        //_platform.Hotkeys.AltChanged += HotkeysOnAltChanged;
        _platform.Hotkeys.AltChangedAsync += HotkeysOnAltChangedAsync;
        
        
    }




    private void OnStartup ()
    {

        //_mainWindowDispatcher = Dispatcher.UIThread;
        AppSettings = SettingsManager.LoadSettings();









        CbUseGse.IsChecked = AppSettings.UseGse;
        CbGseKeyBindSt.SelectedValue = CbGseKeyBindSt.Items[AppSettings.GseStKey];
        CbGseKeyBindMt.SelectedValue = CbGseKeyBindMt.Items[AppSettings.GseMtKey];
        
        
        cbPetAttackKey.SelectedValue = cbPetAttackKey.Items[AppSettings.PetKey];
        if (AppSettings.PetKeyEnables)
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

            if ((x as ComboBoxItem)?.Content?.ToString() == AppSettings.ActivationKey)
            {
                cbActivationKey.SelectedItem = x;
                ProcessActivateKey =  AppSettings.ActivationKey;
            }
        }


        Position = new PixelPoint((int)AppSettings.AppStartX, (int)AppSettings.AppStartY);


        InitializePlatform(ProcessActivateKey);
        _platform!.ScreenCapture.CaptureRegion = _capRegion;
        _platform!.ScreenCapture.EnableRegion = false;



       _platform!.ForegroundWindow.SetWindowToFind("World of Warcraft");
       _platform!.ScreenCapture.OnRegionUpdated += ScreenCaptureOnRegionUpdated;
       _platform!.ScreenCapture.OnFullScreenUpdated += ScreenCaptureOnonFullScreenUpdated;

        ScreenMaxHeight = _platform.ScreenCapture.ScreenHeight;
        ScreenMaxWidth = _platform.ScreenCapture.ScreenWidth;

        _capRegion.Left = (int)(AppSettings.CapX > ScreenMaxWidth ? 100 : Math.Abs(AppSettings.CapX));
        _capRegion.Left = (int)(AppSettings.CapX < 0 ? 0 : Math.Abs(AppSettings.CapX));
        _capRegion.Top = (int)(AppSettings.CapY > ScreenMaxHeight ? 100 : Math.Abs(AppSettings.CapY));
        _capRegion.Width = (int)AppSettings.CapWidth;
        _capRegion.Height = (int)AppSettings.CapHeight;


        PositionChanged += (_, _) =>
            {
                // On Wayland this may stay 0,0 (compositor limitation)
                var p = Position;
                AppSettings.AppStartX = p.X;
                AppSettings.AppStartY = p.Y;
            };


        InitializeContinuousCaptureProcess();
        _platform.ScreenCapture.EnableFullScreen = true;


        // This timer watches for the WoW window (Windows-only).
        _timerWowWindowMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timerWowWindowMonitor.Tick += _TimerWowWindowMonitor_Tick;
        _timerWowWindowMonitor.Stop();


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
        _timerBarcodeMonitor.Tick += _TimerBarcodeMonitor_Tick;
        _timerBarcodeMonitor.Stop();

        Initializing = false;
    }




    #region Key Event Handlers
    private void HotkeysOnActivateKeyChangedAsync(HotkeyActionResult obj)
    {
        
        if (obj.KeyState == HotkeyState.PRESSED)
            ActivationKeyPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            ActivationKeyPressed = false;
    }


    private void HotkeysOnActivateKeyChanged(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            ActivationKeyPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            ActivationKeyPressed = false;
    }

    private void HotkeysOnCtrlChangedAsync(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            CtrlPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            CtrlPressed = false;
    }

    private void HotkeysOnCtrlChanged(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            CtrlPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            CtrlPressed = false;
    }

    private void HotkeysOnShiftChangedAsync(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            ShiftPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            ShiftPressed = false;
    }

    private void HotkeysOnShiftChanged(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            ShiftPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            ShiftPressed = false;
    }    

    private void HotkeysOnAltChangedAsync(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            AltPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            AltPressed = false;
    }

    private void HotkeysOnAltChanged(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            AltPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            AltPressed = false;
    }
    
    #endregion

    private void ScreenCaptureOnonFullScreenUpdated(Mat image)
    {
        
        // swap the values and dispose of the old value.  thread-safe
        Mat old = Interlocked.Exchange(ref _fullScreenMat, image);
        if (old != null && !old.IsDisposed)
            old.Dispose();
        
        try
        {
            var r = DecodeFind(_fullScreenMat);
            if (r.screenID >= 1)
            {

                // Get the window's current location
                _capRegion.Left = r.X;// / dpiX;
                _capRegion.Top = r.Y;// / dpiY;
                _capRegion.Width = r.Width;// / dpiX;
                _capRegion.Height = r.Height;// / dpiY;
                _platform!.ScreenCapture!.CaptureRegion = _capRegion;
                _barCodeFound = true;
            }
        }
            
        finally
        {
            //We could do this as a using.   But I like the try, it helps me visualize when we destroy and what context.
            // We only want to capture the full screen once then stop.   if it needs it again,  it will be triggered set to true elsewhere.
            _platform!.ScreenCapture.EnableFullScreen = false;
            GC.Collect();
            
        }
    }
    
    private void ScreenCaptureOnRegionUpdated(Mat image)
    {
        // swap the values and dispose of the old value.  thread-safe
        Mat old = Interlocked.Exchange(ref _capRegionMat, image);
        if (old != null && !old.IsDisposed)
            old.Dispose();
        
        double threshold = 20;//CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
        var capResult = ProcessImageOpenCv( _capRegionMat,    threshold);
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
            UpdatePreview(BinaryMatHoler);
            // Update the label
            LDetectedValue.Text = capResult.CurrentKeyToSend;
            LDetectedTime.Text = capResult.WaitTime.ToString();
        }
        else
        {
            UpdatePreview(_capRegionMat);
            LDetectedValue.Text = "N/A";
            LDetectedTime.Text = "N/A";
        }

    }
    

    private async void _TimerBarcodeMonitor_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (_continuousScreenCaptureProcess != null && !_barCodeFound & _continuousScreenCaptureProcess.IsCapturing)
            {
                 AttemptToFindBarcode();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            _barCodeFound = false;
        }
    }
        
    private void _TimerWowWindowMonitor_Tick(object? sender, EventArgs e)
    {
        _platform?.ForegroundWindow.SetWindowToFind("World of Warcraft");
    }


    private void AttemptToFindBarcode()
    {
        _platform!.ScreenCapture.EnableFullScreen = true;
    }



    #region UI Event handlers


    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        // ... When you want to stop capturing:
        if (_continuousScreenCaptureProcess!.IsCapturing)
        {
            _platform!.ScreenCapture.EnableRegion = false;
            _continuousScreenCaptureProcess.StopCapture();
            _platform!.Hotkeys.Stop();
            BStart.IsEnabled = true;
            BStop.IsEnabled = false;
            _timer!.Stop();
            _timerWowWindowMonitor!.Stop();
            _timerBarcodeMonitor!.Stop();

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
        if (sliderCaptureRateMS is null) return;
        AppSettings.CaptureRateMs = (int)sliderCaptureRateMS.Value;
        CurrentCaptureRateMs = (int)sliderCaptureRateMS.Value;
        if (tbCaptureRateMS != null)
            tbCaptureRateMS.Text = ((int)sliderCaptureRateMS.Value).ToString();
        if (_continuousScreenCaptureProcess != null)
            _continuousScreenCaptureProcess.CaptureInterval = (int)sliderCaptureRateMS.Value;

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
        if (sliderKeyRateMS is null) return;
        AppSettings.KeyPressSpeedMs = (int)sliderKeyRateMS.Value;
        CurrentKeyDownDelayMs = (int)sliderKeyRateMS.Value;
        if (tbKeyRateMS != null)
            tbKeyRateMS.Text = ((int)sliderKeyRateMS.Value).ToString();

    }

    private void tbKeyRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        if (sender is not TextBox tb || !int.TryParse(tb.Text, out var v)) return;
        {
            if (v > 1000) v = 1000;
            if (v < 5) v = 5;
            sliderKeyRateMS.Value = v;
        }

    }

    private void tbCaptureRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        if (sender is TextBox tb && int.TryParse(tb.Text, out var v))
        {
            if (v > 250) v = 250;
            if (v < 5) v = 5;
            sliderCaptureRateMS.Value = v;
        }
    }

    private void tbWowGamme_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;

        //  sliderWowGamma.Value = int.Parse(((System.Windows.Controls.TextBox)e.Source).Text.ToString());
    }



    private void cbActivationKey_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Initializing) return;
        
        var activationKey = (cbActivationKey.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(activationKey)) return;
        AppSettings.ActivationKey = activationKey;
        ProcessActivateKey = AppSettings.ActivationKey;
        // Make sure we restore the capture state
        var lastPlatformState = _platform!.Hotkeys.isStarted();
        InitializePlatform(ProcessActivateKey);
        if (lastPlatformState) _platform.Hotkeys.Start();
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


        if (e.Source != null)
        {
            AppSettings.PetKey = (e.Source as ComboBox)!.SelectedIndex;
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                PetKeyVkCode = VirtualKeyCodeMapper.GetVirtualKeyCode((((ComboBox)e.Source).SelectedItem as ComboBoxItem)!.Content!.ToString()!);
            }
        }
    }


    private async void bFindBarcode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (IsDesigner) return;
             AttemptToFindBarcode();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            _barCodeFound = false;
        }
    }
        

    private void cbPetKeyEnabled_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if ((e.Source as CheckBox)!.IsChecked == true)
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
        if (Initializing) return;
        if (IsDesigner) return;
        if ((e.Source as CheckBox)!.IsChecked == true)
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
        if ((e.Source as CheckBox)!.IsChecked == true)
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
       
        if ((e.Source as CheckBox)!.IsChecked == null)
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
   
    private void CbGseKeyBindMt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AppSettings.GseMtKey = (e.Source as ComboBox)!.SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                GseMtVkKeyCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((e.Source as ComboBox)!.SelectedItem as ComboBoxItem)!.Content!.ToString()!);
            }
        }
    }

    private void CbGseKeyBindSt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AppSettings.GseStKey = (e.Source as ComboBox)!.SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                GseStVkKeyCode = VirtualKeyCodeMapper.GetVirtualKeyCode(((e.Source as ComboBox)!.SelectedItem as ComboBoxItem)!.Content!.ToString()!);
            }
        }
    }

    private void CbUseGse_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (Initializing) return;
        if (IsDesigner) return;
        if ((e.Source as CheckBox)!.IsChecked == true)
        {
            UseGse = true;
            AppSettings.UseGse = true;
        }
        else
        {
            UseGse = false;
            AppSettings.UseGse = false;
        }
    }
    
    
    #endregion
        

    private void Button_Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        // Start the continuous capturing
        //_wowWindowHandle = WindowsAPICalls.FindWowWindow("World of Warcraft"); //WindowsAPICalls.FindWindow(null, "World of Warcraft");
        if (!_continuousScreenCaptureProcess!.IsCapturing)
        {

            _currentKeyToSend = "";
            _platform!.ScreenCapture.EnableRegion = true;
            _continuousScreenCaptureProcess.StartCapture();
            _platform!.Hotkeys.Start();
            BStart.IsEnabled = false;
            BStop.IsEnabled = true;
            _timerWowWindowMonitor!.Start();
            _timerBarcodeMonitor!.Start();
            _timer!.Start();

        }

    }

        
        
        
    private bool _started;


    public MainWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            if (_started) return;
            //_started = true;

            //OnStartup();
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

        GrayMatHolder.Dispose();
        BinaryMatHoler.Dispose();
        AppSettings.CapX = _capRegion.Left;
        AppSettings.CapY = _capRegion.Top;
        AppSettings.CapWidth = _capRegion.Width;
        AppSettings.CapHeight = _capRegion.Height;

        SettingsManager.SaveSettings(AppSettings);

        _timer!.Stop();
        _timerWowWindowMonitor!.Stop();
        
        if (_continuousScreenCaptureProcess!.IsCapturing)
        {
            _continuousScreenCaptureProcess.StopCapture();
        }


        _platform!.Hotkeys.Stop();

    }


}
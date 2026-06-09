using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OpenCvSharp;
using RuneReader.Classes;
using RuneReader.Classes.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RuneReader.Classes.Platform;
using static RuneReader.BarcodeDecode;



namespace RuneReader;

public partial class MainWindow : Avalonia.Controls.Window
{
    static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);
    //Exponentially Weighted Moving Average
    public sealed class EwmaTimeConstant
    {
        private readonly double _tauSec; // smoothing time constant
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private long _lastTicks;

        public bool HasValue { get; private set; }
        public double ValueMs { get; private set; }

        /// <summary>
        /// EWMA = Exponentially Weighted Moving Average.
        /// It’s an average that weights recent samples more than older ones, with the weights decaying exponentially. So it smooths noisy measurements but still follows changes faster than a simple “average of everything.”
        /// </summary>
        /// <param name="tauSeconds">
        /// τ = 1.0s → reacts quickly (good if delay changes a lot)
        /// τ = 2–3s → smoother and still responsive (usually best)
        /// τ = 5s → very smooth, slower to react
        /// </param>
        public EwmaTimeConstant(double tauSeconds = 2.0)
        {
            _tauSec = Math.Max(0.001, tauSeconds);
            _lastTicks = _sw.ElapsedTicks;
        }

        public void Add(double xMs)
        {
            long now = _sw.ElapsedTicks;
            double dt = (now - _lastTicks) / (double)Stopwatch.Frequency; // seconds
            _lastTicks = now;

            // protect against weird dt (paused debugger, etc.)
            if (dt < 0) dt = 0;
            if (dt > 1.0) dt = 1.0;

            double alpha = 1.0 - Math.Exp(-dt / _tauSec);

            if (!HasValue)
            {
                ValueMs = xMs;
                HasValue = true;
                return;
            }

            ValueMs += alpha * (xMs - ValueMs);
        }
    }
    private static EwmaTimeConstant ewma = new(tauSeconds: 3.0);
    private static UserSettings AppSettings { get; set; } = new();
    
    private volatile string _currentKeyToSend = string.Empty; // Default key to send, can be changed dynamically

    //private bool ActivationKeyPressed { get; set; }
    private int _activationKeyPressed; // 0/1
    private bool ActivationKeyPressed => Volatile.Read(ref _activationKeyPressed) == 1;
    private void SetActivationKeyPressed(bool value) => Volatile.Write(ref _activationKeyPressed, value ? 1 : 0);
    
    private ContinuousScreenCapture? _continuousScreenCaptureProcess;

    private int _barcodeProcessingGate;
    private bool TryEnterBarcodeProcessing() => Interlocked.Exchange(ref _barcodeProcessingGate, 1) == 0;
    private void ExitBarcodeProcessing() => Volatile.Write(ref _barcodeProcessingGate, 0);
    
    private bool _barCodeFound;

    private IPlatformServices? _platform;


    //private MagnifierWindow magnifier;
    private OpenCvSharp.Rect _capRegion;
    
    private volatile ImageRegions _currentImageRegions = new();

    private CancellationTokenSource? _loopsCts;
    private Task? _keyLoopTask; //This task handles sending of the key commands
    private Task? _wowMonitorTask; // This task watches for the WoW window (Windows-only).
   
    private Task? _barcodeMonitorTask; // This task tries to find the barcode on the screen and when found sets the region Note:  This should only happen if the main Barcode comes back as no barcode found.
    private int _isRunning;
    private bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    private bool _ignoreTargetInfo = false;
    
    private void StartBackgroundLoops()
    {
        // already running?

            if (_loopsCts != null) return;

            _loopsCts = new CancellationTokenSource();
            var token = _loopsCts.Token;

            _keyLoopTask = RunKeyLoopAsync(token);
            _wowMonitorTask = RunWowMonitorLoopAsync(token);

            _barcodeMonitorTask = RunBarcodeMonitorLoopAsync(token);
    }
    
    private async Task StopBackgroundLoopsAsync()
    {

        var cts = _loopsCts;
        if (cts == null) return;

        _loopsCts = null;

        try
        {
            await cts.CancelAsync();

            var tasks = new[] { _keyLoopTask, _wowMonitorTask, _barcodeMonitorTask };
            _keyLoopTask = _wowMonitorTask = _barcodeMonitorTask = null;

            await Task.WhenAll(tasks.Where(t => t != null)!).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    
    
    
    private int CurrentCaptureRateMs { get; set; }= 100;
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    private int CurrentKeyPressSpeedMs { get; set; }= 125;
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    private int CurrentKeyDownDelayMs { get; set; }= 25;
    
    private int GseMtVkKeyCode { get; set; }
    private int GseStVkKeyCode { get; set; }
    private bool UseGse { get; set; }
    
    private volatile bool _keyPressMode;
    
    // private int _processingKeyPressed; // 0/1
    // private bool ProcessingKeyPressed => Volatile.Read(ref _processingKeyPressed) == 1;
    // private void SetProcessingKeyPressed(bool value) => Volatile.Write(ref _processingKeyPressed, value ? 1 : 0);
    private int _processingKeyGate; // 0/1
    private bool TryEnterProcessingKey() => Interlocked.Exchange(ref _processingKeyGate, 1) == 0;
    private void ExitProcessingKey() => Volatile.Write(ref _processingKeyGate, 0);
    
    //private volatile bool _processingKey;

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
        if (frame.IsDisposed || frame.Data == IntPtr.Zero) return;
        
        _frameBitmap = frame.ToWriteableBitmap(_frameBitmap);
        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        return;

        void Apply()
        {
            ImageCap.Source = _frameBitmap;
            // Force redraw in cases where Source object is the same instance
            ImageCap.InvalidateVisual();
            // optional: if inside a Border and it caches, invalidate parent too
            ImageCapBorder?.InvalidateVisual();
            CollectGarbage();
        }
    }

    private double avgDelay = 0.0;
    //private Mat GrayMatHolder { get; }= new();  // This is reused. We will let OpenCV manage the disposal of the heap when reassign it
    //private Mat BinaryMatHoler { get; }= new();   // This is reused. We will let OpenCV manage the disposal of the heap when reassign it

    /// <summary>
    /// Used to find the delays and text in the image 
    /// </summary>
    /// <param name="image">OpenCV Mat we are going to process</param>
    /// <param name="threshold">0.0 -> 1.0 How much variance of color are we going to call the same</param>
    /// <returns>ProcessImageResult</returns>
    private (ProcessImageResult, Mat) ProcessImageOpenCv( Mat image,   double threshold)
    {
        
        var currentKeyToSend = string.Empty;
        var result = new ProcessImageResult { CurrentKeyToSend = "", HasTarget = false, WaitTime = 0, regions = new DetectionRegions { HasTarget = false, WaitTime = 0, BottomCenter = false, BottomLeft = false, TopLeft = false, TopRight = false } };
        if (image.IsDisposed || image.Data == IntPtr.Zero)
        {
            return (result, new Mat());
            
        }
        var _grayMat = new Mat();
        var _binMat = new Mat();
        try
        {
            Cv2.CvtColor(image, _grayMat, ColorConversionCodes.BGR2GRAY);

            const double maxValue = 255;
            var thresholdValue = threshold;

            // This is what filters out the background so we can measure the gaps between the bars.
            Cv2.Threshold(_grayMat, _binMat, thresholdValue, maxValue, ThresholdTypes.Binary);



            var barcodeResult = DecodeBarcode(_binMat);
            if (barcodeResult.BarcodeFound)
            {
                // treat negative as jitter/lead and only care about lag:
                barcodeResult.TDiff = Math.Abs(barcodeResult.TDiff);
                // Clamp to something sane 
                double x = Clamp(barcodeResult.TDiff, 0, 5000);

                ewma.Add(x);

                avgDelay = ewma.ValueMs;

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
                        TopRight = (barcodeResult.WaitTime < 1000), GcdTime = barcodeResult.GCD
                    }
                };

                _barCodeFound = true;
                if (barcodeResult.HasTarget || _ignoreTargetInfo == true)
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
        }
        finally
        {
            _grayMat.Dispose();
        }

        result.BarcodeFound = _barCodeFound;
        result.CurrentKeyToSend = currentKeyToSend;


        return (result, _binMat);
    }

    private void InitializeContinuousCaptureProcess()
    {
        // Define the area of the screen you want to capture
        int x = _capRegion.Left,
            y = _capRegion.Top,
            width = _capRegion.Width,
            height = _capRegion.Height;


        // Initialize CaptureScreen with the dispatcher and the UI update action
        var regions = new OpenCvSharp.Rect { X = x, Y = y, Width = width, Height = height };
        if (regions.X + regions.Width > ScreenMaxWidth || regions.Y + regions.Height > ScreenMaxHeight)
        {
            regions = new OpenCvSharp.Rect(0, 0, 10, 10);
        }
        
        
        // Create an instance of ContinuousScreenCapture with the CaptureScreen object
        if (_platform is not null)
        {
            _platform.ScreenCapture.CaptureRegion = regions;
            _continuousScreenCaptureProcess = new ContinuousScreenCapture(
                _platform.ScreenCapture,
            CurrentCaptureRateMs
            );
        }
        
    }



    private async Task RunKeyLoopAsync(CancellationToken token)
    {
        // ensures StartBackgroundLoops returns immediately
        await Task.Yield();

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (IsRunning && ActivationKeyPressed )
                    await ProcessBarCodeKey().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                SetActivationKeyPressed(false);
            }
            
            await Task.Delay(ActivationKeyPressed ? 2 : 10, token).ConfigureAwait(true);
        }
    }


    private async Task ProcessKey(KeyCommand? currentKey)
    {

       
            int CurrentDelay()
            {
                return (int)_currentImageRegions.FirstImageRegions.WaitTime +
                       (int)_currentImageRegions.FirstImageRegions.GcdTime -
                       (int)avgDelay;
            }


            bool keyDown = false;

            bool ctrlDown = false, altDown = false, shiftDown = false;
           
            // check if were currently processing another key
            if (!TryEnterProcessingKey()) return;
            
            int vkCode = 0;
            try
            {
                if (currentKey is null) return;

                if (UseGse)
                {
                    if (!AltPressed && !CtrlPressed)//if (!_currentImageRegions.FirstImageRegions.HasMultiTarget)
                    {
                        currentKey = new KeyCommand(_platform.Keycodes.GetTokenFromKeyCode(GseStVkKeyCode),
                            currentKey.MaxWaitTime, currentKey.HasTarget)
                        {
                            Alt = AltPressed,
                            Ctrl = CtrlPressed,
                            Shift = ShiftPressed
                        };
                    }
                    else
                    {
                        currentKey = new KeyCommand(_platform.Keycodes.GetTokenFromKeyCode(GseMtVkKeyCode),
                            currentKey.MaxWaitTime, currentKey.HasTarget)
                        {
                            Alt = AltPressed,
                            Ctrl = CtrlPressed,
                            Shift = ShiftPressed
                        };
                    }
                }


                if (!_platform!.ForegroundWindow.IsActiveWindow())
                    return;



                if (AltPressed &&
                    currentKey.Key ==
                    "F4") // Somehow AF4 got through and killed wow.   so I want to Explicitly ignore it.  I will never allow ALT-F4
                    return;

                var avgDelayLocalized = avgDelay;


                // Translate the char to the virtual Key Code
                vkCode = _platform.Keycodes.GetKeyCode(currentKey.Key);

                // Wows Default Key behavior is to activate as soon as the key is pressed.   So lets make sure we do not press anything till we have a 0 wait…
                // Pre-pressing is built into the addon calc  so we don't have to worry about command queuing here.
                // 50 seems to be a good magic number.  it stops the rappid key presses and doesn't extend things to long even in linux where it can skew the timeing massivly.
                while ((CurrentDelay() >= 150) && ActivationKeyPressed)
                {
                    await Task.Delay(10).ConfigureAwait(true);
                }


                
                
                // command is tied to CTRL or ALT So have to press them
                if (!UseGse)
                    if (currentKey.Ctrl )
                    {
                        _platform.Input.TrySendCtrlKey(true);
                        ctrlDown = true;
                    }
                    else
                    {
                        // Command isn't tied to CTRL so send a CTRL Up.
                        // This should really be peeking in the message buffer to see if the key is really pressed or not. and only send the up if it is. 
                        // This could also be accomplished by storing off the value in the message processor and storing a flag local if it saw one or not.
                        // keyboards are global so that may work.
                        _platform.Input.TrySendCtrlKey(false);
                    }
                if (!UseGse)
                    if (currentKey.Alt)
                    {
                        _platform.Input.TrySendAltKey(true);
                        altDown = true;
                    }
                    else
                    {
                        // See Notes on CTRL.
                        _platform.Input.TrySendAltKey(false);
                    }
                if (!UseGse)
                    if (currentKey.Shift)
                    {
                        _platform.Input.TrySendShiftKey(true);
                        shiftDown = true;
                    }
                    else
                    {
                        // See Notes on CTRL.
                        _platform.Input.TrySendShiftKey(false);
                    }


                // Press the command Key Down
                _platform.Input.TrySendKey(vkCode, true);
                keyDown = true;


                // CTRL and ALT do not need to be held down just only pressed initially for the command to be interpreted correctly
                if (!UseGse)
                    if (currentKey.Ctrl)
                    {
                        _platform.Input.TrySendCtrlKey(false);
                        ctrlDown = false;
                    }
                if (!UseGse)
                    if (currentKey.Alt)
                    {
                        _platform.Input.TrySendAltKey(false);
                        altDown = false;
                    }
                if (!UseGse)
                    if (currentKey.Shift)
                    {
                        _platform.Input.TrySendShiftKey(false);
                        shiftDown = false;
                    }

                // Add the keypress delay while monitoring that the activation key is still pressed (allows interrupting the delay)
                // Note:  There are 10000 ticks in a millisecond

                if (_keyPressMode)
                {
                    var anticipateWait = CurrentDelay();

                    // Wait time may be out of sync here.  this re-syncs the wait time.
                    while (anticipateWait >= avgDelayLocalized && ActivationKeyPressed)
                    {
                        await Task.Delay(5).ConfigureAwait(true);
                        anticipateWait = CurrentDelay();
                    }



                    while ((int)avgDelay < anticipateWait && ActivationKeyPressed)
                    {
                        await Task.Delay(16).ConfigureAwait(true);
                        anticipateWait = CurrentDelay();
                    }
                }
                else

                    // If where not watching for when things time out, we insert a hard delay
                    // This is no longer need as were putting a hard pause above
                {
                    // add some randomness to the keypress rate,  just in case of throttling for evenly repeated times
                    await Task.Delay(Random.Shared.Next() % 50 + CurrentKeyDownDelayMs);
                }


            }
            finally
            {

                if (keyDown) _platform!.Input.TrySendKey(vkCode, false);
                // always release modifiers if we put them down and they just so happen to be down still.  
                if (!UseGse)
                    if (ctrlDown) _platform!.Input.TrySendCtrlKey(false);
                if (!UseGse)
                    if (altDown) _platform!.Input.TrySendAltKey(false);
                if (!UseGse)
                    if (shiftDown) _platform!.Input.TrySendShiftKey(false);
                ExitProcessingKey();
            }


    }




    private async Task ProcessBarCodeKey()
    {
        if (!TryEnterBarcodeProcessing()) return;
 
        try
        {
            if (!IsRunning) return;
            if (!ActivationKeyPressed) return;
          
            #region WaitFor a Key to show up

            // let's just hang out here till we have a key
            var currentD = DateTime.Now;
            var keyToSendFirst = _currentKeyToSend;
          
            while (String.IsNullOrEmpty(keyToSendFirst) && IsRunning && ActivationKeyPressed)
            {
                await Task.Delay(5).ConfigureAwait(true);
                keyToSendFirst = _currentKeyToSend;
            
                if (currentD.AddMilliseconds(15000) < DateTime.Now) return;
            }


            if (!_platform.Keycodes.HasKey(keyToSendFirst)) return;
            


            #endregion

          
            await ProcessKey(new KeyCommand(keyToSendFirst, _currentImageRegions.FirstImageRegions.WaitTime,
                _currentImageRegions.FirstImageRegions.HasTarget));
        }
        finally
        {
            ExitBarcodeProcessing();
        }

        await Task.Delay(5).ConfigureAwait(true);

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

        _platform.Hotkeys.ActivateKeyChangedAsync += HotkeysOnActivateKeyChangedAsync;
        _platform.Hotkeys.ShiftChangedAsync += HotkeysOnShiftChangedAsync;
        _platform.Hotkeys.CtrlChangedAsync += HotkeysOnCtrlChangedAsync;
        _platform.Hotkeys.AltChangedAsync += HotkeysOnAltChangedAsync;
        
    }

    private void SetControlsToSavedSettings()
    {
        CbUseGse.IsChecked = AppSettings.UseGse;
        UseGse =  AppSettings.UseGse;

        var cbitemSt = (CbGseKeyBindSt.Items[AppSettings.GseStKey] as ComboBoxItem).Content.ToString();
        CbGseKeyBindSt.SelectedValue = CbGseKeyBindSt.Items[AppSettings.GseStKey];
        GseStVkKeyCode = _platform.Keycodes.GetKeyCode(cbitemSt);

        var cbitemMt = (CbGseKeyBindMt.Items[AppSettings.GseMtKey] as ComboBoxItem).Content.ToString();
        CbGseKeyBindMt.SelectedValue = CbGseKeyBindMt.Items[AppSettings.GseMtKey];
        GseMtVkKeyCode = _platform.Keycodes.GetKeyCode(cbitemMt); 




        TbCaptureRateMs.Text = AppSettings.CaptureRateMs.ToString(); 
        SliderCaptureRateMs.Value = AppSettings.CaptureRateMs;
        CurrentCaptureRateMs = AppSettings.CaptureRateMs;

        TbKeyRateMs.Text = AppSettings.KeyPressSpeedMs.ToString();

        SliderKeyRateMs.Value = AppSettings.KeyPressSpeedMs;
        CurrentKeyDownDelayMs = AppSettings.KeyPressSpeedMs;


        CbPushRelease.IsChecked = AppSettings.PushAndRelease;
        _keyPressMode = AppSettings.PushAndRelease;

        CbStayOnTop.IsChecked = AppSettings.KeepOnTop;
        
        if (AppSettings.IgnoreTargetingInfo)
        {
            CbIgnoreTargetInfo.IsChecked = true;
            _ignoreTargetInfo = true;
        }
        else
        {
            CbIgnoreTargetInfo.IsChecked = false;
            _ignoreTargetInfo = false;

        }
      



        foreach (var x in CbActivationKey.Items)
        {

            if ((x as ComboBoxItem)?.Content?.ToString() == AppSettings.ActivationKey)
            {
                CbActivationKey.SelectedItem = x;
                ProcessActivateKey =  AppSettings.ActivationKey;
                // Make sure we restore the capture state
                //var lastPlatformState = _platform!.Hotkeys.isStarted();

                _platform!.Hotkeys.SetHotkey(ProcessActivateKey);
            }
        }

    }


    private void OnStartup ()
    {

        AppSettings = SettingsManager.LoadSettings();


        
        
        this.Topmost = AppSettings.KeepOnTop;




        Position = new PixelPoint((int)AppSettings.AppStartX, (int)AppSettings.AppStartY);


        InitializePlatform(ProcessActivateKey);
        SetControlsToSavedSettings();
        
        _platform!.ScreenCapture.CaptureRegion = _capRegion;
        _platform!.ScreenCapture.EnableFullScreen = false;
        _platform!.ScreenCapture.EnableRegion = false;

        _platform!.ForegroundWindow.SetWindowToFind("World of Warcraft");
      
        _platform!.ScreenCapture.OnRegionUpdated += ScreenCaptureOnRegionUpdated;
        _platform!.ScreenCapture.OnFullScreenUpdated += ScreenCaptureOnFullScreenUpdated;

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
        // This makes sure we do the initial capture to check for a barcode.
        _platform.ScreenCapture.EnableFullScreen = true;

        Initializing = false;
    }




    #region Key Event Handlers
    private void HotkeysOnActivateKeyChangedAsync(HotkeyActionResult obj)
    {
        
        if (obj.KeyState == HotkeyState.PRESSED)
            SetActivationKeyPressed(true);
        if (obj.KeyState == HotkeyState.RELEASED)
            SetActivationKeyPressed(false);

    }
    
    private void HotkeysOnCtrlChangedAsync(HotkeyActionResult obj)
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
    

    private void HotkeysOnAltChangedAsync(HotkeyActionResult obj)
    {
        if (obj.KeyState == HotkeyState.PRESSED)
            AltPressed = true;
        if (obj.KeyState == HotkeyState.RELEASED)
            AltPressed = false;
    }
    
    
    #endregion

    private void ScreenCaptureOnFullScreenUpdated(Mat image)
    {
        
        // swap the values and dispose of the old value.  thread-safe
        var old = Interlocked.Exchange(ref _fullScreenMat, image);
        if (old is { IsDisposed: false })
            old.Dispose();
        var r = DecodeFind(_fullScreenMat);

        try
        {
            if (r.screenID < 1)
            {
                return;
            }

            // Get the window's current location
            _capRegion.Left = r.X; // / dpiX;
            _capRegion.Top = r.Y; // / dpiY;
            _capRegion.Width = r.Width; // / dpiX;
            _capRegion.Height = r.Height; // / dpiY;
            _platform!.ScreenCapture!.CaptureRegion = _capRegion;
            _barCodeFound = true;
        }

        finally
        {
            //We could do this as a using.   But I like the try, it helps me visualize when we destroy and what context.
            // We only want to capture the full screen once then stop.   if it needs it again,  it will be triggered set to true elsewhere.
            _platform!.ScreenCapture.EnableFullScreen = false;
            GC.Collect();

        }
        
    }

    private readonly object _screenFlipLock = new();

    private void ScreenCaptureOnRegionUpdated(Mat image)
    {
        //lock (_screenFlipLock)
        {
            var old = Interlocked.Exchange(ref _capRegionMat, image);
            if (old is { IsDisposed: false })
                old.Dispose();
            double threshold = 20; //CurrentThreshold == 0 ? 0.0 : CurrentThreshold / 100;
            var (capResult, binImage) = ProcessImageOpenCv(_capRegionMat, threshold);

            // This part updates the visual stuff so make sure we launch it on the UIThread.
            // Push the new image out the first image,  this has the markers and delays
            Dispatcher.UIThread.Post(() =>
            {
                _currentImageRegions.FirstImageRegions.TopRight = capResult.regions.TopRight;
                _currentImageRegions.FirstImageRegions.TopLeft = capResult.regions.TopLeft;
                _currentImageRegions.FirstImageRegions.BottomLeft = capResult.regions.BottomLeft;
                _currentImageRegions.FirstImageRegions.BottomCenter = capResult.regions.BottomCenter;
                _currentImageRegions.FirstImageRegions.HasTarget = capResult.regions.HasTarget;
                _currentImageRegions.FirstImageRegions.WaitTime = capResult.regions.WaitTime;
                _currentImageRegions.FirstImageRegions.GcdTime = capResult.regions.GcdTime;
                _currentKeyToSend = capResult.CurrentKeyToSend;
                if (capResult.BarcodeFound)
                {
                    if (!binImage.IsDisposed)
                    {
                        UpdatePreview(binImage);
                    }
                    // Update the label

                    _lastDetectedValue = capResult.CurrentKeyToSend;
                    _lastDetectedTime = capResult.WaitTime.ToString();
                    LDetectedValue.Text = _lastDetectedValue;
                    LDetectedTime.Text = _lastDetectedTime;
                    LSkew.Text = ((int)avgDelay).ToString() + "MS";
                    LGcdWait.Text = capResult.regions.GcdTime.ToString();
                    if (!binImage.IsDisposed)
                    {
                        binImage.Dispose();
                    }
                }
                else
                {
                    //Cv2.ImShow("Error", _capRegionMat);
                    if (!_capRegionMat.IsDisposed)
                    {
                        UpdatePreview(_capRegionMat);
                    }

                    // Use our last values.
                    // Frame doubling or smoothing can cause the barcode to jitter.
                    // No need to panic the keysend with N/A.
                    LDetectedValue.Text = _lastDetectedValue;
                    LDetectedTime.Text = _lastDetectedTime;
                    LSkew.Text = ((int)avgDelay).ToString() + "MS";
                    LGcdWait.Text = "0";
                }
            });
            // swap the values and dispose of the old value.  thread-safe



        }

    }


    private async Task RunBarcodeMonitorLoopAsync(CancellationToken token)
    {
        await Task.Yield();

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_continuousScreenCaptureProcess != null &&
                    _continuousScreenCaptureProcess.IsCapturing &&
                    !_barCodeFound)
                {
                    AttemptToFindBarcode(); // just flips EnableFullScreen = true and the rest trickles down in events.
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                _barCodeFound = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(true);
        }
    }

    private async Task RunWowMonitorLoopAsync(CancellationToken token)
    {
        await Task.Yield();

        while (!token.IsCancellationRequested)
        {
            try
            {
                _platform?.ForegroundWindow.SetWindowToFind("World of Warcraft");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(true);
        }
    }

    private void AttemptToFindBarcode()
    {
        _platform!.ScreenCapture.EnableFullScreen = true;
    }



    #region UI Event handlers


    private async void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        try
        {


            if (_continuousScreenCaptureProcess!.IsCapturing)
            {
                Volatile.Write(ref _isRunning, 0); // stop
                _platform!.ScreenCapture.EnableRegion = false;
                await _continuousScreenCaptureProcess.StopCaptureAsync(); 
                _currentKeyToSend = string.Empty;
                SetActivationKeyPressed(false);
                await StopBackgroundLoopsAsync();
                if (_platform!.Hotkeys != null)
                    _platform!.Hotkeys.Stop();
                BStart.IsEnabled = true;
                BStop.IsEnabled = false;
                _barCodeFound = false;


            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            //throw; // TODO handle exception
        }
    }

    private void Capture_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        // Optional debug capture. The UI button is hidden by default.
        var filePath = ".\\captures\\Cap" + DateTime.Now.ToBinary() + ".png";
            
        // TODO:  this needs to use the OpenCV save not the control.   This should just grab the current capture frame and save it as a PNG.
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
        if (Initializing) return;
        if (SliderCaptureRateMs is null) return;
        AppSettings.CaptureRateMs = (int)SliderCaptureRateMs.Value;
        CurrentCaptureRateMs = (int)SliderCaptureRateMs.Value;
        if (TbCaptureRateMs != null)
            TbCaptureRateMs.Text = ((int)SliderCaptureRateMs.Value).ToString();
        if (_continuousScreenCaptureProcess != null)
            _continuousScreenCaptureProcess.CaptureIntervalMs = (int)SliderCaptureRateMs.Value;

    }

    
    private void sliderKeyRateMS_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        if (SliderKeyRateMs is null) return;
        AppSettings.KeyPressSpeedMs = (int)SliderKeyRateMs.Value;
        CurrentKeyDownDelayMs = (int)SliderKeyRateMs.Value;
        if (TbKeyRateMs != null)
            TbKeyRateMs.Text = ((int)SliderKeyRateMs.Value).ToString();

    }

    private void tbKeyRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;

        if (sender is not TextBox tb || !int.TryParse(tb.Text, out var v)) return;
        {
            if (v > 1000) v = 1000;
            if (v < 5) v = 5;
            SliderKeyRateMs.Value = v;
        }
    }

    private void tbCaptureRateMS_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;

        if (sender is TextBox tb && int.TryParse(tb.Text, out var v))
        {
            if (v > 250) v = 250;
            if (v < 5) v = 5;
            SliderCaptureRateMs.Value = v;
        }
    }

    private void cbActivationKey_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        
        var activationKey = (CbActivationKey.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(activationKey)) return;
        AppSettings.ActivationKey = activationKey;
        ProcessActivateKey = AppSettings.ActivationKey;
        // Make sure we restore the capture state
        //var lastPlatformState = _platform!.Hotkeys.isStarted();
        
        _platform!.Hotkeys.SetHotkey(ProcessActivateKey);

        //InitializePlatform(ProcessActivateKey);
        //if (lastPlatformState) _platform.Hotkeys.Start();
    }

    private void bResetMagPosition_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        AppSettings.CapX = 50;
        AppSettings.CapY = 50;
        AppSettings.CapWidth = 100;
        AppSettings.CapHeight = 100;
        
    }

    private async void bFindBarcode_Click(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
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
        



    private void cbPushRelease_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
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
        if (Initializing) return;
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
        if (Initializing) return;
        if ((e.Source as CheckBox)!.IsChecked == null)
        {
            AppSettings.IgnoreTargetingInfo = true;
            _ignoreTargetInfo = true;
        }
        else
        {
            if (((CheckBox)e.Source).IsChecked == true )
            {
                AppSettings.IgnoreTargetingInfo = true;
                _ignoreTargetInfo = true;
            }
            else
            {
                AppSettings.IgnoreTargetingInfo = false;
                _ignoreTargetInfo = false;
            }
        }

    }
   
    private void CbGseKeyBindMt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        AppSettings.GseMtKey = (e.Source as ComboBox)!.SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                GseMtVkKeyCode = _platform.Keycodes.GetKeyCode(((e.Source as ComboBox)!.SelectedItem as ComboBoxItem)!.Content!.ToString()!);
            }
        }
    }

    private void CbGseKeyBindSt_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
        AppSettings.GseStKey = (e.Source as ComboBox)!.SelectedIndex;
        if (e.Source != null)
        {
            if (((ComboBox)e.Source).SelectedItem != null)
            {
                GseStVkKeyCode = _platform.Keycodes.GetKeyCode(((e.Source as ComboBox)!.SelectedItem as ComboBoxItem)!.Content!.ToString()!);
            }
        }
    }

    private void CbUseGse_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (IsDesigner) return;
        if (Initializing) return;
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
    
    private async void Button_Start_OnClick(object? sender, RoutedEventArgs e)
    {
      
        if (IsDesigner) return;
        if (Initializing) return;
        // Start the continuous capturing
        if (!_continuousScreenCaptureProcess!.IsCapturing)
        {

            _currentKeyToSend = "";
            _platform!.ScreenCapture.EnableRegion = true;
            _continuousScreenCaptureProcess.StartCapture();
            _platform!.Hotkeys.Start();

            _ = Task.Run(async () =>
            {
                StartBackgroundLoops();
            });
            BStart.IsEnabled = false;
            BStop.IsEnabled = true;
            Volatile.Write(ref _isRunning, 1); // start
        }

    }
    #endregion
        



        
        
        
    private bool _started;
    private string _lastDetectedValue  = "N/A";
    private string _lastDetectedTime  = "N/A";


    public MainWindow()
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;
        if (_started) return;

        // This is posting to the UI thread mainly as that makes sure
        // the init of the components is done before calls are made.
        Dispatcher.UIThread.Post(() =>
        {
            if (_started) return;
            _started = true;
            OnStartup();
        }, DispatcherPriority.Loaded);
    }

    protected override async void OnClosed(EventArgs e)
    {
        try
        {
            await Cleanup();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            //throw; // TODO handle exception
        }
        finally
        {
            base.OnClosed(e);
        }
    }
    
    private async Task<bool> Cleanup()
    {
        if (Design.IsDesignMode) return true;

        //GrayMatHolder.Dispose();
        //BinaryMatHoler.Dispose();
        AppSettings.CapX = _capRegion.Left;
        AppSettings.CapY = _capRegion.Top;
        AppSettings.CapWidth = _capRegion.Width;
        AppSettings.CapHeight = _capRegion.Height;

        SettingsManager.SaveSettings(AppSettings);
        await StopBackgroundLoopsAsync();
        
        if (_continuousScreenCaptureProcess!.IsCapturing)
        {
            await _continuousScreenCaptureProcess.StopCaptureAsync();
        }


        _platform!.Hotkeys.Stop();
        return true;

    }


}
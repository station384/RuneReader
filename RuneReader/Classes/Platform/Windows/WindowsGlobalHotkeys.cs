#if WINDOWS
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RuneReader.Classes.Utilities;

namespace RuneReader.Classes.Platform.Windows;

public class WindowsGlobalHotkeys : IGlobalHotkeys
{
    private static IntPtr _hookId = IntPtr.Zero;
    private static IntPtr _mouseHookId = IntPtr.Zero;
    private static WindowsApiCalls.WindowsMessageProc? _proc;
    private bool _started = false;

    private string? _activationKey = null;
    
    private IntPtr HookCallbackActionKey(int nCode, IntPtr wParam, IntPtr lParam)
    {

        nint result = 0;
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            var item = ActivationKeyCodeMapper.GetVirtualKeyCode(_activationKey);


            if (wParam == (IntPtr)WindowsApiCalls.WM_KEYDOWN)
            {
                var state = HotkeyState.PRESSED;
                if (vkCode == WindowsApiCalls.VK_LCONTROL)
                {
                    // Fire Async Version
                    if (CtrlChangedAsync is not null)
                        _ = Task.Run(() => CtrlChangedAsync(new HotkeyActionResult(state)));
                    // Fire Sync Version
                    CtrlChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
                
                if ( vkCode == WindowsApiCalls.VK_LMENU)
                {
                    if (AltChangedAsync is not null)
                        _ = Task.Run(() => AltChangedAsync(new HotkeyActionResult(state)));

                    AltChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
                
                if (vkCode == WindowsApiCalls.VK_LSHIFT)
                {
                    if (ShiftChangedAsync is not null)
                        _ = Task.Run(() => ShiftChangedAsync(new HotkeyActionResult(state)));
                    ShiftChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
                
                if ( vkCode == item)
                {
                    if (ActivateKeyChangedAsync is not null)
                        _ = Task.Run(() => ActivateKeyChangedAsync(new HotkeyActionResult(state)));
                    ActivateKeyChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
            }



            if (wParam == (IntPtr)WindowsApiCalls.WM_KEYUP)
            { 
                var state = HotkeyState.RELEASED;
                if ( vkCode == WindowsApiCalls.VK_LCONTROL)
                {

                    if (CtrlChangedAsync is not null)
                        _ = Task.Run(() => CtrlChangedAsync(new HotkeyActionResult(state)));
                    CtrlChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                } 
                if ( vkCode == WindowsApiCalls.VK_LMENU)
                {
                    if (AltChangedAsync is not null)
                        _ = Task.Run(() => AltChangedAsync(new HotkeyActionResult(state)));
                    AltChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
                if ( vkCode == WindowsApiCalls.VK_LSHIFT)
                {
                    if (ShiftChangedAsync is not null)
                        _ = Task.Run(() => ShiftChangedAsync(new HotkeyActionResult(state)));
                    ShiftChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
                if ( vkCode == item)
                {
                    if (ActivateKeyChangedAsync is not null)
                        _ = Task.Run(() => ActivateKeyChangedAsync(new HotkeyActionResult(state)));
                    ActivateKeyChanged?.Invoke(new HotkeyActionResult(state));
                    return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam); 
                }
            }
            
            // NOTES:  For future self -  READ THIS!! DONT SKIM IT.  WILL SAVE TIME!!!!
            // since "return result = WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam);" 
            // always confused the crap out of me this is what it does.
            // If we do a return 1 that will consume the call chain.  the event will not be forward to the hooked window.
            // if we do a return WindowsApiCalls.CallNextHookEx(_hookId, nCode, wParam, lParam);
            // that will call the next hook in the chain.   and return 0 to the calling function.
            // this in essence allows us to "monitor" the event chain and react to it without consuming any events.  

 
        }
        

        return result;

    }
    
    private IntPtr SetHookActionKey(WindowsApiCalls.WindowsMessageProc proc)
    {
        var result = IntPtr.Zero;
        using Process curProcess = Process.GetCurrentProcess();
        if (curProcess.MainModule == null) return result;
        using ProcessModule curModule = curProcess.MainModule;
        result = WindowsApiCalls.SetWindowsHookEx(WindowsApiCalls.WH_KEYBOARD_LL, proc, WindowsApiCalls.GetModuleHandle(curModule.ModuleName), 0);

        return result;
    }
    
    private void SetHook()
    {
        if (_proc != null || _hookId != IntPtr.Zero) return;
        _proc = HookCallbackActionKey;
        _hookId = _hookId == IntPtr.Zero ? SetHookActionKey(_proc) : IntPtr.Zero;
    }

    private void RemoveHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            WindowsApiCalls.UnhookWindowsHookEx(_hookId);
            _proc = null;
            _hookId = IntPtr.Zero;
        }
    }

        

        
    public void Dispose()
    {
        Stop();
    }

    public event Action<HotkeyActionResult>? ActivateKeyChanged;
    public event Action<HotkeyActionResult>? CtrlChanged;
    public event Action<HotkeyActionResult>? AltChanged;
    public event Action<HotkeyActionResult>? ShiftChanged;
    
    public event Action<HotkeyActionResult>? ActivateKeyChangedAsync;
    public event Action<HotkeyActionResult>? CtrlChangedAsync;
    public event Action<HotkeyActionResult>? AltChangedAsync;
    public event Action<HotkeyActionResult>? ShiftChangedAsync;

    public bool isStarted()
    {
        return _started;
    }

    public void Start()
    {
        SetHook();
        _started = true;
    }

    public void Stop()
    {
      RemoveHook();
      _started = false;
    }

    public void SetHotkey (string? activationKey)
    {
        if (activationKey != null)
          _activationKey = activationKey;
    }


    public WindowsGlobalHotkeys(string? activationKey)
    {
        _activationKey = activationKey;
    }
}
#endif
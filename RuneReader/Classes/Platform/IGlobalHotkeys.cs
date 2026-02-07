using System;

namespace RuneReader.Classes.Platform;

public interface IGlobalHotkeys : IDisposable
{
    public event Action<HotkeyActionResult>? ActivateKeyChanged;

    public event Action<HotkeyActionResult> CtrlChanged;
    public event Action<HotkeyActionResult> AltChanged;
    public event Action<HotkeyActionResult> ShiftChanged;
    
    public event Action<HotkeyActionResult>? ActivateKeyChangedAsync;

    public event Action<HotkeyActionResult> CtrlChangedAsync;
    public event Action<HotkeyActionResult> AltChangedAsync;
    public event Action<HotkeyActionResult> ShiftChangedAsync;
    public bool isStarted(); 
    public void Start();
    public void Stop();

}
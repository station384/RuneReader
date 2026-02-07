using System;

namespace RuneReader.Classes.Platform.Linux
{
    public class LinuxGlobalHotkeys : IGlobalHotkeys
    {
        
        private readonly object? _proc = null;
        private readonly object? _mouseProc = null;

        public void Dispose()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
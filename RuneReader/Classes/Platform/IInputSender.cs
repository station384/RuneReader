using System;

namespace RuneReader.Classes.Platform;

public interface IInputSender : IDisposable
{
    bool TrySendKey(int key, bool pressed);
    bool TrySendCtrlKey(bool pressed);
    bool TrySendAltKey(bool pressed);
    bool TrySendShiftKey(bool pressed);
    
}
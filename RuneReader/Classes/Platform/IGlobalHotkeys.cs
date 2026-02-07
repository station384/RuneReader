using System;

namespace RuneReader.Classes.Platform;

public interface IGlobalHotkeys : IDisposable
{
    event Action<int>? KeyDown;
    event Action<int>? KeyUp;
    void Start();
    void Stop();
}
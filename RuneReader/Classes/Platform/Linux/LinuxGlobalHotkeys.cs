using System;

namespace RuneReader.Classes.Platform.Linux
{
    public class LinuxGlobalHotkeys : IGlobalHotkeys
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public event Action<int>? KeyDown;
        public event Action<int>? KeyUp;
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
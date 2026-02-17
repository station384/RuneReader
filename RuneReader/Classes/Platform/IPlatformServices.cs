namespace RuneReader.Classes.Platform;

public interface IPlatformServices
{
    IGlobalHotkeys Hotkeys { get; }
    IForegroundWindow ForegroundWindow { get; }
    IInputSender Input { get; }
    IScreenCaptureProvider ScreenCapture { get; }
    IKeycodeMapper Keycodes { get; }   
}
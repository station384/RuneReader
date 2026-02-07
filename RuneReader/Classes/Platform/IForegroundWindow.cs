namespace RuneReader.Classes.Platform;

public interface IForegroundWindow
{
    string? GetActiveWindowTitle();
    bool IsActiveWindowTitle(string expectedTitle);
}
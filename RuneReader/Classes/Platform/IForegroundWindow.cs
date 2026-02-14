using System;

namespace RuneReader.Classes.Platform;

public interface IForegroundWindow : IDisposable
{
    /// <summary>
    /// checks if the window exists
    /// </summary>
    /// <returns>
    /// true - window was found
    /// false - window not found
    /// </returns>
    bool IsWindowFound();
    
    /// <summary>
    /// Sets the window title to search for
    /// </summary>
    /// <param name="windowName"></param>
    /// <returns></returns>
    void SetWindowToFind(string windowName);
    
    /// <summary>
    /// Return the title of the window to search for.
    /// </summary>
    /// <returns>Widow title string</returns>
    string? GetWindowTitle();

    /// <summary>
    /// Returns the current window handle.
    /// </summary>
    /// <returns></returns>
    public IntPtr GetWindowHandle();
    
    /// <summary>
    /// Returns the title of the currently focused window
    /// </summary>
    /// <returns>
    /// Title of the current focused window
    /// </returns>
    string? GetActiveWindowTitle();
    
    /// <summary>
    /// Checks if the currently active window matches the window to search for
    /// </summary>
    /// <returns></returns>
    bool IsActiveWindow();
    
}
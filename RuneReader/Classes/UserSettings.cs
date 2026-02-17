using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RuneReader.Classes;

public class UserSettings
{
    public double CapX { get; set; } = 0;
    public double CapY { get; set; } = 50;
    public double CapWidth { get; set; } = 50;
    public double CapHeight { get; set; } = 100;

    public double AppStartX { get; set; } = 150;
    public double AppStartY { get; set; } = 150;

    public string SaveFontName { get; set; } = "PT_Sans";

    public int SaveFontSizeX { get; set; } = 50;
    public int SaveFontSizeY { get; set; } = 50;
    public string ActivationKey { get; set; } = "1";
    public bool ActivationModeSendOnPress { get; set; } = true;
    public double VariancePercent { get; set; } = 20;
    public int CaptureRateMs { get; set; } = 32;  // 30 Frames Per Second
    public int KeyPressSpeedMs { get; set; } = 150;
    public bool PushAndRelease { get; set; } = true;
    public bool KeepOnTop { get; set; } = false;

    public double WowGamma { get; set; } = 1.2;

    public bool PetKeyEnables { get; set; } = false;

    public int PetKey { get; set; } = 0;

    public bool IgnoreTargetingInfo { get; set; } = false;
    public bool IsFirstRun { get; set; } = false;
    
    public int GseMtKey { get; set; } = 0;
    public int GseStKey { get; set; } = 0;
    public bool UseGse { get; set; } = false;


}
public static class SettingsManager
{
    private const string AppName = "RuneReader";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonSaveOptions = new() { WriteIndented = true };

    private static string SettingsFilePath => Path.Combine(GetConfigDirectory(), SettingsFileName);

    private static string GetConfigDirectory()
    {
        try
        {
            // todo this section needs to be moved to being a platform since this can be platform specific.
            if (OperatingSystem.IsLinux())
            {
                // Prefer XDG_CONFIG_HOME, else ~/.config
                var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (!string.IsNullOrWhiteSpace(xdg))
                    return Path.Combine(xdg, AppName);

                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".config", AppName);
            }

            // Windows + macOS default behavior is fine here:
            // Windows: %AppData%
            // macOS: ~/Library/Application Support
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // If baseDir is empty for some reason, fall back to user profile
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(baseDir, AppName);
        }
        catch
        {
            // Last-resort fallback to current directory (not ideal, but prevents "nothing saved")
            return Path.Combine(AppContext.BaseDirectory, AppName);
        }
    }

    public static UserSettings LoadSettings()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path))
                return new UserSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading settings from '{SettingsFilePath}': {ex}");
            return new UserSettings();
        }
    }

    public static async Task<UserSettings> LoadSettingsAsync()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path))
                return new UserSettings();

            await using var fs = File.OpenRead(path);
            return (await JsonSerializer.DeserializeAsync<UserSettings>(fs)) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading settings from '{SettingsFilePath}': {ex}");
            return new UserSettings();
        }
    }

    public static void SaveSettings(UserSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);

            var tmp = SettingsFilePath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonSaveOptions);

            File.WriteAllText(tmp, json);

            // Atomic-ish replace
            if (File.Exists(SettingsFilePath))
                File.Replace(tmp, SettingsFilePath, null);
            else
                File.Move(tmp, SettingsFilePath);

            Debug.WriteLine($"Saved settings to '{SettingsFilePath}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings to '{SettingsFilePath}': {ex}");
        }
    }

    public static async Task SaveSettingsAsync(UserSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);

            var tmp = SettingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(settings, JsonSaveOptions));

            if (File.Exists(SettingsFilePath))
                File.Replace(tmp, SettingsFilePath, null);
            else
                File.Move(tmp, SettingsFilePath);

            Debug.WriteLine($"Saved settings to '{SettingsFilePath}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings to '{SettingsFilePath}': {ex}");
        }
    }
}

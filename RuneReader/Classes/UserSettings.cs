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
    public int CaptureRateMs { get; set; } = 30;
    public int KeyPressSpeedMs { get; set; } = 500;
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
    // This will need to be updated I think for linux.
    private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RuneReaderSettings.json");
    private static readonly JsonSerializerOptions JsonSaveOptions = new() { WriteIndented = true };
    public static async Task<UserSettings?> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                await using FileStream fs = File.OpenRead(SettingsFilePath);
                return await JsonSerializer.DeserializeAsync<UserSettings>(fs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error reading settings: " + ex);
        }
        // Return default settings if file doesn't exist or an error occurs.
        return new UserSettings();
    }

    public static UserSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                using FileStream fs = File.OpenRead(SettingsFilePath);
                return JsonSerializer.Deserialize<UserSettings>(fs)!;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error reading settings: " + ex);
        }
        // Return default settings if file doesn't exist or an error occurs.
        return new UserSettings();
    }




    public static async Task SaveSettingsAsync(UserSettings settings)
    {
        try
        {
            // Optionally create the file's directory if it doesn't exist.
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir == null)
            {
                throw new DirectoryNotFoundException();
            }
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using FileStream fs = File.Create(SettingsFilePath);
            await JsonSerializer.SerializeAsync(fs, settings, JsonSaveOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error saving settings: " + ex);
        }
    }

    public static void SaveSettings(UserSettings settings)
    {
        try
        {
            // Optionally create the file's directory if it doesn't exist.
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir == null)
            {
                throw new DirectoryNotFoundException();
            }
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using FileStream fs = File.Create(SettingsFilePath);
            JsonSerializer.Serialize(fs, settings, JsonSaveOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error saving settings: " + ex);
        }
    }
}
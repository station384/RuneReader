using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace RuneReader.Classes
{
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
        public int CaptureRateMS { get; set; } = 30;
        public int KeyPressSpeedMS { get; set; } = 500;
        public bool PushAndRelease { get; set; } = true;
        public bool KeepOnTop { get; set; } = false;

        public double WowGamma { get; set; } = 1.2;

        public bool PetKeyEnables { get; set; } = false;

        public int PetKey { get; set; } = 0;

        public bool IgnoreTargetingInfo { get; set; } = false;
        public bool IsFirstRun { get; set; } = false;


    }

    public static class SettingsManager
    {
        // Step 2: Determine the file location in the user's Documents folder.
        private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RuneReaderSettings.json");

        // Step 3a: Load settings from file (if available).
        public static async Task<UserSettings> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    using FileStream fs = File.OpenRead(SettingsFilePath);
                    return await JsonSerializer.DeserializeAsync<UserSettings>(fs);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading settings: " + ex.Message);
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
                    return JsonSerializer.Deserialize<UserSettings>(fs);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading settings: " + ex.Message);
            }
            // Return default settings if file doesn't exist or an error occurs.
            return new UserSettings();
        }



        // Step 3b: Save settings to file.
        public static async Task SaveSettingsAsync(UserSettings settings)
        {
            try
            {
                // Optionally create the file's directory if it doesn't exist.
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using FileStream fs = File.Create(SettingsFilePath);
                await JsonSerializer.SerializeAsync(fs, settings, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                // Optionally create the file's directory if it doesn't exist.
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using FileStream fs = File.Create(SettingsFilePath);
                JsonSerializer.Serialize(fs, settings, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }
    }


}

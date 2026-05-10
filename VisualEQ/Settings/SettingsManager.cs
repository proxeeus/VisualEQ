using System;
using System.IO;
using System.Text.Json;

namespace VisualEQ.Settings
{
    public static class SettingsManager
    {
        static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualEQ");

        static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            try
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load settings ({ex.Message}), using defaults.");
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save settings: {ex.Message}");
            }
        }

        public static string SettingsPath => SettingsFile;
    }
}

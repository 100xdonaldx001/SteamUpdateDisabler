using System;
using System.IO;
using System.Text.Json;

namespace SteamManifestToggler
{
    public class AppConfigData
    {
        public string? SteamRoot { get; set; }
    }

    public static class AppConfig
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        private static readonly string ConfigFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamUpdateDisabler");

        private static readonly string ConfigPath = Path.Combine(ConfigFolderPath, "config.json");

        public static AppConfigData Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var data = JsonSerializer.Deserialize<AppConfigData>(json, Options);
                    if (data != null) return data;
                }
            }
            catch
            {
                // ignore corrupt config files and recreate them later
            }

            return new AppConfigData();
        }

        public static void Save(AppConfigData data)
        {
            try
            {
                Directory.CreateDirectory(ConfigFolderPath);
                var json = JsonSerializer.Serialize(data ?? new AppConfigData(), Options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        public static string ConfigFolder => ConfigFolderPath;
    }
}
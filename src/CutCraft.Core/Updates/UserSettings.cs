using System.IO;
using System.Text.Json;

namespace CutCraft.Core.Updates;

public class UserSettings
{
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public string? SkippedVersion { get; set; }
    public DateTime? LastUpdateCheckTime { get; set; }
    public string? LastInstalledVersion { get; set; }

    public static string DefaultSettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CutCraft");

    public static string DefaultSettingsFilePath =>
        Path.Combine(DefaultSettingsDirectory, "settings.json");

    public static UserSettings Load(string? customPath = null)
    {
        var path = customPath ?? DefaultSettingsFilePath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Fallback to defaults if read fails
        }

        // Also check local fallback in BaseDirectory if default doesn't exist
        if (customPath == null)
        {
            var localFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            try
            {
                if (File.Exists(localFallback))
                {
                    var json = File.ReadAllText(localFallback);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch { }
        }

        return new UserSettings();
    }

    public void Save(string? customPath = null)
    {
        var path = customPath ?? DefaultSettingsFilePath;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Fallback: attempt saving next to application
            try
            {
                var localFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(localFallback, json);
            }
            catch { }
        }
    }
}

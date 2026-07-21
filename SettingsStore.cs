using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YouTube4KDownloader;

public sealed class SettingsStore
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YouTube4KDownloader");

    public string SettingsPath => Path.Combine(_directory, "settings.json");
    public string HistoryPath => Path.Combine(_directory, "history.json");

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public List<DownloadHistoryItem> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
                return JsonSerializer.Deserialize<List<DownloadHistoryItem>>(File.ReadAllText(HistoryPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void SaveHistory(IEnumerable<DownloadHistoryItem> items)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(HistoryPath, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

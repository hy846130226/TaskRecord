using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TaskRecorder.Services;

public class AppSettings
{
    public bool IsFilterCompleted { get; set; }
    public bool IsFilterIncludeSubtasks { get; set; }
    public bool IsHighlightRecent { get; set; }
    public List<string> TodayTaskIds { get; set; } = new();
    public string TodayDate { get; set; } = "";
}

public class SettingsService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, s_jsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, s_jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silent fail for settings
        }
    }
}

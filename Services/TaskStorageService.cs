using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using TaskRecorder.Models;

namespace TaskRecorder.Services;

public class TaskStorageService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public TaskStorageService(string filePath)
    {
        _filePath = filePath;
    }

    public List<TaskItem> Load()
    {
        if (!File.Exists(_filePath))
            return new List<TaskItem>();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<TaskItem>>(json, s_jsonOptions) ?? new List<TaskItem>();
        }
        catch (JsonException)
        {
            var bakPath = _filePath + ".bak";
            if (File.Exists(bakPath))
                File.Delete(bakPath);
            File.Move(_filePath, bakPath);
            MessageBox.Show($"任务文件已损坏，已备份至:\n{bakPath}\n\n将以空列表启动。",
                "数据错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new List<TaskItem>();
        }
    }

    public void Save(List<TaskItem> tasks)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(tasks, s_jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

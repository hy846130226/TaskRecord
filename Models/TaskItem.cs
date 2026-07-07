using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TaskRecorder.Models;

public class TaskItem : INotifyPropertyChanged
{
    private string _title = "";
    private bool _isCompleted;
    private DateTime _createdAt;
    private DateTime? _completedAt;
    private string? _notes;
    private int? _milestoneDays;

    [JsonInclude]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonInclude]
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public DateTime? CompletedAt
    {
        get => _completedAt;
        set { _completedAt = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public string? Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public int? MilestoneDays
    {
        get => _milestoneDays;
        set { _milestoneDays = value; OnPropertyChanged(); }
    }

    [JsonInclude]
    public ObservableCollection<TaskItem> SubTasks { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TaskRecorder.Models;
using TaskRecorder.Services;

namespace TaskRecorder.ViewModels;

public class TaskRowViewModel : INotifyPropertyChanged
{
    private ObservableCollection<FlatNodeInfo> _flatNodes = new();

    public TaskItem Root { get; }

    public DateTime LatestCreatedAt
    {
        get
        {
            var max = Root.CreatedAt;
            FindMax(Root);
            return max;

            void FindMax(TaskItem task)
            {
                if (task.CreatedAt > max)
                    max = task.CreatedAt;
                foreach (var sub in task.SubTasks)
                    FindMax(sub);
            }
        }
    }

    public ObservableCollection<FlatNodeInfo> FlatNodes
    {
        get => _flatNodes;
        set { _flatNodes = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskRowViewModel(TaskItem root)
    {
        Root = root;
        RefreshFlatNodes();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RefreshFlatNodes(bool filterCompleted = false)
    {
        var flatList = new List<TaskItem>();
        Flatten(Root);

        void Flatten(TaskItem task)
        {
            if (filterCompleted && task.IsCompleted)
            {
                foreach (var subtask in task.SubTasks)
                    Flatten(subtask);
                return;
            }
            flatList.Add(task);
            foreach (var subtask in task.SubTasks)
                Flatten(subtask);
        }

        var nodes = new ObservableCollection<FlatNodeInfo>(
            flatList.Select((task, i) => new FlatNodeInfo
            {
                Task = task,
                HasNext = i < flatList.Count - 1
            }));
        FlatNodes = nodes;
    }
}

public class FlatNodeInfo : INotifyPropertyChanged
{
    private TaskItem _task = null!;

    public TaskItem Task
    {
        get => _task;
        set { _task = value; OnPropertyChanged(); }
    }
    public bool HasNext { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TaskStorageService _storage;
    private readonly SettingsService _settingsService;
    private ObservableCollection<TaskRowViewModel> _rows = new();
    private bool _isFilterCompleted;
    private bool _isFilterIncludeSubtasks;
    private bool _isHighlightRecent;
    private ICollectionView? _filteredRows;

    public ObservableCollection<TaskRowViewModel> Rows
    {
        get => _rows;
        set { _rows = value; OnPropertyChanged(); }
    }

    public ICollectionView FilteredRows
    {
        get
        {
            if (_filteredRows is null)
            {
                _filteredRows = System.Windows.Data.CollectionViewSource.GetDefaultView(Rows);
                _filteredRows.Filter = FilterRow;
            }
            return _filteredRows;
        }
    }

    public bool IsFilterCompleted
    {
        get => _isFilterCompleted;
        set
        {
            _isFilterCompleted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShouldFilterCompletedNodes));
            RefreshAllFlatNodes();
            _filteredRows?.Refresh();
        }
    }

    public bool IsFilterIncludeSubtasks
    {
        get => _isFilterIncludeSubtasks;
        set
        {
            _isFilterIncludeSubtasks = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShouldFilterCompletedNodes));
            RefreshAllFlatNodes();
            _filteredRows?.Refresh();
        }
    }

    public bool ShouldFilterCompletedNodes => _isFilterCompleted && _isFilterIncludeSubtasks;

    private void RefreshAllFlatNodes()
    {
        foreach (var row in Rows)
            row.RefreshFlatNodes(ShouldFilterCompletedNodes);
    }

    public void SortRows()
    {
        var sorted = Rows.OrderBy(r => r.LatestCreatedAt).ToList();
        var changed = false;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (Rows[i] != sorted[i])
            {
                changed = true;
                break;
            }
        }
        if (!changed) return;

        Rows.Clear();
        foreach (var row in sorted)
            Rows.Add(row);
    }

    public bool IsHighlightRecent
    {
        get => _isHighlightRecent;
        set
        {
            _isHighlightRecent = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(TaskStorageService storage, SettingsService settingsService)
    {
        _storage = storage;
        _settingsService = settingsService;
    }

    private bool FilterRow(object obj)
    {
        if (!_isFilterCompleted)
            return true;

        if (obj is not TaskRowViewModel row)
            return true;

        return _isFilterIncludeSubtasks
            ? HasAnyNonCompleted(row.Root)
            : !IsTreeCompleted(row.Root);
    }

    private static bool IsTreeCompleted(TaskItem task)
    {
        if (!task.IsCompleted)
            return false;
        foreach (var sub in task.SubTasks)
        {
            if (!IsTreeCompleted(sub))
                return false;
        }
        return true;
    }

    private static bool HasAnyNonCompleted(TaskItem task)
    {
        if (!task.IsCompleted)
            return true;
        foreach (var sub in task.SubTasks)
        {
            if (HasAnyNonCompleted(sub))
                return true;
        }
        return false;
    }

    private bool RemoveFromParent(TaskItem parent, TaskItem target)
    {
        if (parent.SubTasks.Contains(target))
        {
            parent.SubTasks.Remove(target);
            return true;
        }

        foreach (var child in parent.SubTasks)
        {
            if (RemoveFromParent(child, target))
                return true;
        }

        return false;
    }

    private static bool ContainsTask(TaskItem parent, TaskItem target)
    {
        if (parent == target)
            return true;
        return parent.SubTasks.Any(child => ContainsTask(child, target));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public TaskRowViewModel? FindRowFor(TaskItem task)
    {
        return Rows.FirstOrDefault(r => ContainsTask(r.Root, task));
    }

    public void RefreshRowContaining(TaskItem task)
    {
        var row = FindRowFor(task);
        row?.RefreshFlatNodes();
    }

    private readonly ObservableCollection<string> _todayTaskIds = new();

    public ObservableCollection<string> TodayTaskIds => _todayTaskIds;

    public void Load()
    {
        var settings = _settingsService.Load();
        _isFilterCompleted = settings.IsFilterCompleted;
        _isFilterIncludeSubtasks = settings.IsFilterIncludeSubtasks;
        _isHighlightRecent = settings.IsHighlightRecent;

        var tasks = _storage.Load();
        var rows = new ObservableCollection<TaskRowViewModel>(
            tasks.Select(t => new TaskRowViewModel(t)));
        Rows = rows;
        SortRows();
        _filteredRows = null;
        OnPropertyChanged(nameof(FilteredRows));
        OnPropertyChanged(nameof(IsFilterCompleted));
        OnPropertyChanged(nameof(IsFilterIncludeSubtasks));
        OnPropertyChanged(nameof(IsHighlightRecent));

        _todayTaskIds.Clear();
        if (settings.TodayDate == DateTime.Today.ToString("yyyy-MM-dd"))
        {
            foreach (var id in settings.TodayTaskIds)
                _todayTaskIds.Add(id);
        }

        RefreshAllFlatNodes();
    }

    public void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            IsFilterCompleted = _isFilterCompleted,
            IsFilterIncludeSubtasks = _isFilterIncludeSubtasks,
            IsHighlightRecent = _isHighlightRecent,
            TodayTaskIds = _todayTaskIds.ToList(),
            TodayDate = DateTime.Today.ToString("yyyy-MM-dd")
        });
    }

    public void AddToToday(string taskId)
    {
        if (!_todayTaskIds.Contains(taskId))
            _todayTaskIds.Add(taskId);
    }

    public void RemoveFromToday(string taskId)
    {
        _todayTaskIds.Remove(taskId);
    }

    public List<TaskItem> GetTodayTasks()
    {
        var allTasks = new List<TaskItem>();
        foreach (var row in Rows)
            CollectTasks(row.Root, allTasks);

        var result = new List<TaskItem>();
        foreach (var id in _todayTaskIds)
        {
            var task = allTasks.FirstOrDefault(t => t.Id == id);
            if (task is not null)
                result.Add(task);
        }
        return result;
    }

    private static void CollectTasks(TaskItem task, List<TaskItem> list)
    {
        list.Add(task);
        foreach (var sub in task.SubTasks)
            CollectTasks(sub, list);
    }

    public void Save()
    {
        var topLevel = Rows.Select(r => r.Root).ToList();
        _storage.Save(topLevel);
    }

    public void AddTask(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var task = new TaskItem
        {
            Title = title.Trim(),
            CreatedAt = DateTime.Now
        };
        var row = new TaskRowViewModel(task);
        Rows.Add(row);
        SortRows();
        Save();
        _filteredRows?.Refresh();
    }

    public void EditTask(TaskItem task, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            return;
        task.Title = newTitle.Trim();
        Save();
    }

    public void ToggleComplete(TaskItem task)
    {
        task.IsCompleted = !task.IsCompleted;
        task.CompletedAt = task.IsCompleted ? DateTime.Now : null;
        Save();
        _filteredRows?.Refresh();
    }

    public void AddSubtask(TaskItem parent, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var subtask = new TaskItem
        {
            Title = title.Trim(),
            CreatedAt = DateTime.Now
        };
        parent.SubTasks.Insert(0, subtask);
        RefreshRowContaining(parent);
        SortRows();
        Save();
    }

    public void DeleteTask(TaskItem task)
    {
        // Search in top-level rows: remove task, reparent its subtasks to top level
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Root == task)
            {
                Rows.RemoveAt(i);
                // Insert subtasks as new top-level rows at the same position
                foreach (var sub in task.SubTasks)
                    Rows.Insert(i++, new TaskRowViewModel(sub));
                SortRows();
                Save();
                return;
            }
        }

        // Search in subtrees: remove task from parent, reparent its subtasks
        foreach (var row in Rows)
        {
            if (ReparentSubtasksAndRemove(row.Root, task))
            {
                RefreshRowContaining(row.Root);
                SortRows();
                Save();
                return;
            }
        }
    }

    private bool ReparentSubtasksAndRemove(TaskItem parent, TaskItem target)
    {
        var index = parent.SubTasks.IndexOf(target);
        if (index >= 0)
        {
            parent.SubTasks.RemoveAt(index);
            // Insert target's subtasks at the same position
            foreach (var sub in target.SubTasks)
                parent.SubTasks.Insert(index++, sub);
            return true;
        }

        foreach (var child in parent.SubTasks)
        {
            if (ReparentSubtasksAndRemove(child, target))
                return true;
        }

        return false;
    }

    public void DeleteTaskAndSubtasks(TaskItem task)
    {
        // Same as DeleteTask: removes the task and all its subtasks from the tree
        foreach (var row in Rows)
        {
            if (row.Root == task)
            {
                Rows.Remove(row);
                SortRows();
                Save();
                return;
            }
        }

        foreach (var row in Rows)
        {
            if (RemoveFromParent(row.Root, task))
            {
                RefreshRowContaining(row.Root);
                SortRows();
                Save();
                return;
            }
        }
    }

    public void EditNotes(TaskItem task, string? notes)
    {
        task.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Save();
    }

    public string? GetNotes(TaskItem task)
    {
        return task.Notes;
    }

    public void AddParentTask(TaskItem task, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var newParent = new TaskItem
        {
            Title = title.Trim(),
            CreatedAt = DateTime.Now
        };
        newParent.SubTasks.Add(task);

        // Top-level task: replace in Rows, new parent takes its position
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Root == task)
            {
                Rows[i] = new TaskRowViewModel(newParent);
                SortRows();
                Save();
                return;
            }
        }

        // Subtask: find parent and insert new task between parent and current
        foreach (var row in Rows)
        {
            if (InsertParentBetween(row.Root, task, newParent))
            {
                RefreshRowContaining(row.Root);
                SortRows();
                Save();
                return;
            }
        }
    }

    private bool InsertParentBetween(TaskItem parent, TaskItem child, TaskItem newParent)
    {
        if (parent.SubTasks.Contains(child))
        {
            parent.SubTasks.Clear();
            parent.SubTasks.Add(newParent);
            return true;
        }

        foreach (var sub in parent.SubTasks)
        {
            if (InsertParentBetween(sub, child, newParent))
                return true;
        }

        return false;
    }
}

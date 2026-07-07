# TaskRecorder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF .NET 8 tree-structured task list app with JSON persistence, password gate, horizontal parent-child task chains, and system tray minimization.

**Architecture:** MVVM-lite with code-behind for dialog/UI concerns. `MainViewModel` owns the top-level task list and exposes `TaskRowViewModel` wrappers that flatten each task tree into a horizontal node list. `TaskStorageService` handles JSON load/save. `PasswordWindow` gates startup. `NotifyIcon` in `App.xaml.cs` manages tray behavior.

**Tech Stack:** WPF + .NET 8, `System.Text.Json`, `System.Windows.Forms.NotifyIcon`, no third-party NuGet packages.

## Global Constraints

- .NET 8 (`net8.0-windows`), WPF + Windows Forms (for NotifyIcon)
- Project path: `D:\tools\TaskRecorder`
- Password hardcoded as `const string` value `"huyang"`
- `ShowInTaskbar = false` (window never in taskbar)
- JSON file: `tasks.json` at app root
- Completed tasks: strikethrough + grey, edit/add-subtask disabled
- New tasks append to list end, auto-scroll to bottom
- Window ✕ = hide to tray, not exit
- C# coding rules per user CLAUDE.md (member order, _camelCase fields, no empty constructors, etc.)

---

### Task 1: Project scaffolding

**Files:**
- Create: `D:\tools\TaskRecorder\TaskRecorder.csproj`
- Create: `D:\tools\TaskRecorder\App.xaml`
- Create: `D:\tools\TaskRecorder\App.xaml.cs`

**Interfaces:**
- Produces: Buildable WPF project, `App` class with entry point

- [ ] **Step 1: Create .csproj**

File: `D:\tools\TaskRecorder\TaskRecorder.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Create App.xaml**

File: `D:\tools\TaskRecorder\App.xaml`
```xml
<Application x:Class="TaskRecorder.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/MainWindow.xaml">
</Application>
```

- [ ] **Step 3: Create App.xaml.cs**

File: `D:\tools\TaskRecorder\App.xaml.cs`
```csharp
using System.Windows;

namespace TaskRecorder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeds (will fail — no MainWindow yet; that's fine, scaffold is correct).

Run: `dotnet restore`
Expected: Restore succeeds.

---

### Task 2: TaskItem model

**Files:**
- Create: `D:\tools\TaskRecorder\Models\TaskItem.cs`

**Interfaces:**
- Produces: `TaskItem` class with `INotifyPropertyChanged`, all properties, `[JsonInclude]` attributes

- [ ] **Step 1: Create Models directory**

Run: `mkdir /d/tools/TaskRecorder/Models`

- [ ] **Step 2: Write TaskItem.cs**

File: `D:\tools\TaskRecorder\Models\TaskItem.cs`
```csharp
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

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set { _completedAt = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TaskItem> SubTasks { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 3: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings.

---

### Task 3: TaskStorageService — JSON persistence

**Files:**
- Create: `D:\tools\TaskRecorder\Services\TaskStorageService.cs`

**Interfaces:**
- Consumes: `TaskItem` (Task 2)
- Produces: `TaskStorageService` class — `List<TaskItem> Load()`, `void Save(List<TaskItem>)`

- [ ] **Step 1: Create Services directory**

Run: `mkdir /d/tools/TaskRecorder/Services`

- [ ] **Step 2: Write TaskStorageService.cs**

File: `D:\tools\TaskRecorder\Services\TaskStorageService.cs`
```csharp
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
```

- [ ] **Step 3: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings.

---

### Task 4: PasswordWindow

**Files:**
- Create: `D:\tools\TaskRecorder\Views\PasswordWindow.xaml`
- Create: `D:\tools\TaskRecorder\Views\PasswordWindow.xaml.cs`

**Interfaces:**
- Produces: `PasswordWindow` — shows on startup, checks hardcoded password, returns `true` on success via `DialogResult`

- [ ] **Step 1: Create Views directory**

Run: `mkdir /d/tools/TaskRecorder/Views`

- [ ] **Step 2: Write PasswordWindow.xaml**

File: `D:\tools\TaskRecorder\Views\PasswordWindow.xaml`
```xml
<Window x:Class="TaskRecorder.Views.PasswordWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="TaskRecorder - 密码"
        Width="320" Height="160"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen"
        WindowStyle="SingleBorderWindow"
        ShowInTaskbar="True">

    <Window.Resources>
        <Storyboard x:Key="ShakeAnimation" Duration="0:0:0.3">
            <DoubleAnimationUsingKeyFrames Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                                           FillBehavior="Stop">
                <EasingDoubleKeyFrame KeyTime="0:0:0.0" Value="0"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.05" Value="-10"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.1" Value="10"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.15" Value="-10"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.2" Value="10"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.25" Value="-5"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.3" Value="0"/>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>
    </Window.Resources>

    <Border x:Name="RootBorder" RenderTransformOrigin="0.5,0.5">
        <Border.RenderTransform>
            <TranslateTransform/>
        </Border.RenderTransform>
        <Grid Margin="20">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Text="请输入密码" FontSize="14" HorizontalAlignment="Center" Margin="0,0,0,15"/>

            <PasswordBox x:Name="PasswordBox" Grid.Row="1" Width="200"
                         HorizontalAlignment="Center" Margin="0,0,0,15"
                         KeyDown="PasswordBox_KeyDown"/>

            <Button Grid.Row="2" Content="确认" Width="80"
                    HorizontalAlignment="Center"
                    Click="ConfirmButton_Click"/>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 3: Write PasswordWindow.xaml.cs**

File: `D:\tools\TaskRecorder\Views\PasswordWindow.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace TaskRecorder.Views;

public partial class PasswordWindow : Window
{
    private const string Password = "huyang";
    private int _failedAttempts;

    public PasswordWindow()
    {
        InitializeComponent();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password == Password)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            HandleWrongPassword();
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ConfirmButton_Click(sender, e);
    }

    private void HandleWrongPassword()
    {
        _failedAttempts++;
        PasswordBox.Clear();
        PasswordBox.Focus();

        if (_failedAttempts >= 3)
        {
            DialogResult = false;
            Close();
            return;
        }

        var shake = (Storyboard)FindResource("ShakeAnimation");
        shake.Begin();
    }
}
```

- [ ] **Step 4: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings. (Might warn about StartupUri in App.xaml pointing to not-yet-existing MainWindow — ignore for now.)

---

### Task 5: MainViewModel + TaskRowViewModel

**Files:**
- Create: `D:\tools\TaskRecorder\ViewModels\MainViewModel.cs`

**Interfaces:**
- Consumes: `TaskItem` (Task 2), `TaskStorageService` (Task 3)
- Produces: `MainViewModel` — `ObservableCollection<TaskRowViewModel> Rows`, `AddTask(string)`, `DeleteTask(TaskItem)`, `Save()`, `Load()`

- [ ] **Step 1: Create ViewModels directory**

Run: `mkdir /d/tools/TaskRecorder/ViewModels`

- [ ] **Step 2: Write MainViewModel.cs**

File: `D:\tools\TaskRecorder\ViewModels\MainViewModel.cs`
```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TaskRecorder.Models;
using TaskRecorder.Services;

namespace TaskRecorder.ViewModels;

public class TaskRowViewModel : INotifyPropertyChanged
{
    private ObservableCollection<FlatNodeInfo> _flatNodes = new();

    public TaskItem Root { get; }

    public ObservableCollection<FlatNodeInfo> FlatNodes
    {
        get => _flatNodes;
        set { _flatNodes = value; OnPropertyChanged(); }
    }

    public TaskRowViewModel(TaskItem root)
    {
        Root = root;
        RefreshFlatNodes();
    }

    public void RefreshFlatNodes()
    {
        var flatList = new List<TaskItem>();
        Flatten(Root);
        void Flatten(TaskItem t)
        {
            flatList.Add(t);
            foreach (var s in t.SubTasks)
                Flatten(s);
        }

        var nodes = new ObservableCollection<FlatNodeInfo>(
            flatList.Select((task, i) => new FlatNodeInfo
            {
                Task = task,
                HasNext = i < flatList.Count - 1
            }));
        FlatNodes = nodes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    private ObservableCollection<TaskRowViewModel> _rows = new();

    public ObservableCollection<TaskRowViewModel> Rows
    {
        get => _rows;
        set { _rows = value; OnPropertyChanged(); }
    }

    public ICommand AddTaskCommand { get; }

    public MainViewModel(TaskStorageService storage)
    {
        _storage = storage;
        AddTaskCommand = new RelayCommand<string>(AddTask);
    }

    public void Load()
    {
        var tasks = _storage.Load();
        var rows = new ObservableCollection<TaskRowViewModel>(
            tasks.Select(t => new TaskRowViewModel(t)));
        Rows = rows;
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
        Save();
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
        parent.SubTasks.Add(subtask);
        RefreshRowContaining(parent);
        Save();
    }

    public void DeleteTask(TaskItem task)
    {
        // Search in top-level rows
        foreach (var row in Rows)
        {
            if (row.Root == task)
            {
                Rows.Remove(row);
                Save();
                return;
            }
        }

        // Search in subtrees and remove from parent
        foreach (var row in Rows)
        {
            if (RemoveFromParent(row.Root, task))
            {
                RefreshRowContaining(row.Root);
                Save();
                return;
            }
        }
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

    public void RefreshRowContaining(TaskItem root)
    {
        var row = Rows.FirstOrDefault(r => r.Root == root);
        row?.RefreshFlatNodes();
    }

    public TaskRowViewModel? FindRowFor(TaskItem task)
    {
        return Rows.FirstOrDefault(r => ContainsTask(r.Root, task));
    }

    private static bool ContainsTask(TaskItem parent, TaskItem target)
    {
        if (parent == target)
            return true;
        return parent.SubTasks.Any(child => ContainsTask(child, target));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute is null)
            return true;
        if (parameter is T t)
            return _canExecute(t);
        return false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T t)
            _execute(t);
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
```

- [ ] **Step 3: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings. (Might warn about StartupUri — okay for now.)

---

### Task 6: MainWindow layout (shell)

**Files:**
- Create: `D:\tools\TaskRecorder\Views\MainWindow.xaml`
- Create: `D:\tools\TaskRecorder\Views\MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel` (Task 5)
- Produces: `MainWindow` with vertical list + bottom input bar, wired to ViewModel

- [ ] **Step 1: Write MainWindow.xaml (shell)**

File: `D:\tools\TaskRecorder\Views\MainWindow.xaml`
```xml
<Window x:Class="TaskRecorder.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:TaskRecorder.ViewModels"
        xmlns:m="clr-namespace:TaskRecorder.Models"
        Title="TaskRecorder"
        Width="900" Height="600"
        MinWidth="500" MinHeight="300"
        WindowStartupLocation="CenterScreen"
        ShowInTaskbar="False"
        Closing="Window_Closing">

    <!-- DataContext set in code-behind constructor -->

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Task list area -->
        <ScrollViewer x:Name="MainScrollViewer" Grid.Row="0"
                      VerticalScrollBarVisibility="Auto"
                      HorizontalScrollBarVisibility="Disabled">

            <!-- Empty state -->
            <TextBlock x:Name="EmptyPlaceholder"
                       Text="没有任务。在下方输入以添加。"
                       FontSize="16" Foreground="#999"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Visibility="Collapsed"/>

            <ItemsControl x:Name="TaskListControl"
                          ItemsSource="{Binding Rows}">

                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:TaskRowViewModel}">
                        <!-- Row template defined inline -->
                        <Border Margin="4,3" Padding="6,4"
                                BorderBrush="#ccc" BorderThickness="0,0,0,1">
                            <ScrollViewer HorizontalScrollBarVisibility="Auto"
                                          VerticalScrollBarVisibility="Disabled">
                                <ItemsControl ItemsSource="{Binding FlatNodes}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel Orientation="Horizontal"/>
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate DataType="{x:Type vm:FlatNodeInfo}">
                                            <!-- Task node: will be refined in Task 7 -->
                                            <Border x:Name="NodeBorder" Margin="2"
                                                    Padding="8,4"
                                                    BorderBrush="#aaa"
                                                    BorderThickness="1"
                                                    CornerRadius="4"
                                                    Background="White">
                                                <Border.ContextMenu>
                                                    <ContextMenu>
                                                        <MenuItem Header="编辑" Click="EditMenuItem_Click"
                                                                  Tag="{Binding}"/>
                                                        <MenuItem Header="完成 / 取消完成"
                                                                  Click="CompleteMenuItem_Click"
                                                                  Tag="{Binding}"/>
                                                        <MenuItem Header="添加子任务"
                                                                  Click="AddSubtaskMenuItem_Click"
                                                                  Tag="{Binding}"/>
                                                        <Separator/>
                                                        <MenuItem Header="删除" Click="DeleteMenuItem_Click"
                                                                  Tag="{Binding}"/>
                                                    </ContextMenu>
                                                </Border.ContextMenu>
                                                <Border.InputBindings>
                                                    <!-- Double-click handled in code-behind (Task 9) -->
                                                </Border.InputBindings>
                                                <StackPanel>
                                                    <TextBlock x:Name="TitleText"
                                                               Text="{Binding Task.Title}"
                                                               FontSize="14"/>
                                                    <TextBlock x:Name="TimeText"
                                                               FontSize="11"
                                                               Foreground="#999"
                                                               Margin="0,2,0,0"/>
                                                    <!-- Connector line placeholder -->
                                                    <Rectangle x:Name="ConnectorLine"
                                                               Height="2"
                                                               Fill="#aaa"
                                                               Visibility="Collapsed"
                                                               Margin="0,4,0,0"/>
                                                </StackPanel>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </ScrollViewer>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <!-- Bottom input bar -->
        <Border Grid.Row="1" BorderBrush="#ccc" BorderThickness="0,1,0,0"
                Background="#f5f5f5" Padding="8,6">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name="TaskInput" Grid.Column="0"
                         VerticalContentAlignment="Center"
                         KeyDown="TaskInput_KeyDown"/>
                <Button Grid.Column="1" Content="添加" Width="60"
                        Margin="8,0,0,0"
                        Click="AddButton_Click"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Write MainWindow.xaml.cs (shell)**

File: `D:\tools\TaskRecorder\Views\MainWindow.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskRecorder.Models;
using TaskRecorder.Services;
using TaskRecorder.ViewModels;

namespace TaskRecorder.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskStorageService _storage;

    public MainWindow()
    {
        _storage = new TaskStorageService("tasks.json");
        _viewModel = new MainViewModel(_storage);
        DataContext = _viewModel;
        InitializeComponent();
        _viewModel.Load();
        UpdateEmptyState();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AddTopLevelTask();
    }

    private void TaskInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            AddTopLevelTask();
    }

    private void AddTopLevelTask()
    {
        var title = TaskInput.Text;
        if (string.IsNullOrWhiteSpace(title))
            return;

        _viewModel.AddTask(title);
        TaskInput.Clear();
        TaskInput.Focus();
        UpdateEmptyState();
        MainScrollViewer.ScrollToBottom();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void UpdateEmptyState()
    {
        var hasTasks = _viewModel.Rows.Count > 0;
        EmptyPlaceholder.Visibility = hasTasks ? Visibility.Collapsed : Visibility.Visible;
        TaskListControl.Visibility = hasTasks ? Visibility.Visible : Visibility.Collapsed;
    }

    // Context menu handlers — stubs for Task 9
    private void EditMenuItem_Click(object sender, RoutedEventArgs e) { }
    private void CompleteMenuItem_Click(object sender, RoutedEventArgs e) { }
    private void AddSubtaskMenuItem_Click(object sender, RoutedEventArgs e) { }
    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) { }
}
```

- [ ] **Step 3: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 errors. (May need to adjust namespace references.)

---

### Task 7: Task node visual template — time display, connector lines, completed style

**Files:**
- Modify: `D:\tools\TaskRecorder\Views\MainWindow.xaml` — replace the inline DataTemplate with proper bindings for time and connector line

**Interfaces:**
- Consumes: `FlatNodeInfo` from Task 5
- Produces: Fully styled task node with time text, connector line, completed styling

- [ ] **Step 1: Add converters for time display and completed style**

Create file: `D:\tools\TaskRecorder\Converters\TaskConverters.cs`

Run: `mkdir /d/tools/TaskRecorder/Converters`

File: `D:\tools\TaskRecorder\Converters\TaskConverters.cs`
```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskRecorder.Converters;

public class TimeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FlatNodeInfo nodeInfo)
            return "";

        var task = nodeInfo.Task;
        var createdStr = task.CreatedAt.ToString("MM-dd HH:mm");

        if (task.IsCompleted && task.CompletedAt.HasValue)
        {
            var completedStr = task.CompletedAt.Value.ToString("MM-dd HH:mm");
            return $"{createdStr}  ✓ {completedStr}";
        }

        return createdStr;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CompletedStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompleted && isCompleted)
            return System.Windows.TextDecorations.Strikethrough;
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CompletedForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompleted && isCompleted)
            return "#999999";
        return "#000000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ConnectorVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasNext && hasNext)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }
}
```

Note: These types reference `FlatNodeInfo` from the ViewModels namespace. The converters file needs `using TaskRecorder.ViewModels;`.

- [ ] **Step 2: Update MainWindow.xaml DataTemplate**

Replace the inline DataTemplate's StackPanel content with proper bindings. The `TitleText` and `TimeText` and `ConnectorLine` blocks in the DataTemplate should become:

```xml
<StackPanel>
    <TextBlock x:Name="TitleText"
               Text="{Binding Task.Title}"
               FontSize="14"
               TextDecorations="{Binding Task.IsCompleted, Converter={StaticResource CompletedStyleConverter}}">
        <TextBlock.Style>
            <Style TargetType="TextBlock">
                <Style.Triggers>
                    <DataTrigger Binding="{Binding Task.IsCompleted}" Value="True">
                        <Setter Property="Foreground" Value="#999"/>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </TextBlock.Style>
    </TextBlock>
    <TextBlock x:Name="TimeText"
               FontSize="11"
               Foreground="#999"
               Margin="0,2,0,0"
               Text="{Binding Converter={StaticResource TimeDisplayConverter}, Mode=OneWay}"/>
    <Rectangle x:Name="ConnectorLine"
               Height="2"
               Fill="#aaa"
               Visibility="{Binding HasNext, Converter={StaticResource ConnectorVisibilityConverter}}"
               Margin="0,4,0,0"/>
</StackPanel>
```

The full replacement requires adding converter resources to the Window.Resources. The complete updated MainWindow.xaml is shown below. Since the file is long, the key addition is the Window.Resources block and the updated node DataTemplate.

- [ ] **Step 3: Update Window.Resources in MainWindow.xaml**

Add before `<Grid>`:

```xml
<Window.Resources>
    <converters:TimeDisplayConverter x:Key="TimeDisplayConverter"/>
    <converters:CompletedStyleConverter x:Key="CompletedStyleConverter"/>
    <converters:ConnectorVisibilityConverter x:Key="ConnectorVisibilityConverter"/>
</Window.Resources>
```

And add the converter namespace:
```xml
xmlns:converters="clr-namespace:TaskRecorder.Converters"
```

- [ ] **Step 4: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings.

---

### Task 8: Tray icon + App startup flow

**Files:**
- Modify: `D:\tools\TaskRecorder\App.xaml.cs` — tray icon, password gate, startup
- Modify: `D:\tools\TaskRecorder\App.xaml` — remove StartupUri (we'll create windows manually)

**Interfaces:**
- Consumes: `PasswordWindow` (Task 4), `MainWindow` (Task 6)
- Produces: Complete startup flow: show PasswordWindow → show MainWindow → tray on minimize

- [ ] **Step 1: Update App.xaml**

Remove `StartupUri`; the app now creates windows in code:

```xml
<Application x:Class="TaskRecorder.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

- [ ] **Step 2: Update App.xaml.cs**

File: `D:\tools\TaskRecorder\App.xaml.cs`
```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using TaskRecorder.Views;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.MenuItem;

namespace TaskRecorder;

public partial class App : Application
{
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Password gate
        var passwordWindow = new PasswordWindow();
        var result = passwordWindow.ShowDialog();
        if (result != true)
        {
            Shutdown();
            return;
        }

        // Create tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "TaskRecorder",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _notifyIcon.ContextMenuStrip.Items.Add("显示", null, ShowMainWindow);
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, ExitApplication);
        _notifyIcon.DoubleClick += (s, args) => ShowMainWindow(s, args);

        // Show main window
        _mainWindow = new MainWindow();
        _mainWindow.Closed += (s, args) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        };
        _mainWindow.Show();
    }

    private void ShowMainWindow(object? sender, EventArgs e)
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication(object? sender, EventArgs e)
    {
        _mainWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings.

---

### Task 9: Wire context menu interactions

**Files:**
- Modify: `D:\tools\TaskRecorder\Views\MainWindow.xaml.cs` — implement the 4 context menu handlers + double-click edit
- Modify: `D:\tools\TaskRecorder\Views\MainWindow.xaml` — update menu items with proper IsEnabled logic for completed tasks

**Interfaces:**
- Consumes: `MainViewModel` (Task 5), `FlatNodeInfo` (Task 5)
- Produces: All CRUD interactions working

- [ ] **Step 1: Replace stub handlers in MainWindow.xaml.cs**

Replace the 4 empty stub methods with:

```csharp
private void EditMenuItem_Click(object sender, RoutedEventArgs e)
{
    if (sender is not MenuItem menuItem) return;
    if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;
    if (nodeInfo.Task.IsCompleted) return;

    var dialog = new InputDialog("编辑任务", "任务名称:", nodeInfo.Task.Title);
    dialog.Owner = this;
    if (dialog.ShowDialog() == true)
    {
        _viewModel.EditTask(nodeInfo.Task, dialog.InputText);
        RefreshAllRows();
    }
}

private void CompleteMenuItem_Click(object sender, RoutedEventArgs e)
{
    if (sender is not MenuItem menuItem) return;
    if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

    _viewModel.ToggleComplete(nodeInfo.Task);
    RefreshAllRows();
}

private void AddSubtaskMenuItem_Click(object sender, RoutedEventArgs e)
{
    if (sender is not MenuItem menuItem) return;
    if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;
    if (nodeInfo.Task.IsCompleted) return;

    var dialog = new InputDialog("添加子任务", "任务名称:", "");
    dialog.Owner = this;
    if (dialog.ShowDialog() == true)
    {
        _viewModel.AddSubtask(nodeInfo.Task, dialog.InputText);
        RefreshAllRows();
    }
}

private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
{
    if (sender is not MenuItem menuItem) return;
    if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

    var result = System.Windows.MessageBox.Show(
        $"确定删除任务 "{nodeInfo.Task.Title}"？\n其所有子任务也将被删除。",
        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

    if (result == MessageBoxResult.Yes)
    {
        _viewModel.DeleteTask(nodeInfo.Task);
        RefreshAllRows();
        UpdateEmptyState();
    }
}

private void RefreshAllRows()
{
    foreach (var row in _viewModel.Rows)
        row.RefreshFlatNodes();
}
```

- [ ] **Step 2: Add double-click edit support**

In the DataTemplate's `Border.InputBindings`, replace with code-behind handler:

Remove the `MouseBinding` from XAML. Add a `MouseDown` handler to the node border instead, or simpler: update the ContextMenu `Tag` binding to use a click handler for the entire node.

Simplest approach: add `PreviewMouseDoubleClick` on the node Border in XAML:
```xml
<Border ... PreviewMouseDoubleClick="NodeBorder_DoubleClick">
```

And in code-behind:
```csharp
private void NodeBorder_DoubleClick(object sender, MouseButtonEventArgs e)
{
    if (sender is not FrameworkElement element) return;
    if (element.DataContext is not FlatNodeInfo nodeInfo) return;
    if (nodeInfo.Task.IsCompleted) return;

    var dialog = new InputDialog("编辑任务", "任务名称:", nodeInfo.Task.Title);
    dialog.Owner = this;
    if (dialog.ShowDialog() == true)
    {
        _viewModel.EditTask(nodeInfo.Task, dialog.InputText);
        RefreshAllRows();
    }
}
```

- [ ] **Step 3: Create InputDialog**

Create new file: `D:\tools\TaskRecorder\Views\InputDialog.xaml`
```xml
<Window x:Class="TaskRecorder.Views.InputDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="InputDialog"
        Width="360" Height="150"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False"
        WindowStyle="ToolWindow">

    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock x:Name="PromptText" Grid.Row="0" FontSize="13" Margin="0,0,0,10"/>

        <TextBox x:Name="InputBox" Grid.Row="1"
                 KeyDown="InputBox_KeyDown"/>

        <StackPanel Grid.Row="2" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="取消" Width="60" IsCancel="True" Margin="0,0,8,0"/>
            <Button Content="确认" Width="60" IsDefault="True"
                    Click="ConfirmButton_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

Create: `D:\tools\TaskRecorder\Views\InputDialog.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Input;

namespace TaskRecorder.Views;

public partial class InputDialog : Window
{
    public string InputText => InputBox.Text;

    public InputDialog(string title, string prompt, string defaultText)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
            return;

        DialogResult = true;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ConfirmButton_Click(sender, e);
    }
}
```

- [ ] **Step 4: Update context menu for completed state**

Modify the context menu in MainWindow.xaml to grey out Edit and AddSubtask when task is completed. The MenuItems for "编辑" and "添加子任务" need IsEnabled bound to `Task.IsCompleted` with a negation converter (BoolInverseConverter already created in Task 7).

Add to Window.Resources: `<converters:BoolInverseConverter x:Key="BoolInverseConverter"/>`

Update menu items:
```xml
<MenuItem Header="编辑" Click="EditMenuItem_Click" Tag="{Binding}"
          IsEnabled="{Binding Task.IsCompleted, Converter={StaticResource BoolInverseConverter}}"/>
<MenuItem Header="完成 / 取消完成" Click="CompleteMenuItem_Click" Tag="{Binding}"/>
<MenuItem Header="添加子任务" Click="AddSubtaskMenuItem_Click" Tag="{Binding}"
          IsEnabled="{Binding Task.IsCompleted, Converter={StaticResource BoolInverseConverter}}"/>
<Separator/>
<MenuItem Header="删除" Click="DeleteMenuItem_Click" Tag="{Binding}"/>
```

Also update the "完成" menu item header dynamically via a converter or handle it in the ContextMenu.Opened event. Simpler: use a converter for the header text.

Add converter: `D:\tools\TaskRecorder\Converters\TaskConverters.cs` — append:
```csharp
public class CompleteMenuHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompleted && isCompleted)
            return "取消完成";
        return "完成";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

Update menu item:
```xml
<MenuItem Header="{Binding Task.IsCompleted, Converter={StaticResource CompleteMenuHeaderConverter}}"
          Click="CompleteMenuItem_Click" Tag="{Binding}"/>
```

- [ ] **Step 5: Build verify**

Run: `dotnet build`
Expected: Build succeeds with 0 warnings.

---

### Task 10: Final polish — scroll behavior, save on close, empty state refresh

**Files:**
- Modify: `D:\tools\TaskRecorder\Views\MainWindow.xaml.cs` — auto-scroll, save on exit
- Modify: `D:\tools\TaskRecorder\App.xaml.cs` — save on exit before shutdown

**Interfaces:**
- Consumes: All prior tasks
- Produces: Complete, polished app

- [ ] **Step 1: Add Loaded event for initial scroll-to-bottom**

In MainWindow.xaml, add `Loaded="Window_Loaded"` on the `<Window>` element.

In MainWindow.xaml.cs, add:
```csharp
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    MainScrollViewer.ScrollToBottom();
}
```

The `AddTopLevelTask()` method already calls `MainScrollViewer.ScrollToBottom()` after adding — verify.

- [ ] **Step 2: Add save on window closing**

Update `Window_Closing` in MainWindow.xaml.cs:
```csharp
private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    _viewModel.Save();
    e.Cancel = true;
    Hide();
}
```

- [ ] **Step 3: Update App.xaml.cs to ensure save on real exit**

The `ExitApplication` method should ensure MainWindow saves before closing:
```csharp
private void ExitApplication(object? sender, EventArgs e)
{
    if (_mainWindow != null)
    {
        // Save happens in Window_Closing → saves → then Closed fires
        _mainWindow.Close();
    }
    Shutdown();
}
```

This already works because `Window_Closing` fires before `Closed`, and `_mainWindow.Close()` triggers `Window_Closing`. After `Close()`, `Shutdown()` triggers `OnExit`.

- [ ] **Step 4: Update PasswordWindow shake animation — needs a using for Media.Animation**

The PasswordWindow.xaml.cs already references `Storyboard` — verify the `using System.Windows.Media.Animation;` directive is present. It is.

- [ ] **Step 5: Build final verify**

Run: `dotnet build`
Expected: Build succeeds with 0 errors and 0 warnings.

- [ ] **Step 6: Run app and manual smoke test**

Run: `dotnet run`

Manual checklist:
1. Password dialog appears → enter wrong password → shake → after 3rd fail → exits
2. Enter "huyang" → main window opens
3. Type task in bottom bar → Enter → appears at bottom, auto-scrolled
4. Right-click task → Edit → change text → title updates
5. Right-click → Complete → strikethrough + grey + time shows ✓
6. Right-click completed task → Edit greyed out, AddSubtask greyed out
7. Right-click → Add Subtask → dialog → new node appears to the right with line
8. Right-click → Delete → confirmation → task removed
9. Add multiple tasks → scroll to bottom works
10. Close window (✕) → hides to tray → tray icon visible
11. Tray → "显示" → window reappears
12. Tray → "退出" → app exits
13. Reopen app → tasks were saved

---

## Task Summary

| Task | Component | Files |
|------|-----------|-------|
| 1 | Project scaffolding | `TaskRecorder.csproj`, `App.xaml`, `App.xaml.cs` |
| 2 | TaskItem model | `Models/TaskItem.cs` |
| 3 | JSON persistence | `Services/TaskStorageService.cs` |
| 4 | Password gate | `Views/PasswordWindow.xaml`, `.cs` |
| 5 | ViewModels | `ViewModels/MainViewModel.cs` |
| 6 | MainWindow shell | `Views/MainWindow.xaml`, `.cs` |
| 7 | Node visuals | `Converters/TaskConverters.cs`, modify `MainWindow.xaml` |
| 8 | Tray + startup | `App.xaml`, `App.xaml.cs` |
| 9 | Context menu wiring | `Views/InputDialog.xaml`, `.cs`, modify `MainWindow.xaml`, `.cs` |
| 10 | Final polish | Modify `MainWindow.xaml.cs`, `App.xaml.cs` |

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskRecorder.Models;
using TaskRecorder.Services;
using TaskRecorder.ViewModels;

namespace TaskRecorder.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskStorageService _storage;
    private TodayTasksWindow? _todayWindow;

    public MainWindow()
    {
        var dataPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tasks.json");
        var settingsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json");
        _storage = new TaskStorageService(dataPath);
        var settingsService = new SettingsService(settingsPath);
        _viewModel = new MainViewModel(_storage, settingsService);
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => MainScrollViewer.ScrollToBottom()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Save();
        _viewModel.SaveSettings();
        e.Cancel = true;
        Hide();
    }

    private void UpdateEmptyState()
    {
        var hasTasks = _viewModel.Rows.Count > 0;
        EmptyPlaceholder.Visibility = hasTasks ? Visibility.Collapsed : Visibility.Visible;
        TaskListControl.Visibility = hasTasks ? Visibility.Visible : Visibility.Collapsed;
    }

    // Context menu handlers
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

    private void MilestoneMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        var dialog = new MilestoneDialog(nodeInfo.Task.MilestoneDays);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            nodeInfo.Task.MilestoneDays = dialog.IsCleared ? null : dialog.ResultDays;
            _viewModel.Save();
            RefreshAllRows();
        }
    }

    private void AddSubtaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;
        if (nodeInfo.Task.IsCompleted) return;

        var dialog = new InputDialog("添加后置任务", "任务名称:", "");
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddSubtask(nodeInfo.Task, dialog.InputText);
            RefreshAllRows();
            MainScrollViewer.ScrollToBottom();
        }
    }

    private void AddParentTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;
        if (nodeInfo.Task.IsCompleted) return;

        var dialog = new InputDialog("添加前置任务", "前置任务名称:", "");
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddParentTask(nodeInfo.Task, dialog.InputText);
            RefreshAllRows();
            UpdateEmptyState();
            MainScrollViewer.ScrollToBottom();
        }
    }

    private void EditNotesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        var currentNotes = _viewModel.GetNotes(nodeInfo.Task);
        var dialog = new NotesDialog(currentNotes);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _viewModel.EditNotes(nodeInfo.Task, dialog.InputText);
        }
    }

    private void ViewNotesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        var notes = _viewModel.GetNotes(nodeInfo.Task);
        if (string.IsNullOrWhiteSpace(notes))
            return;

        System.Windows.MessageBox.Show(notes, "查看补充信息",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        var result = System.Windows.MessageBox.Show(
            $"确定删除任务 \"{nodeInfo.Task.Title}\"？\n子任务将上移。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteTask(nodeInfo.Task);
            RefreshAllRows();
            UpdateEmptyState();
        }
    }

    private void AddToTodayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        _viewModel.AddToToday(nodeInfo.Task.Id);
        _viewModel.SaveSettings();
        _todayWindow?.RefreshList();
    }

    private void DeleteTaskAndSubtaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not FlatNodeInfo nodeInfo) return;

        var result = System.Windows.MessageBox.Show(
            $"确定删除任务 \"{nodeInfo.Task.Title}\" 及其所有子任务？",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteTaskAndSubtasks(nodeInfo.Task);
            RefreshAllRows();
            UpdateEmptyState();
        }
    }

    private void TodayTasksButton_Click(object sender, RoutedEventArgs e)
    {
        if (_todayWindow is null)
        {
            _todayWindow = new TodayTasksWindow(_viewModel);
            _todayWindow.Owner = this;
        }
        _todayWindow.Show();
        _todayWindow.Activate();
    }

    private void NodeBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
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

    private void RefreshAllRows()
    {
        var filter = _viewModel.ShouldFilterCompletedNodes;
        foreach (var row in _viewModel.Rows)
            row.RefreshFlatNodes(filter);
    }

    private void HorizontalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // Walk up the visual tree to find the parent vertical ScrollViewer
        DependencyObject? parent = VisualTreeHelper.GetParent(sv);
        while (parent is not null and not ScrollViewer)
            parent = VisualTreeHelper.GetParent(parent);

        if (parent is ScrollViewer parentSv)
        {
            parentSv.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = parentSv
            });
            e.Handled = true;
        }
    }
}

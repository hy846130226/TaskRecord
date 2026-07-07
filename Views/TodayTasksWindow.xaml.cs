using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TaskRecorder.Models;
using TaskRecorder.ViewModels;

namespace TaskRecorder.Views;

public partial class TodayTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<TaskItem> _todayTasks = new();

    public TodayTasksWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _todayTasks;
        InitializeComponent();
        RefreshList();
    }

    public void RefreshList()
    {
        _todayTasks.Clear();
        foreach (var task in _viewModel.GetTodayTasks())
            _todayTasks.Add(task);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TodayListBox.SelectedItem is not TaskItem task) return;
        _viewModel.RemoveFromToday(task.Id);
        _todayTasks.Remove(task);
        _viewModel.SaveSettings();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

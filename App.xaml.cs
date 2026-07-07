using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using TaskRecorder.Views;
using Application = System.Windows.Application;

namespace TaskRecorder;

public partial class App : Application
{
    private const string MutexName = "TaskRecorder_SingleInstance_Mutex";
    private const string ActivateEventName = "TaskRecorder_Activate_Event";

    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private Icon? _appIcon;
    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance already running — tell it to activate, then exit
            try
            {
                using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
                evt.Set();
            }
            catch
            {
                // Event not found; first instance may be shutting down
            }
            Shutdown();
            return;
        }

        // Create activation event BEFORE password dialog so second instance can signal during entry
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);

        // Background thread: listen for activation signals from other instances
        var activateThread = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    _activateEvent.WaitOne();
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, BringToFront);
                }
            }
            catch (ObjectDisposedException)
            {
                // Event disposed during shutdown — exit thread
            }
        })
        {
            IsBackground = true
        };
        activateThread.Start();

        // Password gate
        var passwordWindow = new PasswordWindow();
        var result = passwordWindow.ShowDialog();
        if (result != true)
        {
            Shutdown();
            return;
        }

        // Load icon from resource file
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", "app.ico");
        }
        _appIcon = new Icon(iconPath);

        // Create tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "TaskRecorder",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _notifyIcon.ContextMenuStrip.Items.Add("显示", null, ShowMainWindow);
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, ExitApplication);
        _notifyIcon.DoubleClick += (s, args) => ShowMainWindow(s, args);

        // Show main window
        try
        {
            _mainWindow = new MainWindow();
            var wpfIcon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(iconPath, UriKind.Absolute));
            _mainWindow.Icon = wpfIcon;
            _mainWindow.Closed += (s, args) =>
            {
                _notifyIcon.Visible = false;
            };
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"启动失败: {ex.Message}\n\n{ex.StackTrace}",
                "错误", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown();
            return;
        }
    }

    private void BringToFront()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ShowMainWindow(object? sender, EventArgs e)
    {
        BringToFront();
    }

    private void ExitApplication(object? sender, EventArgs e)
    {
        _mainWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appIcon?.Dispose();
        _notifyIcon?.Dispose();
        _activateEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

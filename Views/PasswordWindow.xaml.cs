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
        shake.Begin(RootBorder);
    }
}

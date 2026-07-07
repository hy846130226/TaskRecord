using System.Windows;
using System.Windows.Input;

namespace TaskRecorder.Views;

public partial class InputDialog : Window
{
    private readonly bool _allowEmpty;

    public string InputText => InputBox.Text;

    public InputDialog(string title, string prompt, string defaultText, bool allowEmpty = false)
    {
        _allowEmpty = allowEmpty;
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_allowEmpty && string.IsNullOrWhiteSpace(InputBox.Text))
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

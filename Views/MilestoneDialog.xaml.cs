using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace TaskRecorder.Views;

public partial class MilestoneDialog : Window
{
    private static readonly Regex s_integerRegex = new(@"^\d+$");

    public int? ResultDays { get; private set; }
    public bool IsCleared { get; private set; }

    public MilestoneDialog(int? currentDays)
    {
        InitializeComponent();
        if (currentDays.HasValue)
            DaysBox.Text = currentDays.Value.ToString();
        DaysBox.Focus();
    }

    private void DaysBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !s_integerRegex.IsMatch(e.Text);
    }

    private void DaysBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ConfirmButton_Click(sender, e);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var text = DaysBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowError("请输入天数");
            return;
        }

        if (!int.TryParse(text, out var days) || days < 0)
        {
            ShowError("请输入非负整数");
            return;
        }

        ResultDays = days;
        DialogResult = true;
        Close();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        IsCleared = true;
        ResultDays = null;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

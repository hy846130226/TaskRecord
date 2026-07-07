using System.Windows;
using System.Windows.Input;

namespace TaskRecorder.Views;

public partial class NotesDialog : Window
{
    public string InputText => NotesBox.Text;

    public NotesDialog(string? defaultText)
    {
        InitializeComponent();
        NotesBox.Text = defaultText ?? "";
        NotesBox.CaretIndex = NotesBox.Text.Length;
        NotesBox.Focus();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void NotesBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Allow Enter for newlines, no special handling needed
    }
}

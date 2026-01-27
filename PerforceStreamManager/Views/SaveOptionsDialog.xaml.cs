using System.Windows;

namespace PerforceStreamManager.Views;

/// <summary>
/// Dialog to prompt user for save options, including whether to submit immediately
/// </summary>
public partial class SaveOptionsDialog : Window
{
    /// <summary>
    /// Gets whether the user chose to submit the snapshot file immediately
    /// </summary>
    public bool SubmitImmediately { get; private set; } = true;

    /// <summary>
    /// Creates a new SaveOptionsDialog
    /// </summary>
    public SaveOptionsDialog()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitImmediately = SubmitCheckBox.IsChecked ?? true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

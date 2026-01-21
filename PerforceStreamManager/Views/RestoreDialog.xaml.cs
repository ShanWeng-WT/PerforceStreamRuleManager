using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PerforceStreamManager.Models;

namespace PerforceStreamManager.Views;

/// <summary>
/// Dialog to select a version from file history to restore
/// </summary>
public partial class RestoreDialog : Window
{
    /// <summary>
    /// The selected revision to restore, or null if cancelled
    /// </summary>
    public FileRevisionInfo? SelectedRevision { get; private set; }

    /// <summary>
    /// Creates a new RestoreDialog with the list of available revisions
    /// </summary>
    /// <param name="streamPath">Stream path being restored</param>
    /// <param name="revisions">List of file revisions to display</param>
    public RestoreDialog(string streamPath, List<FileRevisionInfo> revisions)
    {
        InitializeComponent();
        
        StreamPathText.Text = $"Stream: {streamPath}";
        RevisionsListView.ItemsSource = revisions;
        
        // Select the first (most recent) revision by default
        if (revisions.Count > 0)
        {
            RevisionsListView.SelectedIndex = 0;
        }
        
        // Enable/disable restore button based on selection
        RevisionsListView.SelectionChanged += (s, e) =>
        {
            RestoreButton.IsEnabled = RevisionsListView.SelectedItem != null;
        };
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (RevisionsListView.SelectedItem is FileRevisionInfo revision)
        {
            SelectedRevision = revision;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Please select a version to restore.", "No Selection", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedRevision = null;
        DialogResult = false;
        Close();
    }

    private void RevisionsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double-click to restore
        if (RevisionsListView.SelectedItem is FileRevisionInfo revision)
        {
            SelectedRevision = revision;
            DialogResult = true;
            Close();
        }
    }
}

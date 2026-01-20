using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using PerforceStreamManager.Models;

namespace PerforceStreamManager.Views;

/// <summary>
/// Dialog to prompt user about unsaved changes when exiting the application
/// </summary>
public partial class UnsavedChangesDialog : Window
{
    /// <summary>
    /// Result indicating what action the user chose
    /// </summary>
    public UnsavedChangesResult UserChoice { get; private set; } = UnsavedChangesResult.Cancel;

    /// <summary>
    /// Creates a new UnsavedChangesDialog with the list of pending changes
    /// </summary>
    /// <param name="changes">List of rule changes to display</param>
    public UnsavedChangesDialog(List<RuleChangeInfo> changes)
    {
        InitializeComponent();
        
        // Set up grouping by change type
        var view = CollectionViewSource.GetDefaultView(changes);
        view.GroupDescriptions.Add(new PropertyGroupDescription("ChangeType"));
        view.SortDescriptions.Add(new SortDescription("ChangeType", ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription("StreamPath", ListSortDirection.Ascending));
        
        ChangesListView.ItemsSource = view;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        UserChoice = UnsavedChangesResult.Save;
        DialogResult = true;
        Close();
    }

    private void DontSaveButton_Click(object sender, RoutedEventArgs e)
    {
        UserChoice = UnsavedChangesResult.DontSave;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        UserChoice = UnsavedChangesResult.Cancel;
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Result of the unsaved changes dialog
/// </summary>
public enum UnsavedChangesResult
{
    /// <summary>User chose to save changes before exiting</summary>
    Save,
    /// <summary>User chose to exit without saving</summary>
    DontSave,
    /// <summary>User cancelled the exit operation</summary>
    Cancel
}

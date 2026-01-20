using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PerforceStreamManager.ViewModels;
using PerforceStreamManager.Models;
using PerforceStreamManager.Views;
using PerforceStreamManager.Services;

namespace PerforceStreamManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _forceClose = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        // Check if password is empty and open settings if needed
        CheckAndOpenSettingsIfNeeded();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is StreamNode selectedNode)
        {
            viewModel.SelectedStream = selectedNode;
        }
    }

    private void CheckAndOpenSettingsIfNeeded()
    {
        try
        {
            if (DataContext is MainViewModel viewModel)
            {
                // Access the settings service through reflection or create a new one
                var loggingService = new LoggingService();
                var settingsService = new SettingsService(loggingService);
                var settings = settingsService.LoadSettings();
                
                // Check if password is empty
                if (settings?.Connection != null && string.IsNullOrWhiteSpace(settings.Connection.Password))
                {
                    // Open settings dialog after window is loaded
                    Loaded += (s, e) => 
                    {
                        // Get P4Service from ViewModel (need to expose it)
                        var p4Service = GetP4ServiceFromViewModel(viewModel);
                        var settingsDialog = new SettingsDialog(settingsService, p4Service)
                        {
                            Owner = this
                        };
                        settingsDialog.ShowDialog();
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't prevent app from starting
            System.Diagnostics.Debug.WriteLine($"Error checking settings: {ex.Message}");
        }
    }
    
    private P4Service GetP4ServiceFromViewModel(MainViewModel viewModel)
    {
        // Use reflection to get the private _p4Service field
        var field = typeof(MainViewModel).GetField("_p4Service", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(viewModel) as P4Service ?? new P4Service(new LoggingService());
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel && viewModel.HasUnsavedChanges)
        {
            var changes = viewModel.GetPendingChanges();
            
            if (changes.Count > 0)
            {
                var dialog = new UnsavedChangesDialog(changes)
                {
                    Owner = this
                };

                dialog.ShowDialog();

                switch (dialog.UserChoice)
                {
                    case UnsavedChangesResult.Save:
                        // Execute the save command
                        if (viewModel.SaveCommand.CanExecute(null))
                        {
                            viewModel.SaveCommand.Execute(null);
                        }
                        // Allow close to proceed
                        break;

                    case UnsavedChangesResult.DontSave:
                        // Allow close without saving
                        break;

                    case UnsavedChangesResult.Cancel:
                        // Cancel the close operation
                        e.Cancel = true;
                        break;
                }
            }
        }
    }
}
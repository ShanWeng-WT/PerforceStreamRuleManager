using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PerforceStreamManager.ViewModels;
using PerforceStreamManager.Models;
using PerforceStreamManager.Views;

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
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is StreamNode selectedNode)
        {
            viewModel.SelectedStream = selectedNode;
        }
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
using System.Windows;
using PerforceStreamManager.ViewModels;

namespace PerforceStreamManager.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Load history when window opens
        Loaded += async (s, e) =>
        {
            if (viewModel.LoadHistoryCommand.CanExecute(null))
            {
                viewModel.LoadHistoryCommand.Execute(null);
            }
        };
    }
}

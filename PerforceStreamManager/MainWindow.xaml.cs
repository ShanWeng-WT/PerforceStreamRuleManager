using System.Windows;
using System.Windows.Controls;
using PerforceStreamManager.ViewModels;
using PerforceStreamManager.Models;

namespace PerforceStreamManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
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
        Application.Current.Shutdown();
    }
}
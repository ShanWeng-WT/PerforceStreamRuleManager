using System.Windows;
using System.Windows.Controls;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.Views;

public partial class RuleDialog : Window
{
    private readonly P4Service _p4Service;
    private readonly string _targetStream;
    private readonly string _currentSelectedStream;
    
    public string RuleType { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string RemapTarget { get; private set; } = string.Empty;
    
    public RuleDialog(P4Service p4Service, string currentSelectedStream, string targetStream,
        StreamRule? existingRule = null)
    {
        InitializeComponent();
        _p4Service = p4Service;
        _targetStream = targetStream;
        _currentSelectedStream = currentSelectedStream;

        // Ensure RuleType is initialized
        if (RuleTypeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
        {
            RuleType = selectedItem.Content.ToString() ?? "ignore";
        }
        
        if (existingRule is not null)
        {
            // Edit mode - populate with existing rule
            if (!string.IsNullOrEmpty(existingRule.Type))
            {
                var itemToSelect = RuleTypeComboBox.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(item.Content?.ToString(), existingRule.Type, StringComparison.OrdinalIgnoreCase));

                if (itemToSelect != null)
                {
                    RuleTypeComboBox.SelectedItem = itemToSelect;
                }
            }

            PathTextBox.Text = existingRule.Path ?? string.Empty;
            RemapTargetTextBox.Text = existingRule.RemapTarget ?? string.Empty;
        }
    }
    
    private void RuleTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RemapTargetPanel == null) return;

        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
        {
            string? ruleType = selectedItem.Content.ToString();
            RemapTargetPanel.Visibility = string.Equals(ruleType, "remap", StringComparison.OrdinalIgnoreCase) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }
    }
    
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var browserDialog = new DepotBrowserDialog(_p4Service, _currentSelectedStream, _targetStream);
        if (browserDialog.ShowDialog() == true)
        {
            PathTextBox.Text = browserDialog.SelectedPath;
        }
    }
    
    private void BrowseRemapButton_Click(object sender, RoutedEventArgs e)
    {
        // For remap target, we might want to browse the whole depot or a different stream
        // But for now, let's start browsing from the current stream as a default convenience
        var browserDialog = new DepotBrowserDialog(_p4Service, _currentSelectedStream, _targetStream);
        if (browserDialog.ShowDialog() == true)
        {
            RemapTargetTextBox.Text = browserDialog.SelectedPath;
        }
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(PathTextBox.Text))
        {
            MessageBox.Show("Path is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (RuleTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            RuleType = selectedItem.Content?.ToString() ?? "ignore";

            if (RuleType == "remap" && string.IsNullOrWhiteSpace(RemapTargetTextBox.Text))
            {
                MessageBox.Show("Remap target is required for remap rules.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        
        Path = PathTextBox.Text;
        RemapTarget = RemapTargetTextBox.Text;
        
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

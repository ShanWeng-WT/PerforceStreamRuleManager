using System.Windows;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    
    public SettingsDialog(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        _settings = _settingsService.LoadSettings();
        
        // Populate connection settings
        ServerTextBox.Text = _settings.Connection?.Server ?? string.Empty;
        PortTextBox.Text = _settings.Connection?.Port ?? string.Empty;
        UserTextBox.Text = _settings.Connection?.User ?? string.Empty;

        PasswordBox.Password = _settings.Connection?.Password ?? string.Empty;
        
        // Populate history storage path
        HistoryPathTextBox.Text = _settings.HistoryStoragePath ?? string.Empty;
        
        // Populate retention policy
        MaxSnapshotsTextBox.Text = _settings.Retention?.MaxSnapshots.ToString() ?? "50";
        MaxAgeDaysTextBox.Text = _settings.Retention?.MaxAgeDays.ToString() ?? "365";
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(ServerTextBox.Text))
        {
            MessageBox.Show("Server is required.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(PortTextBox.Text))
        {
            MessageBox.Show("Port is required.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!int.TryParse(MaxSnapshotsTextBox.Text, out int maxSnapshots) || maxSnapshots <= 0)
        {
            MessageBox.Show("Max Snapshots must be a positive integer.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!int.TryParse(MaxAgeDaysTextBox.Text, out int maxAgeDays) || maxAgeDays <= 0)
        {
            MessageBox.Show("Max Age Days must be a positive integer.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Update settings object
        _settings.Connection = new P4ConnectionSettings
        {
            Server = ServerTextBox.Text,
            Port = PortTextBox.Text,
            User = UserTextBox.Text,
            Password = PasswordBox.Password
        };
        
        _settings.HistoryStoragePath = HistoryPathTextBox.Text;
        
        _settings.Retention = new RetentionPolicy
        {
            MaxSnapshots = maxSnapshots,
            MaxAgeDays = maxAgeDays
        };
        
        // Save settings
        try
        {
            _settingsService.SaveSettings(_settings);
            MessageBox.Show("Settings saved successfully.", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

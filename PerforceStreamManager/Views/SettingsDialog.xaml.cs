using System.Windows;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly P4Service _p4Service;
    private AppSettings _settings;
    
    public SettingsDialog(SettingsService settingsService, P4Service p4Service)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _p4Service = p4Service;
        
        LoadSettings();
        UpdateConnectionStatus();
    }
    
    private void LoadSettings()
    {
        _settings = _settingsService.LoadSettings();
        
        // Populate connection settings
        ServerTextBox.Text = _settings.Connection?.Server ?? string.Empty;
        PortTextBox.Text = _settings.Connection?.Port ?? string.Empty;
        UserTextBox.Text = _settings.Connection?.User ?? string.Empty;
        PasswordBox.Password = _settings.Connection?.Password ?? string.Empty;
        
        // Populate snapshot storage path
        HistoryPathTextBox.Text = _settings.HistoryStoragePath ?? string.Empty;
    }
    
    private void UpdateConnectionStatus()
    {
        if (_p4Service.IsConnected)
        {
            ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Lime;
            ConnectionStatusText.Text = "Connected";
        }
        else
        {
            ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
            ConnectionStatusText.Text = "Not Connected";
        }
    }
    
    private void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var testSettings = new P4ConnectionSettings
            {
                Server = ServerTextBox.Text,
                Port = PortTextBox.Text,
                User = UserTextBox.Text,
                Password = PasswordBox.Password
            };
            
            if (string.IsNullOrWhiteSpace(testSettings.Server) || 
                string.IsNullOrWhiteSpace(testSettings.Port))
            {
                MessageBox.Show("Server and Port are required to test connection.", 
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "Testing...";
            
            // Test connection in background thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var testService = new P4Service(new LoggingService()))
                    {
                        testService.Connect(testSettings);
                        var isConnected = testService.IsConnected;
                        
                        Dispatcher.Invoke(() =>
                        {
                            if (isConnected)
                            {
                                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Lime;
                                ConnectionStatusText.Text = "Connected";
                                MessageBox.Show("Connection successful!", "Test Result", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
                                ConnectionStatusText.Text = "Connection Failed";
                                MessageBox.Show("Connection failed. Please check your settings.", "Test Result", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
                        ConnectionStatusText.Text = "Connection Failed";
                        MessageBox.Show($"Connection failed: {ex.Message}", "Test Result", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        TestConnectionButton.IsEnabled = true;
                        TestConnectionButton.Content = "Test Connection";
                    });
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to test connection: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        
        // Update settings object
        _settings.Connection = new P4ConnectionSettings
        {
            Server = ServerTextBox.Text,
            Port = PortTextBox.Text,
            User = UserTextBox.Text,
            Password = PasswordBox.Password
        };
        
        _settings.HistoryStoragePath = HistoryPathTextBox.Text;
        
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

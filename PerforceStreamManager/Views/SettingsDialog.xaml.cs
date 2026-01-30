using System.Security;
using System.Windows;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly P4Service _p4Service;
    private readonly ErrorMessageSanitizer _errorSanitizer;
    private AppSettings _settings;

    public SettingsDialog(SettingsService settingsService, P4Service p4Service)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _p4Service = p4Service;
        _errorSanitizer = new ErrorMessageSanitizer(p4Service.Logger);

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
            // Validate inputs first
            if (string.IsNullOrWhiteSpace(ServerTextBox.Text) ||
                string.IsNullOrWhiteSpace(PortTextBox.Text))
            {
                MessageBox.Show("Server and Port are required to test connection.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!P4InputValidator.ValidateServerAddress(ServerTextBox.Text, out string serverError))
            {
                MessageBox.Show(serverError, "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!P4InputValidator.ValidatePort(PortTextBox.Text, out string portError))
            {
                MessageBox.Show(portError, "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var testSettings = new P4ConnectionSettings
            {
                Server = ServerTextBox.Text,
                Port = PortTextBox.Text,
                User = UserTextBox.Text,
                Password = null // Will be handled via SecureString
            };

            // Copy SecurePassword for use in background thread
            SecureString securePassword = PasswordBox.SecurePassword.Copy();

            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "Testing...";

            // Test connection in background thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var testService = new P4Service(new LoggingService()))
                    {
                        testService.Connect(testSettings, securePassword);
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

                        // Check if this is a rate limiting error
                        if (ex.Message.Contains("Too many failed connection attempts"))
                        {
                            // Show the rate limit message directly (it's user-friendly)
                            MessageBox.Show(ex.Message, "Rate Limited",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            string safeMessage = _errorSanitizer.SanitizeForUser(ex, "TestConnection");
                            MessageBox.Show(safeMessage, "Test Result",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                finally
                {
                    // Dispose the SecureString copy
                    securePassword?.Dispose();

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
            string safeMessage = _errorSanitizer.SanitizeForUser(ex, "TestConnectionButton_Click");
            MessageBox.Show(safeMessage, "Error",
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

        // Validate server and port format
        if (!P4InputValidator.ValidateServerAddress(ServerTextBox.Text, out string serverError))
        {
            MessageBox.Show(serverError, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!P4InputValidator.ValidatePort(PortTextBox.Text, out string portError))
        {
            MessageBox.Show(portError, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(UserTextBox.Text) &&
            !P4InputValidator.ValidateUsername(UserTextBox.Text, out string userError))
        {
            MessageBox.Show(userError, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Update settings object (without password - that's handled separately)
        _settings.Connection = new P4ConnectionSettings
        {
            Server = ServerTextBox.Text,
            Port = PortTextBox.Text,
            User = UserTextBox.Text,
            Password = null // Password handled via SecureString
        };

        _settings.HistoryStoragePath = HistoryPathTextBox.Text;

        // Save settings with SecureString password
        try
        {
            // Get SecurePassword from PasswordBox (more secure than .Password)
            SecureString securePassword = PasswordBox.SecurePassword;
            _settingsService.SaveSettings(_settings, securePassword);

            MessageBox.Show("Settings saved successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            string safeMessage = _errorSanitizer.SanitizeForUser(ex, "SaveSettings");
            MessageBox.Show(safeMessage, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

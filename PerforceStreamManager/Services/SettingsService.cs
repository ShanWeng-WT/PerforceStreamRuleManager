using System;
using System.IO;
using System.Security;
using System.Text.Json;
using PerforceStreamManager.Models;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Service for managing application settings persistence.
    /// Passwords are encrypted using Windows DPAPI before storage.
    /// </summary>
    public class SettingsService
    {
        private readonly LoggingService _loggingService;
        private readonly string _settingsFilePath;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Track if we need to re-save for password migration
        private bool _needsPasswordMigration = false;

        /// <summary>
        /// Initializes a new instance of the SettingsService
        /// </summary>
        public SettingsService(LoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            // Store settings in AppData\Local\PerforceStreamManager
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "PerforceStreamManager");
            
            // Ensure directory exists
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
        }

        // Keep default constructor for now but it's deprecated
        public SettingsService() : this(new LoggingService())
        {
        }

        /// <summary>
        /// Loads application settings from disk.
        /// Passwords are automatically decrypted. Legacy plaintext passwords are migrated on next save.
        /// </summary>
        /// <returns>Application settings, or default settings if file doesn't exist</returns>
        public AppSettings LoadSettings()
        {
            try
            {
                _needsPasswordMigration = false;

                // If settings file doesn't exist, return default settings
                if (!File.Exists(_settingsFilePath))
                {
                    _loggingService.LogInfo($"Settings file not found at {_settingsFilePath}, creating defaults.");
                    return CreateDefaultSettings();
                }

                // Read and deserialize settings file
                string json = File.ReadAllText(_settingsFilePath);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                // Return default settings if deserialization fails
                if (settings == null)
                {
                    _loggingService.LogInfo("Failed to deserialize settings, using defaults.");
                    return CreateDefaultSettings();
                }

                // Decrypt password if present
                if (settings.Connection != null && !string.IsNullOrEmpty(settings.Connection.Password))
                {
                    // Check if password needs migration (legacy plaintext)
                    if (!SecureCredentialManager.IsEncrypted(settings.Connection.Password))
                    {
                        _loggingService.LogInfo("Detected legacy plaintext password, will migrate on save.");
                        _needsPasswordMigration = true;
                        // Password is already plaintext, no decryption needed
                    }
                    else
                    {
                        try
                        {
                            // Decrypt the password for use
                            settings.Connection.Password = SecureCredentialManager.DecryptPassword(settings.Connection.Password);
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogError(ex, "Failed to decrypt password");
                            // Clear the invalid password
                            settings.Connection.Password = null;
                        }
                    }
                }

                // Auto-migrate if needed
                if (_needsPasswordMigration)
                {
                    try
                    {
                        _loggingService.LogInfo("Migrating password to encrypted storage...");
                        SaveSettings(settings);
                        _loggingService.LogInfo("Password migration completed.");
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError(ex, "Failed to migrate password");
                        // Continue anyway - migration will be attempted again next time
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "LoadSettings");
                return CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Saves application settings to disk.
        /// Passwords are encrypted using Windows DPAPI before storage.
        /// </summary>
        /// <param name="settings">Settings to save</param>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    throw new ArgumentNullException(nameof(settings));
                }

                _loggingService.LogInfo("Saving settings.");

                // Create a copy for serialization to avoid modifying the original
                var settingsToSave = new AppSettings
                {
                    Connection = new P4ConnectionSettings
                    {
                        Server = settings.Connection?.Server ?? "",
                        Port = settings.Connection?.Port ?? "",
                        User = settings.Connection?.User ?? "",
                        Password = settings.Connection?.Password
                    },
                    HistoryStoragePath = settings.HistoryStoragePath,
                    LastUsedStream = settings.LastUsedStream
                };

                // Encrypt password before saving
                if (!string.IsNullOrEmpty(settingsToSave.Connection.Password))
                {
                    settingsToSave.Connection.Password = SecureCredentialManager.EncryptPassword(settingsToSave.Connection.Password);
                }

                // Serialize settings to JSON
                string json = JsonSerializer.Serialize(settingsToSave, _jsonOptions);

                // Write to file
                File.WriteAllText(_settingsFilePath, json);

                _needsPasswordMigration = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "SaveSettings");
                throw;
            }
        }

        /// <summary>
        /// Saves application settings with a SecureString password.
        /// This is the preferred method for setting passwords.
        /// </summary>
        /// <param name="settings">Settings to save (password will be overwritten)</param>
        /// <param name="securePassword">Secure password to save</param>
        public void SaveSettings(AppSettings settings, SecureString securePassword)
        {
            try
            {
                if (settings == null)
                {
                    throw new ArgumentNullException(nameof(settings));
                }

                _loggingService.LogInfo("Saving settings with secure password.");

                // Create a copy for serialization
                var settingsToSave = new AppSettings
                {
                    Connection = new P4ConnectionSettings
                    {
                        Server = settings.Connection?.Server ?? "",
                        Port = settings.Connection?.Port ?? "",
                        User = settings.Connection?.User ?? "",
                        Password = null
                    },
                    HistoryStoragePath = settings.HistoryStoragePath,
                    LastUsedStream = settings.LastUsedStream
                };

                // Encrypt SecureString password directly
                if (securePassword != null && securePassword.Length > 0)
                {
                    settingsToSave.Connection.Password = SecureCredentialManager.EncryptPassword(securePassword);
                }

                // Serialize settings to JSON
                string json = JsonSerializer.Serialize(settingsToSave, _jsonOptions);

                // Write to file
                File.WriteAllText(_settingsFilePath, json);

                _needsPasswordMigration = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "SaveSettings");
                throw;
            }
        }

        /// <summary>
        /// Creates default application settings
        /// </summary>
        /// <returns>Default settings</returns>
        private AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                Connection = new P4ConnectionSettings
                {
                    Server = "localhost",
                    Port = "1666",
                    User = Environment.UserName
                },
                HistoryStoragePath = "stream-history"
            };
        }
    }
}
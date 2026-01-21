using System;
using System.IO;
using System.Text.Json;
using PerforceStreamManager.Models;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Service for managing application settings persistence
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
        /// Loads application settings from disk
        /// </summary>
        /// <returns>Application settings, or default settings if file doesn't exist</returns>
        public AppSettings LoadSettings()
        {
            try
            {
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

                return settings;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "LoadSettings");
                return CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Saves application settings to disk
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

                // Serialize settings to JSON
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                
                // Write to file
                File.WriteAllText(_settingsFilePath, json);
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
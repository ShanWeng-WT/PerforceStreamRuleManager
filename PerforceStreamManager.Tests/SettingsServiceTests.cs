using NUnit.Framework;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;
using System;
using System.IO;

namespace PerforceStreamManager.Tests
{
    [TestFixture]
    public class SettingsServiceTests
    {
        private SettingsService _settingsService;
        private string _testSettingsPath;

        [SetUp]
        public void Setup()
        {
            _settingsService = new SettingsService();
            
            // Get the settings file path for cleanup
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "PerforceStreamManager");
            _testSettingsPath = Path.Combine(appFolder, "settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test settings file
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }
        }

        [Test]
        public void LoadSettings_WhenFileDoesNotExist_ReturnsDefaultSettings()
        {
            // Arrange - ensure file doesn't exist
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            // Act
            AppSettings settings = _settingsService.LoadSettings();

            // Assert
            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.Connection);
            Assert.IsNotNull(settings.Retention);
            Assert.AreEqual("localhost", settings.Connection.Server);
            Assert.AreEqual("1666", settings.Connection.Port);
            Assert.AreEqual(Environment.UserName, settings.Connection.User);
            Assert.AreEqual("//depot/stream-history", settings.HistoryStoragePath);
            Assert.AreEqual(50, settings.Retention.MaxSnapshots);
            Assert.AreEqual(365, settings.Retention.MaxAgeDays);
        }

        [Test]
        public void SaveSettings_CreatesSettingsFile()
        {
            // Arrange
            var settings = new AppSettings
            {
                Connection = new P4ConnectionSettings
                {
                    Server = "test-server",
                    Port = "1234",
                    User = "testuser"
                },
                HistoryStoragePath = "//depot/test-history",
                Retention = new RetentionPolicy
                {
                    MaxSnapshots = 100,
                    MaxAgeDays = 180
                }
            };

            // Act
            _settingsService.SaveSettings(settings);

            // Assert
            Assert.IsTrue(File.Exists(_testSettingsPath));
        }

        [Test]
        public void SaveAndLoadSettings_RoundTrip_PreservesAllValues()
        {
            // Arrange
            var originalSettings = new AppSettings
            {
                Connection = new P4ConnectionSettings
                {
                    Server = "test-server",
                    Port = "1234",
                    User = "testuser"
                },
                HistoryStoragePath = "//depot/test-history",
                Retention = new RetentionPolicy
                {
                    MaxSnapshots = 100,
                    MaxAgeDays = 180
                }
            };

            // Act
            _settingsService.SaveSettings(originalSettings);
            AppSettings loadedSettings = _settingsService.LoadSettings();

            // Assert
            Assert.IsNotNull(loadedSettings);
            Assert.AreEqual(originalSettings.Connection.Server, loadedSettings.Connection.Server);
            Assert.AreEqual(originalSettings.Connection.Port, loadedSettings.Connection.Port);
            Assert.AreEqual(originalSettings.Connection.User, loadedSettings.Connection.User);
            Assert.AreEqual(originalSettings.HistoryStoragePath, loadedSettings.HistoryStoragePath);
            Assert.AreEqual(originalSettings.Retention.MaxSnapshots, loadedSettings.Retention.MaxSnapshots);
            Assert.AreEqual(originalSettings.Retention.MaxAgeDays, loadedSettings.Retention.MaxAgeDays);
        }

        [Test]
        public void SaveSettings_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _settingsService.SaveSettings(null));
        }
    }
}

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
            var loggingService = new LoggingService();
            _settingsService = new SettingsService(loggingService);

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
            Assert.That(settings.Connection.Server, Is.EqualTo("localhost"));
            Assert.That(settings.Connection.Port, Is.EqualTo("1666"));
            Assert.That(settings.Connection.User, Is.EqualTo(Environment.UserName));
            Assert.That(settings.HistoryStoragePath, Is.EqualTo("stream-history"));
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
                HistoryStoragePath = "//depot/test-history"
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
                HistoryStoragePath = "//depot/test-history"
            };

            // Act
            _settingsService.SaveSettings(originalSettings);
            AppSettings loadedSettings = _settingsService.LoadSettings();

            // Assert
            Assert.IsNotNull(loadedSettings);
            Assert.That(loadedSettings.Connection.Server, Is.EqualTo(originalSettings.Connection.Server));
            Assert.That(loadedSettings.Connection.Port, Is.EqualTo(originalSettings.Connection.Port));
            Assert.That(loadedSettings.Connection.User, Is.EqualTo(originalSettings.Connection.User));
            Assert.That(loadedSettings.HistoryStoragePath, Is.EqualTo(originalSettings.HistoryStoragePath));
        }

        [Test]
        public void SaveSettings_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _settingsService.SaveSettings(null!));
        }
    }
}

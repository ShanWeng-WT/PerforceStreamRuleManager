using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Logging service with fallback to Windows Event Log when file logging fails.
    /// </summary>
    public class LoggingService
    {
        private readonly string _logPath;
        private const string EventLogSource = "PerforceStreamManager";
        private const string EventLogName = "Application";
        private bool _fileLoggingHealthy = true;
        private bool _eventLogAvailable;
        private const long MaxLogFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private const int MaxArchivedLogs = 5;

        /// <summary>
        /// Gets the path to the log file.
        /// </summary>
        public string LogFilePath => _logPath;

        public LoggingService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "PerforceStreamManager");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _logPath = Path.Combine(appFolder, "application.log");

            // Check if event log is available as fallback
            InitializeEventLog();

            // Check initial log file health
            CheckLogFileHealth();
        }

        /// <summary>
        /// Initializes the Windows Event Log source if possible.
        /// </summary>
        private void InitializeEventLog()
        {
            try
            {
                // Check if we can use Event Log (requires admin to create source first time)
                if (!EventLog.SourceExists(EventLogSource))
                {
                    // Note: Creating event source requires admin privileges
                    // If this fails, we'll fall back to Debug output
                    _eventLogAvailable = false;
                }
                else
                {
                    _eventLogAvailable = true;
                }
            }
            catch
            {
                _eventLogAvailable = false;
            }
        }

        /// <summary>
        /// Checks if the log file can be written to.
        /// </summary>
        private void CheckLogFileHealth()
        {
            try
            {
                // Try to open the file for writing
                using (var fs = new FileStream(_logPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    _fileLoggingHealthy = true;
                }
            }
            catch
            {
                _fileLoggingHealthy = false;
                LogToFallback("WARNING", "Log file is not writable. Using fallback logging.");
            }
        }

        /// <summary>
        /// Logs an error with full exception details.
        /// </summary>
        public void LogError(Exception? ex, string context = "")
        {
            string message;
            if (ex != null)
            {
                message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.GetType().Name}: {ex.Message}\nContext: {context}\nStack Trace: {ex.StackTrace}\n--------------------------------------------------\n";
            }
            else
            {
                message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: (null exception)\nContext: {context}\n--------------------------------------------------\n";
            }

            WriteLog(message, EventLogEntryType.Error);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public void LogWarning(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: {message}\n";
            WriteLog(logMessage, EventLogEntryType.Warning);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public void LogInfo(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n";
            WriteLog(logMessage, EventLogEntryType.Information);
        }

        /// <summary>
        /// Writes to the log file with fallback to Event Log and Debug output.
        /// </summary>
        private void WriteLog(string message, EventLogEntryType entryType)
        {
            bool loggedSuccessfully = false;

            // Try file logging first
            if (_fileLoggingHealthy)
            {
                try
                {
                    // Check if log rotation is needed
                    RotateLogIfNeeded();

                    File.AppendAllText(_logPath, message);
                    loggedSuccessfully = true;
                }
                catch (Exception fileEx)
                {
                    _fileLoggingHealthy = false;
                    LogToFallback("ERROR", $"File logging failed: {fileEx.Message}");
                }
            }

            // If file logging failed, try fallback
            if (!loggedSuccessfully)
            {
                LogToFallback(entryType.ToString(), message);
            }
        }

        /// <summary>
        /// Logs to fallback destinations (Event Log or Debug output).
        /// </summary>
        private void LogToFallback(string level, string message)
        {
            // Try Windows Event Log
            if (_eventLogAvailable)
            {
                try
                {
                    EventLogEntryType entryType = level switch
                    {
                        "ERROR" => EventLogEntryType.Error,
                        "WARNING" or "Warning" => EventLogEntryType.Warning,
                        _ => EventLogEntryType.Information
                    };

                    // Truncate message if too long for Event Log
                    string truncatedMessage = message.Length > 31000
                        ? message.Substring(0, 31000) + "...[truncated]"
                        : message;

                    EventLog.WriteEntry(EventLogSource, truncatedMessage, entryType);
                    return;
                }
                catch
                {
                    // Event log write failed, fall through to Debug
                }
            }

            // Last resort - write to Debug output
            Debug.WriteLine($"[PerforceStreamManager] {level}: {message}");
        }

        /// <summary>
        /// Gets whether file logging is currently working.
        /// </summary>
        public bool IsFileLoggingHealthy => _fileLoggingHealthy;

        /// <summary>
        /// Rotates the log file if it exceeds the maximum size.
        /// Archives old logs and keeps only the most recent ones.
        /// </summary>
        private void RotateLogIfNeeded()
        {
            try
            {
                if (!File.Exists(_logPath))
                    return;

                var fileInfo = new FileInfo(_logPath);
                if (fileInfo.Length < MaxLogFileSizeBytes)
                    return;

                // Archive the current log
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string archivePath = Path.Combine(
                    Path.GetDirectoryName(_logPath) ?? "",
                    $"application_{timestamp}.log"
                );

                File.Move(_logPath, archivePath);

                // Clean up old archives
                CleanupOldArchives();

                // Create a new log file with a rotation message
                File.WriteAllText(_logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: Log rotated. Previous log archived to {Path.GetFileName(archivePath)}\n");
            }
            catch
            {
                // If rotation fails, just continue - the file will keep growing
                // This is better than stopping logging entirely
            }
        }

        /// <summary>
        /// Removes old archived log files, keeping only the most recent ones.
        /// </summary>
        private void CleanupOldArchives()
        {
            try
            {
                string logDirectory = Path.GetDirectoryName(_logPath) ?? "";
                if (!Directory.Exists(logDirectory))
                    return;

                // Find all archived logs
                var archivedLogs = Directory.GetFiles(logDirectory, "application_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Delete old archives beyond the maximum
                foreach (var oldLog in archivedLogs.Skip(MaxArchivedLogs))
                {
                    try
                    {
                        oldLog.Delete();
                    }
                    catch
                    {
                        // Ignore errors deleting old logs
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        /// <summary>
        /// Attempts to repair file logging by retrying access to the log file.
        /// </summary>
        public void RepairFileLogging()
        {
            CheckLogFileHealth();
        }
    }
}


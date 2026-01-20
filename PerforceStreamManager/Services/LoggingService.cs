using System;
using System.IO;

namespace PerforceStreamManager.Services
{
    public class LoggingService
    {
        private readonly string _logPath;

        public LoggingService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "PerforceStreamManager");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _logPath = Path.Combine(appFolder, "application.log");
        }

        public void LogError(Exception ex, string context = "")
        {
            try
            {
                string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.GetType().Name}: {ex.Message}\nContext: {context}\nStack Trace: {ex.StackTrace}\n--------------------------------------------------\n";
                File.AppendAllText(_logPath, message);
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        public void LogInfo(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n";
                File.AppendAllText(_logPath, logMessage);
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
}

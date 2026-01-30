using System;
using Perforce.P4;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Provides error message sanitization for user-facing error displays.
    /// Logs full exception details while returning generic, safe messages to users.
    /// </summary>
    public class ErrorMessageSanitizer
    {
        private readonly LoggingService _loggingService;
        private readonly string _logFilePath;

        public ErrorMessageSanitizer(LoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            // Construct log file path for user reference
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFilePath = System.IO.Path.Combine(appData, "PerforceStreamManager", "application.log");
        }

        /// <summary>
        /// Sanitizes an exception for user display.
        /// Logs the full exception details and returns a generic, safe message.
        /// </summary>
        /// <param name="ex">The exception to sanitize</param>
        /// <param name="context">Context describing where the error occurred</param>
        /// <returns>A generic, safe error message for user display</returns>
        public string SanitizeForUser(Exception ex, string context)
        {
            if (ex == null)
            {
                return "An unknown error occurred. Check the application log for details.";
            }

            // Log full details for debugging
            _loggingService.LogError(ex, context);

            // Return generic message based on exception type
            return GetGenericMessage(ex);
        }

        /// <summary>
        /// Gets a generic error message based on exception type.
        /// Does not expose internal details, paths, or stack traces.
        /// </summary>
        private string GetGenericMessage(Exception ex)
        {
            string logHint = $"\n\nLog file: {_logFilePath}";

            return ex switch
            {
                UnauthorizedAccessException =>
                    $"Access denied. Check your permissions.{logHint}",

                P4Exception p4Ex =>
                    GetP4ErrorMessage(p4Ex, logHint),

                InvalidOperationException when ex.Message.Contains("Not connected") =>
                    $"Not connected to Perforce server. Please check your connection settings.{logHint}",

                InvalidOperationException =>
                    $"The operation could not be completed. Check the application log for details.{logHint}",

                ArgumentException =>
                    $"Invalid input provided. Please check your input and try again.{logHint}",

                // FileNotFoundException and DirectoryNotFoundException must come before IOException
                // as they are subclasses of IOException
                System.IO.FileNotFoundException =>
                    $"The requested file was not found.{logHint}",

                System.IO.DirectoryNotFoundException =>
                    $"The requested directory was not found.{logHint}",

                System.IO.IOException =>
                    $"A file operation failed. Check the application log for details.{logHint}",

                TimeoutException =>
                    $"The operation timed out. Please try again.{logHint}",

                System.Net.Sockets.SocketException =>
                    $"Network connection failed. Check your network and server settings.{logHint}",

                _ =>
                    $"An error occurred. Check the application log for details.{logHint}"
            };
        }

        /// <summary>
        /// Gets a user-friendly message for P4 exceptions without exposing server details.
        /// </summary>
        private string GetP4ErrorMessage(P4Exception p4Ex, string logHint)
        {
            // Check for common P4 error scenarios based on error severity or message patterns
            // without exposing the actual error message which may contain paths/server info

            if (p4Ex.ErrorLevel == ErrorSeverity.E_FATAL)
            {
                return $"A critical Perforce error occurred. Check the application log for details.{logHint}";
            }

            if (p4Ex.ErrorLevel == ErrorSeverity.E_FAILED)
            {
                return $"The Perforce operation failed. Check the application log for details.{logHint}";
            }

            return $"A Perforce operation error occurred. Check the application log for details.{logHint}";
        }

        /// <summary>
        /// Returns a generic error message without logging (for cases where logging is handled elsewhere).
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <returns>A generic error message</returns>
        public string GetSafeMessage(Exception ex)
        {
            return GetGenericMessage(ex);
        }

        /// <summary>
        /// Returns a simple error message for user display without the log file hint.
        /// Use this for less severe errors or when the log hint would be redundant.
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="context">Context for logging</param>
        /// <returns>A simple generic error message</returns>
        public string SanitizeForUserSimple(Exception ex, string context)
        {
            if (ex == null)
            {
                return "An unknown error occurred.";
            }

            // Log full details
            _loggingService.LogError(ex, context);

            return ex switch
            {
                UnauthorizedAccessException => "Access denied. Check your permissions.",
                P4Exception => "Perforce operation failed.",
                InvalidOperationException => "The operation could not be completed.",
                ArgumentException => "Invalid input provided.",
                System.IO.IOException => "A file operation failed.",
                TimeoutException => "The operation timed out.",
                _ => "An error occurred."
            };
        }
    }
}

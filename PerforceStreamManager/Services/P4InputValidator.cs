using System;
using System.Text.RegularExpressions;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Provides input validation for Perforce paths and commands to prevent
    /// command injection and path traversal attacks.
    /// </summary>
    public static class P4InputValidator
    {
        // Characters that could be used for command injection
        private static readonly char[] DangerousChars = { ';', '|', '&', '$', '`', '\n', '\r', '<', '>', '\0' };

        // Valid depot path pattern: starts with // and contains only safe characters
        private static readonly Regex ValidDepotPathPattern = new Regex(
            @"^//[a-zA-Z0-9_\-./]+$",
            RegexOptions.Compiled);

        // Valid stream path pattern: //depot/stream format
        private static readonly Regex ValidStreamPathPattern = new Regex(
            @"^//[a-zA-Z0-9_\-]+/[a-zA-Z0-9_\-./]+$",
            RegexOptions.Compiled);

        /// <summary>
        /// Validates a depot path for safety.
        /// </summary>
        /// <param name="depotPath">Depot path to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateDepotPath(string depotPath, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(depotPath))
            {
                error = "Depot path cannot be empty.";
                return false;
            }

            // Check for dangerous characters
            if (depotPath.IndexOfAny(DangerousChars) >= 0)
            {
                error = "Depot path contains invalid characters.";
                return false;
            }

            // Must start with //
            if (!depotPath.StartsWith("//"))
            {
                error = "Depot path must start with //.";
                return false;
            }

            // Check for path traversal attempts
            if (ContainsPathTraversal(depotPath))
            {
                error = "Path traversal is not allowed.";
                return false;
            }

            // Validate overall pattern (allow wildcards for queries)
            string pathWithoutWildcards = depotPath.Replace("*", "x").Replace("...", "x");
            if (!ValidDepotPathPattern.IsMatch(pathWithoutWildcards))
            {
                error = "Depot path contains invalid characters or format.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a stream path for safety.
        /// </summary>
        /// <param name="streamPath">Stream path to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateStreamPath(string streamPath, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(streamPath))
            {
                error = "Stream path cannot be empty.";
                return false;
            }

            // Check for dangerous characters
            if (streamPath.IndexOfAny(DangerousChars) >= 0)
            {
                error = "Stream path contains invalid characters.";
                return false;
            }

            // Must start with //
            if (!streamPath.StartsWith("//"))
            {
                error = "Stream path must start with //.";
                return false;
            }

            // Check for path traversal attempts
            if (ContainsPathTraversal(streamPath))
            {
                error = "Path traversal is not allowed.";
                return false;
            }

            // Validate stream path pattern (no wildcards allowed in stream paths)
            if (!ValidStreamPathPattern.IsMatch(streamPath))
            {
                error = "Stream path format is invalid. Expected format: //depot/stream";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a depot path and throws an exception if invalid.
        /// </summary>
        /// <param name="depotPath">Depot path to validate</param>
        /// <param name="parameterName">Parameter name for the exception</param>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        public static void ValidateDepotPathOrThrow(string depotPath, string parameterName = "depotPath")
        {
            if (!ValidateDepotPath(depotPath, out string error))
            {
                throw new ArgumentException(error, parameterName);
            }
        }

        /// <summary>
        /// Validates a stream path and throws an exception if invalid.
        /// </summary>
        /// <param name="streamPath">Stream path to validate</param>
        /// <param name="parameterName">Parameter name for the exception</param>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        public static void ValidateStreamPathOrThrow(string streamPath, string parameterName = "streamPath")
        {
            if (!ValidateStreamPath(streamPath, out string error))
            {
                throw new ArgumentException(error, parameterName);
            }
        }

        /// <summary>
        /// Sanitizes a depot path by removing or escaping dangerous characters.
        /// Use ValidateDepotPath instead when possible - this is a fallback.
        /// </summary>
        /// <param name="depotPath">Depot path to sanitize</param>
        /// <returns>Sanitized depot path</returns>
        public static string SanitizeDepotPath(string depotPath)
        {
            if (string.IsNullOrWhiteSpace(depotPath))
                return string.Empty;

            // Remove dangerous characters
            string sanitized = depotPath;
            foreach (char c in DangerousChars)
            {
                sanitized = sanitized.Replace(c.ToString(), string.Empty);
            }

            // Remove path traversal sequences
            sanitized = RemovePathTraversal(sanitized);

            return sanitized;
        }

        /// <summary>
        /// Validates a server address (host:port format).
        /// </summary>
        /// <param name="serverAddress">Server address to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateServerAddress(string serverAddress, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                error = "Server address cannot be empty.";
                return false;
            }

            // Check for dangerous characters
            if (serverAddress.IndexOfAny(DangerousChars) >= 0)
            {
                error = "Server address contains invalid characters.";
                return false;
            }

            // Basic format validation - should be hostname or IP, optionally with ssl: prefix
            string addressToCheck = serverAddress;
            if (addressToCheck.StartsWith("ssl:", StringComparison.OrdinalIgnoreCase))
            {
                addressToCheck = addressToCheck.Substring(4);
            }

            // Allow alphanumeric, dots, hyphens for hostname/IP
            if (!Regex.IsMatch(addressToCheck, @"^[a-zA-Z0-9.\-]+$"))
            {
                error = "Server address format is invalid.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a port number.
        /// </summary>
        /// <param name="port">Port string to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidatePort(string port, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(port))
            {
                error = "Port cannot be empty.";
                return false;
            }

            if (!int.TryParse(port, out int portNumber))
            {
                error = "Port must be a number.";
                return false;
            }

            if (portNumber < 1 || portNumber > 65535)
            {
                error = "Port must be between 1 and 65535.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a Perforce username.
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateUsername(string username, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                error = "Username cannot be empty.";
                return false;
            }

            // Check for dangerous characters
            if (username.IndexOfAny(DangerousChars) >= 0)
            {
                error = "Username contains invalid characters.";
                return false;
            }

            // Perforce usernames: alphanumeric, underscore, hyphen, dot
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_.\-]+$"))
            {
                error = "Username contains invalid characters.";
                return false;
            }

            if (username.Length > 256)
            {
                error = "Username is too long.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a path contains path traversal sequences.
        /// </summary>
        private static bool ContainsPathTraversal(string path)
        {
            // Check for ".." which could be used for directory traversal
            // But allow "..." which is a valid P4 wildcard

            // Split on / and check each segment
            string[] segments = path.Split('/');
            foreach (string segment in segments)
            {
                // Exact ".." is path traversal
                if (segment == "..")
                    return true;

                // ".." at start or end of segment (like "../" or "..something")
                if (segment.StartsWith("..") && segment != "...")
                    return true;
                if (segment.EndsWith("..") && segment != "...")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes path traversal sequences from a path.
        /// </summary>
        private static string RemovePathTraversal(string path)
        {
            // Remove ".." segments but preserve "..." (P4 wildcard)
            string result = path;

            // Replace standalone ".." with empty
            result = Regex.Replace(result, @"(?<!/\.)/\.\.(?!/|$)", string.Empty);
            result = Regex.Replace(result, @"(?<=^|/)\.\.(?=/|$)", string.Empty);

            // Clean up any resulting double slashes (except the leading //)
            while (result.Contains("///"))
            {
                result = result.Replace("///", "//");
            }

            return result;
        }
    }
}

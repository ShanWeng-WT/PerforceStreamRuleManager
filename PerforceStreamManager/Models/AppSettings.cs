using System.Collections.Generic;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Application settings for Perforce connection and snapshot management
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Perforce connection settings
        /// </summary>
        public P4ConnectionSettings Connection { get; set; }

        /// <summary>
        /// Depot path where snapshot files are stored.
        /// P4's versioning provides the history.
        /// </summary>
        public string HistoryStoragePath { get; set; }

        /// <summary>
        /// Last used stream path
        /// </summary>
        public string? LastUsedStream { get; set; }

        /// <summary>
        /// Session timeout in minutes. Idle sessions will be disconnected after this period.
        /// Set to 0 to disable session timeout. Default is 30 minutes.
        /// </summary>
        public int SessionTimeoutMinutes { get; set; }

        /// <summary>
        /// Whether to validate SSL/TLS certificates when connecting to Perforce.
        /// Default is true for security.
        /// </summary>
        public bool ValidateSslCertificates { get; set; }

        /// <summary>
        /// List of trusted SSL certificate fingerprints for self-signed certificates.
        /// </summary>
        public List<string> TrustedCertificateFingerprints { get; set; }

        /// <summary>
        /// Whether connection rate limiting is enabled to prevent brute force attacks.
        /// Default is true.
        /// </summary>
        public bool RateLimitingEnabled { get; set; }

        public AppSettings()
        {
            Connection = new P4ConnectionSettings();
            HistoryStoragePath = "stream-history";
            SessionTimeoutMinutes = 30; // Default 30 minutes
            ValidateSslCertificates = true; // Default enabled for security
            TrustedCertificateFingerprints = new List<string>();
            RateLimitingEnabled = true; // Default enabled for security
        }
    }

    /// <summary>
    /// Perforce connection parameters
    /// </summary>
    public class P4ConnectionSettings
    {
        /// <summary>
        /// Perforce server address
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Perforce server port
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// Perforce user name
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Perforce password (optional)
        /// </summary>
        public string? Password { get; set; }

        public P4ConnectionSettings()
        {
            Server = "";
            Port = "";
            User = "";
        }
    }
}

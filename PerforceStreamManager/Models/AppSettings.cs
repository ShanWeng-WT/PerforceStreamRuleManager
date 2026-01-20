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
        /// Depot path where snapshot history files are stored
        /// </summary>
        public string HistoryStoragePath { get; set; }

        /// <summary>
        /// Retention policy for snapshots
        /// </summary>
        public RetentionPolicy Retention { get; set; }

        /// <summary>
        /// Last used stream path
        /// </summary>
        public string? LastUsedStream { get; set; }

        public AppSettings()
        {
            Connection = new P4ConnectionSettings();
            Retention = new RetentionPolicy();
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
        }
    }

    /// <summary>
    /// Retention policy for managing snapshot history
    /// </summary>
    public class RetentionPolicy
    {
        /// <summary>
        /// Maximum number of snapshots to keep per stream
        /// </summary>
        public int MaxSnapshots { get; set; }

        /// <summary>
        /// Maximum age of snapshots in days
        /// </summary>
        public int MaxAgeDays { get; set; }

        public RetentionPolicy()
        {
            MaxSnapshots = 50; // Default value
            MaxAgeDays = 365; // Default value
        }
    }
}

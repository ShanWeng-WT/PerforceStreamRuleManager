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

        public AppSettings()
        {
            Connection = new P4ConnectionSettings();
            HistoryStoragePath = "stream-history";
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

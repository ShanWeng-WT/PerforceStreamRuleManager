using System;
using System.Collections.Generic;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents a snapshot of stream rules at a point in time
    /// </summary>
    public class Snapshot
    {
        /// <summary>
        /// Full depot path of the stream
        /// </summary>
        public string StreamPath { get; set; }

        /// <summary>
        /// When the snapshot was created
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// User who created the snapshot
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// All rules captured in this snapshot
        /// </summary>
        public List<StreamRule> Rules { get; set; }

        /// <summary>
        /// Optional description of the snapshot
        /// </summary>
        public string Description { get; set; }

        public Snapshot()
        {
            Rules = new List<StreamRule>();
            Timestamp = DateTime.UtcNow;
        }

        public Snapshot(string streamPath, string createdBy, List<StreamRule> rules, string description = null)
        {
            StreamPath = streamPath;
            Timestamp = DateTime.UtcNow;
            CreatedBy = createdBy;
            Rules = rules ?? new List<StreamRule>();
            Description = description;
        }
    }
}

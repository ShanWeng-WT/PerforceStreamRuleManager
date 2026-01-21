using System.Collections.Generic;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents a snapshot of stream rules.
    /// History is tracked by P4's versioning of the snapshot file.
    /// </summary>
    public class Snapshot
    {
        /// <summary>
        /// Full depot path of the stream
        /// </summary>
        public string StreamPath { get; set; }

        /// <summary>
        /// All rules captured in this snapshot
        /// </summary>
        public List<StreamRule> Rules { get; set; }

        public Snapshot()
        {
            StreamPath = "";
            Rules = new List<StreamRule>();
        }

        public Snapshot(string streamPath, List<StreamRule> rules)
        {
            StreamPath = streamPath;
            Rules = rules ?? new List<StreamRule>();
        }
    }
}

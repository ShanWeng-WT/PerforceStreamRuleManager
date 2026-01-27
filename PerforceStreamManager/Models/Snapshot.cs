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
        /// All rules captured in this snapshot
        /// </summary>
        public List<StreamRule> Rules { get; set; }

        public Snapshot()
        {
            Rules = new List<StreamRule>();
        }

        public Snapshot(List<StreamRule> rules)
        {
            Rules = rules ?? new List<StreamRule>();
        }
    }
}

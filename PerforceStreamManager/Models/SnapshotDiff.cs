using System.Collections.Generic;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents the differences between two snapshots
    /// </summary>
    public class SnapshotDiff
    {
        /// <summary>
        /// Rules that were added in the second snapshot
        /// </summary>
        public List<StreamRule> AddedRules { get; set; }

        /// <summary>
        /// Rules that were removed in the second snapshot
        /// </summary>
        public List<StreamRule> RemovedRules { get; set; }

        /// <summary>
        /// Rules that were modified between snapshots
        /// </summary>
        public List<RuleChange> ModifiedRules { get; set; }

        public SnapshotDiff()
        {
            AddedRules = new List<StreamRule>();
            RemovedRules = new List<StreamRule>();
            ModifiedRules = new List<RuleChange>();
        }
    }

    /// <summary>
    /// Represents a change to a rule between snapshots
    /// </summary>
    public class RuleChange
    {
        /// <summary>
        /// The rule before the change
        /// </summary>
        public StreamRule OldRule { get; set; }

        /// <summary>
        /// The rule after the change
        /// </summary>
        public StreamRule NewRule { get; set; }

        public RuleChange(StreamRule oldRule, StreamRule newRule)
        {
            OldRule = oldRule;
            NewRule = newRule;
        }
    }
}

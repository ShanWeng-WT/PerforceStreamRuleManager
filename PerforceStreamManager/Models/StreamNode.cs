using System.Collections.Generic;
using System.Linq;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents a node in the stream hierarchy tree
    /// </summary>
    public class StreamNode
    {
        /// <summary>
        /// Display name of the stream
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full depot path of the stream
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Parent stream node (null for root streams)
        /// </summary>
        public StreamNode? Parent { get; set; }

        /// <summary>
        /// Full depot path of the parent stream (null or empty for mainline streams)
        /// </summary>
        public string ParentPath { get; set; } = string.Empty;

        /// <summary>
        /// Child stream nodes
        /// </summary>
        public List<StreamNode> Children { get; set; }

        /// <summary>
        /// Rules defined locally in this stream
        /// </summary>
        public List<StreamRule> LocalRules { get; set; }

        public StreamNode()
        {
            Children = new List<StreamNode>();
            LocalRules = new List<StreamRule>();
        }

        /// <summary>
        /// Gets all rules for this stream (local + inherited from parents)
        /// </summary>
        /// <returns>List of all rules with source stream information</returns>
        public List<StreamRule> GetAllRules()
        {
            var allRules = new List<StreamRule>();
            var currentStream = this;

            while (currentStream != null)
            {
                foreach (var rule in currentStream.LocalRules)
                {
                    // Create a copy with source stream set
                    var ruleWithSource = new StreamRule(
                        rule.Type,
                        rule.Path,
                        rule.RemapTarget,
                        currentStream.Path
                    );
                    allRules.Add(ruleWithSource);
                }
                currentStream = currentStream.Parent;
            }

            return allRules;
        }

        /// <summary>
        /// Gets only inherited rules from parent streams
        /// </summary>
        /// <returns>List of inherited rules with source stream information</returns>
        public List<StreamRule> GetInheritedRules()
        {
            var inheritedRules = new List<StreamRule>();
            var currentStream = this.Parent;

            while (currentStream != null)
            {
                foreach (var rule in currentStream.LocalRules)
                {
                    // Create a copy with source stream set
                    var ruleWithSource = new StreamRule(
                        rule.Type,
                        rule.Path,
                        rule.RemapTarget,
                        currentStream.Path
                    );
                    inheritedRules.Add(ruleWithSource);
                }
                currentStream = currentStream.Parent;
            }

            return inheritedRules;
        }

        /// <summary>
        /// Gets only local rules defined in this stream
        /// </summary>
        /// <returns>List of local rules with source stream information</returns>
        public List<StreamRule> GetLocalRules()
        {
            return LocalRules.Select(rule => new StreamRule(
                rule.Type,
                rule.Path,
                rule.RemapTarget,
                this.Path
            )).ToList();
        }
    }
}

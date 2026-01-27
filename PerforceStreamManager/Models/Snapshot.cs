using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents a snapshot of stream rules for an entire stream hierarchy.
    /// History is tracked by P4's versioning of the snapshot file.
    /// </summary>
    public class Snapshot
    {
        /// <summary>
        /// Rules organized by stream path. Key is the stream path, value is the list of local rules for that stream.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<StreamRule>>? StreamRules { get; set; }

        /// <summary>
        /// Legacy property for backward compatibility - returns flattened list of all rules.
        /// When deserializing old snapshots, this will be populated instead of StreamRules.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<StreamRule>? Rules { get; set; }

        public Snapshot()
        {
            StreamRules = new Dictionary<string, List<StreamRule>>();
            Rules = null; // Don't initialize legacy property for new snapshots
        }

        /// <summary>
        /// Creates a snapshot from a dictionary of stream rules
        /// </summary>
        public Snapshot(Dictionary<string, List<StreamRule>> streamRules)
        {
            StreamRules = streamRules ?? new Dictionary<string, List<StreamRule>>();
            Rules = null; // Don't use legacy property for new snapshots
        }

        /// <summary>
        /// Legacy constructor for backward compatibility
        /// </summary>
        public Snapshot(List<StreamRule> rules)
        {
            Rules = rules ?? new List<StreamRule>();
            StreamRules = null; // Don't use new property for legacy snapshots
        }

        /// <summary>
        /// Gets all rules as a flattened list (for backward compatibility and display)
        /// </summary>
        public List<StreamRule> GetAllRules()
        {
            // If we have the new StreamRules format, flatten it
            if (StreamRules != null && StreamRules.Count > 0)
            {
                return StreamRules.SelectMany(kvp => kvp.Value.Select(rule => 
                    new StreamRule(rule.Type, rule.Path, rule.RemapTarget, kvp.Key)
                )).ToList();
            }

            // Fall back to legacy Rules list
            return Rules ?? new List<StreamRule>();
        }

        /// <summary>
        /// Gets rules for a specific stream path
        /// </summary>
        public List<StreamRule> GetRulesForStream(string streamPath)
        {
            if (StreamRules != null && StreamRules.Count > 0)
            {
                // Try exact match first
                if (StreamRules.TryGetValue(streamPath, out var rules))
                {
                    return rules;
                }
                
                // Try case-insensitive match
                var key = StreamRules.Keys.FirstOrDefault(k => 
                    string.Equals(k, streamPath, System.StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    return StreamRules[key];
                }
            }

            // Fall back to filtering legacy Rules by SourceStream
            if (Rules != null)
            {
                return Rules.Where(r => 
                    string.Equals(r.SourceStream, streamPath, System.StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return new List<StreamRule>();
        }
    }
}

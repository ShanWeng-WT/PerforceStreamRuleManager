using System;

namespace PerforceStreamManager.Models
{
    /// <summary>
    /// Represents an ignore or remap path rule in a Perforce stream
    /// </summary>
    public class StreamRule : IEquatable<StreamRule>
    {
        /// <summary>
        /// Type of rule: "ignore" or "remap"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The depot path pattern for this rule
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Target path for remap rules (null for ignore rules)
        /// </summary>
        public string RemapTarget { get; set; }

        /// <summary>
        /// The stream path that defined this rule
        /// </summary>
        public string SourceStream { get; set; }

        public StreamRule()
        {
        }

        public StreamRule(string type, string path, string remapTarget = null, string sourceStream = null)
        {
            Type = type;
            Path = path;
            RemapTarget = remapTarget;
            SourceStream = sourceStream;
        }

        // Equality comparison implementation
        public bool Equals(StreamRule other)
        {
            if (other == null)
                return false;

            return Type == other.Type &&
                   Path == other.Path &&
                   RemapTarget == other.RemapTarget &&
                   SourceStream == other.SourceStream;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StreamRule);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Type?.GetHashCode() ?? 0);
                hash = hash * 23 + (Path?.GetHashCode() ?? 0);
                hash = hash * 23 + (RemapTarget?.GetHashCode() ?? 0);
                hash = hash * 23 + (SourceStream?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static bool operator ==(StreamRule left, StreamRule right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(StreamRule left, StreamRule right)
        {
            return !(left == right);
        }
    }
}

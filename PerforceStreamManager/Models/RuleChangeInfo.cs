namespace PerforceStreamManager.Models;

/// <summary>
/// Represents a change to a stream rule (added, deleted)
/// </summary>
public class RuleChangeInfo
{
    /// <summary>
    /// Type of change (Added, Deleted)
    /// </summary>
    public RuleChangeType ChangeType { get; set; }

    /// <summary>
    /// The stream path where the change occurred
    /// </summary>
    public string StreamPath { get; set; } = string.Empty;

    /// <summary>
    /// The rule that was changed
    /// </summary>
    public StreamRule Rule { get; set; } = new();

    /// <summary>
    /// Gets a formatted description of the change
    /// </summary>
    public string Description
    {
        get
        {
            string action = ChangeType switch
            {
                RuleChangeType.Added => "Added",
                RuleChangeType.Deleted => "Deleted",
                _ => "Changed"
            };

            string ruleDesc = Rule.Type?.ToLower() == "remap"
                ? $"{Rule.Type}: {Rule.Path} → {Rule.RemapTarget}"
                : $"{Rule.Type}: {Rule.Path}";

            return $"[{action}] {ruleDesc}";
        }
    }

    /// <summary>
    /// Gets the stream name (last segment of path) for display
    /// </summary>
    public string StreamName => StreamPath?.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s)) ?? StreamPath ?? "";
}

/// <summary>
/// Type of change made to a rule
/// </summary>
public enum RuleChangeType
{
    Added,
    Deleted
}

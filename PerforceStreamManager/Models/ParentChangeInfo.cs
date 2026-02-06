namespace PerforceStreamManager.Models;

/// <summary>
/// Represents a change to a stream's parent
/// </summary>
public class ParentChangeInfo
{
    /// <summary>
    /// The stream path whose parent is being changed
    /// </summary>
    public string StreamPath { get; set; } = string.Empty;

    /// <summary>
    /// The original parent stream path (null for mainline)
    /// </summary>
    public string? OriginalParent { get; set; }

    /// <summary>
    /// The new parent stream path (null for mainline)
    /// </summary>
    public string? NewParent { get; set; }

    /// <summary>
    /// Gets a formatted description of the change
    /// </summary>
    public string Description
    {
        get
        {
            string originalDisplay = string.IsNullOrEmpty(OriginalParent) ? "(mainline)" : OriginalParent;
            string newDisplay = string.IsNullOrEmpty(NewParent) ? "(mainline)" : NewParent;
            return $"Parent: {originalDisplay} → {newDisplay}";
        }
    }

    /// <summary>
    /// Gets the stream name (last segment of path) for display
    /// </summary>
    public string StreamName => StreamPath?.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s)) ?? StreamPath ?? "";
}

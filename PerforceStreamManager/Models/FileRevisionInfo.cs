using System;

namespace PerforceStreamManager.Models;

/// <summary>
/// Represents information about a file revision in Perforce
/// </summary>
public class FileRevisionInfo
{
    /// <summary>
    /// Revision number (e.g., 1, 2, 3)
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Changelist number that created this revision
    /// </summary>
    public int Changelist { get; set; }

    /// <summary>
    /// Date and time when this revision was submitted
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// User who submitted this revision
    /// </summary>
    public string User { get; set; } = "";

    /// <summary>
    /// Changelist description
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Action performed (add, edit, delete, etc.)
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// Display string for UI (e.g., "#3 - 2026-01-21 by user - description")
    /// </summary>
    public string DisplayText => $"#{Revision} - {Date:yyyy-MM-dd HH:mm} by {User} - {TruncatedDescription}";

    /// <summary>
    /// Truncated description for display (max 50 chars)
    /// </summary>
    private string TruncatedDescription => 
        string.IsNullOrEmpty(Description) ? "(no description)" :
        Description.Length > 50 ? Description.Substring(0, 47) + "..." : Description;
}

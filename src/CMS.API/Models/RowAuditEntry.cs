namespace CMS.API.Models;

/// <summary>
/// One row of a record's audit trail, projected from <c>dbo.RowAudit</c> for the
/// history endpoint. Carries only the client-facing columns (no TableName/PrimaryKeyValues,
/// which the caller already knows).
/// </summary>
public sealed class RowAuditEntry
{
    /// <summary>When the change happened.</summary>
    public System.DateTime DateTime { get; set; }

    /// <summary>Acting user's UserName, or "system".</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>"Insert", "Update" or "Delete".</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Human description (first string column, or the changed-column list for Update).</summary>
    public string? ActionDesc { get; set; }
}

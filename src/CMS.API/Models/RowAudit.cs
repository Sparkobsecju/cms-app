namespace CMS.API.Models;

/// <summary>
/// One audit row describing a change (Insert/Update/Delete) to a single business-table row.
/// Mirrors the <c>dbo.RowAudit</c> table. <see cref="Pkid"/> is IDENTITY and never written.
/// </summary>
public sealed class RowAudit
{
    /// <summary>Primary key (int IDENTITY; assigned by the database, never inserted).</summary>
    public int Pkid { get; set; }

    /// <summary>Name of the business table that changed (e.g. "Course").</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>UserName of the signed-in user, or "system" when unauthenticated.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The changed row's pkid, rendered as a string.</summary>
    public string PrimaryKeyValues { get; set; } = string.Empty;

    /// <summary>"Insert", "Update" or "Delete".</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Human description: for Insert/Delete the first string property's value; for Update a
    /// comma-separated list of changed property names. Truncated to 1000 characters.
    /// </summary>
    public string? ActionDesc { get; set; }

    /// <summary>When the change happened.</summary>
    public System.DateTime DateTime { get; set; }
}

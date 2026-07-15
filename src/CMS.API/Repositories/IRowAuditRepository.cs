using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Read access to the <c>dbo.RowAudit</c> trail for a single record.</summary>
public interface IRowAuditRepository
{
    /// <summary>
    /// Returns the audit rows for one record (matched by table name + pkid), newest first.
    /// </summary>
    Task<IReadOnlyList<RowAuditEntry>> GetForRecordAsync(string tableName, string pkid, CancellationToken cancellationToken = default);
}

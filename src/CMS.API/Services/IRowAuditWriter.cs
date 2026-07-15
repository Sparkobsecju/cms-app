namespace CMS.API.Services;

/// <summary>
/// Cross-cutting writer that records one <c>dbo.RowAudit</c> row per business-table change.
/// Repositories call the matching method after a successful Insert / Update / Delete.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>Records an Insert. ActionDesc is the entity's first string property value.</summary>
    Task LogInsertAsync(string tableName, object entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an Update. ActionDesc lists the property names that changed between
    /// <paramref name="before"/> and <paramref name="after"/>; no row is written when nothing changed.
    /// </summary>
    Task LogUpdateAsync(string tableName, object before, object after, CancellationToken cancellationToken = default);

    /// <summary>Records a Delete. ActionDesc is the deleted entity's first string property value.</summary>
    Task LogDeleteAsync(string tableName, object entity, CancellationToken cancellationToken = default);
}

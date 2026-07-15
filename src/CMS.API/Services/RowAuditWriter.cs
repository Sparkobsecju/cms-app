using System.Data;
using System.Security.Claims;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Services;

/// <summary>
/// Generic <see cref="IRowAuditWriter"/> implementation. Derives the audit column values from
/// any entity type via <see cref="RowAuditReflection"/>, resolves the acting user from the
/// current request's JWT claims, and inserts one row into <c>dbo.RowAudit</c> with Dapper on the
/// caller-supplied connection/transaction.
/// </summary>
public sealed class RowAuditWriter : IRowAuditWriter
{
    private const string SystemUser = "system";

    // Claim names that may carry the signed-in UserName (the JWT claim from Lab 05, then the
    // standard name-claim aliases), tried in order.
    private static readonly string[] UserNameClaims =
    {
        "UserName",
        ClaimTypes.Name,
        "name",
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public RowAuditWriter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogInsertAsync(IDbConnection connection, IDbTransaction? transaction, string tableName, object entity, CancellationToken cancellationToken = default) =>
        InsertAsync(connection, transaction, BuildInsertAudit(tableName, entity), cancellationToken);

    public Task LogUpdateAsync(IDbConnection connection, IDbTransaction? transaction, string tableName, object before, object after, CancellationToken cancellationToken = default)
    {
        var audit = BuildUpdateAudit(tableName, before, after);
        // Nothing changed → no audit row.
        return audit is null ? Task.CompletedTask : InsertAsync(connection, transaction, audit, cancellationToken);
    }

    public Task LogDeleteAsync(IDbConnection connection, IDbTransaction? transaction, string tableName, object entity, CancellationToken cancellationToken = default) =>
        InsertAsync(connection, transaction, BuildDeleteAudit(tableName, entity), cancellationToken);

    /// <summary>Builds (but does not persist) the audit row for an Insert. Exposed for unit tests.</summary>
    public RowAudit BuildInsertAudit(string tableName, object entity) =>
        BuildAudit(tableName, "Insert", entity, RowAuditReflection.FirstStringPropertyValue(entity));

    /// <summary>
    /// Builds (but does not persist) the audit row for an Update, or null when no property changed.
    /// Exposed for unit tests.
    /// </summary>
    public RowAudit? BuildUpdateAudit(string tableName, object before, object after)
    {
        var changed = RowAuditReflection.ChangedPropertyNames(before, after);
        if (string.IsNullOrEmpty(changed))
        {
            return null;
        }
        return BuildAudit(tableName, "Update", after, changed);
    }

    /// <summary>Builds (but does not persist) the audit row for a Delete. Exposed for unit tests.</summary>
    public RowAudit BuildDeleteAudit(string tableName, object entity) =>
        BuildAudit(tableName, "Delete", entity, RowAuditReflection.FirstStringPropertyValue(entity));

    private RowAudit BuildAudit(string tableName, string actionType, object entity, string? actionDesc) => new()
    {
        TableName = tableName,
        UserName = ResolveUserName(),
        PrimaryKeyValues = RowAuditReflection.PrimaryKeyValue(entity),
        ActionType = actionType,
        ActionDesc = RowAuditReflection.TruncateActionDesc(actionDesc),
        DateTime = System.DateTime.Now,
    };

    /// <summary>Reads the acting UserName from the current request's claims, or "system".</summary>
    private string ResolveUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return SystemUser;
        }

        foreach (var claim in UserNameClaims)
        {
            var value = user.FindFirst(claim)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return SystemUser;
    }

    private static async Task InsertAsync(IDbConnection connection, IDbTransaction? transaction, RowAudit audit, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
            VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);";
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                audit.TableName,
                audit.UserName,
                audit.PrimaryKeyValues,
                audit.ActionType,
                audit.ActionDesc,
                audit.DateTime,
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}

using System.Security.Claims;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Services;

/// <summary>
/// Generic <see cref="IRowAuditWriter"/> implementation. Derives the audit column values from
/// any entity type via <see cref="RowAuditReflection"/>, resolves the acting user from the
/// current request's JWT claims, and inserts one row into <c>dbo.RowAudit</c> with Dapper.
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

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RowAuditWriter(IDbConnectionFactory connectionFactory, IHttpContextAccessor httpContextAccessor)
    {
        _connectionFactory = connectionFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogInsertAsync(string tableName, object entity, CancellationToken cancellationToken = default) =>
        InsertAsync(BuildInsertAudit(tableName, entity), cancellationToken);

    public Task LogUpdateAsync(string tableName, object before, object after, CancellationToken cancellationToken = default)
    {
        var audit = BuildUpdateAudit(tableName, before, after);
        // Nothing changed → no audit row.
        return audit is null ? Task.CompletedTask : InsertAsync(audit, cancellationToken);
    }

    public Task LogDeleteAsync(string tableName, object entity, CancellationToken cancellationToken = default) =>
        InsertAsync(BuildDeleteAudit(tableName, entity), cancellationToken);

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

    private async Task InsertAsync(RowAudit audit, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
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
            cancellationToken: cancellationToken));
    }
}

using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="AppRole"/>.</summary>
public sealed class AppRoleRepository : IAppRoleRepository
{
    private const string TableName = "AppRole";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection; UserCount comes from the AppUserRole junction.
    private const string SelectColumns = @"
        r.pkid AS Pkid,
        r.RoleId AS RoleId,
        r.RoleName AS RoleName,
        r.PermissionLevel AS PermissionLevel,
        r.Description AS Description,
        (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount";

    public AppRoleRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM AppRole r ORDER BY r.RoleId ASC;";
        var rows = await connection.QueryAsync<AppRole>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            FROM AppRole r
            WHERE (@Keyword IS NULL
                   OR r.RoleId LIKE '%' + @Keyword + '%'
                   OR r.RoleName LIKE '%' + @Keyword + '%'
                   OR r.Description LIKE '%' + @Keyword + '%')
              AND (@PermissionLevel IS NULL OR r.PermissionLevel = @PermissionLevel)
            ORDER BY r.RoleId ASC;";
        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.PermissionLevel,
        };
        var rows = await connection.QueryAsync<AppRole>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, roleId, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // (including the assigned UserIds) see uncommitted state on the same connection.
    private static async Task<AppRole?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, string roleId, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT {SelectColumns} FROM AppRole r WHERE r.RoleId = @RoleId;
            SELECT UserId FROM AppUserRole WHERE RoleId = @RoleId ORDER BY UserId;";
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        var role = await multi.ReadSingleOrDefaultAsync<AppRole>();
        if (role is null)
        {
            return null;
        }

        role.UserIds = (await multi.ReadAsync<string>()).AsList();
        return role;
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
            VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description,
            }, transaction, cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        var created = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return request.RoleId;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);
        if (before is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE AppRole
            SET RoleName = @RoleName,
                PermissionLevel = @PermissionLevel,
                Description = @Description
            WHERE RoleId = @RoleId;",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description,
            }, transaction, cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        var after = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);
        await _auditWriter.LogUpdateAsync(connection, transaction, TableName, before, after!, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var row = await GetByIdAsync(connection, transaction, roleId, cancellationToken);
        if (row is null)
        {
            transaction.Rollback();
            return false;
        }

        // Remove junction rows first to satisfy the FK constraint.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }

    // N-N sync: delete-then-reinsert the AppUserRole rows for this role.
    private static async Task ReplaceUserAssignmentsAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string roleId,
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        var distinctUserIds = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctUserIds.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            distinctUserIds.Select(userId => new { UserId = userId, RoleId = roleId }),
            transaction, cancellationToken: cancellationToken));
    }
}

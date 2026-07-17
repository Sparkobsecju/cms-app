using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="AppUser"/>.</summary>
public sealed class AppUserRepository : IAppUserRepository
{
    private const string TableName = "AppUser";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection; RoleCount comes from the AppUserRole junction.
    // PasswordHash is deliberately never selected — it must not reach the client.
    private const string SelectColumns = @"
        u.pkid AS Pkid,
        u.UserId AS UserId,
        u.UserName AS UserName,
        u.IsActive AS IsActive,
        u.PasswordUpdatedTime AS PasswordUpdatedTime,
        (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount";

    public AppUserRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM AppUser u ORDER BY u.UserId ASC;";
        var rows = await connection.QueryAsync<AppUser>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            FROM AppUser u
            WHERE (@Keyword IS NULL
                   OR u.UserId LIKE '%' + @Keyword + '%' ESCAPE '\'
                   OR u.UserName LIKE '%' + @Keyword + '%' ESCAPE '\')
              AND (@IsActive IS NULL OR u.IsActive = @IsActive)
            ORDER BY u.UserId ASC;";
        var parameters = new
        {
            Keyword = SqlLike.EscapeWildcards(string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()),
            query.IsActive,
        };
        var rows = await connection.QueryAsync<AppUser>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, userId, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // (including the assigned RoleIds) see uncommitted state on the same connection. PasswordHash
    // is never selected, so it never reaches an audit row.
    private static async Task<AppUser?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, string userId, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT {SelectColumns} FROM AppUser u WHERE u.UserId = @UserId;
            SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;";
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        var user = await multi.ReadSingleOrDefaultAsync<AppUser>();
        if (user is null)
        {
            return null;
        }

        user.RoleIds = (await multi.ReadAsync<string>()).AsList();
        return user;
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId }, cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<string> CreateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Password is set server-side from the configured default; never supplied by the client.
        var passwordHash = await ReadDefaultPasswordHashAsync(connection, transaction, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, GETDATE());",
            new
            {
                request.UserId,
                request.UserName,
                request.IsActive,
                PasswordHash = passwordHash,
            }, transaction, cancellationToken: cancellationToken));

        await ReplaceRoleAssignmentsAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        var created = await GetByIdAsync(connection, transaction, request.UserId, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return request.UserId;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await GetByIdAsync(connection, transaction, request.UserId, cancellationToken);
        if (before is null)
        {
            transaction.Rollback();
            return false;
        }

        // PasswordHash / PasswordUpdatedTime are intentionally untouched here.
        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE AppUser
            SET UserName = @UserName,
                IsActive = @IsActive
            WHERE UserId = @UserId;",
            new
            {
                request.UserId,
                request.UserName,
                request.IsActive,
            }, transaction, cancellationToken: cancellationToken));

        await ReplaceRoleAssignmentsAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        var after = await GetByIdAsync(connection, transaction, request.UserId, cancellationToken);
        await _auditWriter.LogUpdateAsync(connection, transaction, TableName, before, after!, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var row = await GetByIdAsync(connection, transaction, userId, cancellationToken);
        if (row is null)
        {
            transaction.Rollback();
            return false;
        }

        // Remove junction rows first to satisfy the FK constraint.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Re-read the configured default password and re-hash it; no value comes from the caller.
        var passwordHash = await ReadDefaultPasswordHashAsync(connection, transaction: null, cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = GETDATE()
            WHERE UserId = @UserId;",
            new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    // Reads SysConfig['appConfig'].defaultPassword and returns its SHA-256 hex hash.
    private static async Task<string> ReadDefaultPasswordHashAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var configValue = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'",
            transaction: transaction, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new InvalidOperationException("SysConfig 'appConfig' entry is missing.");
        }

        string? defaultPassword;
        using (var doc = JsonDocument.Parse(configValue))
        {
            if (!doc.RootElement.TryGetProperty("defaultPassword", out var element))
            {
                throw new InvalidOperationException("SysConfig 'appConfig' has no 'defaultPassword' property.");
            }
            defaultPassword = element.GetString();
        }

        if (string.IsNullOrEmpty(defaultPassword))
        {
            throw new InvalidOperationException("SysConfig 'appConfig'.defaultPassword is empty.");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(defaultPassword));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // N-N sync: delete-then-reinsert the AppUserRole rows for this user.
    private static async Task ReplaceRoleAssignmentsAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string userId,
        IEnumerable<string> roleIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        var distinctRoleIds = roleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctRoleIds.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            distinctRoleIds.Select(roleId => new { UserId = userId, RoleId = roleId }),
            transaction, cancellationToken: cancellationToken));
    }
}

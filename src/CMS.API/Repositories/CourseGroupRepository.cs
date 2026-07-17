using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="CourseGroup"/>.</summary>
public sealed class CourseGroupRepository : ICourseGroupRepository
{
    private const string TableName = "CourseGroup";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection.
    private const string SelectColumns = @"
        g.pkid AS Pkid,
        g.Description AS Description";

    public CourseGroupRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM CourseGroup g ORDER BY g.pkid DESC;";
        var rows = await connection.QueryAsync<CourseGroup>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            FROM CourseGroup g
            WHERE (@Keyword IS NULL OR g.Description LIKE '%' + @Keyword + '%' ESCAPE '\')
            ORDER BY g.pkid DESC;";
        var parameters = new
        {
            Keyword = SqlLike.EscapeWildcards(string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()),
        };
        var rows = await connection.QueryAsync<CourseGroup>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, pkid, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // see uncommitted state on the same connection.
    private static async Task<CourseGroup?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, short pkid, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM CourseGroup g WHERE g.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<CourseGroup>(
            new CommandDefinition(sql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    // pkid is smallint IDENTITY — assigned by the DB; read back via SCOPE_IDENTITY.
    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            INSERT INTO CourseGroup (Description) VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new { request.Description }, transaction, cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        if (before is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE CourseGroup SET Description = @Description WHERE pkid = @Pkid;",
            new { request.Pkid, request.Description }, transaction, cancellationToken: cancellationToken));

        var after = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogUpdateAsync(connection, transaction, TableName, before, after!, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var row = await GetByIdAsync(connection, transaction, pkid, cancellationToken);
        if (row is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }
}

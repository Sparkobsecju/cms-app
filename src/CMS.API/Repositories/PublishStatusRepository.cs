using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="PublishStatus"/>.</summary>
public sealed class PublishStatusRepository : IPublishStatusRepository
{
    private const string TableName = "PublishStatus";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection.
    private const string SelectColumns = @"
        s.pkid AS Pkid,
        s.Description AS Description,
        s.IsDraft AS IsDraft,
        s.IsPublished AS IsPublished,
        s.IsDiscontinued AS IsDiscontinued";

    public PublishStatusRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM PublishStatus s ORDER BY s.pkid ASC;";
        var rows = await connection.QueryAsync<PublishStatus>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            FROM PublishStatus s
            WHERE (@Keyword IS NULL OR s.Description LIKE '%' + @Keyword + '%')
              AND (@IsDraft IS NULL OR s.IsDraft = @IsDraft)
              AND (@IsPublished IS NULL OR s.IsPublished = @IsPublished)
              AND (@IsDiscontinued IS NULL OR s.IsDiscontinued = @IsDiscontinued)
            ORDER BY s.pkid ASC;";
        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.IsDraft,
            query.IsPublished,
            query.IsDiscontinued,
        };
        var rows = await connection.QueryAsync<PublishStatus>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, pkid, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // see uncommitted state on the same connection.
    private static async Task<PublishStatus?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, byte pkid, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM PublishStatus s WHERE s.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<PublishStatus>(
            new CommandDefinition(sql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    // pkid is caller-supplied (tinyint, NOT IDENTITY) — written explicitly, no SCOPE_IDENTITY.
    public async Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
            VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);",
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued,
            }, transaction, cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        if (before is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE PublishStatus
            SET Description = @Description,
                IsDraft = @IsDraft,
                IsPublished = @IsPublished,
                IsDiscontinued = @IsDiscontinued
            WHERE pkid = @Pkid;",
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued,
            }, transaction, cancellationToken: cancellationToken));

        var after = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogUpdateAsync(connection, transaction, TableName, before, after!, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
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
            "DELETE FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }
}

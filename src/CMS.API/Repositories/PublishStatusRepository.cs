using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="PublishStatus"/>.</summary>
public sealed class PublishStatusRepository : IPublishStatusRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    // Shared SELECT projection.
    private const string SelectColumns = @"
        s.pkid AS Pkid,
        s.Description AS Description,
        s.IsDraft AS IsDraft,
        s.IsPublished AS IsPublished,
        s.IsDiscontinued AS IsDiscontinued";

    public PublishStatusRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
        var sql = $"SELECT {SelectColumns} FROM PublishStatus s WHERE s.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<PublishStatus>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
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
            }, cancellationToken: cancellationToken));

        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
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
            }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}

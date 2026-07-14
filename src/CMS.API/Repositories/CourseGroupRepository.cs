using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="CourseGroup"/>.</summary>
public sealed class CourseGroupRepository : ICourseGroupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    // Shared SELECT projection.
    private const string SelectColumns = @"
        g.pkid AS Pkid,
        g.Description AS Description";

    public CourseGroupRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
            WHERE (@Keyword IS NULL OR g.Description LIKE '%' + @Keyword + '%')
            ORDER BY g.pkid DESC;";
        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
        };
        var rows = await connection.QueryAsync<CourseGroup>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM CourseGroup g WHERE g.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<CourseGroup>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    // pkid is smallint IDENTITY — assigned by the DB; read back via SCOPE_IDENTITY.
    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            INSERT INTO CourseGroup (Description) VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new { request.Description }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE CourseGroup SET Description = @Description WHERE pkid = @Pkid;",
            new { request.Pkid, request.Description }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}

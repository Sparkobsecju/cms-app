using CMS.API.Data;
using CMS.API.Models.Lookups;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for lookup lists.</summary>
public sealed class LookupRepository : ILookupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LookupRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppUserLookup>(new CommandDefinition(
            "SELECT UserId, UserName FROM AppUser WHERE IsActive = 1 ORDER BY UserName ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublishStatusLookup>(new CommandDefinition(
            "SELECT pkid AS Pkid, Description FROM PublishStatus ORDER BY pkid ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CourseGroupLookup>(new CommandDefinition(
            "SELECT pkid AS Pkid, Description FROM CourseGroup ORDER BY pkid ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}

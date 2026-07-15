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

    public async Task<IReadOnlyList<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PartnerLookup>(new CommandDefinition(
            "SELECT pkid AS Pkid, Name FROM Partner ORDER BY DisplayOrder ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Title is nchar(100) → RTRIM; JOIN Partner for a disambiguated label.
        var rows = await connection.QueryAsync<CertificationLookup>(new CommandDefinition(
            @"SELECT ce.pkid AS Pkid, p.Name AS PartnerName, RTRIM(ce.Title) AS Title
              FROM Certification ce
              JOIN Partner p ON p.pkid = ce.Partner_pkid
              ORDER BY p.Name ASC, ce.Title ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<JobCategoryLookup>(new CommandDefinition(
            "SELECT pkid AS Pkid, Description FROM JobCategory ORDER BY Description ASC;",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}

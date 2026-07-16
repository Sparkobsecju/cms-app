using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="Partner"/>.</summary>
public sealed class PartnerRepository : IPartnerRepository
{
    private const string TableName = "Partner";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection.
    private const string SelectColumns = @"
        p.pkid AS Pkid,
        p.Name AS Name,
        p.AppKey AS AppKey,
        p.NameOnPartnerMenu AS NameOnPartnerMenu,
        p.NameOnCourseDetailPage AS NameOnCourseDetailPage,
        p.DisplayOrder AS DisplayOrder,
        p.ImageFilename AS ImageFilename";

    public PartnerRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM Partner p ORDER BY p.DisplayOrder ASC, p.pkid ASC;";
        var rows = await connection.QueryAsync<Partner>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            FROM Partner p
            WHERE (@Keyword IS NULL
                OR p.Name LIKE '%' + @Keyword + '%'
                OR p.AppKey LIKE '%' + @Keyword + '%'
                OR p.NameOnPartnerMenu LIKE '%' + @Keyword + '%'
                OR p.NameOnCourseDetailPage LIKE '%' + @Keyword + '%')
            ORDER BY p.DisplayOrder ASC, p.pkid ASC;";
        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
        };
        var rows = await connection.QueryAsync<Partner>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, pkid, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // see uncommitted state on the same connection.
    private static async Task<Partner?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, short pkid, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM Partner p WHERE p.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<Partner>(
            new CommandDefinition(sql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
    }

    // pkid is smallint IDENTITY — assigned by the DB; read back via SCOPE_IDENTITY.
    public async Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
            VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new
            {
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                ImageFilename = string.IsNullOrWhiteSpace(request.ImageFilename) ? null : request.ImageFilename,
            },
            transaction, cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
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
            UPDATE Partner
            SET Name = @Name,
                AppKey = @AppKey,
                NameOnPartnerMenu = @NameOnPartnerMenu,
                NameOnCourseDetailPage = @NameOnCourseDetailPage,
                DisplayOrder = @DisplayOrder,
                ImageFilename = @ImageFilename
            WHERE pkid = @Pkid;",
            new
            {
                request.Pkid,
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                ImageFilename = string.IsNullOrWhiteSpace(request.ImageFilename) ? null : request.ImageFilename,
            },
            transaction, cancellationToken: cancellationToken));

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
            "DELETE FROM Partner WHERE pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }
}

using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Services;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Proves the RowAudit retrofit end-to-end for a representative repository
/// (<see cref="PublishStatusRepository"/>) by running its real Dapper SQL against an in-memory
/// SQLite database — no SQL Server required. PublishStatus is chosen because its SQL is portable
/// (caller-supplied PK, no SCOPE_IDENTITY, no JOINs). Verifies that Insert/Update/Delete each
/// write the correct RowAudit row and that a failed change writes none (audit shares the same
/// transaction as the change).
/// </summary>
public sealed class PublishStatusRepositoryAuditTests : IDisposable
{
    // A named, shared-cache in-memory DB lives as long as one connection stays open; the master
    // connection below keeps it alive for the lifetime of the test instance.
    private readonly string _connectionString =
        $"Data Source=audit_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;
    private readonly PublishStatusRepository _repository;

    public PublishStatusRepositoryAuditTests()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        _keepAlive.Execute(@"
            CREATE TABLE PublishStatus (
                pkid INTEGER PRIMARY KEY,
                Description TEXT NOT NULL,
                IsDraft INTEGER NOT NULL,
                IsPublished INTEGER NOT NULL,
                IsDiscontinued INTEGER NOT NULL
            );
            CREATE TABLE RowAudit (
                pkid INTEGER PRIMARY KEY AUTOINCREMENT,
                TableName TEXT NOT NULL,
                UserName TEXT NOT NULL,
                PrimaryKeyValues TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                ActionDesc TEXT NULL,
                [DateTime] TEXT NOT NULL
            );");

        // No HttpContext → UserName resolves to "system".
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var auditWriter = new RowAuditWriter(accessor.Object);

        _repository = new PublishStatusRepository(new SqliteConnectionFactory(_connectionString), auditWriter);
    }

    public void Dispose() => _keepAlive.Dispose();

    // ----- Insert -----

    [Fact]
    public async Task Create_WritesInsertAudit_WithFirstStringColumn()
    {
        var request = new PublishStatusRequest
        {
            Pkid = 50,
            Description = "草稿",
            IsDraft = true,
            IsPublished = false,
            IsDiscontinued = false,
        };

        await _repository.CreateAsync(request, CancellationToken.None);

        var audit = Assert.Single(ReadAudits());
        Assert.Equal("PublishStatus", audit.TableName);
        Assert.Equal("Insert", audit.ActionType);
        Assert.Equal("50", audit.PrimaryKeyValues);
        Assert.Equal("草稿", audit.ActionDesc);       // Description is the first string property
        Assert.Equal("system", audit.UserName);
    }

    // ----- Update -----

    [Fact]
    public async Task Update_WritesUpdateAudit_ListingExactlyTheChangedColumns()
    {
        Seed(60, "草稿", isDraft: true, isPublished: false, isDiscontinued: false);

        var request = new PublishStatusRequest
        {
            Pkid = 60,
            Description = "已上架",     // changed
            IsDraft = true,             // unchanged
            IsPublished = true,         // changed
            IsDiscontinued = false,     // unchanged
        };

        var ok = await _repository.UpdateAsync(request, CancellationToken.None);

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits());
        Assert.Equal("Update", audit.ActionType);
        Assert.Equal("60", audit.PrimaryKeyValues);
        Assert.Equal("Description, IsPublished", audit.ActionDesc);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_WritesDeleteAudit_WithFirstStringColumn()
    {
        Seed(70, "已停用", isDraft: false, isPublished: false, isDiscontinued: true);

        var ok = await _repository.DeleteAsync(70, CancellationToken.None);

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits());
        Assert.Equal("Delete", audit.ActionType);
        Assert.Equal("70", audit.PrimaryKeyValues);
        Assert.Equal("已停用", audit.ActionDesc);
    }

    // ----- Failed change leaves no audit row -----

    [Fact]
    public async Task Update_MissingRow_WritesNoAudit()
    {
        var request = new PublishStatusRequest { Pkid = 99, Description = "Ghost" };

        var ok = await _repository.UpdateAsync(request, CancellationToken.None);

        Assert.False(ok);
        Assert.Empty(ReadAudits());
    }

    [Fact]
    public async Task Delete_MissingRow_WritesNoAudit()
    {
        var ok = await _repository.DeleteAsync(99, CancellationToken.None);

        Assert.False(ok);
        Assert.Empty(ReadAudits());
    }

    // ----- helpers -----

    private void Seed(byte pkid, string description, bool isDraft, bool isPublished, bool isDiscontinued) =>
        _keepAlive.Execute(
            "INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued) VALUES (@pkid, @description, @isDraft, @isPublished, @isDiscontinued);",
            new { pkid, description, isDraft, isPublished, isDiscontinued });

    private IReadOnlyList<AuditRow> ReadAudits() =>
        _keepAlive.Query<AuditRow>(
            "SELECT TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc FROM RowAudit ORDER BY pkid;").AsList();

    private sealed record AuditRow(string TableName, string UserName, string PrimaryKeyValues, string ActionType, string? ActionDesc);

    // Opens a fresh connection to the shared in-memory DB for each repository call.
    private sealed class SqliteConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        public SqliteConnectionFactory(string connectionString) => _connectionString = connectionString;

        public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}

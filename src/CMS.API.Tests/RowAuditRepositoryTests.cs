using System.Data;
using CMS.API.Data;
using CMS.API.Repositories;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CMS.API.Tests;

/// <summary>
/// Proves <see cref="RowAuditRepository"/> against an in-memory SQLite database (its read SQL is
/// portable — no SQL Server specifics): the history is filtered by TableName + pkid and returned
/// newest first. No SQL Server required.
/// </summary>
public sealed class RowAuditRepositoryTests : IDisposable
{
    private readonly string _connectionString =
        $"Data Source=auditread_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;
    private readonly RowAuditRepository _repository;

    public RowAuditRepositoryTests()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        _keepAlive.Execute(@"
            CREATE TABLE RowAudit (
                pkid INTEGER PRIMARY KEY AUTOINCREMENT,
                TableName TEXT NOT NULL,
                UserName TEXT NOT NULL,
                PrimaryKeyValues TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                ActionDesc TEXT NULL,
                [DateTime] TEXT NOT NULL
            );");

        // Target record (Course #123): three changes at increasing times.
        Seed("Course", "123", "Insert", "課程A", new DateTime(2026, 6, 1, 9, 0, 0));
        Seed("Course", "123", "Update", "Title", new DateTime(2026, 6, 2, 10, 0, 0));
        Seed("Course", "123", "Delete", "課程A", new DateTime(2026, 6, 3, 11, 0, 0));
        // Decoys that must be filtered out.
        Seed("Course", "124", "Insert", "課程B", new DateTime(2026, 6, 4, 12, 0, 0));
        Seed("Partner", "123", "Insert", "原廠X", new DateTime(2026, 6, 5, 13, 0, 0));

        _repository = new RowAuditRepository(new SqliteConnectionFactory(_connectionString));
    }

    public void Dispose() => _keepAlive.Dispose();

    [Fact]
    public async Task GetForRecord_ReturnsOnlyMatchingRows_NewestFirst()
    {
        var rows = await _repository.GetForRecordAsync("Course", "123", CancellationToken.None);

        // Only the three Course/123 rows, newest DateTime first.
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "Delete", "Update", "Insert" }, rows.Select(r => r.ActionType).ToArray());
        Assert.Equal(new DateTime(2026, 6, 3, 11, 0, 0), rows[0].DateTime);
        Assert.Equal("Title", rows[1].ActionDesc);
    }

    [Fact]
    public async Task GetForRecord_ReturnsEmpty_WhenNoHistory()
    {
        var rows = await _repository.GetForRecordAsync("Course", "999", CancellationToken.None);

        Assert.Empty(rows);
    }

    private void Seed(string tableName, string pkid, string actionType, string actionDesc, DateTime when) =>
        _keepAlive.Execute(
            "INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]) VALUES (@tableName, 'system', @pkid, @actionType, @actionDesc, @when);",
            new { tableName, pkid, actionType, actionDesc, when });

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

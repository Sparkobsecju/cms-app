using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based read access for a record's <see cref="RowAuditEntry"/> history.</summary>
public sealed class RowAuditRepository : IRowAuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RowAuditRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RowAuditEntry>> GetForRecordAsync(string tableName, string pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Newest first: [DateTime] descending, tie-broken by the IDENTITY pkid (insert order).
        const string sql = @"
            SELECT [DateTime] AS DateTime, UserName AS UserName, ActionType AS ActionType, ActionDesc AS ActionDesc
            FROM RowAudit
            WHERE TableName = @TableName AND PrimaryKeyValues = @Pkid
            ORDER BY [DateTime] DESC, pkid DESC;";
        var rows = await connection.QueryAsync<RowAuditEntry>(
            new CommandDefinition(sql, new { TableName = tableName, Pkid = pkid }, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}

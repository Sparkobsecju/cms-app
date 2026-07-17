using System.Data;
using Microsoft.Data.SqlClient;

namespace CMS.API.Data;

/// <summary>SQL Server implementation of <see cref="IDbConnectionFactory"/>.</summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CMS")
            ?? throw new InvalidOperationException("Connection string 'CMS' is not configured.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            // Open failed (transient fault, cancellation) — dispose so the half-built
            // connection is returned to the pool instead of leaking under load.
            await connection.DisposeAsync();
            throw;
        }
        return connection;
    }
}

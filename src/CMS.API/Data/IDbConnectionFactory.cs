using System.Data;

namespace CMS.API.Data;

/// <summary>Creates open connections to the CMS database.</summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates and opens a new database connection.</summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

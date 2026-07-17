using System.Data;
using CMS.API.Data;
using CMS.API.Services;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests;

/// <summary>
/// Covers <see cref="SigningKeyProvider"/> end-to-end against an in-memory SQLite
/// <c>SysConfig</c> table (the review flagged its load/cache/error paths as untested). Proves the
/// JSON extraction, the caching, and the new minimum-key-length guard (HMAC-SHA256 needs a
/// >=32-byte key — a short secret is rejected fail-closed rather than used).
/// </summary>
public sealed class SigningKeyProviderTests : IDisposable
{
    private readonly string _connectionString =
        $"Data Source=signingkey_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public SigningKeyProviderTests()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        _keepAlive.Execute(@"
            CREATE TABLE SysConfig (
                configKey TEXT NOT NULL,
                configValue TEXT NULL
            );");
    }

    public void Dispose() => _keepAlive.Dispose();

    private void SeedAppConfig(string? configValue) =>
        _keepAlive.Execute(
            "INSERT INTO SysConfig (configKey, configValue) VALUES ('appConfig', @configValue);",
            new { configValue });

    private SigningKeyProvider CreateProvider() =>
        new(new SqliteConnectionFactory(_connectionString));

    [Fact]
    public void GetSigningKey_ReturnsSymmetricKey_ForValidSecret()
    {
        const string secret = "this-is-a-sufficiently-long-signing-key-0123456789";
        SeedAppConfig($"{{\"symmetricSecurityKey\":\"{secret}\"}}");

        var key = Assert.IsType<SymmetricSecurityKey>(CreateProvider().GetSigningKey());

        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(secret), key.Key);
    }

    [Fact]
    public void GetSigningKey_CachesAfterFirstRead()
    {
        SeedAppConfig("{\"symmetricSecurityKey\":\"this-is-a-sufficiently-long-signing-key-0123456789\"}");
        var provider = CreateProvider();

        Assert.Same(provider.GetSigningKey(), provider.GetSigningKey());
    }

    [Fact]
    public void GetSigningKey_Throws_WhenKeyShorterThanMinimum()
    {
        // 31 bytes — one short of the 32-byte (256-bit) HMAC-SHA256 minimum.
        var shortKey = new string('a', SigningKeyProvider.MinKeyBytes - 1);
        SeedAppConfig($"{{\"symmetricSecurityKey\":\"{shortKey}\"}}");

        var ex = Assert.Throws<InvalidOperationException>(() => CreateProvider().GetSigningKey());
        Assert.Contains("at least", ex.Message);
    }

    [Fact]
    public void GetSigningKey_Throws_WhenAppConfigRowMissing()
    {
        // No SysConfig row seeded.
        Assert.Throws<InvalidOperationException>(() => CreateProvider().GetSigningKey());
    }

    [Fact]
    public void GetSigningKey_Throws_WhenSecretPropertyMissing()
    {
        SeedAppConfig("{\"somethingElse\":\"value\"}");
        Assert.Throws<InvalidOperationException>(() => CreateProvider().GetSigningKey());
    }

    [Fact]
    public void GetSigningKey_Throws_WhenSecretEmpty()
    {
        SeedAppConfig("{\"symmetricSecurityKey\":\"\"}");
        Assert.Throws<InvalidOperationException>(() => CreateProvider().GetSigningKey());
    }

    // Opens a fresh connection to the shared in-memory DB for each call.
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

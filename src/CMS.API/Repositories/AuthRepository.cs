using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for the login flow.</summary>
public sealed class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // All three checks live in the WHERE clause so a failure of any one returns no row — the caller
        // can't tell which part failed. PasswordHash is compared, never projected out.
        var passwordHash = HashPassword(password);
        var user = await connection.QuerySingleOrDefaultAsync<AuthenticatedUser>(new CommandDefinition(@"
            SELECT UserId AS UserId, UserName AS UserName
            FROM AppUser
            WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash;",
            new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken));

        if (user is null)
        {
            return null;
        }

        var roles = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;",
            new { user.UserId }, cancellationToken: cancellationToken));
        user.RoleIds = roles.AsList();
        return user;
    }

    public async Task<string> GetSigningSecretAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await ReadSigningSecretAsync(connection, cancellationToken);
    }

    // Reads SysConfig['appConfig'].symmetricSecurityKey.
    private static async Task<string> ReadSigningSecretAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var configValue = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'",
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new InvalidOperationException("SysConfig 'appConfig' entry is missing.");
        }

        string? secret;
        using (var doc = JsonDocument.Parse(configValue))
        {
            if (!doc.RootElement.TryGetProperty("symmetricSecurityKey", out var element))
            {
                throw new InvalidOperationException("SysConfig 'appConfig' has no 'symmetricSecurityKey' property.");
            }
            secret = element.GetString();
        }

        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("SysConfig 'appConfig'.symmetricSecurityKey is empty.");
        }

        return secret;
    }

    // SHA-256 hex, lower-case — the same scheme AppUserRepository uses when writing PasswordHash.
    private static string HashPassword(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

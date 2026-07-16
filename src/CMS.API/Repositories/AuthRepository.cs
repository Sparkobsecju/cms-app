using System.Data;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
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

    public async Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Only UserName is written; UserId, roles, IsActive and PasswordHash are untouched. This is a
        // self-service action endpoint (like reset-password), so — consistent with AuthController — it is
        // not row-audited.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId;",
            new { UserId = userId, UserName = userName }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> VerifyCurrentPasswordAsync(string userId, string currentPassword, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // The hash is compared inside the WHERE clause and never selected out, so the stored PasswordHash
        // never leaves the repository — the same discipline as ValidateCredentialsAsync.
        var passwordHash = HashPassword(currentPassword);
        var match = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM AppUser WHERE UserId = @UserId AND PasswordHash = @PasswordHash;",
            new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken));

        return match is not null;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Hash the new plaintext here (it never leaves this method) and stamp PasswordUpdatedTime in the
        // same UPDATE. Mirrors AppUserRepository.ResetPasswordAsync; a self-service action, so not audited.
        var passwordHash = HashPassword(newPassword);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = GETDATE()
            WHERE UserId = @UserId;",
            new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken));

        return affected > 0;
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
    private static string HashPassword(string password) => PasswordHasher.Hash(password);
}

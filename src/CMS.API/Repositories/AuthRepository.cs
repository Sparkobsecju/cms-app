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

        // Fetch the stored hash for an active user. Verification happens in-process because PBKDF2 hashes are
        // salted and cannot be compared in SQL. PasswordHash is read into this method only and never leaves
        // it (it is not on AuthenticatedUser and never reaches the client). A missing row — unknown user OR
        // IsActive = 0 — is indistinguishable to the caller from a wrong password: both return null.
        var record = await connection.QuerySingleOrDefaultAsync<CredentialRecord>(new CommandDefinition(@"
            SELECT UserId AS UserId, UserName AS UserName, PasswordHash AS PasswordHash
            FROM AppUser
            WHERE UserId = @UserId AND IsActive = 1;",
            new { UserId = userId }, cancellationToken: cancellationToken));

        if (record is null || !PasswordHasher.Verify(password, record.PasswordHash, out var needsRehash))
        {
            return null;
        }

        // Lazy migration: the stored hash used the deprecated unsalted SHA-256 scheme. We now hold the
        // verified plaintext, so re-hash it under PBKDF2 and persist — upgrading the weak hash without
        // forcing a password reset. PasswordUpdatedTime is deliberately NOT stamped: the password itself
        // did not change, only its storage format.
        if (needsRehash)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE AppUser SET PasswordHash = @PasswordHash WHERE UserId = @UserId;",
                new { UserId = record.UserId, PasswordHash = PasswordHasher.Hash(password) },
                cancellationToken: cancellationToken));
        }

        var user = new AuthenticatedUser { UserId = record.UserId, UserName = record.UserName };
        var roles = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;",
            new { user.UserId }, cancellationToken: cancellationToken));
        user.RoleIds = roles.AsList();
        return user;
    }

    // Backend-only projection carrying PasswordHash for in-process verification. Never returned to callers.
    private sealed class CredentialRecord
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
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

        // Read the stored hash into this method only (never projected to the client) and verify in-process,
        // since PBKDF2 hashes are salted and cannot be compared in SQL. Deprecated SHA-256 hashes still
        // verify here; no rehash is done because the very next step (ChangePasswordAsync) rewrites the hash.
        var storedHash = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT PasswordHash FROM AppUser WHERE UserId = @UserId;",
            new { UserId = userId }, cancellationToken: cancellationToken));

        return PasswordHasher.Verify(currentPassword, storedHash, out _);
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

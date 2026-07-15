using System.Text;
using System.Text.Json;
using CMS.API.Data;
using Dapper;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Services;

/// <summary>
/// Reads the JWT signing secret from <c>SysConfig['appConfig'].symmetricSecurityKey</c> and exposes it
/// as a <see cref="SecurityKey"/> for token validation. The value is the same key the
/// <c>AuthController</c> issues tokens with. It is read once (on the first validation) and cached — the
/// key is static configuration, so a change requires an app restart, and caching keeps every subsequent
/// request off the database.
/// </summary>
public sealed class SigningKeyProvider : ISigningKeyProvider
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly object _gate = new();
    private SecurityKey? _cached;

    public SigningKeyProvider(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public SecurityKey GetSigningKey()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        lock (_gate)
        {
            _cached ??= new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ReadSecret()));
            return _cached;
        }
    }

    // One-time synchronous read of SysConfig['appConfig'].symmetricSecurityKey.
    private string ReadSecret()
    {
        using var connection = _connectionFactory.CreateOpenConnectionAsync().GetAwaiter().GetResult();
        var configValue = connection.ExecuteScalar<string?>(
            "SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'");

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
}

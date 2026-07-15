using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="AuthController"/>'s login endpoint. The repository is mocked, but the
/// real <see cref="JwtTokenService"/> is used so the issued JWT can be decoded and its claims,
/// expiry, and (absent) secrets asserted. No live DB is required.
/// </summary>
public class AuthControllerTests
{
    // HMAC-SHA256 needs a key of at least 256 bits (32 bytes); this stand-in is comfortably longer.
    private const string SigningSecret = "unit-test-signing-secret-key-please-ignore-0123456789";

    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(_repository.Object, new JwtTokenService());
    }

    private static AuthenticatedUser ActiveUser(string userId = "helen", string name = "Helen", params string[] roles) => new()
    {
        UserId = userId,
        UserName = name,
        RoleIds = roles.ToList(),
    };

    private void SetupSecret() =>
        _repository.Setup(r => r.GetSigningSecretAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(SigningSecret);

    private static JwtSecurityToken Decode(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ----- Success -----

    [Fact]
    public async Task Login_ReturnsProfileAndToken_ForValidActiveUser()
    {
        var request = new LoginRequest { UserId = "helen", Password = "secret" };
        _repository.Setup(r => r.ValidateCredentialsAsync("helen", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ActiveUser("helen", "Helen", "Admin"));
        SetupSecret();

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("helen", body.UserId);
        Assert.Equal("Helen", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        // The access token is a well-formed, decodable JWT.
        var jwt = Decode(body.AccessToken);
        Assert.Equal("helen", jwt.Claims.Single(c => c.Type == JwtTokenService.UserIdClaimType).Value);
        Assert.Equal("Helen", jwt.Claims.Single(c => c.Type == JwtTokenService.UserNameClaimType).Value);
    }

    [Fact]
    public async Task Login_TokenCarriesEveryRoleClaim()
    {
        var request = new LoginRequest { UserId = "helen", Password = "secret" };
        _repository.Setup(r => r.ValidateCredentialsAsync("helen", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ActiveUser("helen", "Helen", "Admin", "Editor", "Viewer"));
        SetupSecret();

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        var roleClaims = Decode(body.AccessToken).Claims
            .Where(c => c.Type == JwtTokenService.RoleClaimType)
            .Select(c => c.Value)
            .ToList();
        Assert.Equal(new[] { "Admin", "Editor", "Viewer" }, roleClaims);
    }

    [Fact]
    public async Task Login_TokenExpiresApproximately24HoursAfterIssue()
    {
        var request = new LoginRequest { UserId = "helen", Password = "secret" };
        _repository.Setup(r => r.ValidateCredentialsAsync("helen", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ActiveUser());
        SetupSecret();

        var before = DateTime.UtcNow;
        var result = await _controller.Login(request, CancellationToken.None);
        var after = DateTime.UtcNow;

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);

        // ValidTo (UTC) should sit 24h after issue — allow a small window for execution time.
        var validTo = Decode(body.AccessToken).ValidTo;
        Assert.InRange(validTo, before.AddHours(24).AddSeconds(-30), after.AddHours(24).AddSeconds(30));
    }

    [Fact]
    public async Task Login_ResponseNeverContainsPasswordHash()
    {
        var request = new LoginRequest { UserId = "helen", Password = "secret" };
        _repository.Setup(r => r.ValidateCredentialsAsync("helen", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ActiveUser("helen", "Helen", "Admin"));
        SetupSecret();

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);

        // Neither the serialized profile nor the token payload may carry a password/hash.
        var serialized = JsonSerializer.Serialize(body);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", serialized, StringComparison.OrdinalIgnoreCase);

        var jwt = Decode(body.AccessToken);
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    // ----- Failure: all return a generic 401, never a token -----

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForWrongPassword()
    {
        var request = new LoginRequest { UserId = "helen", Password = "wrong" };
        // Repository returns null when the password hash does not match.
        _repository.Setup(r => r.ValidateCredentialsAsync("helen", "wrong", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((AuthenticatedUser?)null);

        var result = await _controller.Login(request, CancellationToken.None);

        AssertGenericUnauthorized(result);
        _repository.Verify(r => r.GetSigningSecretAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForUnknownUserId()
    {
        var request = new LoginRequest { UserId = "ghost", Password = "secret" };
        _repository.Setup(r => r.ValidateCredentialsAsync("ghost", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((AuthenticatedUser?)null);

        var result = await _controller.Login(request, CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForInactiveUser()
    {
        var request = new LoginRequest { UserId = "miles", Password = "secret" };
        // IsActive = 0 is filtered out in the repository's WHERE clause, so it too returns null.
        _repository.Setup(r => r.ValidateCredentialsAsync("miles", "secret", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((AuthenticatedUser?)null);

        var result = await _controller.Login(request, CancellationToken.None);

        AssertGenericUnauthorized(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsMissing_WithoutHittingRepository()
    {
        var request = new LoginRequest { UserId = "", Password = "" };

        var result = await _controller.Login(request, CancellationToken.None);

        AssertGenericUnauthorized(result);
        _repository.Verify(r => r.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The 401 body is the generic message — it must not leak which check failed.
    private static void AssertGenericUnauthorized(ActionResult<LoginResponse> result)
    {
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("Invalid credentials.", error.Message);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// End-to-end authorization tests over the real HTTP pipeline (<see cref="WebApplicationFactory{TEntryPoint}"/>).
/// A protected controller must reject requests without a valid bearer token and accept them with one;
/// <c>AuthController</c> must stay anonymous. DB-backed services are swapped for in-memory stubs and the
/// signing key is fixed, so no live database is required.
/// </summary>
public class AuthorizationTests : IClassFixture<AuthorizationTests.AuthTestFactory>
{
    // Must be >= 32 bytes for HMAC-SHA256.
    private const string Secret = "integration-test-signing-secret-key-0123456789-ABCDEF";

    private readonly AuthTestFactory _factory;

    public AuthorizationTests(AuthTestFactory factory) => _factory = factory;

    // Issues a token signed with the same key the test host validates against.
    private static string IssueToken(params string[] roles) =>
        new JwtTokenService().CreateAccessToken("tester", "Tester", roles, Secret).Token;

    // Builds a token signed with the correct key but carrying a foreign issuer — used to prove
    // issuer validation rejects tokens minted for a different service that shares the key.
    private static string IssueTokenWithIssuer(string issuer)
    {
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer,
            audience: JwtTokenService.Audience,
            claims: [new System.Security.Claims.Claim(JwtTokenService.UserNameClaimType, "Tester")],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/publishstatuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithInvalidBearerToken()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/publishstatuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns200_WithValidBearerToken()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken("Admin"));

        var response = await client.GetAsync("/api/publishstatuses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();
        Assert.NotNull(body);
        Assert.Single(body!);
    }

    [Fact]
    public async Task AdminEndpoint_Returns403_ForAuthenticatedNonAdmin()
    {
        var client = _factory.CreateClient();
        // A valid, authenticated token — but carrying no Admin role.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken("Editor"));

        var response = await client.GetAsync("/api/publishstatuses");

        // Authenticated but not authorized: role gate must reject with 403, not admit with 200.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_ForTokenWithWrongIssuer()
    {
        var client = _factory.CreateClient();
        // Correctly signed and unexpired, but issued by a different service (wrong `iss`).
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueTokenWithIssuer("some-other-service"));

        var response = await client.GetAsync("/api/publishstatuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthLogin_IsAnonymous_AndReturnsToken_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        // No Authorization header — if AuthController were protected this would 401 before the action ran.
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login", new LoginRequest { UserId = "helen", Password = "secret" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal("helen", body!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task UpdateProfile_UsesJwtUserId_IgnoringUserIdInBody()
    {
        var client = _factory.CreateClient();
        // Token subject is "tester" (see IssueToken).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken());

        // The body tries to smuggle a different UserId — it must be ignored; the JWT subject wins.
        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile", new { userId = "attacker", userName = "Renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal("tester", body!.UserId);

        // The update targeted the token's user, never the id supplied in the body.
        _factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync("tester", "Renamed", It.IsAny<CancellationToken>()), Times.Once);
        _factory.AuthRepository.Verify(
            r => r.UpdateUserNameAsync("attacker", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_Returns401_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile", new { userName = "Renamed" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Returns401_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new { currentPassword = "OldPass1!", newPassword = "NewPass9#", confirmPassword = "NewPass9#" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_UsesJwtUserId_AndPersistsForTokenSubject()
    {
        var client = _factory.CreateClient();
        // Token subject is "tester" (see IssueToken).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken());

        var response = await client.PostAsJsonAsync(
            "/api/Auth/change-password",
            new { currentPassword = "OldPass1!", newPassword = "NewPass9#", confirmPassword = "NewPass9#" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // The change targeted the token's user; the current-password check and the update both used "tester".
        _factory.AuthRepository.Verify(
            r => r.VerifyCurrentPasswordAsync("tester", "OldPass1!", It.IsAny<CancellationToken>()), Times.Once);
        _factory.AuthRepository.Verify(
            r => r.ChangePasswordAsync("tester", "NewPass9#", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Boots the real app but replaces every DB-touching service used by these tests with a stub and
    /// pins the validation key, so the suite runs without SQL Server.
    /// </summary>
    public sealed class AuthTestFactory : WebApplicationFactory<Program>
    {
        /// <summary>The stub auth repository, exposed so tests can assert which UserId an update targeted.</summary>
        public Mock<IAuthRepository> AuthRepository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // A dummy connection string keeps SqlConnectionFactory's ctor happy if it is ever resolved.
            builder.UseSetting("ConnectionStrings:CMS", "Server=(local);Database=None;Trusted_Connection=True;");

            builder.ConfigureServices(services =>
            {
                // Validation key = the fixed test secret (matches the tokens these tests issue).
                services.RemoveAll<ISigningKeyProvider>();
                services.AddSingleton<ISigningKeyProvider>(
                    new StubSigningKeyProvider(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))));

                // Protected endpoint's repository → stub returning one row (no DB).
                services.RemoveAll<IPublishStatusRepository>();
                var statuses = new Mock<IPublishStatusRepository>();
                statuses.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new List<PublishStatus> { new() { Pkid = 1, Description = "Draft" } });
                services.AddScoped(_ => statuses.Object);

                // Auth repository → stub so anonymous login succeeds and returns a token signed with Secret.
                services.RemoveAll<IAuthRepository>();
                AuthRepository.Setup(r => r.ValidateCredentialsAsync("helen", "secret", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AuthenticatedUser { UserId = "helen", UserName = "Helen", RoleIds = { "Admin" } });
                AuthRepository.Setup(r => r.GetSigningSecretAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Secret);
                AuthRepository.Setup(r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                AuthRepository.Setup(r => r.VerifyCurrentPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                AuthRepository.Setup(r => r.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                services.AddScoped(_ => AuthRepository.Object);
            });
        }
    }

    private sealed class StubSigningKeyProvider : ISigningKeyProvider
    {
        private readonly SecurityKey _key;
        public StubSigningKeyProvider(SecurityKey key) => _key = key;
        public SecurityKey GetSigningKey() => _key;
    }
}

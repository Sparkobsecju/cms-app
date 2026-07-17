using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Services;
using Microsoft.AspNetCore.Http;
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

    // ----- Update profile (self-service UserName change) -----

    // Puts a signed-in user on the controller by seeding the JWT UserId claim, as the auth pipeline would.
    private void SignInAs(string userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(JwtTokenService.UserIdClaimType, userId) }, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    [Fact]
    public async Task UpdateProfile_UpdatesUserName_ForJwtUser_AndTrims()
    {
        SignInAs("helen");
        _repository.Setup(r => r.UpdateUserNameAsync("helen", "Helen Wu", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        // Leading/trailing whitespace is trimmed before it reaches the repository.
        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "  Helen Wu  " }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal("helen", body.UserId);
        Assert.Equal("Helen Wu", body.UserName);
        _repository.Verify(r => r.UpdateUserNameAsync("helen", "Helen Wu", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_TargetsJwtUser_NotAnyBodySuppliedId()
    {
        // Even if the request DTO could carry another id, only the JWT subject ("helen") is ever used.
        SignInAs("helen");
        _repository.Setup(r => r.UpdateUserNameAsync("helen", "Renamed", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = "Renamed" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal("helen", body.UserId);
        // The repository is only ever asked to update the JWT user, never some other account.
        _repository.Verify(r => r.UpdateUserNameAsync("helen", "Renamed", It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.UpdateUserNameAsync(
            It.Is<string>(id => id != "helen"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateProfile_ReturnsBadRequest_ForBlankUserName_WithoutHittingRepository(string blank)
    {
        SignInAs("helen");

        var result = await _controller.UpdateProfile(new UpdateProfileRequest { UserName = blank }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("UserName is required.", error.Message);
        _repository.Verify(r => r.UpdateUserNameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Change password (self-service) -----

    private static ChangePasswordRequest ChangeRequest(
        string current = "OldPass1!", string next = "NewPass9#", string? confirm = null) => new()
        {
            CurrentPassword = current,
            NewPassword = next,
            ConfirmPassword = confirm ?? next,
        };

    [Fact]
    public async Task ChangePassword_ValidRequest_PersistsNewPasswordForJwtUser_AndReturnsNoContent()
    {
        SignInAs("helen");
        _repository.Setup(r => r.VerifyCurrentPasswordAsync("helen", "OldPass1!", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.ChangePasswordAsync("helen", "NewPass9#", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.ChangePassword(ChangeRequest(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        // The new password is persisted for the JWT user exactly once. ChangePasswordAsync is the method
        // whose single UPDATE sets PasswordHash = SHA256(new) and stamps PasswordUpdatedTime = GETDATE().
        _repository.Verify(r => r.ChangePasswordAsync("helen", "NewPass9#", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void PasswordHasher_ProducesSaltedPbkdf2_ThatRoundTrips()
    {
        // New hashes are PBKDF2 (salted, non-deterministic): two hashes of the same password differ, but
        // each verifies, and a wrong password does not. needsRehash is false for a current-format hash.
        const string password = "NewPass9#";

        var hashA = CMS.API.Security.PasswordHasher.Hash(password);
        var hashB = CMS.API.Security.PasswordHasher.Hash(password);

        Assert.StartsWith("PBKDF2$SHA256$", hashA);
        Assert.NotEqual(hashA, hashB); // random per-call salt
        Assert.True(CMS.API.Security.PasswordHasher.Verify(password, hashA, out var needsRehashA));
        Assert.False(needsRehashA);
        Assert.False(CMS.API.Security.PasswordHasher.Verify("WrongPass9#", hashA, out _));
    }

    [Fact]
    public void PasswordHasher_VerifiesDeprecatedSha256Hash_AndFlagsForRehash()
    {
        // Deprecated unsalted SHA-256 hex hashes (written by earlier versions) still verify, and Verify
        // signals needsRehash so the login path can upgrade the row to PBKDF2 transparently.
        const string password = "NewPass9#";
        var legacyHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();

        Assert.True(CMS.API.Security.PasswordHasher.Verify(password, legacyHash, out var needsRehash));
        Assert.True(needsRehash);
        Assert.False(CMS.API.Security.PasswordHasher.Verify("WrongPass9#", legacyHash, out _));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ChangesNothing()
    {
        SignInAs("helen");
        // Current password does not match the stored hash.
        _repository.Setup(r => r.VerifyCurrentPasswordAsync("helen", "wrong", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.ChangePassword(ChangeRequest(current: "wrong"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("目前密碼不正確。 Current password is incorrect.", error.Message);
        // Nothing is changed when the current password is wrong.
        _repository.Verify(r => r.ChangePasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Ab1!")]        // 4 classes but too short (< 8)
    [InlineData("aB1")]         // 3 classes but too short (< 8)
    [InlineData("password")]    // length 8 but only 1 class (lower)
    [InlineData("Password")]    // length 8 but only 2 classes (upper, lower)
    [InlineData("PASSWORD123")] // only 2 classes (upper, digit)
    [InlineData("12345678")]    // only 1 class (digit)
    public async Task ChangePassword_RejectsPasswordsFailingComplexity_WithoutPersisting(string weak)
    {
        SignInAs("helen");
        _repository.Setup(r => r.VerifyCurrentPasswordAsync("helen", "OldPass1!", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.ChangePassword(ChangeRequest(next: weak), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal(PasswordPolicy.Requirement, error.Message);
        _repository.Verify(r => r.ChangePasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_AcceptsThreeOfFourClassesAtMinLength()
    {
        // Exactly 8 chars, exactly 3 classes (upper, lower, digit; no symbol) — the policy's boundary.
        SignInAs("helen");
        _repository.Setup(r => r.VerifyCurrentPasswordAsync("helen", "OldPass1!", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.ChangePasswordAsync("helen", "Abc12345", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.ChangePassword(ChangeRequest(next: "Abc12345"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repository.Verify(r => r.ChangePasswordAsync("helen", "Abc12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_NewAndConfirmMismatch_ChangesNothing()
    {
        SignInAs("helen");
        _repository.Setup(r => r.VerifyCurrentPasswordAsync("helen", "OldPass1!", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.ChangePassword(
            ChangeRequest(next: "NewPass9#", confirm: "NewPass9$"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("兩次輸入的新密碼不一致。 New password and confirmation do not match.", error.Message);
        _repository.Verify(r => r.ChangePasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

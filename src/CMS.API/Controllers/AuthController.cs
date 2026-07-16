using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Auth endpoints (登入): <c>login</c> issues a JWT access token (anonymous), and <c>profile</c> lets the
/// signed-in user update their own display name. Only <c>login</c> is anonymous; <c>profile</c> requires a
/// valid bearer token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    // Deliberately generic: never reveals whether the UserId, the active flag, or the password
    // was the failing check.
    private static readonly ErrorResponse InvalidCredentials = new("Invalid credentials.");

    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _tokenService;

    public AuthController(IAuthRepository repository, IJwtTokenService tokenService)
    {
        _repository = repository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Authenticates a user and returns their profile plus a signed 24-hour JWT access token.
    /// Returns <c>401</c> with a generic message when the credentials are invalid.
    /// </summary>
    // Anonymous by design: this is the one action reachable without a bearer token, so users can
    // obtain one. Every other endpoint is protected by the global fallback authorization policy.
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(InvalidCredentials);
        }

        var user = await _repository.ValidateCredentialsAsync(request.UserId, request.Password, cancellationToken);
        if (user is null)
        {
            return Unauthorized(InvalidCredentials);
        }

        var secret = await _repository.GetSigningSecretAsync(cancellationToken);
        var token = _tokenService.CreateAccessToken(user.UserId, user.UserName, user.RoleIds, secret);

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AccessToken = token.Token,
        });
    }

    /// <summary>
    /// Updates the signed-in user's own display name. The target <c>UserId</c> is taken from the JWT
    /// (never from the request body), and only <c>UserName</c> is changed — the user cannot rename another
    /// account or alter their roles. <c>UserName</c> is required (non-empty after trimming); a blank value
    /// returns <c>400</c>.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        // Identity comes from the validated token, so a UserId in the body (there is none on the DTO) can
        // never redirect the update at another account.
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length == 0)
        {
            return BadRequest(new ErrorResponse("UserName is required."));
        }

        var updated = await _repository.UpdateUserNameAsync(userId, userName, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse { UserId = userId, UserName = userName });
    }

    /// <summary>
    /// Changes the signed-in user's own password. The target <c>UserId</c> is taken from the JWT (never the
    /// body). Verifies the current password against the stored hash, enforces the complexity policy on the
    /// new password, and requires the new password and its confirmation to match; only then is the new hash
    /// persisted (with <c>PasswordUpdatedTime</c> stamped). No password hash is ever accepted from or
    /// returned to the client. Any validation failure returns <c>400</c> and changes nothing.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        // Identity comes from the validated token — the DTO carries no UserId, so the change can never be
        // redirected at another account.
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // 1. The current password must match the stored hash — checked before anything is changed.
        if (!await _repository.VerifyCurrentPasswordAsync(userId, request.CurrentPassword, cancellationToken))
        {
            return BadRequest(new ErrorResponse("目前密碼不正確。 Current password is incorrect."));
        }

        // 2. The new password must satisfy the complexity policy.
        if (!PasswordPolicy.IsCompliant(request.NewPassword))
        {
            return BadRequest(new ErrorResponse(PasswordPolicy.Requirement));
        }

        // 3. The new password and its confirmation must match.
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new ErrorResponse("兩次輸入的新密碼不一致。 New password and confirmation do not match."));
        }

        // 4. Persist the new hash and stamp PasswordUpdatedTime.
        var changed = await _repository.ChangePasswordAsync(userId, request.NewPassword, cancellationToken);
        if (!changed)
        {
            return NotFound();
        }

        return NoContent();
    }
}

using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Login endpoint (登入). Issues a JWT access token for valid, active users.</summary>
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
}

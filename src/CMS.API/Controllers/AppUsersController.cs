using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for system users (使用者). Passwords are never exposed or accepted.</summary>
// Administrative surface: require the Admin role, not merely an authenticated session. The global
// fallback policy only enforces authentication; user management must be gated by role.
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/appusers")]
[Produces("application/json")]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserRepository _repository;

    public AppUsersController(IAppUserRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _repository.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Returns users matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> Query(
        [FromBody] AppUserQuery query, CancellationToken cancellationToken)
    {
        var users = await _repository.QueryAsync(query, cancellationToken);
        return Ok(users);
    }

    /// <summary>Returns a single user (with assigned roles) by UserId.</summary>
    // String PK: no ":int" route constraint.
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Creates a new user (password set from the configured default).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppUser>> Create(
        [FromBody] AppUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest("UserId and UserName are required.");
        }

        if (await _repository.ExistsAsync(request.UserId, cancellationToken))
        {
            return Conflict($"User '{request.UserId}' already exists.");
        }

        var userId = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = userId }, created);
    }

    /// <summary>Updates an existing user (UserId taken from the body; password unchanged).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> Update(
        [FromBody] AppUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest("UserId and UserName are required.");
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        return Ok(user);
    }

    /// <summary>Deletes a user by UserId.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Resets the user's password to the configured default. Takes no body — no password
    /// value ever crosses the wire. This is the only path that mutates the password after creation.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        var reset = await _repository.ResetPasswordAsync(id, cancellationToken);
        return reset ? NoContent() : NotFound();
    }
}

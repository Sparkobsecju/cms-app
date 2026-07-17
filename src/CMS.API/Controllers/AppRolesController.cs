using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for application roles (角色).</summary>
// Administrative surface: require the Admin role (the global fallback only enforces authentication).
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/approles")]
[Produces("application/json")]
public class AppRolesController : ControllerBase
{
    private readonly IAppRoleRepository _repository;

    public AppRolesController(IAppRoleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all roles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _repository.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>Returns roles matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRole>>> Query(
        [FromBody] AppRoleQuery query, CancellationToken cancellationToken)
    {
        var roles = await _repository.QueryAsync(query, cancellationToken);
        return Ok(roles);
    }

    /// <summary>Returns a single role (with assigned users) by RoleId.</summary>
    // String PK: no ":int" route constraint.
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken cancellationToken)
    {
        var role = await _repository.GetByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>Creates a new role.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create(
        [FromBody] AppRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoleId) || string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest("RoleId and RoleName are required.");
        }

        if (await _repository.ExistsAsync(request.RoleId, cancellationToken))
        {
            return Conflict($"Role '{request.RoleId}' already exists.");
        }

        var roleId = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(roleId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = roleId }, created);
    }

    /// <summary>Updates an existing role (RoleId taken from the body).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> Update(
        [FromBody] AppRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoleId) || string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest("RoleId and RoleName are required.");
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var role = await _repository.GetByIdAsync(request.RoleId, cancellationToken);
        return Ok(role);
    }

    /// <summary>Deletes a role by RoleId.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

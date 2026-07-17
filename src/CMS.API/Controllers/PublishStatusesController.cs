using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for publishing statuses (發布狀態).</summary>
// Administrative surface: require the Admin role (the global fallback only enforces authentication).
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/publishstatuses")]
[Produces("application/json")]
public class PublishStatusesController : ControllerBase
{
    private readonly IPublishStatusRepository _repository;

    public PublishStatusesController(IPublishStatusRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all statuses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> GetAll(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetAllAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>Returns statuses matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query, CancellationToken cancellationToken)
    {
        var statuses = await _repository.QueryAsync(query, cancellationToken);
        return Ok(statuses);
    }

    /// <summary>Returns a single status by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken cancellationToken)
    {
        var status = await _repository.GetByIdAsync(id, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>Creates a new status. The pkid is caller-supplied and must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required.");
        }

        if (await _repository.ExistsAsync(request.Pkid, cancellationToken))
        {
            return Conflict($"PublishStatus '{request.Pkid}' already exists.");
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates an existing status (pkid taken from the body; immutable).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> Update(
        [FromBody] PublishStatusRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required.");
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var status = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(status);
    }

    /// <summary>Deletes a status by pkid.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(byte id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

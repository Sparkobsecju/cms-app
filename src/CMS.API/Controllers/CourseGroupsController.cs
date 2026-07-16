using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for course groups (課程群組).</summary>
[ApiController]
[Route("api/coursegroups")]
[Produces("application/json")]
public class CourseGroupsController : ControllerBase
{
    private readonly ICourseGroupRepository _repository;

    public CourseGroupsController(ICourseGroupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all course groups.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroup>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetAllAsync(cancellationToken);
        return Ok(groups);
    }

    /// <summary>Returns course groups matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroup>>> Query(
        [FromBody] CourseGroupQuery query, CancellationToken cancellationToken)
    {
        var groups = await _repository.QueryAsync(query, cancellationToken);
        return Ok(groups);
    }

    /// <summary>Returns a single course group by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> GetById(short id, CancellationToken cancellationToken)
    {
        var group = await _repository.GetByIdAsync(id, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>Creates a new course group. The pkid is database-assigned (IDENTITY).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseGroup>> Create(
        [FromBody] CourseGroupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Description is required.");
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates an existing course group (pkid taken from the body; immutable).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> Update(
        [FromBody] CourseGroupRequest request, CancellationToken cancellationToken)
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

        var group = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(group);
    }

    /// <summary>Deletes a course group by pkid.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

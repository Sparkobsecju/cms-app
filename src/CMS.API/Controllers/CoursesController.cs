using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for courses (課程).</summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _repository;

    public CoursesController(ICourseRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all courses (with FK display names).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> GetAll(CancellationToken cancellationToken)
    {
        var courses = await _repository.GetAllAsync(cancellationToken);
        return Ok(courses);
    }

    /// <summary>Returns courses matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Course>>> Query(
        [FromBody] CourseQuery query, CancellationToken cancellationToken)
    {
        var courses = await _repository.QueryAsync(query, cancellationToken);
        return Ok(courses);
    }

    /// <summary>Returns a single course by pkid (including N-N pkid lists).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken cancellationToken)
    {
        var course = await _repository.GetByIdAsync(id, cancellationToken);
        return course is null ? NotFound() : Ok(course);
    }

    /// <summary>Creates a new course. The pkid is database-assigned (IDENTITY).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Course>> Create(
        [FromBody] CourseRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates an existing course (pkid taken from the body; immutable).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> Update(
        [FromBody] CourseRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var course = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(course);
    }

    /// <summary>Deletes a course by pkid.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // Required-field validation for the NOT NULL columns; returns an error message or null.
    private static string? Validate(CourseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Title is required.";
        }
        if (string.IsNullOrWhiteSpace(request.CourseId))
        {
            return "CourseId is required.";
        }
        if (string.IsNullOrWhiteSpace(request.ProdCourseId))
        {
            return "ProdCourseId is required.";
        }
        if (string.IsNullOrWhiteSpace(request.FriendlyUrl))
        {
            return "FriendlyUrl is required.";
        }
        if (request.PartnerPkid <= 0)
        {
            return "Partner is required.";
        }
        if (request.PublishStatusPkid <= 0)
        {
            return "PublishStatus is required.";
        }
        return null;
    }
}

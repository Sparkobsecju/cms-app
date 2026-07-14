using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Slim lookup lists used to populate form selects.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Active application users (for the role users multi-select).</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
    {
        var users = await _repository.GetAppUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Publishing statuses (for Course/Promotion form selects).</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatusLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatusLookup>>> GetPublishStatuses(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetPublishStatusesAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>Course groups (for the Course form select).</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroupLookup>>> GetCourseGroups(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetCourseGroupsAsync(cancellationToken);
        return Ok(groups);
    }
}

using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD endpoints for training partners (原廠).</summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerRepository _repository;

    public PartnersController(IPartnerRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all partners.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> GetAll(CancellationToken cancellationToken)
    {
        var partners = await _repository.GetAllAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>Returns partners matching the supplied filter.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Partner>>> Query(
        [FromBody] PartnerQuery query, CancellationToken cancellationToken)
    {
        var partners = await _repository.QueryAsync(query, cancellationToken);
        return Ok(partners);
    }

    /// <summary>Returns a single partner by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        return partner is null ? NotFound() : Ok(partner);
    }

    /// <summary>Creates a new partner. The pkid is database-assigned (IDENTITY).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } error)
        {
            return BadRequest(error);
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Updates an existing partner (pkid taken from the body; immutable).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> Update(
        [FromBody] PartnerRequest request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } error)
        {
            return BadRequest(error);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var partner = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(partner);
    }

    /// <summary>Deletes a partner by pkid.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // Explicit required-field validation so unit tests exercising the action directly
    // (which bypass the [ApiController] model-validation pipeline) still hit the 400 path.
    private static string? Validate(PartnerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }
        if (string.IsNullOrWhiteSpace(request.AppKey))
        {
            return "AppKey is required.";
        }
        if (string.IsNullOrWhiteSpace(request.NameOnPartnerMenu))
        {
            return "NameOnPartnerMenu is required.";
        }
        if (string.IsNullOrWhiteSpace(request.NameOnCourseDetailPage))
        {
            return "NameOnCourseDetailPage is required.";
        }
        return null;
    }
}

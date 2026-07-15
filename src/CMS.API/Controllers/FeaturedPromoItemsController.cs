using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>CRUD + slot-move endpoints for featured promo items (上稿作業).</summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController : ControllerBase
{
    private readonly IFeaturedPromoItemRepository _repository;

    public FeaturedPromoItemsController(IFeaturedPromoItemRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns all featured promo items (with FK display names).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedPromoItem>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns items matching the supplied filter (week range + training center).</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query, CancellationToken cancellationToken)
    {
        var items = await _repository.QueryAsync(query, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns a single item by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Creates a new item. The pkid is database-assigned (IDENTITY).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request, CancellationToken cancellationToken)
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

    /// <summary>Updates an existing item (pkid taken from the body; immutable).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> Update(
        [FromBody] FeaturedPromoItemRequest request, CancellationToken cancellationToken)
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

        var item = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(item);
    }

    /// <summary>Deletes an item by pkid.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Moves an item one slot up or down, swapping with the neighbour when present.</summary>
    [HttpPost("{id:int}/move/{direction}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveSlot(int id, string direction, CancellationToken cancellationToken)
    {
        if (!TryParseDirection(direction, out var parsed))
        {
            return BadRequest("Direction must be 'up' or 'down'.");
        }

        var result = await _repository.MoveSlotAsync(id, parsed, cancellationToken);
        return result switch
        {
            MoveResult.Moved => NoContent(),
            MoveResult.NotFound => NotFound(),
            _ => BadRequest("Slot is already at the boundary."),
        };
    }

    private static bool TryParseDirection(string direction, out SlotDirection parsed)
    {
        switch (direction?.ToLowerInvariant())
        {
            case "up":
                parsed = SlotDirection.Up;
                return true;
            case "down":
                parsed = SlotDirection.Down;
                return true;
            default:
                parsed = default;
                return false;
        }
    }

    // Required-field validation for the NOT NULL columns; returns an error message or null.
    private static string? Validate(FeaturedPromoItemRequest request)
    {
        if (request.TrainingCenterPkid <= 0)
        {
            return "TrainingCenter is required.";
        }
        if (request.Slot is < 1 or > 3)
        {
            return "Slot must be 1, 2 or 3.";
        }
        if (request.PromotionPkid <= 0)
        {
            return "Promotion (PromoCode) is required.";
        }
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return "Topic is required.";
        }
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return "Description is required.";
        }
        return null;
    }
}

using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Direction for a slot move (版位 上/下).</summary>
public enum SlotDirection
{
    /// <summary>Move toward slot 1 (e.g. 2 → 1).</summary>
    Up,

    /// <summary>Move toward slot 3 (e.g. 1 → 2).</summary>
    Down,
}

/// <summary>Outcome of a <see cref="IFeaturedPromoItemRepository.MoveSlotAsync"/> call.</summary>
public enum MoveResult
{
    /// <summary>The item does not exist.</summary>
    NotFound,

    /// <summary>The move would push the slot outside the 1..3 range.</summary>
    OutOfRange,

    /// <summary>The slot was moved (swapping with the neighbour when present).</summary>
    Moved,
}

/// <summary>Data access for <see cref="FeaturedPromoItem"/> records.</summary>
public interface IFeaturedPromoItemRepository
{
    /// <summary>Returns all items (with FK display names) ordered by date, center, slot.</summary>
    Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns items matching the supplied filter (week range + training center).</summary>
    Task<IReadOnlyList<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single item or null if not found.</summary>
    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>Creates an item; returns the database-assigned pkid.</summary>
    Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an item; returns false if the row does not exist.</summary>
    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an item; returns false if the row does not exist.</summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>Moves the item one slot in the given direction, swapping with the neighbour
    /// occupying the target slot (same date + center) in a single transaction.</summary>
    Task<MoveResult> MoveSlotAsync(int pkid, SlotDirection direction, CancellationToken cancellationToken = default);
}

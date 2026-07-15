using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="FeaturedPromoItem"/>.</summary>
public sealed class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    // Shared SELECT projection; PromoCode / TrainingCenterName come from flat JOIN aliases.
    private const string SelectColumns = @"
        f.pkid AS Pkid,
        f.ScheduleOn AS ScheduleOn,
        f.TrainingCenter_pkid AS TrainingCenterPkid,
        f.Slot AS Slot,
        f.Promotion_pkid AS PromotionPkid,
        f.Topic AS Topic,
        f.Description AS Description,
        pr.PromoCode AS PromoCode,
        tc.Name AS TrainingCenterName";

    private const string FromJoins = @"
        FROM FeaturedPromoItem f
        JOIN Promotion2 pr ON pr.pkid = f.Promotion_pkid
        JOIN TrainingCenter tc ON tc.pkid = f.TrainingCenter_pkid";

    private const string OrderBy = " ORDER BY f.ScheduleOn ASC, f.TrainingCenter_pkid ASC, f.Slot ASC;";

    public FeaturedPromoItemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} {FromJoins}{OrderBy}";
        var rows = await connection.QueryAsync<FeaturedPromoItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            {FromJoins}
            WHERE (@TrainingCenterPkid IS NULL OR f.TrainingCenter_pkid = @TrainingCenterPkid)
              AND (@ScheduleOnFrom IS NULL OR f.ScheduleOn >= @ScheduleOnFrom)
              AND (@ScheduleOnTo IS NULL OR f.ScheduleOn <= @ScheduleOnTo)
              AND (@Slot IS NULL OR f.Slot = @Slot)
            {OrderBy}";
        var parameters = new
        {
            query.TrainingCenterPkid,
            query.ScheduleOnFrom,
            query.ScheduleOnTo,
            query.Slot,
        };
        var rows = await connection.QueryAsync<FeaturedPromoItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} {FromJoins} WHERE f.pkid = @Pkid;";
        return await connection.QuerySingleOrDefaultAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    // pkid is int IDENTITY — assigned by the DB; read back via SCOPE_IDENTITY.
    public async Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO FeaturedPromoItem
                (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
            VALUES
                (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            WriteParams(request), cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE FeaturedPromoItem SET
                ScheduleOn = @ScheduleOn,
                TrainingCenter_pkid = @TrainingCenterPkid,
                Slot = @Slot,
                Promotion_pkid = @PromotionPkid,
                Topic = @Topic,
                Description = @Description
            WHERE pkid = @Pkid;",
            WriteParams(request), cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid;",
            new { Pkid = pkid }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<MoveResult> MoveSlotAsync(int pkid, SlotDirection direction, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var current = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot FROM FeaturedPromoItem WHERE pkid = @Pkid;",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (current is null)
        {
            transaction.Rollback();
            return MoveResult.NotFound;
        }

        DateTime scheduleOn = current.ScheduleOn;
        short trainingCenterPkid = current.TrainingCenterPkid;
        byte oldSlot = current.Slot;
        int targetSlot = direction == SlotDirection.Down ? oldSlot + 1 : oldSlot - 1;

        if (targetSlot is < 1 or > 3)
        {
            transaction.Rollback();
            return MoveResult.OutOfRange;
        }

        // Swap within one transaction. Park the moving row at slot 0 first so the
        // UNIQUE(ScheduleOn, TrainingCenter_pkid, Slot) index is never violated mid-swap.
        var swapParams = new
        {
            Pkid = pkid,
            ScheduleOn = DateOnly.FromDateTime(scheduleOn),
            TrainingCenterPkid = trainingCenterPkid,
            OldSlot = oldSlot,
            Target = (byte)targetSlot,
        };
        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE FeaturedPromoItem SET Slot = 0 WHERE pkid = @Pkid;
            UPDATE FeaturedPromoItem SET Slot = @OldSlot
                WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Target;
            UPDATE FeaturedPromoItem SET Slot = @Target WHERE pkid = @Pkid;",
            swapParams, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return MoveResult.Moved;
    }

    // Named parameters shared by INSERT and UPDATE (Pkid is ignored by INSERT).
    private static object WriteParams(FeaturedPromoItemRequest request) => new
    {
        request.Pkid,
        request.ScheduleOn,
        request.TrainingCenterPkid,
        request.Slot,
        request.PromotionPkid,
        request.Topic,
        request.Description,
    };
}

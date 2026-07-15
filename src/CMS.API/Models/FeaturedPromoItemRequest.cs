namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating a <see cref="FeaturedPromoItem"/>.</summary>
public class FeaturedPromoItemRequest
{
    /// <summary>Primary key (主代碼). Ignored on create (IDENTITY); identifies the row on update.</summary>
    public int Pkid { get; set; }

    /// <summary>Scheduled listing date (上稿日期).</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>Training center FK (訓練中心).</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>Slot position within the day (版位): 1, 2 or 3.</summary>
    public byte Slot { get; set; }

    /// <summary>Promotion FK (活動); resolved on the client from the entered PromoCode.</summary>
    public int PromotionPkid { get; set; }

    /// <summary>Topic / headline (標題).</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Description (說明).</summary>
    public string Description { get; set; } = string.Empty;
}

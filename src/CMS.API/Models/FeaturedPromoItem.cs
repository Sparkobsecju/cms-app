namespace CMS.API.Models;

/// <summary>Response model for a featured promo item / homepage slot (上稿作業).</summary>
public class FeaturedPromoItem
{
    /// <summary>Primary key (主代碼). int IDENTITY; assigned by the database.</summary>
    public int Pkid { get; set; }

    /// <summary>Scheduled listing date (上稿日期).</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>Training center FK (訓練中心).</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>Slot position within the day (版位): 1, 2 or 3.</summary>
    public byte Slot { get; set; }

    /// <summary>Promotion FK (活動).</summary>
    public int PromotionPkid { get; set; }

    /// <summary>Topic / headline (標題).</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Description (說明).</summary>
    public string Description { get; set; } = string.Empty;

    // ----- FK display names (JOIN, read-only) -----

    /// <summary>Promotion code (活動代碼) resolved via JOIN on Promotion_pkid.</summary>
    public string PromoCode { get; set; } = string.Empty;

    /// <summary>Training center name (訓練中心) resolved via JOIN.</summary>
    public string TrainingCenterName { get; set; } = string.Empty;
}

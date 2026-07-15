namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a promotion; used to resolve an entered PromoCode to its pkid,
/// and to pre-fill Topic/Description on the FeaturedPromoItem form.</summary>
public class PromotionLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public int Pkid { get; set; }

    /// <summary>Promotion code (活動代碼); unique in Promotion2.</summary>
    public string PromoCode { get; set; } = string.Empty;

    /// <summary>Promotion topic (活動標題).</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Promotion description (活動說明).</summary>
    public string Description { get; set; } = string.Empty;
}

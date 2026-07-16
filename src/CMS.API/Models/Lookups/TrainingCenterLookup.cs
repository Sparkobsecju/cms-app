namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a training center (used by the FeaturedPromoItem tabs / filter).</summary>
public class TrainingCenterLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public short Pkid { get; set; }

    /// <summary>Training center name (訓練中心名稱).</summary>
    public string Name { get; set; } = string.Empty;
}

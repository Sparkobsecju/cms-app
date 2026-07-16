namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="FeaturedPromoItem"/> records.</summary>
public class FeaturedPromoItemQuery
{
    /// <summary>Training center FK exact match (訓練中心) — drives the tab filter.</summary>
    public short? TrainingCenterPkid { get; set; }

    /// <summary>Schedule-on range lower bound, inclusive (上稿日期 起) — the week's Monday.</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>Schedule-on range upper bound, inclusive (上稿日期 迄) — the week's Sunday.</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>Slot exact match (版位); usually left null.</summary>
    public byte? Slot { get; set; }
}

namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="Course"/> records.</summary>
public class CourseQuery
{
    /// <summary>Keyword; LIKE match on Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl.</summary>
    public string? Keyword { get; set; }

    /// <summary>Partner FK exact match (原廠).</summary>
    public short? PartnerPkid { get; set; }

    /// <summary>Course group FK exact match (課程群組).</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>Publish status FK exact match (上架狀態).</summary>
    public byte? PublishStatusPkid { get; set; }

    /// <summary>Schedule-on range lower bound, inclusive (上架日期 起).</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>Schedule-on range upper bound, inclusive (上架日期 迄).</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>Schedule-off range lower bound, inclusive (下架日期 起).</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>Schedule-off range upper bound, inclusive (下架日期 迄).</summary>
    public DateOnly? ScheduleOffTo { get; set; }

    /// <summary>Tri-state repeat-attendance filter (允許重聽).</summary>
    public bool? CanRepeat { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating a <see cref="Course"/>.</summary>
public class CourseRequest
{
    /// <summary>Primary key (主代碼). Ignored on create (IDENTITY); identifies the row on update.</summary>
    public int Pkid { get; set; }

    /// <summary>Course name (課程名稱).</summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Official course name (官方課程名稱).</summary>
    [MaxLength(300)]
    public string? OfficialTitle { get; set; }

    /// <summary>Brochure code (簡介代碼).</summary>
    [Required]
    [MaxLength(50)]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>Subject code (科目代碼).</summary>
    [Required]
    [MaxLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>Friendly URL (友善網址).</summary>
    [Required]
    [MaxLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>Display order (顯示順序).</summary>
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>Partner FK (原廠).</summary>
    public short PartnerPkid { get; set; }

    /// <summary>Course group FK (課程群組); nullable.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>Publish status FK (上架狀態).</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>Schedule on / listing date (上架日期).</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>Schedule off / delisting date (下架日期).</summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>Hours (時數).</summary>
    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    /// <summary>List price (定價).</summary>
    [Range(0, double.MaxValue)]
    public decimal ListPrice { get; set; }

    /// <summary>Learning credit / points (點數).</summary>
    [Range(0, double.MaxValue)]
    public decimal LearningCredit { get; set; }

    /// <summary>Course materials (教材).</summary>
    [MaxLength(500)]
    public string? Material { get; set; }

    /// <summary>Objective (課程目標).</summary>
    [MaxLength(4000)]
    public string? Objective { get; set; }

    /// <summary>Target audience (適合對象).</summary>
    [MaxLength(500)]
    public string? Target { get; set; }

    /// <summary>Prerequisites (先備知識).</summary>
    [MaxLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>Outline (課程大綱). Stored as nvarchar(max) — unbounded.</summary>
    public string? Outline { get; set; }

    /// <summary>Certification / exam notes (考試／認證說明). Stored as nvarchar(max) — unbounded.</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>Note (備註).</summary>
    [MaxLength(4000)]
    public string? Note { get; set; }

    /// <summary>Other info (其他資訊).</summary>
    [MaxLength(4000)]
    public string? OtherInfo { get; set; }

    /// <summary>Whether repeat attendance is allowed (允許重聽).</summary>
    public bool CanRepeat { get; set; }

    /// <summary>Associated Certification pkids (CourseInCertification junction).</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>Associated JobCategory pkids (CourseJobCategories junction).</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}

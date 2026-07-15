namespace CMS.API.Models;

/// <summary>Response model for a training course row (課程).</summary>
public class Course
{
    /// <summary>Primary key (主代碼). int IDENTITY; assigned by the database.</summary>
    public int Pkid { get; set; }

    /// <summary>Course name (課程名稱).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Official course name (官方課程名稱).</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>Brochure code (簡介代碼).</summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>Subject code (科目代碼).</summary>
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>Friendly URL (友善網址).</summary>
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>Display order (顯示順序).</summary>
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
    public short Hour { get; set; }

    /// <summary>List price (定價).</summary>
    public decimal ListPrice { get; set; }

    /// <summary>Learning credit / points (點數).</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>Course materials (教材).</summary>
    public string? Material { get; set; }

    /// <summary>Objective (課程目標).</summary>
    public string? Objective { get; set; }

    /// <summary>Target audience (適合對象).</summary>
    public string? Target { get; set; }

    /// <summary>Prerequisites (先備知識).</summary>
    public string? Prerequisites { get; set; }

    /// <summary>Outline (課程大綱).</summary>
    public string? Outline { get; set; }

    /// <summary>Certification / exam notes (考試／認證說明).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>Note (備註).</summary>
    public string? Note { get; set; }

    /// <summary>Other info (其他資訊).</summary>
    public string? OtherInfo { get; set; }

    /// <summary>Whether repeat attendance is allowed (允許重聽).</summary>
    public bool CanRepeat { get; set; }

    // ----- FK display names (JOIN, read-only) -----

    /// <summary>Partner name (原廠) resolved via JOIN.</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>Course group description (課程群組) resolved via LEFT JOIN; null when unset.</summary>
    public string? CourseGroupDescription { get; set; }

    /// <summary>Publish status description (上架狀態) resolved via JOIN.</summary>
    public string PublishStatusDescription { get; set; } = string.Empty;

    // ----- N-N pkid lists (populated on GET by id) -----

    /// <summary>Associated Certification pkids (CourseInCertification junction).</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>Associated JobCategory pkids (CourseJobCategories junction).</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}

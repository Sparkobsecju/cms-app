namespace CMS.API.Models;

/// <summary>
/// Read model for the Course PDF (課程 PDF) — the curated "approval essentials"
/// subset of a published course plus its partner and linked certifications.
/// Populated by <see cref="Repositories.ICoursePdfRepository"/>; consumed by the
/// PDF document builder. Not a full Course model — only what the PDF renders.
/// </summary>
public sealed class CoursePdf
{
    /// <summary>Business primary key / public course code (課程編號).</summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>Marketing title (課程名稱).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Formal/official title, shown as the heading when present (正式名稱).</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>Partner name for the course-detail page (合作夥伴).</summary>
    public string? PartnerName { get; set; }

    /// <summary>Partner logo filename; text-only fallback if unresolved (標誌檔名).</summary>
    public string? PartnerImageFilename { get; set; }

    /// <summary>Duration in hours (時數).</summary>
    public int Hour { get; set; }

    /// <summary>List price (定價).</summary>
    public decimal ListPrice { get; set; }

    /// <summary>Learning credits awarded (學分).</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>Course objective (課程目標).</summary>
    public string? Objective { get; set; }

    /// <summary>Intended audience (適合對象).</summary>
    public string? Target { get; set; }

    /// <summary>Prerequisites (先修條件).</summary>
    public string? Prerequisites { get; set; }

    /// <summary>Course outline (課程大綱).</summary>
    public string? Outline { get; set; }

    /// <summary>Materials (教材).</summary>
    public string? Material { get; set; }

    /// <summary>Certification / exam this course counts toward (對應認證／考試).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>Notes (備註).</summary>
    public string? Note { get; set; }

    /// <summary>Other information (其他資訊).</summary>
    public string? OtherInfo { get; set; }

    /// <summary>Titles of certifications this course is part of (相關認證).</summary>
    public IReadOnlyList<string> Certifications { get; set; } = [];
}

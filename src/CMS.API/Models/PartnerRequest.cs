using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating a <see cref="Partner"/>.</summary>
public class PartnerRequest
{
    /// <summary>Primary key (主代碼). Ignored on create (IDENTITY); identifies the row on update.</summary>
    public short Pkid { get; set; }

    /// <summary>Partner name (原廠名稱).</summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Application key (應用金鑰).</summary>
    [Required]
    [MaxLength(10)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>Display name shown on the partner menu (選單顯示名稱).</summary>
    [Required]
    [MaxLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>Display name shown on the course detail page (課程頁顯示名稱).</summary>
    [Required]
    [MaxLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>Display order (顯示順序).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Image filename (圖片檔名); nullable — send null when blank.</summary>
    [MaxLength(50)]
    public string? ImageFilename { get; set; }
}

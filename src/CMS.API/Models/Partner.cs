namespace CMS.API.Models;

/// <summary>Response model for a training partner row (原廠).</summary>
public class Partner
{
    /// <summary>Primary key (主代碼). smallint IDENTITY; assigned by the database.</summary>
    public short Pkid { get; set; }

    /// <summary>Partner name (原廠名稱).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Application key (應用金鑰).</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>Display name shown on the partner menu (選單顯示名稱).</summary>
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>Display name shown on the course detail page (課程頁顯示名稱).</summary>
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>Display order (顯示順序).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Image filename (圖片檔名); nullable.</summary>
    public string? ImageFilename { get; set; }
}

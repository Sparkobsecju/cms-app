using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating a <see cref="PublishStatus"/>.</summary>
public class PublishStatusRequest
{
    /// <summary>Primary key (主代碼). Caller-supplied on create; immutable on update.</summary>
    public byte Pkid { get; set; }

    /// <summary>Status description (狀態說明).</summary>
    [Required]
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this status represents a draft (草稿).</summary>
    public bool IsDraft { get; set; }

    /// <summary>Whether this status represents published content (已發布).</summary>
    public bool IsPublished { get; set; }

    /// <summary>Whether this status represents discontinued content (已停用).</summary>
    public bool IsDiscontinued { get; set; }
}

namespace CMS.API.Models;

/// <summary>Response model for a publishing-status lookup row (發布狀態).</summary>
public class PublishStatus
{
    /// <summary>Primary key (主代碼). Caller-supplied tinyint; immutable once created.</summary>
    public byte Pkid { get; set; }

    /// <summary>Status description (狀態說明).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this status represents a draft (草稿).</summary>
    public bool IsDraft { get; set; }

    /// <summary>Whether this status represents published content (已發布).</summary>
    public bool IsPublished { get; set; }

    /// <summary>Whether this status represents discontinued content (已停用).</summary>
    public bool IsDiscontinued { get; set; }
}

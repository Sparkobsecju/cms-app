namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a publishing status (used by Course/Promotion form selects).</summary>
public class PublishStatusLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public byte Pkid { get; set; }

    /// <summary>Status description (狀態說明).</summary>
    public string Description { get; set; } = string.Empty;
}

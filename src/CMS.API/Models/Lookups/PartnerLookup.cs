namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a partner (used by the Course form select).</summary>
public class PartnerLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public short Pkid { get; set; }

    /// <summary>Partner name (原廠名稱).</summary>
    public string Name { get; set; } = string.Empty;
}

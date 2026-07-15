namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a certification (used by the Course form multi-select).</summary>
public class CertificationLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public int Pkid { get; set; }

    /// <summary>Owning partner name (原廠名稱), for a disambiguated option label.</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>Certification title (認證名稱); RTRIM'd from an nchar column.</summary>
    public string Title { get; set; } = string.Empty;
}

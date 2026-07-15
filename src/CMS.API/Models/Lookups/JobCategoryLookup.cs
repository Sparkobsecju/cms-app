namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a job category (used by the Course form multi-select).</summary>
public class JobCategoryLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public short Pkid { get; set; }

    /// <summary>Job category description (職務類別).</summary>
    public string Description { get; set; } = string.Empty;
}

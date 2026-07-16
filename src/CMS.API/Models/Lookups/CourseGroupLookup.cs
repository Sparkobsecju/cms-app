namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for a course group (used by the Course form select).</summary>
public class CourseGroupLookup
{
    /// <summary>Primary key (主代碼).</summary>
    public short Pkid { get; set; }

    /// <summary>Group name (群組名稱).</summary>
    public string Description { get; set; } = string.Empty;
}

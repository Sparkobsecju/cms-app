namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating a <see cref="CourseGroup"/>.</summary>
public class CourseGroupRequest
{
    /// <summary>Primary key (主代碼). Ignored on create (IDENTITY); identifies the row on update.</summary>
    public short Pkid { get; set; }

    /// <summary>Group name (群組名稱).</summary>
    public string Description { get; set; } = string.Empty;
}

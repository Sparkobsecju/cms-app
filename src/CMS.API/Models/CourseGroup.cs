namespace CMS.API.Models;

/// <summary>Response model for a course-group category row (課程群組).</summary>
public class CourseGroup
{
    /// <summary>Primary key (主代碼). smallint IDENTITY; assigned by the database.</summary>
    public short Pkid { get; set; }

    /// <summary>Group name (群組名稱).</summary>
    public string Description { get; set; } = string.Empty;
}

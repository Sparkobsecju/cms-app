using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based read access for the Course PDF (published courses only).</summary>
public sealed class CoursePdfRepository : ICoursePdfRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CoursePdfRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CoursePdf?> GetPublishedForPdfAsync(string courseId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // 1) Course + partner + publish status, gated to published courses.
        // 2) Certification titles this course counts toward (nchar -> RTRIM).
        const string sql = @"
            SELECT c.CourseId, c.Title, c.OfficialTitle,
                   p.NameOnCourseDetailPage AS PartnerName, p.ImageFilename AS PartnerImageFilename,
                   c.Hour, c.ListPrice, c.LearningCredit,
                   c.Objective, c.Target, c.Prerequisites, c.Outline,
                   c.Material, c.TowardCertOrExam, c.Note, c.OtherInfo
            FROM Course c
            JOIN Partner p ON p.pkid = c.Partner_pkid
            JOIN PublishStatus ps ON ps.pkid = c.PublishStatus_pkid
            WHERE c.CourseId = @CourseId AND ps.IsPublished = 1;

            SELECT RTRIM(cert.Title) AS Title
            FROM CourseInCertification cic
            JOIN Course c2 ON c2.pkid = cic.Course_pkid
            JOIN Certification cert ON cert.pkid = cic.Certification_pkid
            WHERE c2.CourseId = @CourseId AND cert.Title IS NOT NULL
            ORDER BY cert.Title;";

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { CourseId = courseId }, cancellationToken: cancellationToken));

        var course = await multi.ReadSingleOrDefaultAsync<CoursePdf>();
        if (course is null)
        {
            return null;
        }

        course.Certifications = (await multi.ReadAsync<string>()).AsList();
        return course;
    }
}

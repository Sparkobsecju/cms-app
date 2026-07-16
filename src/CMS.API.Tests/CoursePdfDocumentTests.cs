using CMS.API.Models;
using CMS.API.Pdf;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="CoursePdfDocument"/>: it renders a valid PDF from a
/// model, including Traditional Chinese content (exercising the embedded CJK font).
/// </summary>
public class CoursePdfDocumentTests
{
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46]; // "%PDF"

    [Fact]
    public void Render_ProducesValidPdf_WithChineseContent()
    {
        var course = new CoursePdf
        {
            CourseId = "SEC-200",
            Title = "資訊安全實務",
            OfficialTitle = "資訊安全實務 Information Security in Practice",
            PartnerName = "資安學院",
            Hour = 14,
            ListPrice = 12000m,
            LearningCredit = 2m,
            Objective = "建立企業資安防護的實務能力，涵蓋弱點掃描與事件應變。",
            Prerequisites = "具備基礎網路知識。",
            Certifications = ["CISSP", "CEH"],
        };

        var bytes = CoursePdfDocument.Render(course);

        Assert.NotEmpty(bytes);
        Assert.Equal(PdfMagic, bytes[..4]);
    }

    [Fact]
    public void Render_HandlesMinimalCourse_WithEmptyOptionalFields()
    {
        var course = new CoursePdf
        {
            CourseId = "MIN-001",
            Title = "Minimal Course",
            Hour = 1,
            ListPrice = 0m,
            LearningCredit = 0m,
        };

        var bytes = CoursePdfDocument.Render(course);

        Assert.NotEmpty(bytes);
        Assert.Equal(PdfMagic, bytes[..4]);
    }
}

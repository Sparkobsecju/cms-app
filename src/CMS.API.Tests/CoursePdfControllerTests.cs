using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="CoursePdfController"/>: a published course yields a
/// PDF attachment; anything else yields 404. The repository is mocked so no live
/// DB is required; PDF rendering runs for real against the embedded font.
/// </summary>
public class CoursePdfControllerTests
{
    private readonly Mock<ICoursePdfRepository> _repository = new(MockBehavior.Strict);
    private readonly CoursePdfController _controller;

    public CoursePdfControllerTests()
    {
        _controller = new CoursePdfController(_repository.Object);
    }

    private static CoursePdf Course(string courseId = "NET-101") => new()
    {
        CourseId = courseId,
        Title = ".NET 基礎課程",
        OfficialTitle = ".NET Fundamentals",
        PartnerName = "Contoso 教育中心",
        Hour = 21,
        ListPrice = 18000m,
        LearningCredit = 3.5m,
        Objective = "了解 .NET 平台與 C# 語言基礎。",
        Outline = "1. CLR 與型別系統\n2. 例外處理\n3. 非同步程式設計",
        Certifications = ["MCSA", "Azure Developer Associate"],
    };

    [Fact]
    public async Task GetPdf_ReturnsPdfAttachment_WhenPublished()
    {
        _repository.Setup(r => r.GetPublishedForPdfAsync("NET-101", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Course());

        var result = await _controller.GetPdf("NET-101", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("NET-101.pdf", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
        // PDF magic number "%PDF".
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, file.FileContents[..4]);
    }

    [Fact]
    public async Task GetPdf_ReturnsNotFound_WhenUnpublishedOrMissing()
    {
        _repository.Setup(r => r.GetPublishedForPdfAsync("Ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((CoursePdf?)null);

        var result = await _controller.GetPdf("Ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

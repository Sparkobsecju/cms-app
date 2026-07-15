using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="CoursesController"/> covering the Course API endpoints:
/// list, filtered query, view, add, edit and delete. The repository is mocked so no live
/// DB is required. pkid is an int IDENTITY (DB-assigned) so create takes no uniqueness check.
/// </summary>
public class CoursesControllerTests
{
    private readonly Mock<ICourseRepository> _repository = new(MockBehavior.Strict);
    private readonly CoursesController _controller;

    public CoursesControllerTests()
    {
        _controller = new CoursesController(_repository.Object);
    }

    private static Course Course(int pkid, string title = "Azure 基礎") => new()
    {
        Pkid = pkid,
        Title = title,
        CourseId = $"C{pkid:000}",
        ProdCourseId = $"P{pkid:000}",
        FriendlyUrl = $"course-{pkid}",
        DisplayOrder = pkid,
        PartnerPkid = 1,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 8,
        ListPrice = 12000,
        LearningCredit = 3.5m,
        CanRepeat = true,
        PartnerName = "Microsoft",
        PublishStatusDescription = "已發布",
    };

    private static CourseRequest ValidRequest(int pkid = 0) => new()
    {
        Pkid = pkid,
        Title = "Azure 基礎",
        CourseId = "C001",
        ProdCourseId = "P001",
        FriendlyUrl = "azure-basics",
        DisplayOrder = 1,
        PartnerPkid = 1,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 8,
        ListPrice = 12000,
        LearningCredit = 3.5m,
        CanRepeat = true,
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllCourses()
    {
        var courses = new List<Course> { Course(1), Course(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(courses);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<Course>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new CourseQuery { PartnerPkid = 1 };
        var matches = new List<Course> { Course(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<Course>>(ok.Value);
        Assert.Single(returned);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsCourse_WhenFound()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Course(1));

        var result = await _controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Course>(ok.Value);
        Assert.Equal(1, returned.Pkid);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Course?)null);

        var result = await _controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = ValidRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(5);
        _repository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Course(5));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);
        Assert.Equal(5, created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTitleMissing()
    {
        var request = ValidRequest();
        request.Title = "";

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CourseRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenPartnerMissing()
    {
        var request = ValidRequest();
        request.PartnerPkid = 0;

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CourseRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = ValidRequest(1);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Course(1));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Course>(ok.Value);
        Assert.Equal(1, returned.Pkid);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = ValidRequest(99);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCourseIdMissing()
    {
        var request = ValidRequest(1);
        request.CourseId = "";

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync(2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Delete(2, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync(99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

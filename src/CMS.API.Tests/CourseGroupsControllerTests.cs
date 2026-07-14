using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="CourseGroupsController"/> covering the CourseGroup API
/// endpoints: list, filtered query, view, add, edit and delete. The repository is mocked
/// so no live DB is required. pkid is a smallint IDENTITY (DB-assigned) so create takes
/// no uniqueness check.
/// </summary>
public class CourseGroupsControllerTests
{
    private readonly Mock<ICourseGroupRepository> _repository = new(MockBehavior.Strict);
    private readonly CourseGroupsController _controller;

    public CourseGroupsControllerTests()
    {
        _controller = new CourseGroupsController(_repository.Object);
    }

    private static CourseGroup Group(short pkid, string description = "在職進修") => new()
    {
        Pkid = pkid,
        Description = description,
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllGroups()
    {
        var groups = new List<CourseGroup> { Group(2, "在職進修"), Group(1, "資訊技術") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(groups);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<CourseGroup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new CourseGroupQuery { Keyword = "資訊" };
        var matches = new List<CourseGroup> { Group(1, "資訊技術") };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<CourseGroup>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("資訊技術", returned[0].Description);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsGroup_WhenFound()
    {
        _repository.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Group(1, "資訊技術"));

        var result = await _controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<CourseGroup>(ok.Value);
        Assert.Equal((short)1, returned.Pkid);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync((short)99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((CourseGroup?)null);

        var result = await _controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = new CourseGroupRequest { Description = "數位轉型" };
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((short)5);
        _repository.Setup(r => r.GetByIdAsync((short)5, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Group(5, "數位轉型"));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CourseGroupsController.GetById), created.ActionName);
        Assert.Equal((short)5, created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var request = new CourseGroupRequest { Description = "" };

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CourseGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = new CourseGroupRequest { Pkid = 1, Description = "資訊技術 (edited)" };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Group(1, "資訊技術 (edited)"));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<CourseGroup>(ok.Value);
        Assert.Equal("資訊技術 (edited)", returned.Description);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = new CourseGroupRequest { Pkid = 99, Description = "Ghost" };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<short>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var request = new CourseGroupRequest { Pkid = 1, Description = "" };

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync((short)2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Delete(2, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync((short)99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

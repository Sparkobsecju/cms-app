using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="PublishStatusesController"/> covering the PublishStatus API
/// endpoints: list, filtered query, view, add, edit and delete. The repository is mocked
/// so no live DB is required.
/// </summary>
public class PublishStatusesControllerTests
{
    private readonly Mock<IPublishStatusRepository> _repository = new(MockBehavior.Strict);
    private readonly PublishStatusesController _controller;

    public PublishStatusesControllerTests()
    {
        _controller = new PublishStatusesController(_repository.Object);
    }

    private static PublishStatus Status(byte pkid, string description = "Draft") => new()
    {
        Pkid = pkid,
        Description = description,
        IsDraft = true,
        IsPublished = false,
        IsDiscontinued = false,
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllStatuses()
    {
        var statuses = new List<PublishStatus> { Status(1, "Draft"), Status(2, "Published") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(statuses);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<PublishStatus>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new PublishStatusQuery { Keyword = "pub", IsPublished = true };
        var matches = new List<PublishStatus> { Status(2, "Published") };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<PublishStatus>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("Published", returned[0].Description);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsStatus_WhenFound()
    {
        _repository.Setup(r => r.GetByIdAsync((byte)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Status(1, "Draft"));

        var result = await _controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<PublishStatus>(ok.Value);
        Assert.Equal((byte)1, returned.Pkid);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync((byte)99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((PublishStatus?)null);

        var result = await _controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = new PublishStatusRequest { Pkid = 3, Description = "Archived" };
        _repository.Setup(r => r.ExistsAsync((byte)3, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((byte)3);
        _repository.Setup(r => r.GetByIdAsync((byte)3, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Status(3, "Archived"));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PublishStatusesController.GetById), created.ActionName);
        Assert.Equal((byte)3, created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPkidExists()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "Draft" };
        _repository.Setup(r => r.ExistsAsync((byte)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<PublishStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var request = new PublishStatusRequest { Pkid = 4, Description = "" };

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "Draft (edited)" };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync((byte)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Status(1, "Draft (edited)"));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<PublishStatus>(ok.Value);
        Assert.Equal("Draft (edited)", returned.Description);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = new PublishStatusRequest { Pkid = 99, Description = "Ghost" };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "" };

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync((byte)2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Delete(2, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync((byte)99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

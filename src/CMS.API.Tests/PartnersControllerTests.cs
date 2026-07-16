using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="PartnersController"/> covering the Partner API endpoints:
/// list, filtered query, view, add, edit and delete. The repository is mocked so no live
/// DB is required. pkid is a smallint IDENTITY (DB-assigned) so create takes no uniqueness
/// check.
/// </summary>
public class PartnersControllerTests
{
    private readonly Mock<IPartnerRepository> _repository = new(MockBehavior.Strict);
    private readonly PartnersController _controller;

    public PartnersControllerTests()
    {
        _controller = new PartnersController(_repository.Object);
    }

    private static Partner Make(short pkid, string name = "Microsoft") => new()
    {
        Pkid = pkid,
        Name = name,
        AppKey = "MS",
        NameOnPartnerMenu = $"{name} 選單",
        NameOnCourseDetailPage = name,
        DisplayOrder = 1,
        ImageFilename = null,
    };

    private static PartnerRequest Request(short pkid = 0, string name = "Microsoft") => new()
    {
        Pkid = pkid,
        Name = name,
        AppKey = "MS",
        NameOnPartnerMenu = $"{name} 選單",
        NameOnCourseDetailPage = name,
        DisplayOrder = 1,
        ImageFilename = null,
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllPartners()
    {
        var partners = new List<Partner> { Make(1, "Microsoft"), Make(2, "Cisco") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(partners);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<Partner>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new PartnerQuery { Keyword = "Cisco" };
        var matches = new List<Partner> { Make(2, "Cisco") };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<Partner>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("Cisco", returned[0].Name);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsPartner_WhenFound()
    {
        _repository.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Make(1, "Microsoft"));

        var result = await _controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Partner>(ok.Value);
        Assert.Equal((short)1, returned.Pkid);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync((short)99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Partner?)null);

        var result = await _controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = Request(name: "Amazon");
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((short)5);
        _repository.Setup(r => r.GetByIdAsync((short)5, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Make(5, "Amazon"));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PartnersController.GetById), created.ActionName);
        Assert.Equal((short)5, created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameMissing()
    {
        var request = Request(name: "");

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<PartnerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = Request(pkid: 1, name: "Microsoft (edited)");
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Make(1, "Microsoft (edited)"));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Partner>(ok.Value);
        Assert.Equal("Microsoft (edited)", returned.Name);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = Request(pkid: 99, name: "Ghost");
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<short>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenAppKeyMissing()
    {
        var request = Request(pkid: 1);
        request.AppKey = "";

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

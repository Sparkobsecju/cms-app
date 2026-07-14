using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="AppRolesController"/> covering the AppRole API endpoints:
/// list, filtered query, view, add and edit. The repository is mocked so no live DB is required.
/// </summary>
public class AppRolesControllerTests
{
    private readonly Mock<IAppRoleRepository> _repository = new(MockBehavior.Strict);
    private readonly AppRolesController _controller;

    public AppRolesControllerTests()
    {
        _controller = new AppRolesController(_repository.Object);
    }

    private static AppRole Role(string roleId, string name = "Name", int level = 100) => new()
    {
        Pkid = 1,
        RoleId = roleId,
        RoleName = name,
        PermissionLevel = level,
        Description = "desc",
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllRoles()
    {
        var roles = new List<AppRole> { Role("Admin", "Administrator", 1), Role("User", "User") };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(roles);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AppRole>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new AppRoleQuery { Keyword = "adm", PermissionLevel = 1 };
        var matches = new List<AppRole> { Role("Admin", "Administrator", 1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AppRole>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("Admin", returned[0].RoleId);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsRole_WithAssignedUsers()
    {
        var role = Role("Admin", "Administrator", 1);
        role.UserIds = ["helen", "miles@uuu.com.tw"];
        _repository.Setup(r => r.GetByIdAsync("Admin", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(role);

        var result = await _controller.GetById("Admin", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppRole>(ok.Value);
        Assert.Equal("Admin", returned.RoleId);
        Assert.Equal(2, returned.UserIds.Count);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync("Ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((AppRole?)null);

        var result = await _controller.GetById("Ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = new AppRoleRequest { RoleId = "Editor", RoleName = "Editor", PermissionLevel = 50 };
        _repository.Setup(r => r.ExistsAsync("Editor", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync("Editor");
        _repository.Setup(r => r.GetByIdAsync("Editor", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Role("Editor", "Editor", 50));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppRolesController.GetById), created.ActionName);
        Assert.Equal("Editor", created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRoleIdExists()
    {
        var request = new AppRoleRequest { RoleId = "Admin", RoleName = "Administrator" };
        _repository.Setup(r => r.ExistsAsync("Admin", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AppRoleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoleIdMissing()
    {
        var request = new AppRoleRequest { RoleId = "", RoleName = "X" };

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = new AppRoleRequest { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1 };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync("Admin", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Role("Admin", "Administrator", 1));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppRole>(ok.Value);
        Assert.Equal("Admin", returned.RoleId);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = new AppRoleRequest { RoleId = "Ghost", RoleName = "Ghost" };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Delete -----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync("User", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Delete("User", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync("Ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Delete("Ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="AppUsersController"/> covering the AppUser API endpoints:
/// list, filtered query, view, add, edit, delete and reset-password. The repository is
/// mocked so no live DB is required.
/// </summary>
public class AppUsersControllerTests
{
    private readonly Mock<IAppUserRepository> _repository = new(MockBehavior.Strict);
    private readonly AppUsersController _controller;

    public AppUsersControllerTests()
    {
        _controller = new AppUsersController(_repository.Object);
    }

    private static AppUser User(string userId, string name = "Name", bool active = true) => new()
    {
        Pkid = 1,
        UserId = userId,
        UserName = name,
        IsActive = active,
        RoleCount = 0,
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllUsers()
    {
        var users = new List<AppUser> { User("helen", "Helen"), User("miles", "Miles", false) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(users);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AppUser>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsMatches()
    {
        var query = new AppUserQuery { Keyword = "hel", IsActive = true };
        var matches = new List<AppUser> { User("helen", "Helen") };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AppUser>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("helen", returned[0].UserId);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsUser_WithAssignedRoles()
    {
        var user = User("helen", "Helen");
        user.RoleIds = ["Admin", "Editor"];
        _repository.Setup(r => r.GetByIdAsync("helen", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(user);

        var result = await _controller.GetById("helen", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppUser>(ok.Value);
        Assert.Equal("helen", returned.UserId);
        Assert.Equal(2, returned.RoleIds.Count);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync("ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((AppUser?)null);

        var result = await _controller.GetById("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ----- Add (POST) -----

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var request = new AppUserRequest { UserId = "newbie", UserName = "Newbie", IsActive = true };
        _repository.Setup(r => r.ExistsAsync("newbie", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync("newbie");
        _repository.Setup(r => r.GetByIdAsync("newbie", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(User("newbie", "Newbie"));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppUsersController.GetById), created.ActionName);
        Assert.Equal("newbie", created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenUserIdExists()
    {
        var request = new AppUserRequest { UserId = "helen", UserName = "Helen" };
        _repository.Setup(r => r.ExistsAsync("helen", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AppUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenUserIdMissing()
    {
        var request = new AppUserRequest { UserId = "", UserName = "X" };

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenUserNameMissing()
    {
        var request = new AppUserRequest { UserId = "x", UserName = "" };

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = new AppUserRequest { UserId = "helen", UserName = "Helen Wu", IsActive = true };
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync("helen", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(User("helen", "Helen Wu"));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<AppUser>(ok.Value);
        Assert.Equal("helen", returned.UserId);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var request = new AppUserRequest { UserId = "ghost", UserName = "Ghost" };
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
        _repository.Setup(r => r.DeleteAsync("miles", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.Delete("miles", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync("ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.Delete("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ----- Reset password -----

    [Fact]
    public async Task ResetPassword_ReturnsNoContent_WhenReset()
    {
        _repository.Setup(r => r.ResetPasswordAsync("helen", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await _controller.ResetPassword("helen", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repository.Verify(r => r.ResetPasswordAsync("helen", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.ResetPasswordAsync("ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await _controller.ResetPassword("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

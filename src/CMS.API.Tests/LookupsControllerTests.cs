using CMS.API.Controllers;
using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class LookupsControllerTests
{
    [Fact]
    public async Task GetAppUsers_ReturnsActiveUsers()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var users = new List<AppUserLookup>
        {
            new() { UserId = "helen", UserName = "helen" },
            new() { UserId = "miles@uuu.com.tw", UserName = "Miles Sun" },
        };
        repo.Setup(r => r.GetAppUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetAppUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AppUserLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }
}

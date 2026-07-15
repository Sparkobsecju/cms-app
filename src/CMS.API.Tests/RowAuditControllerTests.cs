using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="RowAuditController"/>: the history endpoint forwards the
/// tableName + pkid filter to the repository, returns its (newest-first) rows, and rejects
/// missing arguments. The repository is mocked so no live DB is required.
/// </summary>
public class RowAuditControllerTests
{
    private readonly Mock<IRowAuditRepository> _repository = new(MockBehavior.Strict);
    private readonly RowAuditController _controller;

    public RowAuditControllerTests()
    {
        _controller = new RowAuditController(_repository.Object);
    }

    private static RowAuditEntry Entry(string actionType, string userName, System.DateTime when) => new()
    {
        ActionType = actionType,
        UserName = userName,
        DateTime = when,
        ActionDesc = actionType == "Update" ? "Title" : "課程A",
    };

    [Fact]
    public async Task GetForRecord_FiltersByTableAndPkid_AndReturnsRowsNewestFirst()
    {
        // Repository contract is newest-first; the controller returns them unchanged.
        var newestFirst = new List<RowAuditEntry>
        {
            Entry("Delete", "alice", new System.DateTime(2026, 6, 3, 11, 0, 0)),
            Entry("Update", "bob", new System.DateTime(2026, 6, 2, 10, 0, 0)),
            Entry("Insert", "system", new System.DateTime(2026, 6, 1, 9, 0, 0)),
        };
        _repository.Setup(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(newestFirst);

        var result = await _controller.GetForRecord("Course", "123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<RowAuditEntry>>(ok.Value);
        Assert.Equal(3, returned.Count);
        Assert.Equal("Delete", returned[0].ActionType);
        Assert.Equal("Update", returned[1].ActionType);
        Assert.Equal("Insert", returned[2].ActionType);
        _repository.Verify(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForRecord_ReturnsBadRequest_WhenTableNameMissing()
    {
        var result = await _controller.GetForRecord(tableName: "", pkid: "123", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForRecord_ReturnsBadRequest_WhenPkidMissing()
    {
        var result = await _controller.GetForRecord(tableName: "Course", pkid: null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

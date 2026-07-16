using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="FeaturedPromoItemsController"/> covering the FeaturedPromoItem API:
/// list, filtered query (one-week ScheduleOn range + TrainingCenter), view, add, edit, delete and
/// the slot-move action. The repository is mocked so no live DB is required. pkid is an int
/// IDENTITY (DB-assigned) so create takes no uniqueness check.
/// </summary>
public class FeaturedPromoItemsControllerTests
{
    private readonly Mock<IFeaturedPromoItemRepository> _repository = new(MockBehavior.Strict);
    private readonly FeaturedPromoItemsController _controller;

    public FeaturedPromoItemsControllerTests()
    {
        _controller = new FeaturedPromoItemsController(_repository.Object);
    }

    private static FeaturedPromoItem Item(int pkid, byte slot = 1, short trainingCenter = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = trainingCenter,
        Slot = slot,
        PromotionPkid = 100 + pkid,
        Topic = $"主題 {pkid}",
        Description = $"說明 {pkid}",
        PromoCode = $"2025120{pkid}_SkillTrainAI",
        TrainingCenterName = "台北",
    };

    private static FeaturedPromoItemRequest ValidRequest(int pkid = 0) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = 1,
        Slot = 1,
        PromotionPkid = 101,
        Topic = "主題",
        Description = "說明",
    };

    // ----- List -----

    [Fact]
    public async Task GetAll_ReturnsAllItems()
    {
        var items = new List<FeaturedPromoItem> { Item(1), Item(2, slot: 2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(items);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<FeaturedPromoItem>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // ----- Filtered query -----

    [Fact]
    public async Task Query_OneWeekScheduleOnRange_PassesThrough_AndReturnsMatches()
    {
        // A Monday-to-Sunday week window (3/16 一 .. 3/22 日), as the list's week nav sends.
        var query = new FeaturedPromoItemQuery
        {
            ScheduleOnFrom = new DateOnly(2026, 3, 16),
            ScheduleOnTo = new DateOnly(2026, 3, 22),
        };
        var matches = new List<FeaturedPromoItem> { Item(1), Item(2, slot: 2) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<FeaturedPromoItem>>(ok.Value);
        Assert.Equal(2, returned.Count);
        _repository.Verify(r => r.QueryAsync(
            It.Is<FeaturedPromoItemQuery>(q =>
                q.ScheduleOnFrom == new DateOnly(2026, 3, 16) &&
                q.ScheduleOnTo == new DateOnly(2026, 3, 22)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_TrainingCenterFilter_PassesThrough_AndReturnsMatches()
    {
        var query = new FeaturedPromoItemQuery { TrainingCenterPkid = 2 };
        var matches = new List<FeaturedPromoItem> { Item(3, trainingCenter: 2) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(matches);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<FeaturedPromoItem>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal((short)2, returned[0].TrainingCenterPkid);
        _repository.Verify(r => r.QueryAsync(
            It.Is<FeaturedPromoItemQuery>(q => q.TrainingCenterPkid == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- View (GET by id) -----

    [Fact]
    public async Task GetById_ReturnsItem_WhenFound()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Item(1));

        var result = await _controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<FeaturedPromoItem>(ok.Value);
        Assert.Equal(1, returned.Pkid);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((FeaturedPromoItem?)null);

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
                   .ReturnsAsync(Item(5));

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(5, created.RouteValues!["id"]);
        _repository.Verify(r => r.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenPromotionMissing()
    {
        var request = ValidRequest();
        request.PromotionPkid = 0;

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenSlotOutOfRange()
    {
        var request = ValidRequest();
        request.Slot = 4;

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTopicMissing()
    {
        var request = ValidRequest();
        request.Topic = "";

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- Edit (PUT) -----

    [Fact]
    public async Task Update_ReturnsOk_WhenExisting()
    {
        var request = ValidRequest(1);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Item(1));

        var result = await _controller.Update(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<FeaturedPromoItem>(ok.Value);
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
    public async Task Update_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var request = ValidRequest(1);
        request.Description = "";

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

    // ----- Slot move -----

    [Fact]
    public async Task MoveSlot_ReturnsNoContent_WhenMoved()
    {
        _repository.Setup(r => r.MoveSlotAsync(1, SlotDirection.Down, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MoveResult.Moved);

        var result = await _controller.MoveSlot(1, "down", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task MoveSlot_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.MoveSlotAsync(9, SlotDirection.Up, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MoveResult.NotFound);

        var result = await _controller.MoveSlot(9, "up", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MoveSlot_ReturnsBadRequest_WhenOutOfRange()
    {
        _repository.Setup(r => r.MoveSlotAsync(1, SlotDirection.Up, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MoveResult.OutOfRange);

        var result = await _controller.MoveSlot(1, "up", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MoveSlot_ReturnsBadRequest_WhenDirectionInvalid()
    {
        var result = await _controller.MoveSlot(1, "sideways", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _repository.Verify(r => r.MoveSlotAsync(It.IsAny<int>(), It.IsAny<SlotDirection>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

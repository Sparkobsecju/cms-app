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

    [Fact]
    public async Task GetPublishStatuses_ReturnsStatuses()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var statuses = new List<PublishStatusLookup>
        {
            new() { Pkid = 1, Description = "Draft" },
            new() { Pkid = 2, Description = "Published" },
        };
        repo.Setup(r => r.GetPublishStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetPublishStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<PublishStatusLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetCourseGroups_ReturnsGroups()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var groups = new List<CourseGroupLookup>
        {
            new() { Pkid = 1, Description = "資訊技術" },
            new() { Pkid = 2, Description = "在職進修" },
        };
        repo.Setup(r => r.GetCourseGroupsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetCourseGroups(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<CourseGroupLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetPartners_ReturnsPartners()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var partners = new List<PartnerLookup>
        {
            new() { Pkid = 1, Name = "Microsoft" },
            new() { Pkid = 2, Name = "AWS" },
        };
        repo.Setup(r => r.GetPartnersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(partners);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetPartners(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<PartnerLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetCertifications_ReturnsCertifications()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var certifications = new List<CertificationLookup>
        {
            new() { Pkid = 1, PartnerName = "Microsoft", Title = "AZ-900" },
        };
        repo.Setup(r => r.GetCertificationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(certifications);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetCertifications(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<CertificationLookup>>(ok.Value);
        Assert.Single(returned);
    }

    [Fact]
    public async Task GetJobCategories_ReturnsJobCategories()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var jobCategories = new List<JobCategoryLookup>
        {
            new() { Pkid = 1, Description = "軟體工程師" },
            new() { Pkid = 2, Description = "資料分析師" },
        };
        repo.Setup(r => r.GetJobCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(jobCategories);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetJobCategories(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<JobCategoryLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetTrainingCenters_ReturnsCenters()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var centers = new List<TrainingCenterLookup>
        {
            new() { Pkid = 1, Name = "台北" },
            new() { Pkid = 2, Name = "新竹" },
        };
        repo.Setup(r => r.GetTrainingCentersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(centers);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetTrainingCenters(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<TrainingCenterLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
    }

    // The PromoCode lookup: the FeaturedPromoItem form resolves an entered PromoCode to its pkid.
    [Fact]
    public async Task GetPromotions_ReturnsPromotions_ForPromoCodeLookup()
    {
        var repo = new Mock<ILookupRepository>(MockBehavior.Strict);
        var promotions = new List<PromotionLookup>
        {
            new() { Pkid = 101, PromoCode = "20251204_SkillTrainAI", Topic = "成為能AI協作的程式設計師", Description = "轉職就業養成班" },
            new() { Pkid = 102, PromoCode = "251211_GoogleAI", Topic = "Google AI工具一次掌握", Description = "不需技術基礎" },
        };
        repo.Setup(r => r.GetPromotionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(promotions);
        var controller = new LookupsController(repo.Object);

        var result = await controller.GetPromotions(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<PromotionLookup>>(ok.Value);
        Assert.Equal(2, returned.Count);
        // The pkid a PromoCode resolves to is what the form writes into Promotion_pkid.
        Assert.Equal(101, returned.Single(p => p.PromoCode == "20251204_SkillTrainAI").Pkid);
    }
}

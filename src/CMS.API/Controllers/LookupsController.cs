using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Slim lookup lists used to populate form selects.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Active application users (for the role users multi-select).</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
    {
        var users = await _repository.GetAppUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Application roles (for the user roles multi-select).</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IReadOnlyList<AppRoleLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRoleLookup>>> GetAppRoles(CancellationToken cancellationToken)
    {
        var roles = await _repository.GetAppRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>Publishing statuses (for Course/Promotion form selects).</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatusLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatusLookup>>> GetPublishStatuses(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetPublishStatusesAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>Course groups (for the Course form select).</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroupLookup>>> GetCourseGroups(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetCourseGroupsAsync(cancellationToken);
        return Ok(groups);
    }

    /// <summary>Partners (for the Course form select).</summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerLookup>>> GetPartners(CancellationToken cancellationToken)
    {
        var partners = await _repository.GetPartnersAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>Certifications (for the Course form multi-select).</summary>
    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IReadOnlyList<CertificationLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CertificationLookup>>> GetCertifications(CancellationToken cancellationToken)
    {
        var certifications = await _repository.GetCertificationsAsync(cancellationToken);
        return Ok(certifications);
    }

    /// <summary>Job categories (for the Course form multi-select).</summary>
    [HttpGet("job-categories")]
    [ProducesResponseType(typeof(IReadOnlyList<JobCategoryLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobCategoryLookup>>> GetJobCategories(CancellationToken cancellationToken)
    {
        var jobCategories = await _repository.GetJobCategoriesAsync(cancellationToken);
        return Ok(jobCategories);
    }

    /// <summary>Training centers (for the FeaturedPromoItem tabs / filter).</summary>
    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrainingCenterLookup>>> GetTrainingCenters(CancellationToken cancellationToken)
    {
        var centers = await _repository.GetTrainingCentersAsync(cancellationToken);
        return Ok(centers);
    }

    /// <summary>Promotions (for the FeaturedPromoItem PromoCode lookup).</summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(IReadOnlyList<PromotionLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionLookup>>> GetPromotions(CancellationToken cancellationToken)
    {
        var promotions = await _repository.GetPromotionsAsync(cancellationToken);
        return Ok(promotions);
    }
}

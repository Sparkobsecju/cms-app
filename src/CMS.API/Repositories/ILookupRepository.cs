using CMS.API.Models.Lookups;

namespace CMS.API.Repositories;

/// <summary>Data access for slim lookup lists used by form selects.</summary>
public interface ILookupRepository
{
    /// <summary>Returns active application users for the role users multi-select.</summary>
    Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns application roles for the user roles multi-select.</summary>
    Task<IReadOnlyList<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns publishing statuses for Course/Promotion form selects.</summary>
    Task<IReadOnlyList<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns course groups for the Course form select.</summary>
    Task<IReadOnlyList<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns partners for the Course form select, ordered by DisplayOrder.</summary>
    Task<IReadOnlyList<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns certifications for the Course form multi-select.</summary>
    Task<IReadOnlyList<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns job categories for the Course form multi-select.</summary>
    Task<IReadOnlyList<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns training centers for the FeaturedPromoItem tabs, ordered by DisplayOrder.</summary>
    Task<IReadOnlyList<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns promotions (pkid + PromoCode + Topic/Description) so the
    /// FeaturedPromoItem form can resolve an entered PromoCode to its pkid.</summary>
    Task<IReadOnlyList<PromotionLookup>> GetPromotionsAsync(CancellationToken cancellationToken = default);
}

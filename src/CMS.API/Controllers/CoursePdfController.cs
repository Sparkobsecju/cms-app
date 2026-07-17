using CMS.API.Pdf;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Public, anonymous endpoint that returns a course's detail as a downloadable
/// PDF (課程 PDF). Only published courses are served; anything else returns 404
/// so unpublished catalogue content can't be discovered by id-guessing.
/// </summary>
[ApiController]
[Route("api/courses")]
public sealed class CoursePdfController : ControllerBase
{
    private readonly ICoursePdfRepository _repository;

    public CoursePdfController(ICoursePdfRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns the published course as a PDF attachment, or 404.</summary>
    // String business key: no ":int" route constraint.
    // [AllowAnonymous] scoped to this single action (consumed anonymously by public-site students) so a
    // future action added to this controller doesn't silently inherit anonymous access.
    [AllowAnonymous]
    [HttpGet("{courseId}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(string courseId, CancellationToken cancellationToken)
    {
        var course = await _repository.GetPublishedForPdfAsync(courseId, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        var bytes = CoursePdfDocument.Render(course);
        // Name the file from the canonical DB CourseId, not the un-canonicalized route value, so a
        // case-insensitive match still yields the spec's filename={CourseId}.pdf.
        return File(bytes, "application/pdf", $"{course.CourseId}.pdf");
    }
}

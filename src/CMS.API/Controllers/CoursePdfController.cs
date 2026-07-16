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
// Opt out of the global fallback policy (auth required on every endpoint):
// this is consumed anonymously by public-site students.
[AllowAnonymous]
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
        return File(bytes, "application/pdf", $"{courseId}.pdf");
    }
}

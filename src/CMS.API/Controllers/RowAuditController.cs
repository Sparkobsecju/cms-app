using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Read endpoint for a record's Row Audit trail (異動紀錄).</summary>
[ApiController]
[Route("api/rowaudit")]
[Produces("application/json")]
public class RowAuditController : ControllerBase
{
    private readonly IRowAuditRepository _repository;

    public RowAuditController(IRowAuditRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Returns the audit trail for one record, filtered by table name and pkid, newest first
    /// (e.g. <c>GET /api/rowaudit?tableName=Course&amp;pkid=123</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RowAuditEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RowAuditEntry>>> GetForRecord(
        [FromQuery] string? tableName, [FromQuery] string? pkid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(pkid))
        {
            return BadRequest("tableName and pkid are required.");
        }

        var rows = await _repository.GetForRecordAsync(tableName, pkid, cancellationToken);
        return Ok(rows);
    }
}

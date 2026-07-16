namespace CMS.API.Models;

/// <summary>
/// The single, consistent shape returned for an unexpected server error. Carries only a
/// generic, safe message — never exception details, stack traces, SQL, or connection info.
/// </summary>
public sealed record ErrorResponse(string Message);

namespace CMS.API.Data;

/// <summary>Helpers for building safe <c>LIKE</c> patterns from user-supplied keywords.</summary>
public static class SqlLike
{
    /// <summary>
    /// Escapes the LIKE wildcards (<c>%</c>, <c>_</c>, <c>[</c>) and the escape character itself so a
    /// user keyword matches literally instead of acting as a pattern (a search for <c>%</c> should not
    /// match every row). The SQL must append <c>ESCAPE '\'</c> to the LIKE predicate. A null keyword
    /// (meaning "no filter") is returned unchanged.
    /// </summary>
    public static string? EscapeWildcards(string? value) =>
        value is null
            ? null
            : value.Replace("\\", "\\\\")
                   .Replace("%", "\\%")
                   .Replace("_", "\\_")
                   .Replace("[", "\\[");
}

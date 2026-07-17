using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace CMS.API.Services;

/// <summary>
/// Pure, DB-free reflection helpers used by <see cref="RowAuditWriter"/> to derive audit
/// column values from an arbitrary entity type. Kept separate so the generic logic can be
/// unit-tested without a database or an HTTP context.
/// </summary>
public static class RowAuditReflection
{
    /// <summary>Maximum length of the ActionDesc column (dbo.RowAudit.ActionDesc varchar(1000)).</summary>
    public const int ActionDescMaxLength = 1000;

    /// <summary>Per-type cache of the ordered property set — reflection metadata is immutable.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> OrderedPropertyCache = new();

    /// <summary>
    /// Public, readable, non-indexer instance properties in declaration order. Reflection does
    /// not guarantee ordering, so we sort by metadata token, which reflects declaration order.
    /// The result is cached per type so each insert/update/delete doesn't re-run the reflection
    /// scan and LINQ sort (~60 calls for a wide entity).
    /// </summary>
    private static PropertyInfo[] OrderedProperties(Type type) =>
        OrderedPropertyCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.MetadataToken)
                .ToArray());

    /// <summary>
    /// Reads the entity's pkid (case-insensitive property name "pkid") as a string.
    /// Returns an empty string when the property is absent or null.
    /// </summary>
    public static string PrimaryKeyValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var prop = OrderedProperties(entity.GetType())
            .FirstOrDefault(p => p.Name.Equals("pkid", StringComparison.OrdinalIgnoreCase));
        return prop?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Value of the first string-typed property in declaration order (e.g. a Name/Title/Code).
    /// Returns null when there is no string property or its value is null.
    /// </summary>
    public static string? FirstStringPropertyValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var prop = OrderedProperties(entity.GetType())
            .FirstOrDefault(p => p.PropertyType == typeof(string));
        return prop?.GetValue(entity) as string;
    }

    /// <summary>
    /// Comma-separated names of the properties whose value differs between <paramref name="before"/>
    /// and <paramref name="after"/>, in declaration order. Empty string when nothing changed.
    /// Both arguments are expected to be the same runtime type.
    /// </summary>
    public static string ChangedPropertyNames(object before, object after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changed = new List<string>();
        foreach (var prop in OrderedProperties(before.GetType()))
        {
            if (!ValuesEqual(prop.GetValue(before), prop.GetValue(after)))
            {
                changed.Add(prop.Name);
            }
        }
        return string.Join(", ", changed);
    }

    /// <summary>Truncates <paramref name="value"/> to <see cref="ActionDescMaxLength"/> characters.</summary>
    public static string? TruncateActionDesc(string? value) =>
        value is { Length: > ActionDescMaxLength } ? value[..ActionDescMaxLength] : value;

    /// <summary>
    /// Equality that treats two non-string sequences as equal when their elements match, so a
    /// freshly-rebuilt collection with the same contents is not reported as a change.
    /// </summary>
    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        if (a is not string && b is not string && a is IEnumerable ea && b is IEnumerable eb)
        {
            return ea.Cast<object?>().SequenceEqual(eb.Cast<object?>());
        }
        return a.Equals(b);
    }
}

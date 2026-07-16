using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CMS.API.Pdf;

/// <summary>
/// Turns the course's HTML-formatted rich-text fields into clean plain text for
/// the PDF, and inserts line-break opportunities between CJK characters so
/// MigraDoc (which only wraps at whitespace) doesn't overflow long Chinese lines.
/// </summary>
internal static partial class RichText
{
    private const char ZeroWidthSpace = (char)0x200B;
    private const char NonBreakingSpace = (char)0x00A0;

    /// <summary>Strips HTML markup and decodes entities to readable plain text.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html;
        text = ListItemTag().Replace(text, "\n• ");      // <li> -> bullet line
        text = BreakTag().Replace(text, "\n");                // <br> -> newline
        text = BlockCloseTag().Replace(text, "\n");           // </p>, </div>, </li>, </tr>, </hN> -> newline
        text = AnyTag().Replace(text, string.Empty);          // drop all remaining tags
        text = WebUtility.HtmlDecode(text);                   // &amp; &nbsp; &#8226; ...

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace(NonBreakingSpace, ' ');
        var lines = text.Split('\n').Select(line => line.Trim());
        text = string.Join("\n", lines);
        text = BlankLines().Replace(text, "\n\n");            // collapse 3+ newlines to a blank line
        return text.Trim();
    }

    /// <summary>Inserts a zero-width space after each CJK character to allow wrapping.</summary>
    public static string AddCjkBreaks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            builder.Append(ch);
            if (IsCjk(ch))
            {
                builder.Append(ZeroWidthSpace);
            }
        }

        return builder.ToString();
    }

    private static bool IsCjk(char c) =>
        c is >= '一' and <= '鿿' ||   // CJK Unified Ideographs
        c is >= '㐀' and <= '䶿' ||   // CJK Extension A
        c is >= '　' and <= '〿' ||   // CJK symbols & punctuation
        c is >= '＀' and <= '￯';     // Fullwidth forms

    [GeneratedRegex(@"<\s*li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemTag();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"<\s*/\s*(p|div|li|tr|h[1-6])\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockCloseTag();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}

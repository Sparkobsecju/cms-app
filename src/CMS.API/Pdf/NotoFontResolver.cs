using PdfSharp.Fonts;

namespace CMS.API.Pdf;

/// <summary>
/// Supplies the embedded Noto Sans TC font (SIL OFL) to PDFsharp/MigraDoc. The
/// Core build ships no default font resolver and platform fonts don't reliably
/// cover CJK, so we resolve every requested family to the bundled Noto face and
/// let PDFsharp simulate bold/italic. A TrueType (glyf) font is used so PDFsharp
/// subsets it to just the glyphs actually used — keeping each PDF small enough to
/// email. Registered once via <see cref="CoursePdfDocument"/>.
/// </summary>
public sealed class NotoFontResolver : IFontResolver
{
    /// <summary>Family name to set on MigraDoc styles.</summary>
    public const string FamilyName = "Noto Sans TC";

    private const string FaceName = "NotoSansTC";

    private static readonly byte[] FontBytes = Load("NotoSansTC-VF.ttf");

    public byte[] GetFont(string faceName) => FontBytes;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // One family/face is shipped; PDFsharp simulates bold and italic from it.
        return new FontResolverInfo(FaceName, isBold, isItalic);
    }

    private static byte[] Load(string fileName)
    {
        var assembly = typeof(NotoFontResolver).Assembly;
        var resourceName = $"CMS.API.Assets.Fonts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource '{resourceName}' not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

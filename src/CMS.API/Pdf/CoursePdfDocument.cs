using System.Globalization;
using CMS.API.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace CMS.API.Pdf;

/// <summary>
/// Builds the Course PDF (課程 PDF) — a clean, branded, single-flow document of a
/// course's approval essentials. Pure: takes a <see cref="CoursePdf"/> and returns
/// the PDF bytes, with no DB or HTTP concerns, so it is unit-testable in isolation.
/// </summary>
public static class CoursePdfDocument
{
    static CoursePdfDocument()
    {
        // Register the embedded CJK font once per process. Guarded so the web host
        // and the test host can both trigger it without double-setting (which throws).
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new NotoFontResolver();
        }
    }

    /// <summary>Renders the course to PDF bytes.</summary>
    public static byte[] Render(CoursePdf course)
    {
        ArgumentNullException.ThrowIfNull(course);

        var document = BuildDocument(course);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static Document BuildDocument(CoursePdf course)
    {
        var document = new Document();
        var normal = document.Styles["Normal"]!; // always present on a new Document
        normal.Font.Name = NotoFontResolver.FamilyName;
        normal.Font.Size = 10.5;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);

        // Partner banner (text-only fallback until logo asset location is known).
        if (!string.IsNullOrWhiteSpace(course.PartnerName))
        {
            var partner = section.AddParagraph(RichText.AddCjkBreaks(course.PartnerName.Trim()));
            partner.Format.Font.Size = 11;
            partner.Format.Font.Color = Colors.Gray;
            partner.Format.SpaceAfter = Unit.FromPoint(2);
        }

        // Title (prefer the official title) + course code.
        var titleText = !string.IsNullOrWhiteSpace(course.OfficialTitle)
            ? course.OfficialTitle!.Trim()
            : course.Title.Trim();
        var title = section.AddParagraph(RichText.AddCjkBreaks(titleText));
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(2);

        var code = section.AddParagraph($"課程編號 Course ID：{course.CourseId}");
        code.Format.Font.Size = 9;
        code.Format.Font.Color = Colors.Gray;
        code.Format.SpaceAfter = Unit.FromPoint(10);

        // At-a-glance facts.
        var facts = new List<string>
        {
            $"時數：{course.Hour} 小時",
            $"定價：NT$ {course.ListPrice.ToString("#,##0", CultureInfo.InvariantCulture)}",
            $"學分：{course.LearningCredit.ToString("0.#", CultureInfo.InvariantCulture)}",
        };
        var glance = section.AddParagraph(string.Join("      ", facts));
        glance.Format.Font.Bold = true;
        glance.Format.SpaceAfter = Unit.FromPoint(12);

        // Curated content sections (skipped when empty).
        AddField(section, "課程目標 Objective", course.Objective);
        AddField(section, "適合對象 Target", course.Target);
        AddField(section, "先修條件 Prerequisites", course.Prerequisites);
        AddField(section, "課程大綱 Outline", course.Outline);
        AddField(section, "教材 Material", course.Material);
        AddField(section, "對應認證／考試 Certification / Exam", course.TowardCertOrExam);

        if (course.Certifications.Count > 0)
        {
            AddHeading(section, "相關認證 Certifications");
            foreach (var cert in course.Certifications)
            {
                var item = section.AddParagraph($"• {RichText.AddCjkBreaks(cert)}");
                item.Format.SpaceAfter = Unit.FromPoint(2);
            }
            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);
        }

        AddField(section, "備註 Note", course.Note);
        AddField(section, "其他資訊 Other Info", course.OtherInfo);

        return document;
    }

    private static void AddField(Section section, string label, string? rawValue)
    {
        // Course fields hold HTML rich text; flatten to clean plain text first.
        var text = RichText.ToPlainText(rawValue);
        if (text.Length == 0)
        {
            return;
        }

        AddHeading(section, label);
        AddBody(section, text);
    }

    // Renders text as one paragraph, honouring embedded newlines as line breaks
    // and inserting CJK break opportunities so long Chinese lines wrap.
    private static void AddBody(Section section, string text)
    {
        var body = section.AddParagraph();
        body.Format.SpaceAfter = Unit.FromPoint(10);

        var lines = RichText.AddCjkBreaks(text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                body.AddLineBreak();
            }

            body.AddText(lines[i]);
        }
    }

    private static void AddHeading(Section section, string text)
    {
        var heading = section.AddParagraph(text);
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(2);
        heading.Format.SpaceAfter = Unit.FromPoint(3);
    }
}

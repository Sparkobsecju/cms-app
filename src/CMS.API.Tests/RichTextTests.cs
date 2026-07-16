using CMS.API.Pdf;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="RichText"/>: course fields hold HTML rich text, which
/// must be flattened to clean plain text (no tags) with CJK break opportunities.
/// </summary>
public class RichTextTests
{
    [Fact]
    public void ToPlainText_StripsTags_AndDecodesEntities()
    {
        var html = "<font color=\"#BD0000\">● 重點</font><br>第二行 &amp; 更多<sup>®</sup>";

        var text = RichText.ToPlainText(html);

        Assert.DoesNotContain("<", text);
        Assert.DoesNotContain("&amp;", text);
        Assert.Contains("● 重點", text);
        Assert.Contains("第二行 & 更多®", text);
        Assert.Contains("\n", text); // <br> became a newline
    }

    [Fact]
    public void ToPlainText_ConvertsListItems_ToBullets()
    {
        var text = RichText.ToPlainText("<ul><li>甲</li><li>乙</li></ul>");

        Assert.Contains("• 甲", text);
        Assert.Contains("• 乙", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToPlainText_ReturnsEmpty_ForNullOrBlank(string? input)
    {
        Assert.Equal(string.Empty, RichText.ToPlainText(input));
    }

    [Fact]
    public void AddCjkBreaks_InsertsZeroWidthSpaceAfterCjk_ButNotLatin()
    {
        var result = RichText.AddCjkBreaks("A中B");

        Assert.Equal("A中​B", result); // ZWSP inserted after 中, none after Latin
    }
}

using CMS.API.Data;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="SqlLike.EscapeWildcards"/> — the helper that stops a user keyword from
/// acting as a LIKE pattern (a search for <c>%</c> must not match every row). Pairs with the
/// <c>ESCAPE '\'</c> clause added to every keyword LIKE in the repositories.
/// </summary>
public class SqlLikeTests
{
    [Fact]
    public void Null_PassesThrough() => Assert.Null(SqlLike.EscapeWildcards(null));

    [Fact]
    public void PlainKeyword_Unchanged() =>
        Assert.Equal("dotnet", SqlLike.EscapeWildcards("dotnet"));

    [Theory]
    [InlineData("50%", "50\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("[abc]", "\\[abc]")]
    [InlineData("%", "\\%")]
    public void Wildcards_AreEscaped(string input, string expected) =>
        Assert.Equal(expected, SqlLike.EscapeWildcards(input));

    [Fact]
    public void Backslash_IsEscapedFirst_SoEscapesAreNotDoubledIncorrectly() =>
        // The escape character itself must be escaped, and before the wildcards, so "\%" becomes "\\\%".
        Assert.Equal("\\\\\\%", SqlLike.EscapeWildcards("\\%"));

    [Fact]
    public void Combined_AllMetacharactersEscaped() =>
        Assert.Equal("a\\%b\\_c\\[d", SqlLike.EscapeWildcards("a%b_c[d"));
}

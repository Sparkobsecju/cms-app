using System.Security.Claims;
using CMS.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for the generic reflection logic in <see cref="RowAuditWriter"/> /
/// <see cref="RowAuditReflection"/>: how ActionDesc, PrimaryKeyValues and UserName are derived
/// from an arbitrary entity and the current request. These exercise the Build* seams so no DB
/// connection is opened.
/// </summary>
public class RowAuditWriterTests
{
    /// <summary>First string property (in declaration order) is Title; pkid is Pkid.</summary>
    private sealed class SampleEntity
    {
        public int Pkid { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    private static RowAuditWriter Writer(ClaimsPrincipal? user = null)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext)
                .Returns(user is null ? null : new DefaultHttpContext { User = user });
        return new RowAuditWriter(accessor.Object);
    }

    private static ClaimsPrincipal AuthedUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

    // ----- ActionDesc: Insert / Delete use the first string property -----

    [Fact]
    public void BuildInsertAudit_UsesFirstStringProperty_AsActionDesc()
    {
        var entity = new SampleEntity { Pkid = 3, Title = "Azure Fundamentals", Code = "AZ-900" };

        var audit = Writer().BuildInsertAudit("Course", entity);

        Assert.Equal("Insert", audit.ActionType);
        Assert.Equal("Course", audit.TableName);
        Assert.Equal("Azure Fundamentals", audit.ActionDesc);
    }

    [Fact]
    public void BuildDeleteAudit_UsesFirstStringProperty_AsActionDesc()
    {
        var entity = new SampleEntity { Pkid = 8, Title = "Retired Course", Code = "OLD-1" };

        var audit = Writer().BuildDeleteAudit("Course", entity);

        Assert.Equal("Delete", audit.ActionType);
        Assert.Equal("Retired Course", audit.ActionDesc);
    }

    // ----- PrimaryKeyValues reads pkid -----

    [Fact]
    public void BuildAudit_ReadsPkid_IntoPrimaryKeyValues()
    {
        var entity = new SampleEntity { Pkid = 42, Title = "X" };

        var audit = Writer().BuildInsertAudit("Course", entity);

        Assert.Equal("42", audit.PrimaryKeyValues);
    }

    // ----- ActionDesc: Update lists the changed property names -----

    [Fact]
    public void BuildUpdateAudit_ListsExactlyTheChangedPropertyNames_InDeclarationOrder()
    {
        var before = new SampleEntity { Pkid = 1, Title = "Old", Code = "C1", DisplayOrder = 1, IsActive = true };
        var after = new SampleEntity { Pkid = 1, Title = "New", Code = "C1", DisplayOrder = 5, IsActive = true };

        var audit = Writer().BuildUpdateAudit("Course", before, after);

        Assert.NotNull(audit);
        Assert.Equal("Update", audit!.ActionType);
        Assert.Equal("Title, DisplayOrder", audit.ActionDesc);
    }

    [Fact]
    public void BuildUpdateAudit_SingleChangedProperty()
    {
        var before = new SampleEntity { Pkid = 1, Title = "Same", Code = "C1", DisplayOrder = 1 };
        var after = new SampleEntity { Pkid = 1, Title = "Same", Code = "C2", DisplayOrder = 1 };

        var audit = Writer().BuildUpdateAudit("Course", before, after);

        Assert.Equal("Code", audit!.ActionDesc);
    }

    [Fact]
    public void BuildUpdateAudit_ReturnsNull_WhenNothingChanged()
    {
        var before = new SampleEntity { Pkid = 1, Title = "Same", Code = "C1", DisplayOrder = 1, IsActive = true };
        var after = new SampleEntity { Pkid = 1, Title = "Same", Code = "C1", DisplayOrder = 1, IsActive = true };

        var audit = Writer().BuildUpdateAudit("Course", before, after);

        Assert.Null(audit);
    }

    // ----- UserName from JWT claims, else "system" -----

    [Fact]
    public void UserName_FallsBackToSystem_WhenNoAuthenticatedUser()
    {
        // No HttpContext at all.
        var audit = Writer(user: null).BuildInsertAudit("Course", new SampleEntity { Title = "X" });

        Assert.Equal("system", audit.UserName);
    }

    [Fact]
    public void UserName_FallsBackToSystem_WhenIdentityNotAuthenticated()
    {
        // Claims present but the identity has no authentication type → not authenticated.
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("UserName", "ghost") }));

        var audit = Writer(unauthenticated).BuildInsertAudit("Course", new SampleEntity { Title = "X" });

        Assert.Equal("system", audit.UserName);
    }

    [Fact]
    public void UserName_ReadsUserNameClaim_WhenAuthenticated()
    {
        var user = AuthedUser(new Claim("UserName", "alice"));

        var audit = Writer(user).BuildInsertAudit("Course", new SampleEntity { Title = "X" });

        Assert.Equal("alice", audit.UserName);
    }

    [Fact]
    public void UserName_FallsBackToNameClaim_WhenNoUserNameClaim()
    {
        var user = AuthedUser(new Claim(ClaimTypes.Name, "bob"));

        var audit = Writer(user).BuildInsertAudit("Course", new SampleEntity { Title = "X" });

        Assert.Equal("bob", audit.UserName);
    }

    // ----- ActionDesc truncation at 1000 characters -----

    [Fact]
    public void ActionDesc_IsTruncated_At1000Characters()
    {
        var entity = new SampleEntity { Pkid = 1, Title = new string('a', 1500) };

        var audit = Writer().BuildInsertAudit("Course", entity);

        Assert.Equal(1000, audit.ActionDesc!.Length);
    }

    // ----- Reflection helper edge cases -----

    [Fact]
    public void FirstStringPropertyValue_ReturnsNull_WhenEntityHasNoStringProperty()
    {
        Assert.Null(RowAuditReflection.FirstStringPropertyValue(new { Pkid = 1, Count = 2 }));
    }

    [Fact]
    public void PrimaryKeyValue_ReturnsEmpty_WhenNoPkidProperty()
    {
        Assert.Equal(string.Empty, RowAuditReflection.PrimaryKeyValue(new { Title = "no key here" }));
    }
}

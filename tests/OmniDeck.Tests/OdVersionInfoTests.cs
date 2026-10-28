using System.Reflection;
using System.Reflection.Emit;
using OmniDeck.Core;

namespace OmniDeck.Tests;

public sealed class OdVersionInfoTests
{
    [Fact]
    public void Parse_SplitsVersionAndShortCommit()
    {
        var info = OdVersionInfo.Parse("0.0.1-alpha.1+1ab1b365867c3642a489fc43250f14d567ec1e6f");

        Assert.Equal("0.0.1-alpha.1", info.Version);
        Assert.Equal("1ab1b36", info.Commit);
        Assert.Equal("0.0.1-alpha.1 (1ab1b36)", info.DisplayText);
    }

    [Fact]
    public void Parse_WithoutMetadata_HasNoCommit()
    {
        var info = OdVersionInfo.Parse("1.2.3");

        Assert.Equal("1.2.3", info.Version);
        Assert.Null(info.Commit);
        Assert.Equal("1.2.3", info.DisplayText);
    }

    [Theory]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+   ")]
    public void Parse_WithEmptyMetadata_HasNoCommit(string informationalVersion)
    {
        var info = OdVersionInfo.Parse(informationalVersion);

        Assert.Equal("1.2.3", info.Version);
        Assert.Null(info.Commit);
    }

    [Fact]
    public void Parse_WithShortMetadata_KeepsItWhole()
    {
        Assert.Equal("abc", OdVersionInfo.Parse("1.0.0+abc").Commit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsBlankInput(string informationalVersion)
    {
        Assert.ThrowsAny<ArgumentException>(() => OdVersionInfo.Parse(informationalVersion));
    }

    [Fact]
    public void FromAssembly_ReadsTheStampedVersion()
    {
        var info = OdVersionInfo.FromAssembly(typeof(OdVersionInfo).Assembly);

        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.DoesNotContain("+", info.Version, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void FromAssembly_WithoutInformationalVersion_IsUnknown(string? informationalVersion)
    {
        var info = OdVersionInfo.FromAssembly(CreateAssembly(informationalVersion));

        Assert.Same(OdVersionInfo.Unknown, info);
    }

    [Fact]
    public void FromAssembly_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => OdVersionInfo.FromAssembly(null!));
    }

    /// <summary>A dynamic assembly, optionally stamped with an informational version.</summary>
    private static Assembly CreateAssembly(string? informationalVersion)
    {
        var builder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"OdVersionInfoTests.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            builder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return builder;
    }
}

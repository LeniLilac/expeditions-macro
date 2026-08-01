using ExpeditionsMacro.Automation.Updates;

namespace ExpeditionsMacro.Tests;

public sealed class ApplicationSemanticVersionTests
{
    [Theory]
    [InlineData("1.3.0-beta.9", "1.3.0-beta.10")]
    [InlineData("1.3.0-beta.54", "1.3.0")]
    [InlineData("1.3.0-alpha", "1.3.0-beta")]
    [InlineData("1.3.0-beta.1", "1.3.0-beta.release")]
    [InlineData("1.3.0", "1.3.1")]
    [InlineData("1.3.9", "1.4.0")]
    [InlineData("1.9.9", "2.0.0")]
    public void CompareTo_UsesSemanticPrecedence(
        string older,
        string newer)
    {
        ApplicationSemanticVersion left =
            ApplicationSemanticVersion.Parse(older);
        ApplicationSemanticVersion right =
            ApplicationSemanticVersion.Parse(newer);

        Assert.True(left.CompareTo(right) < 0);
        Assert.True(right.CompareTo(left) > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-beta..1")]
    [InlineData("1.2.3-beta_1")]
    [InlineData("1.2.3-beta.01")]
    public void TryParse_RejectsNonSemanticVersions(
        string value)
    {
        Assert.False(
            ApplicationSemanticVersion.TryParse(
                value,
                out _));
    }

    [Fact]
    public void Parse_IgnoresBuildMetadata()
    {
        ApplicationSemanticVersion version =
            ApplicationSemanticVersion.Parse(
                "1.3.0-beta.54+abcdef");

        Assert.Equal("1.3.0-beta.54", version.ToString());
    }
}

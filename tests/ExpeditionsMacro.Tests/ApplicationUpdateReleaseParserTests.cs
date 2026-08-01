using System.Text.Json;
using ExpeditionsMacro.Automation.Updates;

namespace ExpeditionsMacro.Tests;

public sealed class ApplicationUpdateReleaseParserTests
{
    private const string Hash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ParseLatest_PrereleaseBuildSelectsHighestEligibleVersion()
    {
        byte[] json = Releases(
            Release("1.3.0-beta.54", prerelease: true),
            Release("1.3.0", prerelease: false));

        ApplicationUpdateRelease? release =
            ApplicationUpdateReleaseParser.ParseLatest(
                json,
                ApplicationSemanticVersion.Parse(
                    "1.3.0-beta.53"));

        Assert.NotNull(release);
        Assert.Equal("1.3.0", release.Version.ToString());
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public void ParseLatest_StableBuildNeverCrossesIntoPrereleaseChannel()
    {
        byte[] json = Releases(
            Release("1.3.0-beta.54", prerelease: true),
            Release("1.2.1", prerelease: false));

        ApplicationUpdateRelease? release =
            ApplicationUpdateReleaseParser.ParseLatest(
                json,
                ApplicationSemanticVersion.Parse("1.2.0"));

        Assert.NotNull(release);
        Assert.Equal("1.2.1", release.Version.ToString());
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public void ParseLatest_IgnoresDraftAndOlderReleases()
    {
        byte[] json = Releases(
            Release(
                "1.3.0-beta.55",
                prerelease: true,
                draft: true),
            Release("1.3.0-beta.52", prerelease: true));

        ApplicationUpdateRelease? release =
            ApplicationUpdateReleaseParser.ParseLatest(
                json,
                ApplicationSemanticVersion.Parse(
                    "1.3.0-beta.53"));

        Assert.Null(release);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("url")]
    [InlineData("tag")]
    [InlineData("prerelease")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("release_url")]
    [InlineData("missing_draft")]
    [InlineData("invalid_prerelease_type")]
    public void ParseLatest_RejectsInconsistentReleaseInventory(
        string mutation)
    {
        Dictionary<string, object?> release =
            Release("1.3.0-beta.54", prerelease: true);
        Mutate(release, mutation);

        ApplicationUpdateRelease? parsed =
            ApplicationUpdateReleaseParser.ParseLatest(
                Releases(release),
                ApplicationSemanticVersion.Parse(
                    "1.3.0-beta.53"));

        Assert.Null(parsed);
    }

    [Fact]
    public void ParseLatest_ExposesOnlyInstallerAndChecksumAssets()
    {
        byte[] json = Releases(
            Release("1.3.0-beta.54", prerelease: true));

        ApplicationUpdateRelease release =
            Assert.IsType<ApplicationUpdateRelease>(
                ApplicationUpdateReleaseParser.ParseLatest(
                    json,
                    ApplicationSemanticVersion.Parse(
                        "1.3.0-beta.53")));

        Assert.Equal(
            "ExpeditionsMacro-1.3.0-beta.54-win-x64-setup.exe",
            release.Installer.Name);
        Assert.Equal("SHA256SUMS.txt", release.Checksums.Name);
        Assert.Equal(Hash, release.Installer.Sha256);
        Assert.Equal(Hash, release.Checksums.Sha256);
    }

    private static void Mutate(
        Dictionary<string, object?> release,
        string mutation)
    {
        List<Dictionary<string, object?>> assets =
            Assert.IsType<
                List<Dictionary<string, object?>>>(
                release["assets"]);
        switch (mutation)
        {
            case "digest":
                assets[0]["digest"] = "sha256:1234";
                break;
            case "url":
                assets[0]["browser_download_url"] =
                    "https://evil.example/installer.exe";
                break;
            case "tag":
                release["tag_name"] = "v1.3.0-beta.53";
                break;
            case "prerelease":
                release["prerelease"] = false;
                break;
            case "missing":
                assets.RemoveAt(0);
                break;
            case "extra":
                assets.Add(Asset(
                    "unexpected.bin",
                    "1.3.0-beta.54"));
                break;
            case "duplicate":
                assets.Add(new Dictionary<string, object?>(
                    assets[0],
                    StringComparer.Ordinal));
                break;
            case "release_url":
                release["html_url"] =
                    "https://evil.example/release";
                break;
            case "missing_draft":
                release.Remove("draft");
                break;
            case "invalid_prerelease_type":
                release["prerelease"] = "true";
                break;
        }
    }

    private static byte[] Releases(
        params Dictionary<string, object?>[] releases) =>
        JsonSerializer.SerializeToUtf8Bytes(releases);

    private static Dictionary<string, object?> Release(
        string version,
        bool prerelease,
        bool draft = false) =>
        new(StringComparer.Ordinal)
        {
            ["draft"] = draft,
            ["prerelease"] = prerelease,
            ["tag_name"] = $"v{version}",
            ["name"] = $"Expeditions Macro v{version}",
            ["html_url"] =
                $"https://github.com/LeniLilac/expeditions-macro/releases/tag/v{version}",
            ["assets"] = new List<Dictionary<string, object?>>
            {
                Asset(
                    $"ExpeditionsMacro-{version}-win-x64-setup.exe",
                    version),
                Asset(
                    $"ExpeditionsMacro-{version}-win-x64.zip",
                    version),
                Asset("dependencies.json", version),
                Asset("SHA256SUMS.txt", version),
            },
        };

    private static Dictionary<string, object?> Asset(
        string name,
        string version) =>
        new(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["size"] = 128,
            ["digest"] = $"sha256:{Hash}",
            ["browser_download_url"] =
                $"https://github.com/LeniLilac/expeditions-macro/releases/download/v{version}/{name}",
        };
}

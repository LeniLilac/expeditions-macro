using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxPrivateServerLinkTests
{
    [Theory]
    [InlineData(
        "https://www.roblox.com/share?code=Share_Code-123&type=Server",
        RobloxPrivateServerLinkKind.ShareCode,
        "roblox://navigation/share_links?code=Share_Code-123&type=Server")]
    [InlineData(
        "roblox://navigation/share_links?code=Share_Code-123&type=Server",
        RobloxPrivateServerLinkKind.ShareCode,
        "roblox://navigation/share_links?code=Share_Code-123&type=Server")]
    [InlineData(
        "https://www.roblox.com/games/8304191830/Anime-Expeditions?privateServerLinkCode=Legacy_Code-456",
        RobloxPrivateServerLinkKind.LegacyLinkCode,
        "roblox://experiences/start?placeId=8304191830&linkCode=Legacy_Code-456")]
    [InlineData(
        "roblox://experiences/start?placeId=8304191830&linkCode=Legacy_Code-456",
        RobloxPrivateServerLinkKind.LegacyLinkCode,
        "roblox://experiences/start?placeId=8304191830&linkCode=Legacy_Code-456")]
    public void Parse_ProducesCredentialFreeRobloxLaunchUri(
        string input,
        RobloxPrivateServerLinkKind expectedKind,
        string expectedUri)
    {
        RobloxPrivateServerLaunchTarget target =
            RobloxPrivateServerLaunchTarget.Parse(input);

        Assert.Equal(expectedKind, target.Kind);
        Assert.Equal(expectedUri, target.LaunchUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://roblox.com.example.com/share?code=Secret&type=Server")]
    [InlineData("https://www.roblox.com/share?code=Secret&type=Experience")]
    [InlineData("https://www.roblox.com/games/8304191830/Anime-Expeditions")]
    [InlineData("roblox://navigation/share_links?code=x&type=Server")]
    public void Parse_RejectsNonPrivateOrLookalikeLinks(string input)
    {
        Assert.False(
            RobloxPrivateServerLaunchTarget.TryParse(
                input,
                out RobloxPrivateServerLaunchTarget? target));
        Assert.Null(target);
    }
}

using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Abstractions;
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

    [Fact]
    public async Task LaunchAsync_SendsNormalizedUriWithoutClosingRoblox()
    {
        RecordingProcessController processes = new();
        RobloxPrivateServerRecoveryService recovery = new(
            automation: null!,
            processes);
        RobloxPrivateServerLaunchTarget target =
            RobloxPrivateServerLaunchTarget.Parse(
                "https://www.roblox.com/share?code=Share_Code-123&type=Server");

        await recovery.LaunchAsync(target);

        Assert.Equal(target.LaunchUri, Assert.Single(processes.Launched));
        Assert.Equal(0, processes.CloseCount);
    }

    private sealed class RecordingProcessController : IRobloxProcessController
    {
        public List<Uri> Launched { get; } = [];

        public int CloseCount { get; private set; }

        public Task CloseAsync(
            RobloxWindow? window,
            CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }

        public Task LaunchAsync(
            Uri launchUri,
            CancellationToken cancellationToken)
        {
            Launched.Add(launchUri);
            return Task.CompletedTask;
        }
    }
}

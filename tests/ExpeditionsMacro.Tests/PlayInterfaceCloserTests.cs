using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlayInterfaceCloserTests
{
    [Fact]
    public void ReviewedChallengeHandoffLayers_AreRecognized()
    {
        ImageFrame selector = ImageCodec.Load(
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameModeSelector",
                "GameModeSelector_05.png"));
        ImageFrame party = ImageCodec.Load(
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "PostMatchPreview",
                "PostMatchPreview_01.png"));
        ImageFrame prestart = ImageCodec.Load(
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "Prestart_FairyKingForest",
                "Prestart_FairyKingForest_01.png"));

        Assert.Equal(
            PlayInterfaceLayer.GameModeSelector,
            PlayInterfaceCloser.DetectLayer(selector));
        Assert.Equal(
            PlayInterfaceLayer.PostMatchParty,
            PlayInterfaceCloser.DetectLayer(party));
        Assert.Equal(
            PlayInterfaceLayer.Closed,
            PlayInterfaceCloser.DetectLayer(prestart));
    }

    [Fact]
    public async Task ChallengeSelectorHandoff_ClicksBackThroughPartyBeforeLobbyNavigation()
    {
        var observations = new Queue<PlayInterfaceLayer>(
        [
            PlayInterfaceLayer.GameModeSelector,
            PlayInterfaceLayer.GameModeSelector,
            PlayInterfaceLayer.PostMatchParty,
            PlayInterfaceLayer.PostMatchParty,
            PlayInterfaceLayer.Closed,
            PlayInterfaceLayer.Closed,
            PlayInterfaceLayer.Closed,
        ]);
        int clicks = 0;

        await PlayInterfaceCloser.CloseAsync(
            () => observations.Dequeue(),
            token =>
            {
                clicks++;
                return Task.CompletedTask;
            },
            CancellationToken.None,
            static (duration, token) =>
                Task.CompletedTask);

        Assert.Equal(2, clicks);
        Assert.Empty(observations);
    }

    [Fact]
    public async Task UnresponsivePlayInterface_RemainsARecoveryCandidate()
    {
        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<
                RobloxUiUnavailableException>(
                () => PlayInterfaceCloser.CloseAsync(
                    () =>
                        PlayInterfaceLayer
                            .GameModeSelector,
                    token => Task.CompletedTask,
                    CancellationToken.None,
                    static (duration, token) =>
                        Task.CompletedTask));

        Assert.Contains(
            "remained open",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            RobloxRuntimeRecoveryPolicy
                .IsRestartCandidate(error));
    }
}

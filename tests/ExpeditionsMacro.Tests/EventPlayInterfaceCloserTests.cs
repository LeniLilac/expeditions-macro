using ExpeditionsMacro.Automation.Events;
using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class EventPlayInterfaceCloserTests
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
            EventPlayInterfaceLayer.GameModeSelector,
            EventPlayInterfaceCloser.DetectLayer(selector));
        Assert.Equal(
            EventPlayInterfaceLayer.PostMatchParty,
            EventPlayInterfaceCloser.DetectLayer(party));
        Assert.Equal(
            EventPlayInterfaceLayer.Closed,
            EventPlayInterfaceCloser.DetectLayer(prestart));
    }

    [Fact]
    public async Task ChallengeSelectorHandoff_ClicksBackThroughPartyBeforeLobbyNavigation()
    {
        var observations = new Queue<EventPlayInterfaceLayer>(
        [
            EventPlayInterfaceLayer.GameModeSelector,
            EventPlayInterfaceLayer.GameModeSelector,
            EventPlayInterfaceLayer.PostMatchParty,
            EventPlayInterfaceLayer.PostMatchParty,
            EventPlayInterfaceLayer.Closed,
            EventPlayInterfaceLayer.Closed,
            EventPlayInterfaceLayer.Closed,
        ]);
        int clicks = 0;

        await EventPlayInterfaceCloser.CloseAsync(
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
                () => EventPlayInterfaceCloser.CloseAsync(
                    () =>
                        EventPlayInterfaceLayer
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

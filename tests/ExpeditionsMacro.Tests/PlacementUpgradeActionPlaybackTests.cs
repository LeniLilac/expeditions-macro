using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementUpgradeActionPlaybackTests
{
    [Fact]
    public async Task Affordable_RequiresTwoFreshFramesBeforeInput()
    {
        PlacementServiceTests.FakeAutomation automation =
            Automation(
                "UpgradeUnitAffordable_01.png",
                "UpgradeUnitAffordable_01.png");
        PlacementUpgradeActionPlayback playback =
            Playback(automation);

        await ApplyAsync(playback, automation);

        Assert.Equal(2, automation.CaptureCount);
        Assert.Single(
            automation.InputActions,
            action => action == "letter:U");
    }

    [Fact]
    public async Task Unaffordable_WaitsForStableAffordableState()
    {
        PlacementServiceTests.FakeAutomation automation =
            Automation(
                "UpgradeUnitUnaffordable_01.png",
                "UpgradeUnitUnaffordable_01.png",
                "UpgradeUnitAffordable_01.png",
                "UpgradeUnitAffordable_02.png");
        PlacementUpgradeActionPlayback playback =
            Playback(automation);
        List<string> status = [];

        await ApplyAsync(
            playback,
            automation,
            status: status.Add);

        Assert.Equal(4, automation.CaptureCount);
        Assert.Single(
            automation.InputActions,
            action => action == "letter:U");
        Assert.Contains(
            status,
            message => message.Contains(
                "waiting for Upgrade Unit",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Maxed_StopsRemainingPressesWithoutError()
    {
        PlacementServiceTests.FakeAutomation automation =
            Automation(
                "UpgradeUnitMaxed_01.png",
                "UpgradeUnitMaxed_01.png");
        PlacementUpgradeActionPlayback playback =
            Playback(automation);
        List<string> status = [];

        await ApplyAsync(
            playback,
            automation,
            pressCount: 3,
            status: status.Add);

        Assert.DoesNotContain(
            automation.InputActions,
            action => action == "letter:U");
        Assert.Contains(
            status,
            message => message.Contains(
                "unit is Maxed",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task LostPanel_FailsBeforeSendingInput()
    {
        PlacementServiceTests.FakeAutomation automation =
            Automation(
                "SelectedUnitPanelHoverNegative_01.png",
                "SelectedUnitPanelHoverNegative_01.png");
        PlacementUpgradeActionPlayback playback =
            Playback(automation);

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<
                RobloxUiUnavailableException>(
                () => ApplyAsync(
                    playback,
                    automation));

        Assert.Contains(
            "panel disappeared",
            error.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            automation.InputActions,
            action => action == "letter:U");
    }

    [Fact]
    public async Task ReadinessDisabled_SendsConfiguredPressesDirectly()
    {
        PlacementServiceTests.FakeAutomation automation =
            Automation();
        PlacementUpgradeActionPlayback playback =
            Playback(automation);

        await ApplyAsync(
            playback,
            automation,
            pressCount: 3,
            requireReadiness: false);

        Assert.Equal(0, automation.CaptureCount);
        Assert.Equal(
            3,
            automation.InputActions.Count(
                action => action == "letter:U"));
    }

    private static Task ApplyAsync(
        PlacementUpgradeActionPlayback playback,
        PlacementServiceTests.FakeAutomation automation,
        int pressCount = 1,
        bool requireReadiness = true,
        Action<string>? status = null) =>
        playback.ApplyAsync(
            automation.FindWindow()!.Value,
            'U',
            pressCount,
            actionIntervalMilliseconds: 0,
            requireReadiness,
            stepNumber: 2,
            stepCount: 4,
            status,
            CancellationToken.None);

    private static PlacementUpgradeActionPlayback Playback(
        PlacementServiceTests.FakeAutomation automation) =>
        new(
            automation,
            utcNow: () => DateTimeOffset.UtcNow,
            delay: static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

    private static PlacementServiceTests.FakeAutomation
        Automation(
        params string[] frameNames) =>
        new(
            frameNames
                .Select(LoadStage)
                .ToArray());

    private static ImageFrame LoadStage(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                fileName));
}

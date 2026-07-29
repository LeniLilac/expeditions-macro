using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementServiceObservationBoundaryTests
{
    private static readonly RobloxWindow Window =
        new((nint)42, "Roblox");

    [Fact]
    public async Task Playback_AcceptsPanelAtTimeoutBoundaryWithoutAnotherClick()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame negative = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanelHoverNegative_01.png"));
            ImageFrame positive = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanel_01.png"));
            PlacementServiceTests.FakeAutomation automation = new(
                Enumerable.Repeat(negative, 7)
                    .Concat([positive, positive])
                    .ToArray());
            PlacementService service = new(
                automation,
                new PlacementServiceTests.FakeCaptureService(
                    automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                targetingKey: () => 'T',
                matchStartPlayback:
                    new PlacementMatchStartPlaybackStub());
            PlacementModel model = new()
            {
                Id = "boundary",
                Name = "Boundary",
                ClientWidth = 808,
                ClientHeight = 611,
                Steps =
                [
                    new PlacementStep
                    {
                        UnitKey = 2,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                    },
                ],
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                4,
                automation.InputActions.Count(
                    action =>
                        action == "click-retain:320,280"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task SelectedUnitProof_SlowObservationsCanCompleteBeforeTheHardDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-29T12:00:00Z");
        ImageFrame negative = LoadPanel(
            "SelectedUnitPanelHoverNegative_01.png");
        ImageFrame positive = LoadPanel(
            "SelectedUnitPanel_01.png");
        PlacementServiceTests.FakeAutomation automation =
            new(negative, positive, positive)
            {
                CaptureCompleted = () =>
                    now += TimeSpan.FromSeconds(10),
            };
        SelectedUnitPanelPlayback playback = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            });

        bool visible = await playback.WaitForVisibleAsync(
            Window,
            CancellationToken.None);

        Assert.True(visible);
        Assert.Equal(3, automation.CaptureCount);
        Assert.InRange(
            now,
            DateTimeOffset.Parse(
                "2026-07-29T12:00:30Z"),
            DateTimeOffset.Parse(
                "2026-07-29T12:00:31Z"));
    }

    [Fact]
    public async Task SelectedUnitProof_DoesNotStartAnotherObservationAfterTheHardDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-29T12:00:00Z");
        PlacementServiceTests.FakeAutomation automation =
            new(LoadPanel(
                "SelectedUnitPanelHoverNegative_01.png"))
            {
                CaptureCompleted = () =>
                    now += TimeSpan.FromSeconds(49),
            };
        SelectedUnitPanelPlayback playback = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            });

        bool visible = await playback.WaitForVisibleAsync(
            Window,
            CancellationToken.None);

        Assert.False(visible);
        Assert.Equal(1, automation.CaptureCount);
    }

    [Fact]
    public async Task SelectedUnitProof_CancellationStopsBeforeAnotherObservation()
    {
        PlacementServiceTests.FakeAutomation automation =
            new(LoadPanel(
                "SelectedUnitPanelHoverNegative_01.png"));
        SelectedUnitPanelPlayback playback = new(
            automation);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => playback.WaitForVisibleAsync(
                Window,
                cancellation.Token));

        Assert.Equal(0, automation.CaptureCount);
    }

    [Fact]
    public async Task PanelDismissal_SlowHiddenObservationsCompleteWithoutAnotherClick()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-29T12:00:00Z");
        PlacementServiceTests.FakeAutomation automation =
            new(LoadPanel("SelectedUnitPanel_01.png"))
            {
                CaptureCompleted = () =>
                    now += TimeSpan.FromSeconds(6),
            };
        SelectedUnitPanelPlayback playback = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            });

        await playback.DismissAsync(
            Window,
            808,
            611,
            CancellationToken.None);

        Assert.Single(
            automation.InputActions,
            action => action == "click:783,586");
        Assert.Equal(
            "park",
            automation.InputActions[^1]);
    }

    [Fact]
    public async Task PanelDismissal_HardDeadlineKeepsTheEightClickCap()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-29T12:00:00Z");
        PlacementServiceTests.FakeAutomation automation =
            new(LoadPanel("SelectedUnitPanel_01.png"))
            {
                IdleClicksBeforeDismissal = int.MaxValue,
                CaptureCompleted = () =>
                    now += TimeSpan.FromSeconds(49),
            };
        SelectedUnitPanelPlayback playback = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            });

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => playback.DismissAsync(
                Window,
                808,
                611,
                CancellationToken.None));

        Assert.Equal(
            8,
            automation.InputActions.Count(
                action => action == "click:783,586"));
        Assert.Equal(9, automation.CaptureCount);
    }

    private static ImageFrame LoadPanel(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                fileName));
}

using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementServiceObservationBoundaryTests
{
    [Fact]
    public async Task Playback_ConfirmsFinalSelectionSampleWithoutDuplicateClick()
    {
        ImageFrame hidden = Load("SelectedUnitPanelHoverNegative_01.png");
        ImageFrame visible = Load("SelectedUnitPanel_01.png");
        PlacementServiceTests.FakeAutomation automation = new(
            Enumerable.Repeat(hidden, 8)
                .Concat([visible, visible])
                .ToArray());

        await PlayOneStepAsync(automation);

        Assert.Single(
            automation.InputActions,
            action => action == "click-retain:320,280");
    }

    [Fact]
    public async Task Playback_ConfirmsHiddenPanelAtFinalDismissAttempt()
    {
        PlacementServiceTests.FakeAutomation automation = new()
        {
            IdleClicksBeforeDismissal = 8,
            HiddenCaptureDelayAfterDismissal = 3,
        };

        await PlayOneStepAsync(automation);

        Assert.Equal(
            8,
            automation.InputActions.Count(
                action => action == "click:783,586"));
        Assert.Equal("park", automation.InputActions[^1]);
    }

    private static async Task PlayOneStepAsync(
        PlacementServiceTests.FakeAutomation automation)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementService service = new(
                automation,
                new PlacementServiceTests.FakeCaptureService(automation),
                new PlacementModelRepository(new AppPaths(root)));
            await service.PlayAsync(
                new PlacementModel
                {
                    Id = "boundary",
                    Name = "Boundary",
                    ClientWidth = 808,
                    ClientHeight = 611,
                    Steps =
                    [
                        new PlacementStep
                        {
                            UnitKey = 1,
                            X = 320,
                            Y = 280,
                            DelayAfterMilliseconds = 0,
                        },
                    ],
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static ImageFrame Load(string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                fileName));
}

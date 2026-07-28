using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementServiceObservationBoundaryTests
{
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
                () => 'T');
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
}

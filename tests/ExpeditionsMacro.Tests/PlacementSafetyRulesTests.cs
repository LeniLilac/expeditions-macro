using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementSafetyRulesTests
{
    [Theory]
    [InlineData(235, 525, true)]
    [InlineData(584, 602, true)]
    [InlineData(467, 566, true)]
    [InlineData(534, 572, true)]
    [InlineData(285, 578, true)]
    [InlineData(337, 584, true)]
    [InlineData(384, 578, true)]
    [InlineData(429, 578, true)]
    [InlineData(234, 566, false)]
    [InlineData(585, 566, false)]
    [InlineData(467, 524, false)]
    [InlineData(467, 603, false)]
    public void FixedCentralHotbar_UsesCanonicalGameplayBounds(
        int x,
        int y,
        bool expected) =>
        Assert.Equal(
            expected,
            PlacementSafetyRules
                .IsInsideFixedCentralHotbar(x, y));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 300, true)]
    [InlineData(400, 0, true)]
    [InlineData(807, 300, true)]
    [InlineData(400, 610, true)]
    [InlineData(1, 1, false)]
    [InlineData(806, 609, false)]
    public void CanonicalClientEdge_IsNotAPlacementPoint(
        int x,
        int y,
        bool expected) =>
        Assert.Equal(
            expected,
            PlacementSafetyRules
                .IsOnCanonicalClientEdge(x, y));

    [Fact]
    public void LegacyUnsafeRows_RemainModelValidButHaveSkipReasons()
    {
        PlacementModel hotbar = ExpeditionModel(
            Step(5, 467, 566));
        PlacementModel duplicate = ExpeditionModel(
            Step(1, 388, 265),
            Step(
                1,
                409,
                266,
                PlacementPhase.AfterStart));

        hotbar.Validate();
        duplicate.Validate();

        Assert.Contains(
            "fixed center unit hotbar",
            PlacementSafetyRules.GetPlaybackSkipReason(
                hotbar,
                hotbar.Steps[0]),
            StringComparison.Ordinal);
        Assert.Null(
            PlacementSafetyRules.GetPlaybackSkipReason(
                duplicate,
                duplicate.Steps[0]));
        Assert.Contains(
            "earlier placement",
            PlacementSafetyRules.GetPlaybackSkipReason(
                duplicate,
                duplicate.Steps[1]),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateUnitSlot_IsRestrictedOnlyForExpedition()
    {
        Assert.Equal(
            3,
            PlacementSafetyRules.FindDuplicateUnitSlot(
                PlacementTargetMode.Expedition,
                [3, 2, 3]));
        Assert.Null(
            PlacementSafetyRules.FindDuplicateUnitSlot(
                PlacementTargetMode.Raid,
                [3, 2, 3]));
    }

    [Fact]
    public async Task Playback_SkipsHotbarRowBeforeInputAndPlacesNextRow()
    {
        PlacementModel model = ExpeditionModel(
            Step(5, 467, 566),
            Step(2, 320, 280));
        PlacementServiceTests.FakeAutomation automation =
            new();
        List<string> status = [];
        List<PlacementStep> sent = [];

        await PlayAsync(
            automation,
            model,
            status,
            sent);

        Assert.DoesNotContain(
            "key:5",
            automation.InputActions);
        Assert.DoesNotContain(
            "click-retain:467,566",
            automation.InputActions);
        Assert.Equal(
            1,
            automation.InputActions.Count(
                action => action == "key:2"));
        Assert.Single(sent);
        Assert.Equal(2, sent[0].UnitKey);
        Assert.Contains(
            status,
            message => message.Contains(
                "fixed center unit hotbar",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Playback_SkipsDefaultCoordinateBeforeUnitInput()
    {
        PlacementModel model = ExpeditionModel(
            Step(6, 0, 0),
            Step(2, 320, 280));
        PlacementServiceTests.FakeAutomation automation =
            new();
        List<string> status = [];
        List<PlacementStep> sent = [];

        await PlayAsync(
            automation,
            model,
            status,
            sent);

        Assert.DoesNotContain(
            "key:6",
            automation.InputActions);
        Assert.DoesNotContain(
            "click-retain:0,0",
            automation.InputActions);
        Assert.Contains(
            status,
            message => message.Contains(
                "canonical client edge",
                StringComparison.Ordinal));
        Assert.Single(sent);
        Assert.Equal(2, sent[0].UnitKey);
    }

    [Fact]
    public async Task Playback_SkipsLaterExpeditionDuplicateAndContinues()
    {
        PlacementModel model = ExpeditionModel(
            Step(1, 320, 280),
            Step(
                1,
                360,
                280,
                PlacementPhase.AfterStart),
            Step(
                2,
                400,
                280,
                PlacementPhase.AfterStart));
        PlacementServiceTests.FakeAutomation automation =
            new();
        List<string> status = [];
        List<PlacementStep> sent = [];

        await PlayAsync(
            automation,
            model,
            status,
            sent);

        Assert.Equal(
            1,
            automation.InputActions.Count(
                action => action == "key:1"));
        Assert.DoesNotContain(
            "click-retain:360,280",
            automation.InputActions);
        Assert.Equal(
            4,
            automation.InputActions.Count(
                action =>
                    action == "click-retain:400,280"));
        Assert.Equal(
            [1, 2],
            sent.Select(step => step.UnitKey));
        Assert.Contains(
            status,
            message => message.Contains(
                "earlier placement",
                StringComparison.Ordinal));
    }

    private static async Task PlayAsync(
        PlacementServiceTests.FakeAutomation automation,
        PlacementModel model,
        List<string> status,
        List<PlacementStep> sent)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementService service = new(
                automation,
                new PlacementServiceTests
                    .FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                matchStartPlayback:
                    new PlacementMatchStartPlaybackStub());
            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                stepSent:
                    (_, _, step) => sent.Add(step),
                status: status.Add);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementModel ExpeditionModel(
        params PlacementStep[] steps) =>
        new()
        {
            Id = "expedition-safety",
            Name = "Expedition safety",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 1,
                ActNumber = 0,
            },
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step(
        int unitKey,
        int x,
        int y,
        PlacementPhase phase =
            PlacementPhase.BeforeStart) =>
        new()
        {
            UnitKey = unitKey,
            X = x,
            Y = y,
            Phase = phase,
            DelayAfterMilliseconds = 0,
        };
}

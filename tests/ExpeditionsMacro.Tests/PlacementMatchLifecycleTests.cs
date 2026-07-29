using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementMatchLifecycleTests
{
    [Fact]
    public async Task FullPlayback_RunsActionsAroundVerifiedStartBoundary()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            List<PlacementStep> sent = [];
            List<string> status = [];
            PlacementStep before = Step(1, 300);
            PlacementStep delay = new()
            {
                Kind = MatchStepKind.Delay,
                UnitKey = 1,
                X = 0,
                Y = 0,
                DelayAfterMilliseconds = 0,
                DelayDurationMilliseconds = 1,
            };
            PlacementStep after = Step(2, 360);
            PlacementModel model = Model(
                "full-timeline",
                [
                    before,
                    PlacementTimelinePolicy
                        .CreateStartGameStep(),
                    delay,
                    after,
                ]);
            PlacementService service = Service(
                root,
                automation,
                () =>
                    automation.InputActions.Add(
                        "start-game"));

            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                cancelPlacementKey: 'Z',
                stepSent: (_, _, step) =>
                    sent.Add(step),
                status: status.Add);

            int beforePlacement =
                automation.InputActions.IndexOf(
                    "click-retain:300,280");
            int start =
                automation.InputActions.IndexOf(
                    "start-game");
            int afterPlacement =
                automation.InputActions.IndexOf(
                    "click-retain:360,280");
            Assert.True(beforePlacement < start);
            Assert.True(start < afterPlacement);
            Assert.Equal(
                [
                    before,
                    delay with
                    {
                        Phase =
                            PlacementPhase.AfterStart,
                    },
                    after with
                    {
                        Phase =
                            PlacementPhase.AfterStart,
                    },
                ],
                sent);
            Assert.Contains(
                status,
                message => message.Contains(
                    "waiting 1 ms",
                    StringComparison.Ordinal));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task FullPlayback_RequiresStartActionOwner()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = new(
                automation,
                new PlacementServiceTests
                    .FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)));

            InvalidOperationException error =
                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => service.PlayAsync(
                        Model(
                            "missing-start-owner",
                            [
                                Step(1, 300),
                                PlacementTimelinePolicy
                                    .CreateStartGameStep(),
                            ]),
                        useDefaultInterval: true,
                        defaultIntervalMilliseconds: 0));

            Assert.Contains(
                "verified Start Game",
                error.Message,
                StringComparison.Ordinal);
            Assert.Empty(automation.InputActions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task MatchState_PersistsAcrossStartBoundary()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementStep placement =
                Step(
                    3,
                    320,
                    UnitTargetingPriority.Strongest);
            PlacementStep reconfigure =
                ReconfigureToFirst(placement);
            PlacementModel model = Model(
                "same-match-state",
                [
                    placement,
                    PlacementTimelinePolicy
                        .CreateStartGameStep(),
                    reconfigure,
                ]);
            PlacementService service = Service(
                root,
                automation);
            service.BeginMatch();

            await PlaySubsetAsync(
                service,
                automation,
                model,
                [placement]);
            await PlaySubsetAsync(
                service,
                automation,
                model,
                [reconfigure]);

            Assert.Equal(
                9,
                automation.InputActions.Count(
                    action => action == "letter:T"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task BeginMatch_ClearsPriorUnitConfigurationState()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementStep placement =
                Step(
                    3,
                    320,
                    UnitTargetingPriority.Strongest);
            PlacementStep reconfigure =
                ReconfigureToFirst(placement);
            PlacementModel model = Model(
                "new-match-state",
                [
                    placement,
                    PlacementTimelinePolicy
                        .CreateStartGameStep(),
                    reconfigure,
                ]);
            PlacementService service = Service(
                root,
                automation);
            service.BeginMatch();

            await PlaySubsetAsync(
                service,
                automation,
                model,
                [placement]);
            service.BeginMatch();
            await PlaySubsetAsync(
                service,
                automation,
                model,
                [reconfigure]);

            Assert.Equal(
                3,
                automation.InputActions.Count(
                    action => action == "letter:T"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    private static PlacementStep Step(
        int unit,
        int x,
        UnitTargetingPriority targeting =
            UnitTargetingPriority.First) =>
        new()
        {
            Kind = MatchStepKind.Placement,
            PlacementId =
                $"unit-{unit}-{x}",
            UnitKey = unit,
            X = x,
            Y = 280,
            DelayAfterMilliseconds = 0,
            TargetingPriority = targeting,
        };

    private static PlacementStep ReconfigureToFirst(
        PlacementStep placement) =>
        new()
        {
            Kind = MatchStepKind.ReconfigureUnit,
            UnitKey = placement.UnitKey,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.First,
        };

    private static PlacementModel Model(
        string id,
        IReadOnlyList<PlacementStep> steps) =>
        new()
        {
            Id = id,
            Name = id,
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementService Service(
        string root,
        PlacementServiceTests.FakeAutomation
            automation,
        Action? matchStarted = null) =>
        new(
            automation,
            new PlacementServiceTests
                .FakeCaptureService(automation),
            new PlacementModelRepository(
                new AppPaths(root)),
            targetingKey: () => 'T',
            autoUpgradeKey: () => 'Y',
            matchStartPlayback:
                new PlacementMatchStartPlaybackStub(
                    matchStarted));

    private static Task PlaySubsetAsync(
        PlacementService service,
        PlacementServiceTests.FakeAutomation
            automation,
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps) =>
        service.PlayStepsAsync(
            automation.FindWindow() ??
                throw new InvalidOperationException(),
            model,
            steps,
            useDefaultInterval: true,
            defaultIntervalMilliseconds: 0,
            keyHoldMilliseconds: 0,
            afterKeyMilliseconds: 0,
            cancelPlacementKey: 'Z',
            stepSent: null,
            status: null,
            cancellationToken: default);
}

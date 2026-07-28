using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementServiceKeyValidationTests
{
    [Fact]
    public async Task Playback_EmptyStepSubsetDoesNotRequirePlacementKeys()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = Service(
                root,
                automation,
                targetingKey: default,
                autoUpgradeKey: default);
            PlacementModel model = Model(
                UnitTargetingPriority.Strongest,
                UnitAutoUpgradePriority.Priority6);
            RobloxWindow window =
                automation.FindWindow() ??
                throw new InvalidOperationException();

            await service.PlayStepsAsync(
                window,
                model,
                steps: [],
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                cancelPlacementKey: default,
                stepSent: null,
                status: null,
                cancellationToken: default);

            Assert.Empty(automation.InputActions);
            Assert.Null(automation.ResizeRequest);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_FirstTargetingAndOffAutoUpgradeDoNotRequireActionKeys()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = Service(
                root,
                automation,
                targetingKey: default,
                autoUpgradeKey: default);

            await service.PlayAsync(
                Model(
                    UnitTargetingPriority.First,
                    UnitAutoUpgradePriority.Off),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.DoesNotContain(
                automation.InputActions,
                action =>
                    action.StartsWith(
                        "letter:T",
                        StringComparison.Ordinal) ||
                    action.StartsWith(
                        "letter:Y",
                        StringComparison.Ordinal));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(
        UnitTargetingPriority.Strongest,
        UnitAutoUpgradePriority.Off,
        "Change Unit Targeting")]
    [InlineData(
        UnitTargetingPriority.First,
        UnitAutoUpgradePriority.Priority1,
        "Auto Upgrade Unit")]
    public async Task Playback_RequiredActionKeyMustBeConfigured(
        UnitTargetingPriority targeting,
        UnitAutoUpgradePriority autoUpgrade,
        string expectedControl)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = Service(
                root,
                automation,
                targetingKey: default,
                autoUpgradeKey: default);

            InvalidDataException error =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () => service.PlayAsync(
                        Model(
                            targeting,
                            autoUpgrade),
                        useDefaultInterval: true,
                        defaultIntervalMilliseconds: 0,
                        keyHoldMilliseconds: 0,
                        afterKeyMilliseconds: 0));

            Assert.Contains(
                expectedControl,
                error.Message,
                StringComparison.Ordinal);
            Assert.Empty(automation.InputActions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_UnsetCancelPlacementKeyDoesNotBlockQuickPlacement()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = Service(
                root,
                automation,
                targetingKey: default,
                autoUpgradeKey: default);

            await service.PlayAsync(
                Model(
                    UnitTargetingPriority.First,
                    UnitAutoUpgradePriority.Off),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                cancelPlacementKey: default);

            Assert.Contains(
                "key:1",
                automation.InputActions);
            Assert.DoesNotContain(
                automation.InputActions,
                action => action.StartsWith(
                    "letter:",
                    StringComparison.Ordinal));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_UnsetQuickPlacementKeyUsesDashboardGuidance()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            PlacementService service = new(
                automation,
                new PlacementServiceTests
                    .FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                targetingKey: () => 'T',
                autoUpgradeKey: () => 'Y',
                quickPlacementKey: () => 0);

            InvalidDataException error =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () => service.PlayAsync(
                        Model(
                            UnitTargetingPriority
                                .First,
                            UnitAutoUpgradePriority
                                .Off),
                        useDefaultInterval: true,
                        defaultIntervalMilliseconds: 0,
                        keyHoldMilliseconds: 0,
                        afterKeyMilliseconds: 0));

            Assert.Contains(
                "Quick Placement",
                error.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Controls on the Dashboard",
                error.Message,
                StringComparison.Ordinal);
            Assert.Empty(automation.InputActions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementService Service(
        string root,
        PlacementServiceTests.FakeAutomation
            automation,
        char targetingKey,
        char autoUpgradeKey) =>
        new(
            automation,
            new PlacementServiceTests
                .FakeCaptureService(automation),
            new PlacementModelRepository(
                new AppPaths(root)),
            () => targetingKey,
            () => autoUpgradeKey);

    private static PlacementModel Model(
        UnitTargetingPriority targeting,
        UnitAutoUpgradePriority autoUpgrade) =>
        new()
        {
            Id = "key-validation",
            Name = "Key validation",
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
                    TargetingPriority = targeting,
                    AutoUpgradePriority = autoUpgrade,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };
}

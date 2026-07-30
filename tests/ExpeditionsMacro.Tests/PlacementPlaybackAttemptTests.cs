using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed partial class PlacementPlaybackAttemptTests
{
    [Fact]
    public async Task InitialBatch_ReusesConsecutiveUnitAndVerifiesOnlyAfterRelease()
    {
        AttemptAutomation automation = new();
        List<PlacementStep> sent = [];

        await PlayStepsAsync(
            automation,
            [
                Step(1, 300),
                Step(1, 340),
                Step(2, 380),
            ],
            stepSent: (_, _, step) =>
                sent.Add(step));

        Assert.Equal(
            [
                "letter:Z",
                $"held:{KeyboardKey.LeftShift}:down",
                "unit:1",
                "burst:300,280:3:50",
                "burst:340,280:3:50",
                "unit:2",
                "burst:380,280:3:50",
                $"held:{KeyboardKey.LeftShift}:up",
                "letter:Z",
            ],
            automation.Actions.Take(9));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action => action == "unit:1"));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action => action == "unit:2"));
        int batchFinished =
            automation.Actions.IndexOf(
                "letter:Z",
                1);
        int firstVerification =
            automation.Actions.IndexOf(
                "verify:300,280");
        Assert.True(
            batchFinished < firstVerification);
        Assert.Equal(3, sent.Count);
        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith(
                "move:",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task DefaultSingleAttempt_SkipsOnlyUnverifiedRow()
    {
        AttemptAutomation automation = new();
        automation.NeverVisibleCoordinates.Add(
            (320, 280));
        List<string> status = [];
        List<PlacementStep> sent = [];

        await PlayStepsAsync(
            automation,
            [
                Step(
                    6,
                    320,
                    UnitTargetingPriority.Strongest,
                    UnitAutoUpgradePriority.Priority2),
                Step(2, 360),
            ],
            status.Add,
            (_, _, step) => sent.Add(step));

        Assert.Contains(
            status,
            message => message.Contains(
                "after 1 placement attempt(s)",
                StringComparison.Ordinal));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action =>
                    action ==
                    "burst:320,280:3:50"));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action =>
                    action ==
                    "burst:360,280:3:50"));
        Assert.DoesNotContain(
            "letter:T",
            automation.Actions);
        Assert.DoesNotContain(
            "letter:Y",
            automation.Actions);
        Assert.Single(sent);
        Assert.Equal(2, sent[0].UnitKey);
    }

    [Fact]
    public async Task ConfiguredRetries_ReplacesOnlyFailedRow()
    {
        AttemptAutomation automation = new();
        automation.SuccessfulVerificationAttempt[
            (320, 280)] = 2;

        await PlayStepsAsync(
            automation,
            [Step(4, 320)],
            placementAttempts: 3);

        Assert.Equal(
            2,
            automation.Actions.Count(
                action =>
                    action ==
                    "burst:320,280:3:50"));
        Assert.Equal(
            2,
            automation.Actions.Count(
                action => action == "unit:4"));
        Assert.Equal(
            4,
            automation.Actions.Count(
                action => action == "letter:Z"));
        Assert.Equal(
            2,
            automation.Actions.Count(
                action =>
                    action == "verify:320,280"));
    }

    [Fact]
    public async Task CancellationDuringBurst_ReleasesMouseAndQuickPlacement()
    {
        using CancellationTokenSource cancellation =
            new();
        AttemptAutomation automation = new()
        {
            CancelOnBurst = 1,
            PlacementCancellation = cancellation,
        };

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () => PlayStepsAsync(
                automation,
                [Step(2, 320)],
                cancellationToken:
                    cancellation.Token));

        Assert.Equal(
            [
                "letter:Z",
                $"held:{KeyboardKey.LeftShift}:down",
                "unit:2",
                "burst:320,280:3:50",
                "mouse-down",
                "mouse-up",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            automation.Actions);
    }

    [Fact]
    public async Task ProofAndConfiguration_RunAfterQuickPlacementAndCancel()
    {
        AttemptAutomation automation = new();

        await PlayStepsAsync(
            automation,
            [
                Step(
                    5,
                    320,
                    UnitTargetingPriority.Strongest,
                    UnitAutoUpgradePriority.Priority2),
            ]);

        int heldUp = automation.Actions.IndexOf(
            $"held:{KeyboardKey.LeftShift}:up");
        int trailingCancel =
            automation.Actions.IndexOf(
                "letter:Z",
                1);
        int verification =
            automation.Actions.IndexOf(
                "verify:320,280");
        int targeting =
            automation.Actions.IndexOf(
                "letter:T");
        int autoUpgrade =
            automation.Actions.IndexOf(
                "letter:Y");
        Assert.True(heldUp < trailingCancel);
        Assert.True(trailingCancel < verification);
        Assert.True(verification < targeting);
        Assert.True(targeting < autoUpgrade);
        Assert.Equal(
            2,
            automation.Actions.Count(
                action => action == "letter:Y"));
    }

    [Fact]
    public async Task DelayStep_WaitsWithoutRequiringPlacementKeys()
    {
        AttemptAutomation automation = new();
        List<PlacementStep> sent = [];
        PlacementStep delay = new()
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 1,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            DelayDurationMilliseconds = 1,
        };

        await PlayStepsAsync(
            automation,
            [delay],
            stepSent: (_, _, step) =>
                sent.Add(step),
            cancelPlacementKey: default);

        Assert.Empty(automation.Actions);
        Assert.Equal(delay, Assert.Single(sent));
    }

    [Fact]
    public async Task AdvancedMode_CanSkipProofAndOverrideBurstDuration()
    {
        AttemptAutomation automation = new();
        automation.NeverVisibleCoordinates.Add(
            (360, 280));
        PlacementAdvancedSettings advanced = new()
        {
            Enabled = true,
            UnitSelectionDelayMilliseconds = 0,
            PlacementBurstDurationMilliseconds = 17,
            VerifySelectedUnitPanelBeforeActions = false,
        };

        await PlayStepsAsync(
            automation,
            [
                Step(
                    4,
                    360,
                    UnitTargetingPriority.Last),
            ],
            advancedSettings: advanced);

        Assert.Contains(
            "burst:360,280:3:17",
            automation.Actions);
        Assert.Contains(
            "letter:T",
            automation.Actions);
        Assert.True(
            automation.Actions.IndexOf("letter:T") <
            automation.Actions.IndexOf("capture"));
    }

    [Fact]
    public async Task AdvancedMode_NoProofSkipsDefaultPlacementSelection()
    {
        AttemptAutomation automation = new();
        List<PlacementStep> sent = [];
        PlacementAdvancedSettings advanced = new()
        {
            Enabled = true,
            UnitSelectionDelayMilliseconds = 0,
            VerifySelectedUnitPanelBeforeActions = false,
        };

        await PlayStepsAsync(
            automation,
            [Step(1, 360)],
            stepSent: (_, _, step) =>
                sent.Add(step),
            advancedSettings: advanced);

        Assert.Contains(
            "burst:360,280:3:50",
            automation.Actions);
        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith(
                "verify:",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            "capture",
            automation.Actions);
        Assert.Single(sent);
    }

    private static PlacementStep Step(
        int unit,
        int x,
        UnitTargetingPriority targeting =
            UnitTargetingPriority.First,
        UnitAutoUpgradePriority autoUpgrade =
            UnitAutoUpgradePriority.Off) =>
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
            AutoUpgradePriority = autoUpgrade,
        };

    private static PlacementStep
        ReconfigureAutoUpgrade(
            PlacementStep placement,
            MatchAutoUpgradeAction action) =>
        placement with
        {
            Kind = MatchStepKind.ReconfigureUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
            AutoUpgradeAction = action,
        };

    private static int[]
        AutoUpgradeTapCountsAfterEachSelection(
            IReadOnlyList<string> actions)
    {
        List<int> counts = [];
        int currentCount = 0;
        bool selectionStarted = false;
        foreach (string action in actions)
        {
            if (action.StartsWith(
                    "verify:",
                    StringComparison.Ordinal))
            {
                if (selectionStarted)
                {
                    counts.Add(currentCount);
                }
                selectionStarted = true;
                currentCount = 0;
            }
            else if (selectionStarted &&
                     action == "letter:Y")
            {
                currentCount++;
            }
        }
        if (selectionStarted)
        {
            counts.Add(currentCount);
        }
        return [.. counts];
    }

    private static async Task PlayStepsAsync(
        AttemptAutomation automation,
        IReadOnlyList<PlacementStep> steps,
        Action<string>? status = null,
        Action<int, int, PlacementStep>? stepSent = null,
        int placementAttempts = 1,
        PlacementAdvancedSettings? advancedSettings =
            null,
        char cancelPlacementKey = 'Z',
        char? sellKey = 'X',
        CancellationToken cancellationToken = default)
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementService service = new(
                automation,
                new UnusedCaptureService(),
                new PlacementModelRepository(
                    new AppPaths(root)),
                sellKey: sellKey is null
                    ? null
                    : () => sellKey.Value);
            PlacementModel model = new()
            {
                Id = "attempt-order",
                Name = "Attempt order",
                ClientWidth = 808,
                ClientHeight = 611,
                CameraPreparationMode =
                    CameraPreparationMode.CameraModel,
                PlacementAttempts =
                    placementAttempts,
                AdvancedSettings =
                    advancedSettings ?? new(),
                Steps = steps,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            model.Validate();
            await service.PlayStepsAsync(
                automation.Window,
                model,
                steps,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                cancelPlacementKey,
                stepSent,
                status,
                cancellationToken);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    private sealed class UnusedCaptureService :
        IPlacementCaptureService
    {
        public Task<(
            int ClientWidth,
            int ClientHeight,
            IReadOnlyList<PlacementCapture> Captures)>
            RecordAsync(
            RobloxWindow window,
            Action<PlacementCapture>? captured,
            Action<string>? status,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AttemptAutomation :
        IRobloxAutomation
    {
        private readonly ImageFrame _visible =
            Load("SelectedUnitPanel_01.png");
        private readonly ImageFrame _affordableUpgrade =
            Load("UpgradeUnitAffordable_01.png");
        private readonly ImageFrame _hidden =
            Load(
                "SelectedUnitPanelHoverNegative_01.png");
        private (int X, int Y)? _verification;
        private bool _panelDismissed;
        private int _burstCount;
        private readonly Dictionary<(int X, int Y), int>
            _verificationCounts = [];

        public RobloxWindow Window { get; } =
            new((nint)42, "Roblox");

        public List<string> Actions { get; } = [];

        public HashSet<(int X, int Y)>
            NeverVisibleCoordinates
        { get; } = [];

        public Dictionary<(int X, int Y), int>
            SuccessfulVerificationAttempt
        { get; } = [];

        public int CancelOnBurst { get; init; }

        public bool UseAffordableUpgradeFrame
        {
            get;
            init;
        }

        public CancellationTokenSource?
            PlacementCancellation
        { get; init; }

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Window;

        public RobloxWindow? ForegroundWindow() =>
            Window;

        public ClientBounds GetClientBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public bool Focus(RobloxWindow window) =>
            true;

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(
            ScreenRegion region) =>
            throw new NotSupportedException();

        public ImageFrame CaptureClient(
            RobloxWindow window)
        {
            Actions.Add("capture");
            if (_panelDismissed ||
                _verification is not { } coordinate ||
                NeverVisibleCoordinates.Contains(
                    coordinate))
            {
                return _hidden;
            }
            int required =
                SuccessfulVerificationAttempt
                    .GetValueOrDefault(
                        coordinate,
                        1);
            return _verificationCounts[
                    coordinate] >= required
                ? UseAffordableUpgradeFrame
                    ? _affordableUpgrade
                    : _visible
                : _hidden;
        }

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            Actions.Add("park");
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            Actions.Add($"idle:{x},{y}");
            _panelDismissed = true;
            return Task.CompletedTask;
        }

        public Task ClickClientRetainingCursorAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            Actions.Add($"verify:{x},{y}");
            _verification = (x, y);
            _panelDismissed = false;
            _verificationCounts[(x, y)] =
                _verificationCounts
                    .GetValueOrDefault((x, y)) +
                1;
            return Task.CompletedTask;
        }

        public Task ClickClientBurstRetainingCursorAsync(
            RobloxWindow window,
            int x,
            int y,
            int clickCount,
            int durationMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            _burstCount++;
            Actions.Add(
                $"burst:{x},{y}:{clickCount}:{durationMilliseconds}");
            if (_burstCount == CancelOnBurst)
            {
                Actions.Add("mouse-down");
                try
                {
                    PlacementCancellation?.Cancel();
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }
                finally
                {
                    Actions.Add("mouse-up");
                }
            }
            return Task.CompletedTask;
        }

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ScrollClientAsync(
            RobloxWindow window,
            int notches,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DragCameraAsync(
            RobloxWindow window,
            int deltaX,
            int deltaY,
            int chunkPixels,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ZoomOutFullyAsync(
            RobloxWindow window,
            int ticks,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapShiftLockKeyAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            Actions.Add(
                $"letter:{char.ToUpperInvariant(key)}");
            if (char.ToUpperInvariant(key) == 'X')
            {
                _panelDismissed = true;
            }
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            Actions.Add($"unit:{unitKey}");
            return Task.CompletedTask;
        }

        public async Task<TResult>
            RunWithKeyHeldAsync<TResult>(
            RobloxWindow window,
            int virtualKey,
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken)
        {
            Actions.Add(
                $"held:{virtualKey}:down");
            try
            {
                return await action(
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                Actions.Add(
                    $"held:{virtualKey}:up");
            }
        }

        private static ImageFrame Load(
            string fileName) =>
            ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    fileName));
    }
}

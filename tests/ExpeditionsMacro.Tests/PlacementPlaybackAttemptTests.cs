using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementPlaybackAttemptTests
{
    [Fact]
    public async Task FirstAttempt_HoldsQuickPlacementTapsUnitClicksAndParksBeforeProof()
    {
        AttemptAutomation automation = new()
        {
            SuccessfulSelectionAttempt = 1,
        };

        await PlayOneStepAsync(automation, unitKey: 4);

        int firstCapture = automation.Actions.IndexOf("capture");
        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                "unit:4",
                "click-retain:320,280",
                "park",
                "capture",
            ],
            automation.Actions.Take(firstCapture + 1));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action => action == "click-retain:320,280"));
        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith(
                "move:",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingFirstProof_RepeatsUnitTapAndDirectClickWithoutMouseApproach()
    {
        AttemptAutomation automation = new()
        {
            SuccessfulSelectionAttempt = 2,
        };

        await PlayOneStepAsync(automation, unitKey: 3);

        string[] attemptActions = automation.Actions
            .Where(action => action != "capture")
            .Take(8)
            .ToArray();
        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                "unit:3",
                "click-retain:320,280",
                "park",
                "unit:3",
                "click-retain:320,280",
                "park",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            attemptActions);
        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith(
                "move:",
                StringComparison.Ordinal));
        AssertSelectionParksPrecedeCapture(
            automation,
            expectedSelectionAttempts: 2);
    }

    [Fact]
    public async Task MissingProof_SkipsAfterEightTapClickAttemptsAndPlacesNextStep()
    {
        AttemptAutomation automation = new()
        {
            SuccessfulSelectionAttempt = 9,
        };
        List<string> status = [];
        List<PlacementStep> sent = [];

        await PlayStepsAsync(
            automation,
            [
                new PlacementStep
                {
                    UnitKey = 6,
                    X = 320,
                    Y = 280,
                    DelayAfterMilliseconds = 30_000,
                    TargetingPriority =
                        UnitTargetingPriority.Strongest,
                    AutoUpgradePriority =
                        UnitAutoUpgradePriority.Priority2,
                },
                new PlacementStep
                {
                    UnitKey = 2,
                    X = 360,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                },
            ],
            status.Add,
            (_, _, step) => sent.Add(step),
            useDefaultInterval: false);

        Assert.Contains(
            status,
            message => message.Contains(
                "skipped Unit 6",
                StringComparison.Ordinal));
        Assert.Equal(
            8,
            automation.Actions.Count(
                action => action == "click-retain:320,280"));
        Assert.Equal(
            1,
            automation.Actions.Count(
                action => action == "click-retain:360,280"));
        Assert.Equal(
            8,
            automation.Actions.Count(
                action => action == "unit:6"));
        Assert.DoesNotContain(
            automation.Actions,
            action => action.StartsWith(
                "move:",
                StringComparison.Ordinal));
        Assert.DoesNotContain("letter:T", automation.Actions);
        Assert.DoesNotContain("letter:Y", automation.Actions);
        Assert.Single(sent);
        Assert.Equal(2, sent[0].UnitKey);
        AssertSelectionParksPrecedeCapture(
            automation,
            expectedSelectionAttempts: 9);
    }

    [Fact]
    public async Task CancellationDuringClick_ReleasesMouseAndQuickPlacementKey()
    {
        using CancellationTokenSource cancellation = new();
        AttemptAutomation automation = new()
        {
            CancelOnPlacementClick = 1,
            PlacementCancellation = cancellation,
            TraceAtomicClickCleanup = true,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PlayOneStepAsync(
                automation,
                unitKey: 2,
                cancellation.Token));

        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                "unit:2",
                "click-retain:320,280",
                "mouse-down",
                "mouse-up",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            automation.Actions);
        Assert.DoesNotContain("park", automation.Actions);
        Assert.DoesNotContain("capture", automation.Actions);
    }

    [Fact]
    public async Task QuickPlacementKeyReleasesBeforeTargetingAndAutoUpgrade()
    {
        AttemptAutomation automation = new()
        {
            SuccessfulSelectionAttempt = 1,
        };
        PlacementStep step = new()
        {
            UnitKey = 5,
            X = 320,
            Y = 280,
            DelayAfterMilliseconds = 0,
            TargetingPriority =
                UnitTargetingPriority.Strongest,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Priority2,
        };

        await PlayStepsAsync(
            automation,
            [step]);

        int released = automation.Actions.IndexOf(
            $"held:{KeyboardKey.LeftShift}:up");
        Assert.True(released >= 0);
        Assert.True(
            released <
            automation.Actions.IndexOf("letter:T"));
        Assert.True(
            released <
            automation.Actions.IndexOf("letter:Y"));
        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action == "letter:Y"));
    }

    private static void AssertSelectionParksPrecedeCapture(
        AttemptAutomation automation,
        int expectedSelectionAttempts)
    {
        int[] parkIndexes = automation.Actions
            .Select((action, index) => (action, index))
            .Where(item => item.action == "park")
            .Take(expectedSelectionAttempts)
            .Select(item => item.index)
            .ToArray();
        Assert.Equal(
            expectedSelectionAttempts,
            parkIndexes.Length);
        Assert.All(
            parkIndexes,
            index => Assert.Equal(
                "capture",
                automation.Actions[index + 1]));
    }

    private static async Task PlayOneStepAsync(
        AttemptAutomation automation,
        int unitKey,
        CancellationToken cancellationToken = default) =>
        await PlayStepsAsync(
            automation,
            [
                new PlacementStep
                {
                    UnitKey = unitKey,
                    X = 320,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                },
            ],
            cancellationToken: cancellationToken);

    private static async Task PlayStepsAsync(
        AttemptAutomation automation,
        IReadOnlyList<PlacementStep> steps,
        Action<string>? status = null,
        Action<int, int, PlacementStep>? stepSent = null,
        bool useDefaultInterval = true,
        CancellationToken cancellationToken = default)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementService service = new(
                automation,
                new UnusedCaptureService(),
                new PlacementModelRepository(
                    new AppPaths(root)));
            PlacementModel model = new()
            {
                Id = "attempt-order",
                Name = "Attempt order",
                ClientWidth = 808,
                ClientHeight = 611,
                CameraPreparationMode =
                        CameraPreparationMode.CameraModel,
                Steps = steps,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            model.Validate();
            RobloxWindow window =
                automation.FindWindow()!.Value;
            await service.PlayStepsAsync(
                window,
                model,
                steps,
                useDefaultInterval: useDefaultInterval,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0,
                cancelPlacementKey:
                    AppSettings
                        .DefaultCancelPlacementKeyChar,
                stepSent: stepSent,
                status: status,
                cancellationToken: cancellationToken);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private sealed class UnusedCaptureService :
        IPlacementCaptureService
    {
        public Task<(
            int ClientWidth,
            int ClientHeight,
            IReadOnlyList<PlacementCapture> Captures)> RecordAsync(
            RobloxWindow window,
            Action<PlacementCapture>? captured,
            Action<string>? status,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AttemptAutomation :
        IRobloxAutomation
    {
        private readonly RobloxWindow _window =
            new((nint)42, "Roblox");
        private readonly ImageFrame _visible =
            Load("SelectedUnitPanel_01.png");
        private readonly ImageFrame _hidden =
            Load("SelectedUnitPanelHoverNegative_01.png");
        private bool _cursorParked;
        private bool _selectionEvidenceSeen;
        private bool _dismissed;
        private int _selectionAttempt;
        private int _placementClicks;

        public List<string> Actions { get; } = [];

        public int SuccessfulSelectionAttempt { get; init; } = 1;

        public int CancelOnPlacementClick { get; init; }

        public CancellationTokenSource? PlacementCancellation
        {
            get;
            init;
        }

        public bool TraceAtomicClickCleanup { get; init; }

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            _window;

        public RobloxWindow? ForegroundWindow() =>
            _window;

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
            if (_dismissed)
            {
                return _hidden;
            }

            bool visible =
                _cursorParked &&
                _selectionAttempt >=
                    SuccessfulSelectionAttempt;
            _selectionEvidenceSeen |= visible;
            return visible ? _visible : _hidden;
        }

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MoveCursorBetweenClientPointsAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            int durationMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(
                $"move:{startX},{startY}->{endX},{endY}:{durationMilliseconds}");
            _cursorParked = false;
            return Task.CompletedTask;
        }

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add("park");
            _cursorParked = true;
            if (!_selectionEvidenceSeen)
            {
                _selectionAttempt++;
            }
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add($"idle-click:{x},{y}");
            _dismissed = true;
            _cursorParked = true;
            return Task.CompletedTask;
        }

        public Task ClickClientRetainingCursorAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _placementClicks++;
            Actions.Add($"click-retain:{x},{y}");
            _cursorParked = false;
            if (TraceAtomicClickCleanup)
            {
                Actions.Add("mouse-down");
            }
            try
            {
                if (_placementClicks ==
                    CancelOnPlacementClick)
                {
                    PlacementCancellation?.Cancel();
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                if (TraceAtomicClickCleanup)
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
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(
                $"letter:{char.ToUpperInvariant(key)}");
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                return await action(cancellationToken)
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

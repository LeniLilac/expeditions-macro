using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementQuickSelectionTests
{
    [Fact]
    public async Task QuickPlacementProof_HoldsKeyUntilBoundaryEvidenceIsStable()
    {
        QuickPlacementProofAutomation automation = new();
        Queue<bool> observations =
            new([false, false, false, true, true]);
        QuickPlacementSelectionProof proof = new(
            automation,
            _ => observations.Dequeue());
        bool selected =
            await proof.HasStableSelectionAsync(
                automation.Window,
                KeyboardKey.LeftShift,
                CancellationToken.None);

        Assert.True(selected);
        Assert.Equal(5, automation.CaptureCount);
        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            automation.InputActions);
    }

    [Fact]
    public async Task QuickPlacementProof_AcceptsRefreshedHandleFromSameProcess()
    {
        QuickPlacementProofAutomation automation = new()
        {
            Foreground = new RobloxWindow(
                (nint)84,
                "Roblox",
                314,
                "RobloxPlayerBeta"),
        };
        QuickPlacementSelectionProof proof = new(
            automation,
            _ => true);

        bool selected =
            await proof.HasStableSelectionAsync(
                automation.Window,
                KeyboardKey.LeftShift,
                CancellationToken.None);

        Assert.True(selected);
        Assert.Equal(2, automation.CaptureCount);
        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            automation.InputActions);
    }

    [Fact]
    public async Task QuickPlacementProof_RejectsForeignForegroundAndReleasesKey()
    {
        QuickPlacementProofAutomation automation = new()
        {
            Foreground = new RobloxWindow(
                (nint)84,
                "Other",
                271,
                "Other"),
        };
        QuickPlacementSelectionProof proof = new(
            automation,
            _ => true);

        await Assert.ThrowsAsync<
            RobloxSessionUnavailableException>(
            () => proof.HasStableSelectionAsync(
                automation.Window,
                KeyboardKey.LeftShift,
                CancellationToken.None));

        Assert.Equal(0, automation.CaptureCount);
        Assert.Equal(
            [
                $"held:{KeyboardKey.LeftShift}:down",
                $"held:{KeyboardKey.LeftShift}:up",
            ],
            automation.InputActions);
    }

    [Fact]
    public async Task Playback_ContinuesWhenFirstQuickPlacementProofSucceeds()
    {
        await WithServiceAsync(
            [true],
            async (
                service,
                automation,
                proof,
                root) =>
            {
                await service.PlayAsync(
                    Model(
                        Step(1, 300, 300)),
                    useDefaultInterval: true,
                    defaultIntervalMilliseconds: 0,
                    keyHoldMilliseconds: 0,
                    afterKeyMilliseconds: 0);

                Assert.Equal(1, proof.CallCount);
                Assert.Equal(
                    4,
                    Count(
                        automation,
                        "key:1"));
                Assert.Equal(
                    2,
                    Count(
                        automation,
                        "click-retain:300,300"));
            });
    }

    [Fact]
    public async Task Playback_ReselectsOnceBeforeCoordinateInput()
    {
        await WithServiceAsync(
            [false, true],
            async (
                service,
                automation,
                proof,
                root) =>
            {
                await service.PlayAsync(
                    Model(
                        Step(2, 300, 300)),
                    useDefaultInterval: true,
                    defaultIntervalMilliseconds: 0,
                    keyHoldMilliseconds: 0,
                    afterKeyMilliseconds: 0);

                Assert.Equal(2, proof.CallCount);
                Assert.Equal(
                    5,
                    Count(
                        automation,
                        "key:2"));
                Assert.Equal(
                    2,
                    Count(
                        automation,
                        "click-retain:300,300"));
            });
    }

    [Fact]
    public async Task Playback_SkipsOnlyUnselectedRowAndContinues()
    {
        await WithServiceAsync(
            [false, false, true],
            async (
                service,
                automation,
                proof,
                root) =>
            {
                List<int> sent = [];
                PlacementStep skipped =
                    Step(1, 300, 300) with
                    {
                        TargetingPriority =
                            UnitTargetingPriority
                                .Strongest,
                        AutoUpgradePriority =
                            UnitAutoUpgradePriority
                                .Priority1,
                    };

                await service.PlayAsync(
                    Model(
                        skipped,
                        Step(2, 340, 300)),
                    useDefaultInterval: true,
                    defaultIntervalMilliseconds: 0,
                    keyHoldMilliseconds: 0,
                    afterKeyMilliseconds: 0,
                    stepSent: (
                        index,
                        count,
                        step) => sent.Add(index));

                Assert.Equal(3, proof.CallCount);
                Assert.Equal(
                    5,
                    Count(
                        automation,
                        "key:1"));
                Assert.Equal(
                    4,
                    Count(
                        automation,
                        "key:2"));
                Assert.DoesNotContain(
                    "click-retain:300,300",
                    automation.InputActions);
                Assert.Equal(
                    2,
                    Count(
                        automation,
                        "click-retain:340,300"));
                Assert.DoesNotContain(
                    "letter:T",
                    automation.InputActions);
                Assert.DoesNotContain(
                    "letter:Y",
                    automation.InputActions);
                Assert.Equal([2], sent);
            });
    }

    [Fact]
    public async Task Playback_RejectsUnsetQuickPlacementBeforeInput()
    {
        await WithServiceAsync(
            [true],
            async (
                service,
                automation,
                proof,
                root) =>
            {
                PlacementService invalid = new(
                    automation,
                    new PlacementServiceTests
                        .FakeCaptureService(
                            automation),
                    new PlacementModelRepository(
                        new AppPaths(root)),
                    () => 'T',
                    () => 'Y',
                    () => 0,
                    proof);

                InvalidDataException error =
                    await Assert.ThrowsAsync<
                        InvalidDataException>(
                        () => invalid.PlayAsync(
                            Model(
                                Step(1, 300, 300)),
                            useDefaultInterval: true,
                            defaultIntervalMilliseconds: 0,
                            keyHoldMilliseconds: 0,
                            afterKeyMilliseconds: 0));

                Assert.Contains(
                    "Quick Placement",
                    error.Message,
                    StringComparison.Ordinal);
                Assert.Empty(
                    automation.InputActions);
            });
    }

    private static async Task WithServiceAsync(
        IReadOnlyList<bool> results,
        Func<
            PlacementService,
            PlacementServiceTests.FakeAutomation,
            FakeQuickPlacementProof,
            string,
            Task> test)
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementServiceTests.FakeAutomation
                automation = new();
            FakeQuickPlacementProof proof =
                new(results);
            PlacementService service = new(
                automation,
                new PlacementServiceTests
                    .FakeCaptureService(
                        automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T',
                () => 'Y',
                () => KeyboardKey.LeftShift,
                proof);
            await test(
                service,
                automation,
                proof,
                root);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    private static int Count(
        PlacementServiceTests.FakeAutomation automation,
        string action) =>
        automation.InputActions.Count(
            candidate => candidate == action);

    private static PlacementModel Model(
        params PlacementStep[] steps) =>
        new()
        {
            Id = "quick-placement",
            Name = "Quick placement",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step(
        int unitKey,
        int x,
        int y) =>
        new()
        {
            UnitKey = unitKey,
            X = x,
            Y = y,
            DelayAfterMilliseconds = 0,
        };

    private sealed class FakeQuickPlacementProof(
        IReadOnlyList<bool> results) :
        IQuickPlacementSelectionProof
    {
        private int _index;

        public int CallCount => _index;

        public Task<bool> HasStableSelectionAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            if (_index >= results.Count)
            {
                throw new InvalidOperationException(
                    "No Quick Placement proof result remains.");
            }
            return Task.FromResult(
                results[_index++]);
        }
    }

    private sealed class QuickPlacementProofAutomation :
        IRobloxAutomation
    {
        private readonly ImageFrame _frame = new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

        public RobloxWindow Window { get; } =
            new(
                (nint)42,
                "Roblox",
                314,
                "RobloxPlayerBeta");

        public RobloxWindow Foreground { get; set; } =
            new(
                (nint)42,
                "Roblox",
                314,
                "RobloxPlayerBeta");

        public int CaptureCount { get; private set; }

        public List<string> InputActions { get; } = [];

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Window;

        public RobloxWindow? ForegroundWindow() =>
            Foreground;

        public ClientBounds GetClientBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public bool Focus(
            RobloxWindow window) =>
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
            CaptureCount++;
            return _frame;
        }

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ScrollClientAsync(
            RobloxWindow window,
            int notches,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DragCameraAsync(
            RobloxWindow window,
            int deltaX,
            int deltaY,
            int chunkPixels,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ZoomOutFullyAsync(
            RobloxWindow window,
            int ticks,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TapShiftLockKeyAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<TResult>
            RunWithKeyHeldAsync<TResult>(
            RobloxWindow window,
            int virtualKey,
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken)
        {
            InputActions.Add(
                $"held:{virtualKey}:down");
            try
            {
                return await action(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                InputActions.Add(
                    $"held:{virtualKey}:up");
            }
        }
    }
}

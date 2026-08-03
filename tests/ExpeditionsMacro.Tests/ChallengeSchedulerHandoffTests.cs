using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeSchedulerHandoffTests
{
    [Fact]
    public async Task PersistedDailyLimit_ReturnsToSchedulerInsteadOfWaiting()
    {
        DateTimeOffset dailyLimitUntil =
            DateTimeOffset.UtcNow.AddHours(1);
        ChallengeRotationState rotation = new(
            new ChallengeRotationProgress
            {
                DailyLimitUntilUtc = dailyLimitUntil,
            });
        IDetectorPack detector =
            await LoadDetectorAsync();
        ChallengePreset preset = new()
        {
            Id = "scheduled-challenge",
            Name = "Scheduled Challenge",
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Maps = ChallengePreset.EmptyMapProfiles()
                .Select(profile => profile with
                {
                    PrestartPlacementModelId =
                        "placement",
                })
                .ToArray(),
            PollMilliseconds = 150,
            StableDetections = 1,
        };
        Dictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> models =
            Enum.GetValues<ChallengeMapId>()
                .ToDictionary(
                    map => map,
                    _ => new ChallengeMapRuntimeModels(
                        Placement: null));
        ImageFrame selector = ImageCodec.Load(
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "ChallengeList",
                "ChallengeList_01.png"));
        ImageFrame gameModes = ImageCodec.Load(
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameModeSelector",
                "GameModeSelector_01.png"));
        HandoffAutomation automation = new(
            selector,
            gameModes);
        ChallengeMacroRunner runner = new(
            automation,
            null!,
            null!,
            null!);
        ChallengeRunSummary? summary = null;
        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(2));

        await runner.RunAsync(
            preset,
            models,
            detector,
            rotation,
            webhookUrl: string.Empty,
            playMenuKey: 'P',
            summaryChanged: value => summary = value,
            cancellationToken: timeout.Token,
            maximumCompletedRuns: 1,
            returnWhenUnavailable: true);

        Assert.False(timeout.IsCancellationRequested);
        Assert.NotNull(summary);
        Assert.Equal(
            dailyLimitUntil,
            summary.WaitingUntilUtc);
        Assert.True(summary.DailyLimitReached);
        Assert.Single(automation.Clicks);
        Assert.True(automation.GameModeSelectorVisible);
    }

    private static async Task<IDetectorPack>
        LoadDetectorAsync()
    {
        DetectorPackManifest manifest =
            await JsonFileStore
                .ReadAsync<DetectorPackManifest>(
                    Path.Combine(
                        TestPaths.DetectorPack,
                        "manifest.json"))
            ?? throw new InvalidDataException(
                "Test detector manifest is missing.");
        return new CompiledDetectorPack(
            TestPaths.DetectorPack,
            manifest);
    }

    private sealed class HandoffAutomation(
        ImageFrame selector,
        ImageFrame gameModes) : IRobloxAutomation
    {
        private readonly RobloxWindow _window =
            new((nint)42, "Roblox");

        public List<(int X, int Y)> Clicks
        {
            get;
        } = [];

        public bool GameModeSelectorVisible
        {
            get;
            private set;
        }

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

        public bool Focus(
            RobloxWindow window) =>
            true;

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The canonical client must not be resized.");

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(
            ScreenRegion region) =>
            CurrentFrame();

        public ImageFrame CaptureClient(
            RobloxWindow window) =>
            CurrentFrame();

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            Clicks.Add((x, y));
            GameModeSelectorVisible = true;
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
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        private ImageFrame CurrentFrame() =>
            (GameModeSelectorVisible
                ? gameModes
                : selector).Clone();
    }
}

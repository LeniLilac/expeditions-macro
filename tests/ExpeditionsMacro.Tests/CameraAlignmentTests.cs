using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;

namespace ExpeditionsMacro.Tests;

public sealed class CameraAlignmentTests
{
    [Fact]
    public async Task Align_ResizesUsesRelativeRegionAndRestoresOriginalWindow()
    {
        ImageFrame capture = VisionScorerTests.Pattern(96, 72);
        CameraModel model = CreateModel(capture);
        FakeAutomation automation = new(capture);
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        double score = await engine.AlignAsync(model);

        Assert.True(score > 0.95, $"Alignment score was {score:P1}.");
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Equal(new WindowBounds(40, 50, 1100, 800), automation.RestoredBounds);
        Assert.All(automation.CapturedRegions, region => Assert.Equal(new ScreenRegion(315, 220, 96, 72), region));
        Assert.NotEmpty(automation.Drags);
        Assert.Equal(1, automation.MoveToCenterCount);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Align_WhenShiftLockIsAlreadyManaged_DoesNotToggleIt()
    {
        ImageFrame capture = VisionScorerTests.Pattern(96, 72);
        FakeAutomation automation = new(capture);
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        await engine.AlignAsync(CreateModel(capture), manageShiftLock: false);

        Assert.Equal(0, automation.MoveToCenterCount);
        Assert.Equal(0, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Calibrate_RestoresAutomaticShiftLockWhenCaptureFails()
    {
        ImageFrame capture = VisionScorerTests.Pattern(96, 72);
        FakeAutomation automation = new(capture)
        {
            CaptureFailure = new InvalidOperationException("Synthetic capture failure."),
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());
        CameraCalibrationSettings settings = new()
        {
            Name = "Automatic shift lock",
            CaptureCount = 2,
            CaptureDuration = TimeSpan.Zero,
            CoarseStepPixels = 16,
            FineStepPixels = 1,
            SettleMilliseconds = 25,
            MaximumSamples = 12,
        };

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CalibrateAsync(new ScreenRegion(315, 220, 96, 72), settings));

        Assert.Equal("Synthetic capture failure.", error.Message);
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Equal(new WindowBounds(40, 50, 1100, 800), automation.RestoredBounds);
        Assert.Equal(1, automation.MoveToCenterCount);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Align_WhenFastMatchIsLow_FullTurnFallbackFindsTheGoal()
    {
        ImageFrame goal = VisionScorerTests.Pattern(96, 72);
        ImageFrame wrong = Blank(96, 72);
        CameraModel model = CreateModel(goal, wrong, fullYawPixels: 24, coarseStepPixels: 6);
        FakeAutomation automation = new(wrong)
        {
            FullYawPixels = 24,
            YawPixels = 6,
            CaptureAtYaw = yaw => yaw == 0 ? goal : wrong,
        };
        List<MacroProgress> updates = [];
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        double score = await engine.AlignAsync(model, progress: new InlineProgress<MacroProgress>(updates.Add));

        Assert.True(score > 0.95, $"Fallback alignment score was {score:P1}.");
        Assert.Contains(updates, update => update.Message.Contains("Scanning one full yaw turn", StringComparison.Ordinal));
        Assert.Contains(updates, update => update.Message.Contains("Stable goal found", StringComparison.Ordinal));
        Assert.Equal(0, automation.YawPixels);
    }

    [Fact]
    public async Task Align_WhenFullTurnFallbackStaysBelowTarget_StopsWithFailure()
    {
        ImageFrame goal = VisionScorerTests.Pattern(96, 72);
        ImageFrame wrong = Blank(96, 72);
        CameraModel model = CreateModel(goal, wrong, fullYawPixels: 24, coarseStepPixels: 6);
        FakeAutomation automation = new(wrong)
        {
            FullYawPixels = 24,
            YawPixels = 6,
            CaptureAtYaw = _ => wrong,
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.AlignAsync(model));

        Assert.Contains("Unit placement was not started", error.Message, StringComparison.Ordinal);
        Assert.True(automation.Drags.Where(drag => drag.X > 0).Sum(drag => drag.X) >= 24);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    private static CameraModel CreateModel(
        ImageFrame capture,
        ImageFrame? atlasCapture = null,
        int fullYawPixels = 360,
        int coarseStepPixels = 12)
    {
        ImageFrame reference = VisionScorer.PrepareGray(capture);
        ImageFrame thumbnail = VisionScorer.MakeThumbnail(VisionScorer.PrepareGray(atlasCapture ?? capture));
        ImageFrame[] atlas = [thumbnail, thumbnail, thumbnail];
        return new CameraModel(
            new CameraModelManifest
            {
                Id = "camera-test",
                Name = "Camera test",
                Region = new ScreenRegion(15, 20, 96, 72),
                ClientWidth = 808,
                ClientHeight = 611,
                BaselineScore = 1,
                SuccessThreshold = 0.80,
                CoarseStepPixels = coarseStepPixels,
                FineStepPixels = 1,
                FullYawPixels = fullYawPixels,
                SettleMilliseconds = 0,
                AtlasSampleCount = atlas.Length,
                ScanScores = [1, 0.2, 1],
                CreatedAt = DateTimeOffset.UtcNow,
            },
            reference,
            capture,
            atlas);
    }

    private static ImageFrame Blank(int width, int height) =>
        new(width, height, PixelFormat.Rgb24, new byte[width * height * 3], takeOwnership: true);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FakeAutomation(ImageFrame capture) : IRobloxAutomation
    {
        private readonly RobloxWindow _window = new((nint)42, "Roblox");
        private ClientBounds _client = new(300, 200, 1000, 700);

        public (int Width, int Height)? ResizeRequest { get; private set; }
        public WindowBounds? RestoredBounds { get; private set; }
        public List<ScreenRegion> CapturedRegions { get; } = [];
        public List<(int X, int Y)> Drags { get; } = [];
        public int MoveToCenterCount { get; private set; }
        public int LeftControlTapCount { get; private set; }
        public Exception? CaptureFailure { get; init; }
        public Func<int, ImageFrame>? CaptureAtYaw { get; init; }
        public int FullYawPixels { get; init; } = 360;
        public int YawPixels { get; set; }

        public RobloxWindow? FindWindow(string titleFragment = "Roblox") => _window;
        public RobloxWindow? ForegroundWindow() => _window;
        public ClientBounds GetClientBounds(RobloxWindow window) => _client;
        public WindowBounds GetWindowBounds(RobloxWindow window) => new(40, 50, 1100, 800);
        public bool Focus(RobloxWindow window) => true;
        public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
        {
            ResizeRequest = (width, height);
            _client = new ClientBounds(300, 200, width, height);
            return Task.CompletedTask;
        }
        public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds) => RestoredBounds = bounds;
        public ImageFrame CaptureScreen(ScreenRegion region)
        {
            CapturedRegions.Add(region);
            if (CaptureFailure is not null) throw CaptureFailure;
            return (CaptureAtYaw?.Invoke(YawPixels) ?? capture).Clone();
        }
        public ImageFrame CaptureClient(RobloxWindow window) => throw new NotSupportedException();
        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            MoveToCenterCount++;
            return Task.CompletedTask;
        }
        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken)
        {
            Drags.Add((deltaX, deltaY));
            YawPixels = ((YawPixels + deltaX) % FullYawPixels + FullYawPixels) % FullYawPixels;
            return Task.CompletedTask;
        }
        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            LeftControlTapCount++;
            return Task.CompletedTask;
        }
        public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullCameraRepository : ICameraModelRepository
    {
        public Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CameraModelManifest>>([]);
        public Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<CameraModel?>(null);
        public Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

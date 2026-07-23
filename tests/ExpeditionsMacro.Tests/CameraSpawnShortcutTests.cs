using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class CameraSpawnShortcutTests
{
    private static readonly ScreenRegion[] Regions =
    [
        new(96, 110, 166, 108),
        new(546, 110, 166, 108),
        new(96, 250, 166, 108),
        new(321, 250, 166, 108),
    ];

    [Fact]
    public async Task Repository_RoundTripsDeviceLocalShortcut()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame fingerprint = VisionScorer.PrepareGray(VisionScorerTests.Pattern(160, 101));
            CameraSpawnShortcut expected = new()
            {
                CameraModelId = "camera-test",
                CameraModelCreatedAt = DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                ClientWidth = 808,
                ClientHeight = 611,
                FingerprintWidth = fingerprint.Width,
                FingerprintHeight = fingerprint.Height,
                FingerprintPixels = (byte[])fingerprint.Pixels.Clone(),
                SpawnAtlasIndex = 2,
                DirectDragPixels = -20,
                MousePixelsPerArrowStep = 10,
                MatchingObservations = 2,
                VerifiedUses = 1,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            CameraSpawnShortcutRepository repository = new(new AppPaths(root));

            await repository.SaveAsync(expected);
            CameraSpawnShortcut? actual = await repository.LoadAsync(expected.CameraModelId);

            Assert.NotNull(actual);
            Assert.Equal(expected with { FingerprintPixels = actual.FingerprintPixels }, actual);
            Assert.Equal(expected.FingerprintPixels, actual.FingerprintPixels);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Alignment_LearnsTwoMatchingLoadsThenUsesOneVerifiedMouseDrag()
    {
        CameraModel model = CreateModel();
        ShortcutAutomation automation = new(model) { YawStep = 2 };
        MemoryCameraModelRepository models = new();
        MemoryShortcutRepository shortcuts = new();
        CameraAlignmentEngine engine = new(automation, models, shortcuts);

        await engine.AlignAsync(model, manageShiftLock: false, useSpawnShortcut: true);
        automation.Reset(yawStep: 2);
        await engine.AlignAsync(model, manageShiftLock: false, useSpawnShortcut: true);
        CameraSpawnShortcut learned = Assert.IsType<CameraSpawnShortcut>(await shortcuts.LoadAsync(model.Manifest.Id));
        Assert.Equal(2, learned.MatchingObservations);
        Assert.Equal(-20, learned.DirectDragPixels);

        automation.Reset(yawStep: 2);
        List<Core.Runtime.MacroProgress> updates = [];
        double score = await engine.AlignAsync(
            model,
            manageShiftLock: false,
            progress: new InlineProgress<Core.Runtime.MacroProgress>(updates.Add),
            useSpawnShortcut: true);

        Assert.True(score > 0.90, $"Shortcut score was {score:P1}.");
        Assert.Empty(automation.ArrowPulses);
        Assert.Equal([(-20, 0)], automation.Drags);
        Assert.Contains(updates, update => update.Message.Contains("one cached -20-px mouse drag", StringComparison.Ordinal));
        Assert.Contains(updates, update => update.Message.Contains("with one drag", StringComparison.Ordinal));
        CameraSpawnShortcut verified = Assert.IsType<CameraSpawnShortcut>(await shortcuts.LoadAsync(model.Manifest.Id));
        Assert.Equal(1, verified.VerifiedUses);
        Assert.Equal(0, verified.ConsecutiveFailures);
    }

    [Fact]
    public async Task ManualAlignment_DoesNotTrainTheMacroSpawnShortcut()
    {
        CameraModel model = CreateModel();
        ShortcutAutomation automation = new(model) { YawStep = 2 };
        MemoryShortcutRepository shortcuts = new();
        CameraAlignmentEngine engine = new(automation, new MemoryCameraModelRepository(), shortcuts);

        await engine.AlignAsync(model, manageShiftLock: false);
        automation.Reset(yawStep: 2);
        await engine.AlignAsync(model, manageShiftLock: false);

        Assert.Null(await shortcuts.LoadAsync(model.Manifest.Id));
    }

    [Fact]
    public async Task InvalidCachedDrag_FallsBackToNormalAtlasAlignment()
    {
        CameraModel model = CreateModel();
        ShortcutAutomation automation = new(model) { YawStep = 2 };
        MemoryShortcutRepository shortcuts = new();
        CameraSpawnShortcut incorrect = new()
        {
            CameraModelId = model.Manifest.Id,
            CameraModelCreatedAt = model.Manifest.CreatedAt,
            ClientWidth = model.Manifest.ClientWidth,
            ClientHeight = model.Manifest.ClientHeight,
            FingerprintWidth = model.YawAtlas[2].Width,
            FingerprintHeight = model.YawAtlas[2].Height,
            FingerprintPixels = (byte[])model.YawAtlas[2].Pixels.Clone(),
            SpawnAtlasIndex = 2,
            DirectDragPixels = -5,
            MousePixelsPerArrowStep = 10,
            MatchingObservations = 2,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await shortcuts.SaveAsync(incorrect);
        CameraAlignmentEngine engine = new(automation, new MemoryCameraModelRepository(), shortcuts);

        double score = await engine.AlignAsync(model, manageShiftLock: false, useSpawnShortcut: true);

        Assert.True(score > 0.80, $"Fallback score was {score:P1}.");
        Assert.Equal((-5, 0), automation.Drags[0]);
        Assert.True(automation.ArrowPulses.Count > 0 || automation.Drags.Count > 1);
        CameraSpawnShortcut adjusted = Assert.IsType<CameraSpawnShortcut>(await shortcuts.LoadAsync(model.Manifest.Id));
        Assert.Equal(1, adjusted.ConsecutiveFailures);
    }

    private static CameraModel CreateModel()
    {
        ImageFrame goal = VisionScorerTests.Pattern(808, 611);
        int[] yawOffsets = [0, 10, 20, 30, -20, -10, 0];
        ImageFrame[] yawFrames = yawOffsets.Select(offset => Shift(goal, offset)).ToArray();
        int[] fineOffsets = Enumerable.Range(-12, 25).ToArray();
        ImageFrame reference = CameraRegionAnalyzer.BuildComposite(goal, Regions);
        ImageFrame[] fineAtlas = fineOffsets
            .Select(offset => VisionScorer.MakeThumbnail(CameraRegionAnalyzer.BuildComposite(Shift(goal, offset), Regions)))
            .ToArray();
        ImageFrame[] yawAtlas = yawFrames
            .Select(frame => VisionScorer.MakeThumbnail(CameraRegionAnalyzer.BuildComposite(frame, Regions)))
            .ToArray();
        return new CameraModel(
            new CameraModelManifest
            {
                Id = "shortcut-camera",
                Name = "Shortcut camera",
                Regions = Regions,
                ClientWidth = 808,
                ClientHeight = 611,
                BaselineScore = 1,
                SuccessThreshold = 0.80,
                ArrowHoldMilliseconds = 20,
                FineStepPixels = 1,
                FineSearchPixels = 12,
                FineYawOffsets = fineOffsets,
                FullYawSteps = 6,
                SettleMilliseconds = 0,
                AtlasSampleCount = yawAtlas.Length,
                ScanScores = Enumerable.Repeat(0.2, 6).Prepend(1).ToArray(),
                CreatedAt = DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
            },
            reference,
            CameraRegionAnalyzer.AnnotateGoal(goal, Regions),
            fineAtlas,
            yawAtlas);
    }

    private static ImageFrame Shift(ImageFrame source, int dx)
    {
        byte[] output = new byte[source.Pixels.Length];
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                int sourceX = ((x + dx) % source.Width + source.Width) % source.Width;
                int destination = (y * source.Width + x) * 3;
                int origin = (y * source.Width + sourceX) * 3;
                Buffer.BlockCopy(source.Pixels, origin, output, destination, 3);
            }
        }
        return new ImageFrame(source.Width, source.Height, source.Format, output, takeOwnership: true);
    }

    private sealed class ShortcutAutomation(CameraModel model) : IRobloxAutomation
    {
        private readonly RobloxWindow _window = new((nint)42, "Roblox");
        private readonly ImageFrame _goal = VisionScorerTests.Pattern(808, 611);
        private static readonly int[] YawOffsets = [0, 10, 20, 30, -20, -10];

        public int YawStep { get; set; }
        public int MouseOffset { get; private set; }
        public List<CameraYawDirection> ArrowPulses { get; } = [];
        public List<(int X, int Y)> Drags { get; } = [];

        public void Reset(int yawStep)
        {
            YawStep = yawStep;
            MouseOffset = 0;
            ArrowPulses.Clear();
            Drags.Clear();
        }

        public RobloxWindow? FindWindow(string titleFragment = "Roblox") => _window;
        public RobloxWindow? ForegroundWindow() => _window;
        public ClientBounds GetClientBounds(RobloxWindow window) => new(100, 100, 808, 611);
        public WindowBounds GetWindowBounds(RobloxWindow window) => new(90, 70, 830, 660);
        public bool Focus(RobloxWindow window) => true;
        public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken) => Task.CompletedTask;
        public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds) { }
        public ImageFrame CaptureScreen(ScreenRegion region) => CaptureClient(_window);
        public ImageFrame CaptureClient(RobloxWindow window) => Shift(_goal, YawOffsets[YawStep] + MouseOffset);
        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DragClientAsync(RobloxWindow window, int startX, int startY, int endX, int endY, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken)
        {
            Drags.Add((deltaX, deltaY));
            MouseOffset += deltaX;
            return Task.CompletedTask;
        }
        public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken)
        {
            ArrowPulses.Add(direction);
            YawStep = ((YawStep + (int)direction) % model.Manifest.FullYawSteps + model.Manifest.FullYawSteps) % model.Manifest.FullYawSteps;
            return Task.CompletedTask;
        }
        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MemoryShortcutRepository : ICameraSpawnShortcutRepository
    {
        private CameraSpawnShortcut? _shortcut;
        public Task<CameraSpawnShortcut?> LoadAsync(string cameraModelId, CancellationToken cancellationToken = default) => Task.FromResult(_shortcut);
        public Task SaveAsync(CameraSpawnShortcut shortcut, CancellationToken cancellationToken = default) { _shortcut = shortcut; return Task.CompletedTask; }
        public Task DeleteAsync(string cameraModelId, CancellationToken cancellationToken = default) { _shortcut = null; return Task.CompletedTask; }
    }

    private sealed class MemoryCameraModelRepository : ICameraModelRepository
    {
        public Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CameraModelManifest>>([]);
        public Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<CameraModel?>(null);
        public Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}

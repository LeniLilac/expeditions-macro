using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class CameraAlignmentTests
{
    private static readonly ScreenRegion[] TestRegions =
    [
        new(96, 110, 166, 108),
        new(546, 110, 166, 108),
        new(96, 250, 166, 108),
        new(321, 250, 166, 108),
    ];

    [Fact]
    public void CameraCalibrationSettings_DefaultsToStandardCameraPreparation()
    {
        CameraCalibrationSettings settings = new();

        Assert.Equal(30, settings.ArrowHoldMilliseconds);
        Assert.Equal(100, settings.SettleMilliseconds);
        Assert.Equal(30, settings.ZoomTicks);
        Assert.Equal(1800, settings.PitchDragPixels);
        settings.Validate();
    }

    [Fact]
    public void CameraCalibrationSettings_AllowsOneMillisecondArrowHold()
    {
        CameraCalibrationSettings settings = new() { ArrowHoldMilliseconds = 1 };

        settings.Validate();
    }

    [Fact]
    public void CameraCalibrationSettings_RejectsZeroMillisecondArrowHold()
    {
        CameraCalibrationSettings settings = new() { ArrowHoldMilliseconds = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public async Task SelectRegion_ResizesBeforeSelectionStoresRelativeCoordinatesAndKeepsStandardSize()
    {
        FakeAutomation automation = new(VisionScorerTests.Pattern(96, 72));
        CameraRegionSelectionService service = new(automation);
        ClientBounds clientDuringSelection = default;

        CameraRegionSelection? selection = await service.SelectAsync(client =>
        {
            clientDuringSelection = client;
            return new ScreenRegion(client.X + 15, client.Y + 20, 96, 72);
        });

        Assert.NotNull(selection);
        Assert.Equal(new ClientBounds(300, 200, 808, 611), clientDuringSelection);
        Assert.Equal(new ScreenRegion(15, 20, 96, 72), selection.Region);
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Equal(new ScreenRegion(315, 220, 96, 72), Assert.Single(automation.CapturedRegions));
        Assert.Null(automation.RestoredBounds);
    }

    [Fact]
    public async Task SelectRegion_WhenCanceled_KeepsStandardSize()
    {
        FakeAutomation automation = new(VisionScorerTests.Pattern(96, 72));
        CameraRegionSelectionService service = new(automation);

        CameraRegionSelection? selection = await service.SelectAsync(_ => null);

        Assert.Null(selection);
        Assert.Empty(automation.CapturedRegions);
        Assert.Null(automation.RestoredBounds);
    }

    [Fact]
    public void AutomaticRegions_SelectFourStableAreasAndBuildAnnotatedGoal()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame[] captures = [goal, goal.Clone(), goal.Clone()];

        IReadOnlyList<ScreenRegion> regions = CameraRegionAnalyzer.SelectStableRegions(captures);
        ImageFrame composite = CameraRegionAnalyzer.BuildComposite(goal, regions);
        ImageFrame overlay = CameraRegionAnalyzer.AnnotateGoal(goal, regions);

        Assert.Equal(4, regions.Count);
        Assert.All(regions, region => Assert.True(region.FitsWithin(goal.Width, goal.Height)));
        Assert.Equal(3, regions.Select(region => Math.Clamp((int)(3 * (region.X + region.Width / 2d) / goal.Width), 0, 2)).Distinct().Count());
        Assert.Equal((304, 192, PixelFormat.Gray8), (composite.Width, composite.Height, composite.Format));
        Assert.Equal((goal.Width, goal.Height, PixelFormat.Rgb24), (overlay.Width, overlay.Height, overlay.Format));
        Assert.False(goal.Pixels.SequenceEqual(overlay.Pixels));
    }

    [Theory]
    [InlineData("Expedition_Map1")]
    [InlineData("Expedition_Map2")]
    [InlineData("Expedition_Map3")]
    [InlineData("Story_Map1")]
    [InlineData("Story_Map2")]
    [InlineData("Story_Map3")]
    [InlineData("Story_Map4")]
    public void AutomaticRegions_RealRotationFixturesSeparateGoalFromWrongYaw(string dataset)
    {
        string directory = Path.Combine(TestPaths.CameraRotations, dataset);
        ImageFrame goal = ImageCodec.Load(Path.Combine(directory, "goal.png"));
        ImageFrame wrongYaw = ImageCodec.Load(Path.Combine(directory, "wrong-yaw.png"));

        IReadOnlyList<ScreenRegion> regions = CameraRegionAnalyzer.SelectStableRegions([goal, goal.Clone(), goal.Clone()]);
        ImageFrame reference = CameraRegionAnalyzer.BuildComposite(goal, regions);
        ImageFrame wrong = CameraRegionAnalyzer.BuildComposite(wrongYaw, regions);
        double wrongScore = VisionScorer.RobustSimilarity(reference, wrong);

        Assert.Equal(4, regions.Count);
        Assert.Equal(3, regions.Select(region => Math.Clamp((int)(3 * (region.X + region.Width / 2d) / goal.Width), 0, 2)).Distinct().Count());
        Assert.True(wrongScore < 0.55, $"{dataset} wrong-yaw score was {wrongScore:P1}.");
    }

    [Fact]
    public async Task CameraModelV3_RoundTripsAutomaticRegionsAndBothYawAtlases()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
            CameraModel expected = CreateModel(goal, Enumerable.Repeat(goal, 7).ToArray());
            CameraModelRepository repository = new(new AppPaths(root));

            await repository.SaveAsync(expected);
            CameraModel? actual = await repository.LoadAsync(expected.Manifest.Id);

            Assert.NotNull(actual);
            Assert.Equal(expected.Manifest.Regions, actual.Manifest.Regions);
            Assert.Equal(expected.Manifest.FullYawSteps, actual.Manifest.FullYawSteps);
            Assert.Equal(expected.Manifest.ArrowHoldMilliseconds, actual.Manifest.ArrowHoldMilliseconds);
            Assert.Equal(expected.Manifest.FineYawOffsets, actual.Manifest.FineYawOffsets);
            Assert.Equal(expected.FineYawAtlas.Count, actual.FineYawAtlas.Count);
            Assert.Equal(expected.YawAtlas.Count, actual.YawAtlas.Count);
            Assert.Equal((808, 611), (actual.GoalOverlay.Width, actual.GoalOverlay.Height));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task CameraModelV3_OverwriteWaitsForTemporaryReaderToReleaseTheExistingModel()
    {
        string root = TestPaths.NewTemporaryDirectory();
        FileStream? reader = null;
        Task? releaseReader = null;
        try
        {
            ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
            CameraModel expected = CreateModel(goal, Enumerable.Repeat(goal, 7).ToArray());
            CameraModel replacement = expected with
            {
                Manifest = expected.Manifest with { Name = "Replacement camera" },
            };
            AppPaths paths = new(root);
            CameraModelRepository repository = new(paths);
            await repository.SaveAsync(expected);

            string referencePath = Path.Combine(paths.CameraModels, expected.Manifest.Id, "reference.png");
            reader = new FileStream(referencePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            releaseReader = Task.Run(async () =>
            {
                await Task.Delay(350);
                reader.Dispose();
            });

            await repository.SaveAsync(replacement);
            await releaseReader;
            CameraModel? actual = await repository.LoadAsync(expected.Manifest.Id);

            Assert.NotNull(actual);
            Assert.Equal("Replacement camera", actual.Manifest.Name);
        }
        finally
        {
            reader?.Dispose();
            if (releaseReader is not null) await releaseReader;
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task CameraModelCatalog_IgnoresHiddenTransactionDirectories()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
            CameraModel expected = CreateModel(goal, Enumerable.Repeat(goal, 7).ToArray());
            AppPaths paths = new(root);
            CameraModelRepository repository = new(paths);
            await repository.SaveAsync(expected);

            string transaction = Path.Combine(paths.CameraModels, $".{expected.Manifest.Id}.stale.backup");
            Directory.CreateDirectory(transaction);
            File.Copy(
                Path.Combine(paths.CameraModels, expected.Manifest.Id, "manifest.json"),
                Path.Combine(transaction, "manifest.json"));

            CameraModelManifest model = Assert.Single(await repository.ListAsync());
            Assert.Equal(expected.Manifest.Id, model.Id);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Align_UsesArrowAtlasForCoarseYawAndMouseOnlyForFineYaw()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame[] yawFrames =
        [
            goal,
            Shift(goal, 8),
            Shift(goal, 18),
            Shift(goal, 30),
            Shift(goal, 18),
            Shift(goal, 8),
            goal,
        ];
        CameraModel model = CreateModel(goal, yawFrames);
        FakeAutomation automation = new(goal)
        {
            FullYawSteps = 6,
            YawStep = 2,
            CaptureAtYaw = (yaw, _) => yawFrames[yaw],
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        double score = await engine.AlignAsync(model);

        Assert.True(score > 0.90, $"Alignment score was {score:P1}.");
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Null(automation.RestoredBounds);
        Assert.Contains(CameraYawDirection.Left, automation.ArrowPulses);
        Assert.NotEmpty(automation.Drags);
        Assert.Equal(0, automation.YawStep);
        Assert.Equal(1, automation.MoveToCenterCount);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Align_WhenShiftLockIsAlreadyManaged_DoesNotToggleIt()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        CameraModel model = CreateModel(goal, Enumerable.Repeat(goal, 7).ToArray());
        FakeAutomation automation = new(goal) { FullYawSteps = 6 };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        await engine.AlignAsync(model, manageShiftLock: false);

        Assert.Equal(0, automation.MoveToCenterCount);
        Assert.Equal(0, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Align_UsesPersistedFineYawAtlasForSignedMouseCorrection()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        CameraModel model = CreateModel(goal, Enumerable.Repeat(goal, 7).ToArray());
        FakeAutomation automation = new(goal)
        {
            FullYawSteps = 6,
            CaptureAtYaw = (_, mouse) => Shift(goal, 3 + mouse),
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        double score = await engine.AlignAsync(model, manageShiftLock: false);

        Assert.True(score > 0.90, $"Alignment score was {score:P1}.");
        Assert.Equal(-3, automation.MouseOffset);
    }

    [Fact]
    public async Task Calibrate_SelectsRegionsLearnsArrowTurnAndRestoresShiftLock()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame wrong = Blank(goal.Width, goal.Height);
        FakeAutomation automation = new(goal)
        {
            FullYawSteps = 12,
            CaptureAtYaw = (yaw, mouse) => yaw == 0 ? Shift(goal, mouse) : wrong,
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        CameraModel model = await engine.CalibrateAsync(CalibrationSettings("Automatic regions"));

        Assert.Equal(CameraModelManifest.CurrentSchemaVersion, model.Manifest.SchemaVersion);
        Assert.Equal(4, model.Manifest.Regions.Count);
        Assert.Equal(12, model.Manifest.FullYawSteps);
        Assert.Equal(13, model.Manifest.AtlasSampleCount);
        Assert.Equal(9, model.FineYawAtlas.Count);
        Assert.Equal(Enumerable.Range(-4, 9), model.Manifest.FineYawOffsets);
        Assert.Equal(0, automation.YawStep);
        Assert.Equal(0, automation.MouseOffset);
        Assert.Equal(2, automation.LeftControlTapCount);
        Assert.Equal(30, automation.ZoomTicks);
        Assert.Contains((0, 1800), automation.Drags);
        Assert.All(automation.ArrowPulses.Take(12), pulse => Assert.Equal(CameraYawDirection.Right, pulse));
    }

    [Fact]
    public async Task Calibrate_FineGoalNeighborhoodRecognizesBiasedFullTurnAndRefinesToGoal()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame wrong = Blank(goal.Width, goal.Height);
        FakeAutomation? automation = null;
        automation = new FakeAutomation(goal)
        {
            FullYawSteps = 12,
            CaptureAtYaw = (yaw, mouse) =>
            {
                if (yaw != 0) return wrong;
                int wrapBias = automation!.ArrowPulses.Count >= 12 ? 16 : 0;
                return Shift(goal, wrapBias + mouse);
            },
        };
        CameraCalibrationSettings settings = CalibrationSettings("Fine neighborhood") with
        {
            FineSearchPixels = 16,
            MaximumSamples = 16,
        };
        List<MacroProgress> updates = [];
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        CameraModel model = await engine.CalibrateAsync(settings, new InlineProgress<MacroProgress>(updates.Add));

        Assert.Equal(12, model.Manifest.FullYawSteps);
        Assert.Equal(-16, automation.MouseOffset);
        Assert.Contains(updates, update => update.Message.Contains("Fine goal neighborhood ready with 33 distinct yaw views", StringComparison.Ordinal));
        Assert.Contains(updates, update => update.Message.Contains("neighborhood: 100%", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Calibrate_RestoresAutomaticShiftLockWhenFullClientCaptureFails()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        FakeAutomation automation = new(goal)
        {
            CaptureFailure = new InvalidOperationException("Synthetic capture failure."),
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CalibrateAsync(CalibrationSettings("Automatic shift lock")));

        Assert.Equal("Synthetic capture failure.", error.Message);
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Equal(1, automation.MoveToCenterCount);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    [Fact]
    public async Task Align_WhenFastMatchIsLow_ArrowFullTurnFallbackFindsTheGoal()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame wrong = Blank(goal.Width, goal.Height);
        CameraModel model = CreateModel(goal, Enumerable.Repeat(wrong, 13).ToArray());
        FakeAutomation automation = new(wrong)
        {
            FullYawSteps = 12,
            YawStep = 3,
            CaptureAtYaw = (yaw, _) => yaw == 0 ? goal : wrong,
        };
        List<MacroProgress> updates = [];
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        double score = await engine.AlignAsync(model, progress: new InlineProgress<MacroProgress>(updates.Add));

        Assert.True(score > 0.90, $"Fallback alignment score was {score:P1}.");
        Assert.Contains(updates, update => update.Message.Contains("Scanning one full yaw turn", StringComparison.Ordinal));
        Assert.Contains(updates, update => update.Message.Contains("Stable goal found", StringComparison.Ordinal));
        Assert.Equal(0, automation.YawStep);
    }

    [Fact]
    public async Task Align_WhenArrowFullTurnFallbackStaysBelowTarget_StopsWithFailure()
    {
        ImageFrame goal = VisionScorerTests.Pattern(RobloxClientProfile.Width, RobloxClientProfile.Height);
        ImageFrame wrong = Blank(goal.Width, goal.Height);
        CameraModel model = CreateModel(goal, Enumerable.Repeat(wrong, 13).ToArray());
        FakeAutomation automation = new(wrong)
        {
            FullYawSteps = 12,
            YawStep = 3,
            CaptureAtYaw = (_, _) => wrong,
        };
        CameraAlignmentEngine engine = new(automation, new NullCameraRepository());

        CameraAlignmentException error = await Assert.ThrowsAsync<CameraAlignmentException>(() => engine.AlignAsync(model));

        Assert.Contains("Unit placement was not started", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, error.Attempts);
        Assert.True(automation.ArrowPulses.Count(pulse => pulse == CameraYawDirection.Right) >= 12);
        Assert.True(automation.ArrowPulses.Count(pulse => pulse == CameraYawDirection.Left) >= 12);
        Assert.Equal(2, automation.LeftControlTapCount);
    }

    private static CameraModel CreateModel(ImageFrame goal, IReadOnlyList<ImageFrame> yawFrames)
    {
        int fullYawSteps = yawFrames.Count - 1;
        ImageFrame reference = CameraRegionAnalyzer.BuildComposite(goal, TestRegions);
        ImageFrame[] atlas = yawFrames
            .Select(frame => VisionScorer.MakeThumbnail(CameraRegionAnalyzer.BuildComposite(frame, TestRegions)))
            .ToArray();
        int[] fineOffsets = Enumerable.Range(-4, 9).ToArray();
        ImageFrame[] fineAtlas = fineOffsets
            .Select(offset => VisionScorer.MakeThumbnail(CameraRegionAnalyzer.BuildComposite(Shift(goal, offset), TestRegions)))
            .ToArray();
        return new CameraModel(
            new CameraModelManifest
            {
                Id = "camera-test",
                Name = "Camera test",
                Regions = TestRegions,
                ClientWidth = RobloxClientProfile.Width,
                ClientHeight = RobloxClientProfile.Height,
                BaselineScore = 1,
                SuccessThreshold = 0.80,
                ArrowHoldMilliseconds = 20,
                FineStepPixels = 1,
                FineSearchPixels = 4,
                FineYawOffsets = fineOffsets,
                FullYawSteps = fullYawSteps,
                SettleMilliseconds = 0,
                AtlasSampleCount = atlas.Length,
                ScanScores = Enumerable.Repeat(0.2, atlas.Length - 1).Prepend(1).ToArray(),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            reference,
            CameraRegionAnalyzer.AnnotateGoal(goal, TestRegions),
            fineAtlas,
            atlas);
    }

    private static CameraCalibrationSettings CalibrationSettings(string name) => new()
    {
        Name = name,
        CaptureCount = 2,
        CaptureDuration = TimeSpan.Zero,
        ArrowHoldMilliseconds = 20,
        FineStepPixels = 1,
        FineSearchPixels = 4,
        SettleMilliseconds = 25,
        MaximumSamples = 16,
    };

    private static ImageFrame Blank(int width, int height) =>
        new(width, height, PixelFormat.Rgb24, new byte[width * height * 3], takeOwnership: true);

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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FakeAutomation(ImageFrame screenCapture) : IRobloxAutomation
    {
        private readonly RobloxWindow _window = new((nint)42, "Roblox");
        private ClientBounds _client = new(300, 200, 1000, 700);

        public (int Width, int Height)? ResizeRequest { get; private set; }
        public WindowBounds? RestoredBounds { get; private set; }
        public List<ScreenRegion> CapturedRegions { get; } = [];
        public List<(int X, int Y)> Drags { get; } = [];
        public List<CameraYawDirection> ArrowPulses { get; } = [];
        public int MoveToCenterCount { get; private set; }
        public int LeftControlTapCount { get; private set; }
        public int ZoomTicks { get; private set; }
        public Exception? CaptureFailure { get; init; }
        public Func<int, int, ImageFrame>? CaptureAtYaw { get; init; }
        public int FullYawSteps { get; init; } = 12;
        public int YawStep { get; set; }
        public int MouseOffset { get; private set; }

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
            return screenCapture.Clone();
        }
        public ImageFrame CaptureClient(RobloxWindow window)
        {
            if (CaptureFailure is not null) throw CaptureFailure;
            return (CaptureAtYaw?.Invoke(YawStep, MouseOffset) ?? screenCapture).Clone();
        }
        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            MoveToCenterCount++;
            return Task.CompletedTask;
        }
        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken)
        {
            Drags.Add((deltaX, deltaY));
            MouseOffset += deltaX;
            return Task.CompletedTask;
        }
        public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken)
        {
            ArrowPulses.Add(direction);
            int delta = direction == CameraYawDirection.Right ? 1 : -1;
            YawStep = ((YawStep + delta) % FullYawSteps + FullYawSteps) % FullYawSteps;
            return Task.CompletedTask;
        }
        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken)
        {
            ZoomTicks = ticks;
            return Task.CompletedTask;
        }
        public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken)
        {
            LeftControlTapCount++;
            return Task.CompletedTask;
        }
        public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken) => Task.CompletedTask;
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

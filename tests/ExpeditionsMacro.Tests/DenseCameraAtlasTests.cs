using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class DenseCameraAtlasTests
{
    private static readonly ScreenRegion[] Regions =
    [
        new(96, 110, 166, 108),
        new(546, 110, 166, 108),
        new(96, 250, 166, 108),
        new(321, 250, 166, 108),
    ];

    [Fact]
    public void DenseThumbnailBuilder_ProcessesOneHundredTwentyFramesQuickly()
    {
        ImageFrame frame = Texture(808, 611);
        Stopwatch timer = Stopwatch.StartNew();
        for (int index = 0; index < 120; index++)
        {
            ImageFrame thumbnail =
                CameraDenseThumbnailBuilder.Build(frame, Regions);
            _ = CameraYawAtlasIndex.CameraYawFingerprint.Create(
                thumbnail);
        }
        timer.Stop();
        Assert.True(
            timer.Elapsed < TimeSpan.FromSeconds(2),
            $"Dense thumbnail processing took {timer.Elapsed}.");
    }

    [Fact]
    public async Task Calibrate_DenseHybridAtlasCompletesWithinBudget()
    {
        const int fullYawSteps = 72;
        ImageFrame goal = Texture(
            RobloxClientProfile.Width,
            RobloxClientProfile.Height);
        ImageFrame[] yawFrames = Enumerable.Range(0, fullYawSteps)
            .Select(yaw => Shift(goal, yaw * 7))
            .ToArray();
        FakeAutomation automation = new(goal)
        {
            FullYawSteps = fullYawSteps,
            DenseSweepSamplesPerTurn = 180,
            DenseSweepReleaseMouseOffset = 4,
            DenseFineSweepZeroFrame =
                Shift(goal, goal.Width / 3),
            SimulateDenseSweepTiming = true,
            CaptureAtCameraState = (yaw, mouse, _) =>
                mouse == 0
                    ? yawFrames[yaw]
                    : Shift(yawFrames[yaw], mouse),
        };
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            CameraModelRepository repository =
                new(new AppPaths(root));
            CameraAlignmentEngine engine = new(
                automation,
                repository);
            CameraCalibrationSettings settings = new()
            {
                Name = "Dense hybrid",
                CaptureCount = 6,
                CaptureDuration = TimeSpan.FromSeconds(0.5),
                ArrowHoldMilliseconds = 30,
                FineStepPixels = 1,
                FineSearchPixels = 16,
                SettleMilliseconds = 25,
                MaximumSamples = 240,
            };
            List<MacroProgress> updates = [];
            List<string> timings = [];

            Stopwatch timer = Stopwatch.StartNew();
            CameraModel model;
            try
            {
                model = await engine.CalibrateAsync(
                    settings,
                    new InlineProgress<MacroProgress>(update =>
                    {
                        updates.Add(update);
                        timings.Add(
                            $"{timer.Elapsed.TotalSeconds:F2}s " +
                            update.Message);
                    }));
            }
            catch (Exception error)
            {
                string last = updates.Count == 0
                    ? "no progress"
                    : updates[^1].Message;
                throw new InvalidOperationException(
                    $"Dense setup failed after {timer.Elapsed.TotalSeconds:F2}s; last progress: {last}",
                    error);
            }
            timer.Stop();

            Assert.True(
                timer.Elapsed < TimeSpan.FromSeconds(20),
                $"Dense setup took {timer.Elapsed.TotalSeconds:F2} seconds.{Environment.NewLine}" +
                string.Join(Environment.NewLine, timings));
            Assert.Equal(4, model.Manifest.SchemaVersion);
            Assert.Equal(
                CameraYawAtlasKind.DenseSweep,
                model.Manifest.YawAtlasKind);
            Assert.Equal(fullYawSteps, model.Manifest.FullYawSteps);
            Assert.True(
                model.Manifest.AtlasSampleCount >
                model.Manifest.FullYawSteps);
            Assert.True(model.Manifest.AtlasSampleCount >= 150);
            Assert.InRange(
                model.Manifest.DenseYawTurnMilliseconds,
                2500,
                4000);
            Assert.InRange(
                model.Manifest.CalibrationDurationMilliseconds,
                4000,
                20000);
            Assert.Equal(
                Enumerable.Range(-16, 33),
                model.Manifest.FineYawOffsets);
            Assert.Equal(
                16,
                automation
                    .LastDenseSweepSampleIntervalMilliseconds);
            Assert.True(
                automation.LastDenseSweepMaximumSamples >= 450);
            Assert.Contains(
                updates,
                update => update.Message.Contains(
                    "Ignored one transient moving zero frame",
                    StringComparison.Ordinal));
            int zeroIndex = model.Manifest.FineYawOffsets
                .ToList()
                .IndexOf(0);
            ImageFrame expectedZero =
                CameraDenseThumbnailBuilder.Build(
                    goal,
                    model.Manifest.Regions);
            Assert.Equal(
                expectedZero.Pixels,
                model.FineYawAtlas[zeroIndex].Pixels);
            Assert.NotNull(
                await repository.LoadAsync(model.Manifest.Id));
            Assert.Equal(0, automation.YawStep);
            Assert.Equal(0, automation.MouseOffset);
            Assert.False(automation.ShiftLockState);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(6, 0, "dense sweep release")]
    [InlineData(0, 4, "pulse probe")]
    public async Task Calibrate_DenseGoalReturnUsesClosedLoopAtlasFeedback(
        int sweepReleaseYawOffset,
        int rapidPulseOvershootDivisor,
        string expectedSource)
    {
        const int fullYawSteps = 72;
        ImageFrame goal = Texture(
            RobloxClientProfile.Width,
            RobloxClientProfile.Height);
        ImageFrame[] yawFrames = Enumerable.Range(0, fullYawSteps)
            .Select(yaw => Shift(goal, yaw * 7))
            .ToArray();
        FakeAutomation automation = new(goal)
        {
            FullYawSteps = fullYawSteps,
            DenseSweepSamplesPerTurn = 180,
            DenseSweepReleaseYawOffset =
                sweepReleaseYawOffset,
            RapidYawBatchOvershootDivisor =
                rapidPulseOvershootDivisor,
            CaptureAtCameraState = (yaw, mouse, _) =>
                mouse == 0
                    ? yawFrames[yaw]
                    : Shift(yawFrames[yaw], mouse),
        };
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            CameraAlignmentEngine engine = new(
                automation,
                new CameraModelRepository(new AppPaths(root)));
            List<MacroProgress> updates = [];

            CameraModel model = await engine.CalibrateAsync(
                new CameraCalibrationSettings
                {
                    Name = "Closed-loop dense return",
                    CaptureCount = 3,
                    CaptureDuration = TimeSpan.FromMilliseconds(60),
                    ArrowHoldMilliseconds = 30,
                    FineStepPixels = 1,
                    FineSearchPixels = 16,
                    SettleMilliseconds = 25,
                    MaximumSamples = 240,
                },
                new InlineProgress<MacroProgress>(updates.Add));

            Assert.Equal(
                CameraYawAtlasKind.DenseSweep,
                model.Manifest.YawAtlasKind);
            int visualOffset =
                ((automation.YawStep * 7 +
                  automation.MouseOffset) %
                 goal.Width + goal.Width) %
                goal.Width;
            Assert.Equal(0, visualOffset);
            Assert.Contains(
                updates,
                update =>
                    update.Message.Contains(
                        $"Returning from {expectedSource}",
                        StringComparison.Ordinal));
            Assert.Contains(
                updates,
                update =>
                    update.Message.Contains(
                        $"Returned from {expectedSource}",
                        StringComparison.Ordinal));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(1.00, 0.98, 0.20, true)]
    [InlineData(0.96, 0.25, 0.99, true)]
    [InlineData(0.99, 0.36, 0.70, false)]
    [InlineData(0.80, 0.95, 0.99, false)]
    public void DenseLoopPolicy_RequiresExactOrIndependentFineEvidence(
        double fingerprint,
        double direct,
        double fine,
        bool expected)
    {
        bool actual = DenseYawLoopPolicy.IsReturn(
            fingerprint,
            direct,
            fine,
            exactReturnThreshold: 0.96);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DenseFineMatcherFindsSignedOffsetWithoutHeavyRegistration()
    {
        ImageFrame goal = VisionScorer.MakeThumbnail(
            VisionScorer.PrepareGray(Texture(320, 180)));
        FineYawReference[] references =
            Enumerable.Range(-8, 17)
                .Select(offset => new FineYawReference(
                    offset,
                    ShiftGray(goal, offset)))
                .ToArray();

        FineYawMatch match =
            DenseFineYawMatcher.FindBest(
                references,
                ShiftGray(goal, -3));

        Assert.Equal(-3, match.Offset);
        Assert.True(match.Score > 0.99);
    }

    [Fact]
    public void Manifest_LegacySchemaThreePulseAtlasRemainsValid()
    {
        CameraModelManifest manifest = Manifest(
            schemaVersion: 3,
            CameraYawAtlasKind.PulseSteps,
            fullYawSteps: 6,
            atlasSamples: 7,
            denseTurnMilliseconds: 0);

        manifest.Validate();
    }

    [Fact]
    public void Manifest_DenseAtlasDoesNotRequireOneFramePerPulse()
    {
        CameraModelManifest manifest = Manifest(
            schemaVersion: 4,
            CameraYawAtlasKind.DenseSweep,
            fullYawSteps: 72,
            atlasSamples: 121,
            denseTurnMilliseconds: 4000);

        manifest.Validate();
    }

    [Fact]
    public void Manifest_DenseAtlasAllowsSetupBeyondPerformanceTarget()
    {
        CameraModelManifest manifest = Manifest(
            schemaVersion: 4,
            CameraYawAtlasKind.DenseSweep,
            fullYawSteps: 72,
            atlasSamples: 121,
            denseTurnMilliseconds: 4000) with
        {
            CalibrationDurationMilliseconds = 60000,
        };

        manifest.Validate();
        Assert.Throws<InvalidDataException>(
            () => (manifest with
            {
                CalibrationDurationMilliseconds = 120001,
            }).Validate());
    }

    [Fact]
    public void FingerprintPrefilterStillUsesStructuralReranking()
    {
        ImageFrame goal = VisionScorer.MakeThumbnail(
            VisionScorer.PrepareGray(Texture(320, 180)));
        ImageFrame[] atlas =
        [
            goal,
            ShiftGray(goal, 8),
            ShiftGray(goal, 16),
            ShiftGray(goal, 24),
            goal,
        ];
        ImageFrame current = ShiftGray(goal, 16);

        CameraYawAtlasMatch match =
            CameraYawAtlasIndex.For(atlas).FindBest(current);

        Assert.Equal(2, match.Index);
        Assert.True(match.Score > 0.95);
    }

    [Fact]
    public void BoundedFingerprintSearchStaysInsidePhysicalRange()
    {
        ImageFrame goal = VisionScorer.MakeThumbnail(
            VisionScorer.PrepareGray(Texture(320, 180)));
        ImageFrame[] atlas =
        [
            goal,
            ShiftGray(goal, 8),
            ShiftGray(goal, 16),
            ShiftGray(goal, 24),
            ShiftGray(goal, 32),
            goal,
        ];
        CameraYawAtlasIndex index =
            CameraYawAtlasIndex.For(atlas);

        CameraYawAtlasMatch match =
            index.FindBestWithin(
                ShiftGray(goal, 24),
                minimumIndex: 2,
                maximumIndex: 4);

        Assert.Equal(3, match.Index);
        Assert.True(match.Score > 0.95);
    }

    [Fact]
    public async Task Align_DenseAtlasConvertsVisualBinsToPulseDistance()
    {
        const int fullYawSteps = 72;
        const int atlasBins = 120;
        ImageFrame goal = Texture(808, 611);
        ImageFrame[] yawFrames = Enumerable.Range(0, fullYawSteps)
            .Select(yaw => Shift(goal, yaw * 7))
            .ToArray();
        ImageFrame[] atlas = Enumerable.Range(0, atlasBins + 1)
            .Select(index =>
            {
                int yaw = (int)Math.Round(
                    index * (double)fullYawSteps / atlasBins) %
                    fullYawSteps;
                return CameraDenseThumbnailBuilder.Build(
                    yawFrames[yaw],
                    Regions);
            })
            .ToArray();
        int[] fineOffsets = Enumerable.Range(-4, 9).ToArray();
        CameraModel model = new(
            Manifest(
                schemaVersion: 4,
                CameraYawAtlasKind.DenseSweep,
                fullYawSteps,
                atlas.Length,
                denseTurnMilliseconds: 3600) with
            {
                Id = "dense-runtime",
                ClientWidth = 808,
                ClientHeight = 611,
                Regions = Regions,
                FineYawOffsets = fineOffsets,
                ScanScores = Enumerable.Repeat(
                    0.2,
                    atlas.Length).ToArray(),
            },
            CameraRegionAnalyzer.BuildComposite(goal, Regions),
            CameraRegionAnalyzer.AnnotateGoal(goal, Regions),
            fineOffsets.Select(offset =>
                    CameraDenseThumbnailBuilder.Build(
                        Shift(goal, offset),
                        Regions))
                .ToArray(),
            atlas);
        FakeAutomation automation = new(yawFrames[36])
        {
            FullYawSteps = fullYawSteps,
            YawStep = 36,
            CaptureAtCameraState = (yaw, mouse, _) =>
                mouse == 0
                    ? yawFrames[yaw]
                    : Shift(yawFrames[yaw], mouse),
        };
        CameraAlignmentEngine engine = new(
            automation,
            new NullCameraRepository());

        double score = await engine.AlignAsync(
            model,
            manageShiftLock: false);

        Assert.True(score >= model.Manifest.SuccessThreshold);
        Assert.Equal(0, automation.YawStep);
        Assert.InRange(
            automation.ArrowPulses.Count,
            30,
            45);
    }

    private static CameraModelManifest Manifest(
        int schemaVersion,
        CameraYawAtlasKind kind,
        int fullYawSteps,
        int atlasSamples,
        int denseTurnMilliseconds) => new()
        {
            SchemaVersion = schemaVersion,
            Id = "dense-test",
            Name = "Dense test",
            Regions =
        [
            new ScreenRegion(20, 20, 100, 80),
            new ScreenRegion(140, 20, 100, 80),
        ],
            ClientWidth = 320,
            ClientHeight = 180,
            BaselineScore = 1,
            SuccessThreshold = 0.8,
            ArrowHoldMilliseconds = 30,
            FineStepPixels = 1,
            FineSearchPixels = 4,
            FineYawOffsets = Enumerable.Range(-4, 9).ToArray(),
            FullYawSteps = fullYawSteps,
            SettleMilliseconds = 200,
            YawAtlasKind = kind,
            DenseYawTurnMilliseconds = denseTurnMilliseconds,
            AtlasSampleCount = atlasSamples,
            ScanScores = Enumerable.Repeat(0.2, atlasSamples).ToArray(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static ImageFrame Texture(int width, int height)
    {
        byte[] pixels = new byte[width * height * 3];
        uint state = 0xA341316Cu;
        for (int offset = 0; offset < pixels.Length; offset += 3)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            pixels[offset] = (byte)(state >> 24);
            pixels[offset + 1] = (byte)(state >> 16);
            pixels[offset + 2] = (byte)(state >> 8);
        }
        return new ImageFrame(
            width,
            height,
            PixelFormat.Rgb24,
            pixels,
            takeOwnership: true);
    }

    private static ImageFrame Shift(ImageFrame source, int deltaX)
    {
        byte[] output = new byte[source.Pixels.Length];
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                int sourceX =
                    ((x + deltaX) % source.Width + source.Width) %
                    source.Width;
                int destination =
                    (y * source.Width + x) * source.Channels;
                int origin =
                    (y * source.Width + sourceX) * source.Channels;
                Buffer.BlockCopy(
                    source.Pixels,
                    origin,
                    output,
                    destination,
                    source.Channels);
            }
        }
        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            output,
            takeOwnership: true);
    }

    private static ImageFrame ShiftGray(
        ImageFrame source,
        int deltaX) =>
        Shift(source, deltaX);

    private sealed class InlineProgress<T>(Action<T> report) :
        IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

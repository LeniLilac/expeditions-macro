using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class CameraClosedLoopCorrectionTests
{
    private static readonly ScreenRegion[] Regions =
    [
        new(40, 40, 166, 108),
        new(240, 40, 166, 108),
        new(440, 40, 166, 108),
        new(240, 190, 166, 108),
    ];

    [Fact]
    public async Task Align_WhenRapidArrowBatchOvershoots_ReobservesAndUsesShortestCorrection()
    {
        const int fullTurn = 72;
        ImageFrame goal = VisionScorerTests.Pattern(
            RobloxClientProfile.Width,
            RobloxClientProfile.Height);
        ImageFrame[] yawFrames = Enumerable.Range(0, fullTurn)
            .Select(index => index == 0 ? goal : Pose(index))
            .Append(goal)
            .ToArray();
        CameraModel model = CreateModel(goal, yawFrames);
        FakeAutomation automation = new(yawFrames[37])
        {
            FullYawSteps = fullTurn,
            YawStep = 37,
            RapidYawBatchOvershootDivisor = 4,
            CaptureAtYaw = (yaw, _) => yawFrames[yaw],
        };
        List<MacroProgress> updates = [];
        CameraAlignmentEngine engine = new(
            automation,
            new NullCameraRepository());

        double score = await engine.AlignAsync(
            model,
            manageShiftLock: false,
            progress: new InlineProgress<MacroProgress>(updates.Add));

        Assert.True(score > 0.90, $"Alignment score was {score:P1}.");
        Assert.Equal(0, automation.YawStep);
        Assert.True(
            automation.ArrowPulses.Count < fullTurn / 2,
            $"Closed-loop correction used {automation.ArrowPulses.Count} pulses.");
        Assert.Contains(
            updates,
            update => update.Message.Contains(
                "re-observed atlas position",
                StringComparison.Ordinal));
        Assert.Contains(
            updates,
            update => update.Message.Contains(
                "reached the goal atlas",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            updates,
            update => update.Message.Contains(
                "Scanning one full yaw turn",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Align_WhenCoarseObservationDoesNotMove_StopsFeedbackBeforeFallback()
    {
        const int fullTurn = 6;
        ImageFrame goal = VisionScorerTests.Pattern(
            RobloxClientProfile.Width,
            RobloxClientProfile.Height);
        ImageFrame[] yawFrames = Enumerable.Range(0, fullTurn)
            .Select(index => index == 0 ? goal : Pose(index))
            .Append(goal)
            .ToArray();
        CameraModel model = CreateModel(goal, yawFrames);
        FakeAutomation automation = new(yawFrames[4])
        {
            FullYawSteps = fullTurn,
            YawStep = 2,
            CaptureAtYaw = (_, _) => yawFrames[2],
        };
        List<MacroProgress> updates = [];
        CameraAlignmentEngine engine = new(
            automation,
            new NullCameraRepository());

        await Assert.ThrowsAsync<CameraAlignmentException>(() =>
            engine.AlignAsync(
                model,
                manageShiftLock: false,
                progress: new InlineProgress<MacroProgress>(updates.Add)));

        Assert.Contains(
            updates,
            update => update.Message.Contains(
                "stopped the feedback loop before it could oscillate",
                StringComparison.Ordinal));
        Assert.Contains(
            updates,
            update => update.Message.Contains(
                "Scanning one full yaw turn",
                StringComparison.Ordinal));
    }

    private static CameraModel CreateModel(
        ImageFrame goal,
        IReadOnlyList<ImageFrame> yawFrames)
    {
        int fullTurn = yawFrames.Count - 1;
        ImageFrame reference =
            CameraRegionAnalyzer.BuildComposite(goal, Regions);
        ImageFrame[] atlas = yawFrames
            .Select(frame => VisionScorer.MakeThumbnail(
                CameraRegionAnalyzer.BuildComposite(frame, Regions)))
            .ToArray();
        int[] fineOffsets = Enumerable.Range(-4, 9).ToArray();
        ImageFrame[] fineAtlas = fineOffsets
            .Select(offset => VisionScorer.MakeThumbnail(
                CameraRegionAnalyzer.BuildComposite(
                    Shift(goal, offset),
                    Regions)))
            .ToArray();
        return new CameraModel(
            new CameraModelManifest
            {
                Id = "closed-loop-camera-test",
                Name = "Closed-loop camera test",
                Regions = Regions,
                ClientWidth = RobloxClientProfile.Width,
                ClientHeight = RobloxClientProfile.Height,
                BaselineScore = 1,
                SuccessThreshold = 0.80,
                ArrowHoldMilliseconds = 20,
                FineStepPixels = 1,
                FineSearchPixels = 4,
                FineYawOffsets = fineOffsets,
                FullYawSteps = fullTurn,
                SettleMilliseconds = 0,
                AtlasSampleCount = atlas.Length,
                ScanScores = Enumerable.Repeat(0.2, atlas.Length - 1)
                    .Prepend(1)
                    .ToArray(),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            reference,
            CameraRegionAnalyzer.AnnotateGoal(goal, Regions),
            fineAtlas,
            atlas);
    }

    private static ImageFrame Pose(int seed)
    {
        int width = RobloxClientProfile.Width;
        int height = RobloxClientProfile.Height;
        byte[] pixels = new byte[width * height * 3];
        uint state = unchecked((uint)(seed * 747796405 + 2891336453));
        for (int offset = 0; offset < pixels.Length; offset += 3)
        {
            state = state * 1664525 + 1013904223;
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
                int destination = (y * source.Width + x) * 3;
                int origin = (y * source.Width + sourceX) * 3;
                Buffer.BlockCopy(
                    source.Pixels,
                    origin,
                    output,
                    destination,
                    3);
            }
        }
        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            output,
            takeOwnership: true);
    }

    private sealed class InlineProgress<T>(Action<T> report) :
        IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private async Task<int> CalibrateDensePulseScaleAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        IReadOnlyList<FineYawReference> goalNeighborhood,
        IReadOnlyList<ImageFrame> atlas,
        TimeSpan turnElapsed,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        const int probeBatch = 3;
        CameraYawAtlasIndex atlasIndex =
            CameraYawAtlasIndex.For(atlas);
        int turnBins = atlas.Count - 1;
        double continuousEstimate =
            turnElapsed.TotalMilliseconds /
            settings.ArrowHoldMilliseconds;
        await PulseYawAsync(
            window,
            CameraYawDirection.Right,
            probeBatch,
            settings.ArrowHoldMilliseconds,
            settings.SettleMilliseconds,
            cancellationToken).ConfigureAwait(false);
        ImageFrame firstProbe = CameraDenseThumbnailBuilder.Build(
            _automation.CaptureClient(window),
            regions,
            atlas[0].Width);
        CameraYawAtlasMatch firstMatch =
            FindDensePulseProbe(
                atlasIndex,
                firstProbe,
                probeBatch,
                turnBins,
                continuousEstimate);
        await PulseYawAsync(
            window,
            CameraYawDirection.Right,
            probeBatch,
            settings.ArrowHoldMilliseconds,
            settings.SettleMilliseconds,
            cancellationToken).ConfigureAwait(false);
        ImageFrame secondProbe = CameraDenseThumbnailBuilder.Build(
            _automation.CaptureClient(window),
            regions,
            atlas[0].Width);
        CameraYawAtlasMatch secondMatch =
            FindDensePulseProbe(
                atlasIndex,
                secondProbe,
                probeBatch * 2,
                turnBins,
                continuousEstimate);
        int observedDelta = ForwardAtlasDistance(
            firstMatch.Index,
            secondMatch.Index,
            turnBins);
        if (firstMatch.Index < 1 || observedDelta < 2)
        {
            throw new InvalidOperationException(
                $"The dense atlas could not calibrate arrow movement reliably (three pulses matched {firstMatch.Index}, six matched {secondMatch.Index}, of {turnBins}).");
        }

        int fullYawSteps = (int)Math.Round(
            probeBatch * (double)turnBins / observedDelta);
        CameraYawAtlasMatch finalMatch = secondMatch;
        if (fullYawSteps >= 36)
        {
            await PulseYawAsync(
                window,
                CameraYawDirection.Right,
                probeBatch * 2,
                settings.ArrowHoldMilliseconds,
                settings.SettleMilliseconds,
                cancellationToken).ConfigureAwait(false);
            ImageFrame extendedProbe =
                CameraDenseThumbnailBuilder.Build(
                    _automation.CaptureClient(window),
                    regions,
                    atlas[0].Width);
            finalMatch = FindDensePulseProbe(
                atlasIndex,
                extendedProbe,
                probeBatch * 4,
                turnBins,
                continuousEstimate);
            observedDelta = ForwardAtlasDistance(
                secondMatch.Index,
                finalMatch.Index,
                turnBins);
            if (observedDelta < 2)
            {
                throw new InvalidOperationException(
                    "The extended dense-atlas pulse probe did not move far enough to calibrate yaw.");
            }
            fullYawSteps = (int)Math.Round(
                probeBatch * 2d * turnBins / observedDelta);
        }
        if (fullYawSteps is < 12 or > 500)
        {
            throw new InvalidOperationException(
                $"The measured arrow-pulse turn length ({fullYawSteps}) is outside the supported range.");
        }
        double estimateRatio = fullYawSteps / continuousEstimate;
        if (estimateRatio is < 0.50 or > 1.50)
        {
            throw new InvalidOperationException(
                $"The dense yaw sweep and arrow-pulse probe disagreed (continuous estimate {continuousEstimate:F0}, pulse estimate {fullYawSteps}).");
        }

        await RestoreDenseGoalAsync(
            window,
            regions,
            reference,
            goalNeighborhood,
            atlas,
            fullYawSteps,
            settings,
            progress,
            "pulse probe",
            progressPercent: 90,
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress(
            "Camera setup",
            91,
            $"Pulse probe moved through dense positions {firstMatch.Index}, {secondMatch.Index}, and {finalMatch.Index}/{turnBins}; calibrated {fullYawSteps} pulses per turn.",
            Confidence: new[]
            {
                firstMatch.Score,
                secondMatch.Score,
                finalMatch.Score,
            }.Min()));
        return fullYawSteps;
    }

    private static CameraYawAtlasMatch FindDensePulseProbe(
        CameraYawAtlasIndex atlasIndex,
        ImageFrame probe,
        int pulseCount,
        int turnBins,
        double continuousEstimate)
    {
        double expected =
            pulseCount * turnBins / continuousEstimate;
        int minimum = Math.Max(
            0,
            (int)Math.Floor(expected * 2 / 3) - 2);
        int maximum = Math.Min(
            turnBins - 1,
            (int)Math.Ceiling(expected * 2) + 2);
        return atlasIndex.FindBestWithin(
            probe,
            minimum,
            maximum);
    }

    private static int ForwardAtlasDistance(
        int start,
        int end,
        int turnBins)
    {
        int distance = end - start;
        return distance > 0 ? distance : distance + turnBins;
    }
}

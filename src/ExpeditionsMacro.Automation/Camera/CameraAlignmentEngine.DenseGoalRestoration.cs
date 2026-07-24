using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private const int MaximumDenseGoalCorrectionRounds = 24;
    private const int MaximumDenseGoalPulseBatch = 6;
    private const double DenseGoalDirectThreshold = 0.70;

    private async Task RestoreDenseGoalAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        IReadOnlyList<FineYawReference> goalNeighborhood,
        IReadOnlyList<ImageFrame> atlas,
        int fullYawSteps,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        string source,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        double directScore = await RestoreDenseFineGoalAsync(
            window,
            regions,
            reference,
            goalNeighborhood,
            settings,
            cancellationToken).ConfigureAwait(false);
        if (directScore >= DenseGoalDirectThreshold)
        {
            return;
        }

        CameraYawAtlasIndex atlasIndex =
            CameraYawAtlasIndex.For(atlas);
        int turnBins = atlas.Count - 1;
        int normalizedFullYawSteps = Math.Clamp(
            fullYawSteps,
            12,
            500);
        int maximumBatch = Math.Clamp(
            (int)Math.Ceiling(normalizedFullYawSteps / 12d),
            1,
            MaximumDenseGoalPulseBatch);
        int pulseBudget = normalizedFullYawSteps;
        int pulsesSent = 0;
        CameraYawAtlasMatch lastMatch = default;

        for (int round = 1;
             round <= MaximumDenseGoalCorrectionRounds &&
             pulsesSent < pulseBudget;
             round++)
        {
            ImageFrame current = await StableDenseThumbnailAsync(
                window,
                regions,
                atlas[0].Width,
                cancellationToken).ConfigureAwait(false);
            lastMatch = atlasIndex.FindBest(current);
            AtlasMatch evidence = new(
                lastMatch.Index,
                lastMatch.Score,
                lastMatch.FingerprintScore,
                lastMatch.FingerprintIsolation);
            if (!CameraCoarseAtlasEvidencePolicy.IsReliable(
                    CameraYawAtlasKind.DenseSweep,
                    evidence,
                    minimumRegisteredScore: 0.45))
            {
                throw new InvalidOperationException(
                    $"The {source} could not identify the current view in its captured yaw atlas " +
                    $"({lastMatch.Score:P0} structure, {lastMatch.FingerprintScore:P0} fingerprint, " +
                    $"{lastMatch.FingerprintIsolation:P0} separation).");
            }

            (CameraYawDirection direction, int remaining) =
                ShortestCorrection(
                    normalizedFullYawSteps,
                    turnBins,
                    lastMatch.Index);
            progress?.Report(new MacroProgress(
                "Camera setup",
                progressPercent,
                $"Returning from {source}: atlas {lastMatch.Index}/{turnBins}; " +
                $"{remaining} {DirectionLabel(direction)} pulse{(remaining == 1 ? string.Empty : "s")} remain.",
                Confidence: lastMatch.Score));

            if (remaining <= 1)
            {
                directScore = await RestoreDenseFineGoalAsync(
                    window,
                    regions,
                    reference,
                    goalNeighborhood,
                    settings,
                    cancellationToken).ConfigureAwait(false);
                if (directScore >= DenseGoalDirectThreshold)
                {
                    progress?.Report(new MacroProgress(
                        "Camera setup",
                        progressPercent,
                        $"Returned from {source} after {pulsesSent} closed-loop arrow pulse{(pulsesSent == 1 ? string.Empty : "s")}; goal confidence {directScore:P0}.",
                        Confidence: directScore));
                    return;
                }
                if (remaining == 0)
                {
                    break;
                }
            }

            int batch = Math.Min(
                remaining,
                Math.Min(
                    maximumBatch,
                    pulseBudget - pulsesSent));
            await PulseYawAsync(
                window,
                direction,
                batch,
                settings.ArrowHoldMilliseconds,
                settings.SettleMilliseconds,
                cancellationToken).ConfigureAwait(false);
            pulsesSent += batch;
        }

        directScore = await RestoreDenseFineGoalAsync(
            window,
            regions,
            reference,
            goalNeighborhood,
            settings,
            cancellationToken).ConfigureAwait(false);
        if (directScore >= DenseGoalDirectThreshold)
        {
            progress?.Report(new MacroProgress(
                "Camera setup",
                progressPercent,
                $"Returned from {source} after {pulsesSent} closed-loop arrow pulse{(pulsesSent == 1 ? string.Empty : "s")}; goal confidence {directScore:P0}.",
                Confidence: directScore));
            return;
        }

        throw new InvalidOperationException(
            $"The {source} could not return to the goal after {pulsesSent} bounded arrow pulses " +
            $"(goal {directScore:P0}, last atlas {lastMatch.Index}/{turnBins}).");
    }

    private async Task<double> RestoreDenseFineGoalAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        IReadOnlyList<FineYawReference> goalNeighborhood,
        CameraCalibrationSettings settings,
        CancellationToken cancellationToken)
    {
        (_, double directScore) = await StablePreparedScoreAsync(
            reference,
            window,
            regions,
            2,
            cancellationToken).ConfigureAwait(false);
        if (directScore >= 0.80)
        {
            return directScore;
        }

        ImageFrame thumbnail = await StableDenseThumbnailAsync(
            window,
            regions,
            goalNeighborhood[0].Thumbnail.Width,
            cancellationToken).ConfigureAwait(false);
        FineYawMatch fineMatch = BestFineYawMatch(
            goalNeighborhood,
            thumbnail);
        if (fineMatch.Offset == 0 || fineMatch.Score < 0.65)
        {
            return directScore;
        }

        await MoveMouseAsync(
            window,
            -fineMatch.Offset,
            settings.SettleMilliseconds,
            settings.FineStepPixels,
            cancellationToken).ConfigureAwait(false);
        (_, directScore) = await StablePreparedScoreAsync(
            reference,
            window,
            regions,
            2,
            cancellationToken).ConfigureAwait(false);
        return directScore;
    }
}

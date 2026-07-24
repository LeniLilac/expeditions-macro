using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private const int MaximumClosedLoopCorrectionRounds = 10;
    private const int MaximumClosedLoopPulseBatch = 6;

    private async Task CorrectCoarseYawClosedLoopAsync(
        RobloxWindow window,
        CameraModel model,
        ImageFrame initialThumbnail,
        int attempt,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int fullTurn = model.Manifest.FullYawSteps;
        int maximumBatch = Math.Clamp(
            (int)Math.Ceiling(fullTurn / 12d),
            1,
            MaximumClosedLoopPulseBatch);
        int pulseBudget = fullTurn;
        double minimumAtlasConfidence = Math.Clamp(
            model.Manifest.SuccessThreshold - 0.20,
            0.45,
            0.72);
        AtlasMatch current = BestAtlasMatch(model, initialThumbnail);
        (CameraYawDirection direction, int remaining) =
            ShortestCorrection(model.Manifest, current.Index);
        bool currentReliable =
            CameraCoarseAtlasEvidencePolicy.IsReliable(
                model.Manifest.YawAtlasKind,
                current,
                minimumAtlasConfidence);

        progress?.Report(new MacroProgress(
            "Camera alignment",
            30,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} yaw-atlas match: {AtlasEvidenceLabel(model.Manifest, current)}. Closed-loop correction: {remaining} {DirectionLabel(direction)} arrow step{(remaining == 1 ? string.Empty : "s")}.",
            Confidence: current.Score));

        if (remaining == 0 || !currentReliable)
        {
            if (!currentReliable)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    32,
                    $"Yaw-atlas evidence remained ambiguous ({AtlasEvidenceLabel(model.Manifest, current)}); skipped coarse correction and retained the verified fallback.",
                    Confidence: current.Score));
            }
            return;
        }
        if (current.Score < minimumAtlasConfidence)
        {
            progress?.Report(new MacroProgress(
                "Camera alignment",
                32,
                $"The isolated dense fingerprint supports a bounded coarse correction despite lower pixel registration; every group will be re-observed before continuing.",
                Confidence: current.Score));
        }

        HashSet<int> observedIndices = [current.Index];
        int pulsesSent = 0;
        for (int round = 1;
             round <= MaximumClosedLoopCorrectionRounds &&
             remaining > 0 &&
             pulsesSent < pulseBudget;
             round++)
        {
            int batch = Math.Min(
                remaining,
                Math.Min(maximumBatch, pulseBudget - pulsesSent));
            await PulseYawAsync(
                window,
                direction,
                batch,
                model.Manifest.ArrowHoldMilliseconds,
                model.Manifest.SettleMilliseconds,
                cancellationToken).ConfigureAwait(false);
            pulsesSent += batch;

            ImageFrame thumbnail = await CurrentThumbnailAsync(
                model,
                window,
                model.YawAtlas[0].Width,
                3,
                cancellationToken).ConfigureAwait(false);
            AtlasMatch observed = BestAtlasMatch(model, thumbnail);
            (CameraYawDirection nextDirection, int nextRemaining) =
                ShortestCorrection(model.Manifest, observed.Index);
            bool observedReliable =
                CameraCoarseAtlasEvidencePolicy.IsReliable(
                    model.Manifest.YawAtlasKind,
                    observed,
                    minimumAtlasConfidence);

            progress?.Report(new MacroProgress(
                "Camera alignment",
                Math.Min(58, 32 + round * 4),
                nextRemaining == 0
                    ? $"Closed-loop coarse correction reached the goal atlas after {pulsesSent} arrow pulses ({AtlasEvidenceLabel(model.Manifest, observed)})."
                    : $"Closed-loop coarse correction re-observed atlas position {observed.Index}/{model.Manifest.AtlasSampleCount - 1} with {AtlasEvidenceLabel(model.Manifest, observed)}; shortest remainder is {nextRemaining} {DirectionLabel(nextDirection)}.",
                Confidence: observed.Score));

            if (nextRemaining == 0) return;
            if (!observedReliable)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    60,
                    $"Coarse re-observation became ambiguous ({AtlasEvidenceLabel(model.Manifest, observed)}); stopped correction before using the verified refinement fallback.",
                    Confidence: observed.Score));
                return;
            }
            if (!observedIndices.Add(observed.Index))
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    60,
                    $"Coarse correction revisited atlas position {observed.Index}; stopped the feedback loop before it could oscillate.",
                    Confidence: observed.Score));
                return;
            }

            current = observed;
            direction = nextDirection;
            remaining = nextRemaining;
        }

        progress?.Report(new MacroProgress(
            "Camera alignment",
            60,
            $"Closed-loop coarse correction reached its bounded limit after {pulsesSent} arrow pulses; continuing with verified refinement.",
            Confidence: current.Score));
    }

    private static (CameraYawDirection Direction, int Steps) ShortestCorrection(
        CameraModelManifest manifest,
        int atlasIndex) =>
        ShortestCorrection(
            manifest.FullYawSteps,
            manifest.AtlasSampleCount - 1,
            atlasIndex);

    private static (CameraYawDirection Direction, int Steps) ShortestCorrection(
        int fullTurn,
        int atlasTurn,
        int atlasIndex)
    {
        double pulsePosition =
            atlasIndex * (double)fullTurn / atlasTurn;
        pulsePosition =
            ((pulsePosition % fullTurn) + fullTurn) % fullTurn;
        return pulsePosition <= fullTurn / 2d
            ? (CameraYawDirection.Left, (int)Math.Round(pulsePosition))
            : (CameraYawDirection.Right,
                (int)Math.Round(fullTurn - pulsePosition));
    }

    private static string AtlasEvidenceLabel(
        CameraModelManifest manifest,
        AtlasMatch match) =>
        manifest.YawAtlasKind == CameraYawAtlasKind.DenseSweep
            ? $"{match.Score:P0} registered structure, " +
              $"{match.FingerprintScore:P0} fingerprint, " +
              $"{match.FingerprintIsolation:P0} remote separation"
            : $"{match.Score:P0} registered structure";
}

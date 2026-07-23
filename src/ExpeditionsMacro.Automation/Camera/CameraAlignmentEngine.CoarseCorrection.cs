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
        AtlasMatch current = BestAtlasMatch(model.YawAtlas, initialThumbnail);
        (CameraYawDirection direction, int remaining) =
            ShortestCorrection(current.Index, fullTurn);

        progress?.Report(new MacroProgress(
            "Camera alignment",
            30,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} registered yaw-atlas match: {current.Score:P0}. Closed-loop correction: {remaining} {DirectionLabel(direction)} arrow step{(remaining == 1 ? string.Empty : "s")}.",
            Confidence: current.Score));

        if (remaining == 0 || current.Score < minimumAtlasConfidence)
        {
            if (current.Score < minimumAtlasConfidence)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    32,
                    $"Yaw-atlas evidence was only {current.Score:P0}; skipped open-loop correction and retained the verified fallback.",
                    Confidence: current.Score));
            }
            return;
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
            AtlasMatch observed = BestAtlasMatch(model.YawAtlas, thumbnail);
            (CameraYawDirection nextDirection, int nextRemaining) =
                ShortestCorrection(observed.Index, fullTurn);

            progress?.Report(new MacroProgress(
                "Camera alignment",
                Math.Min(58, 32 + round * 4),
                nextRemaining == 0
                    ? $"Closed-loop coarse correction reached the goal atlas after {pulsesSent} arrow pulses ({observed.Score:P0})."
                    : $"Closed-loop coarse correction re-observed atlas position {observed.Index}/{fullTurn} at {observed.Score:P0}; shortest remainder is {nextRemaining} {DirectionLabel(nextDirection)}.",
                Confidence: observed.Score));

            if (nextRemaining == 0) return;
            if (observed.Score < minimumAtlasConfidence)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    60,
                    $"Coarse re-observation became ambiguous at {observed.Score:P0}; stopped correction before using the verified refinement fallback.",
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
        int atlasIndex,
        int fullTurn)
    {
        int normalized = ((atlasIndex % fullTurn) + fullTurn) % fullTurn;
        return normalized <= fullTurn / 2
            ? (CameraYawDirection.Left, normalized)
            : (CameraYawDirection.Right, fullTurn - normalized);
    }
}

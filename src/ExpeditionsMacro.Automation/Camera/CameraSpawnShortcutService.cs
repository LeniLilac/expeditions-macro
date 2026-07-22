using System.Text.Json;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

internal sealed record CameraSpawnShortcutObservation(
    ImageFrame Fingerprint,
    int AtlasIndex,
    double AtlasScore,
    int EstimatedDragPixels,
    int MousePixelsPerArrowStep,
    CameraSpawnShortcut? Stored,
    bool StoredMatches,
    bool ShouldAttempt);

internal sealed record CameraSpawnShortcutAttempt(bool Attempted, bool Succeeded, double Confidence);

internal sealed class CameraSpawnShortcutService(
    IRobloxAutomation automation,
    ICameraSpawnShortcutRepository repository)
{
    private const int RequiredMatchingObservations = 2;
    private const int FailuresBeforeRelearning = 3;
    private const double FingerprintThreshold = 0.78;
    private const double StepCalibrationThreshold = 0.72;
    private const double FineResidualThreshold = 0.72;

    public async Task<CameraSpawnShortcutObservation?> ObserveAsync(
        CameraModel model,
        ImageFrame fingerprint,
        CancellationToken cancellationToken)
    {
        (int atlasIndex, double atlasScore) = BestAtlasMatch(model.YawAtlas, fingerprint);
        int? mousePixelsPerArrowStep = EstimateMousePixelsPerArrowStep(model);
        if (mousePixelsPerArrowStep is null) return null;

        int estimatedDrag = CorrectionPixels(
            atlasIndex,
            model.Manifest.FullYawSteps,
            mousePixelsPerArrowStep.Value);
        CameraSpawnShortcut? stored = await LoadSafelyAsync(model.Manifest.Id, cancellationToken).ConfigureAwait(false);
        if (stored is not null && !Compatible(stored, model))
        {
            await DeleteSafelyAsync(model.Manifest.Id, cancellationToken).ConfigureAwait(false);
            stored = null;
        }

        bool matches = false;
        if (stored is not null)
        {
            double fingerprintScore = CameraRegisteredScorer.Score(stored.CreateFingerprint(), fingerprint).Score;
            int atlasDistance = CircularDistance(stored.SpawnAtlasIndex, atlasIndex, model.Manifest.FullYawSteps);
            matches = fingerprintScore >= FingerprintThreshold && atlasDistance <= 1;
        }

        return new CameraSpawnShortcutObservation(
            fingerprint.Clone(),
            atlasIndex,
            atlasScore,
            estimatedDrag,
            mousePixelsPerArrowStep.Value,
            stored,
            matches,
            matches && stored!.MatchingObservations >= RequiredMatchingObservations);
    }

    public async Task<CameraSpawnShortcutAttempt> TryAsync(
        RobloxWindow window,
        CameraModel model,
        CameraSpawnShortcutObservation? observation,
        Func<CancellationToken, Task<double>> verify,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (observation is not { ShouldAttempt: true, Stored: not null })
        {
            return new CameraSpawnShortcutAttempt(false, false, 0);
        }

        CameraSpawnShortcut stored = observation.Stored;
        progress?.Report(new MacroProgress(
            "Camera shortcut",
            14,
            $"Recognized the learned spawn view ({observation.AtlasScore:P0}). Applying one cached {stored.DirectDragPixels:+#;-#;0}-px mouse drag."));
        if (stored.DirectDragPixels != 0)
        {
            await automation.DragCameraAsync(
                window,
                stored.DirectDragPixels,
                0,
                model.Manifest.FineStepPixels,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        double confidence = await verify(cancellationToken).ConfigureAwait(false);
        if (confidence >= model.Manifest.SuccessThreshold)
        {
            CameraSpawnShortcut verified = stored with
            {
                MatchingObservations = Math.Min(100000, stored.MatchingObservations + 1),
                VerifiedUses = Math.Min(100000, stored.VerifiedUses + 1),
                ConsecutiveFailures = 0,
                LastVerifiedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await SaveSafelyAsync(verified, progress, cancellationToken).ConfigureAwait(false);
            progress?.Report(new MacroProgress(
                "Camera shortcut",
                100,
                $"Learned spawn shortcut aligned the camera with one drag at {confidence:P0} confidence.",
                Confidence: confidence));
            return new CameraSpawnShortcutAttempt(true, true, confidence);
        }

        ImageFrame current = await CurrentThumbnailAsync(window, model, observation.Fingerprint.Width, cancellationToken).ConfigureAwait(false);
        int residual = ResidualCorrectionPixels(model, current, observation.MousePixelsPerArrowStep);
        int adjustedDrag = ClampDrag(stored.DirectDragPixels + residual, model, observation.MousePixelsPerArrowStep);
        int failures = stored.ConsecutiveFailures + 1;
        bool relearning = failures >= FailuresBeforeRelearning;
        CameraSpawnShortcut failed = stored with
        {
            DirectDragPixels = adjustedDrag,
            MatchingObservations = relearning ? 1 : stored.MatchingObservations,
            VerifiedUses = relearning ? 0 : stored.VerifiedUses,
            ConsecutiveFailures = relearning ? 0 : failures,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await SaveSafelyAsync(failed, progress, cancellationToken).ConfigureAwait(false);
        string adjustment = residual == 0 ? string.Empty : $" The next learned attempt was adjusted by {residual:+#;-#;0} px.";
        progress?.Report(new MacroProgress(
            "Camera shortcut",
            18,
            $"Learned spawn shortcut reached {confidence:P0}, below {model.Manifest.SuccessThreshold:P0}; normal atlas alignment is taking over.{adjustment}",
            Confidence: confidence));
        return new CameraSpawnShortcutAttempt(true, false, confidence);
    }

    public async Task RecordNormalSuccessAsync(
        CameraModel model,
        CameraSpawnShortcutObservation? observation,
        bool shortcutAttempted,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (observation is null || shortcutAttempted || observation.StoredMatches && observation.ShouldAttempt) return;

        CameraSpawnShortcut shortcut;
        if (observation.Stored is null)
        {
            shortcut = Create(model, observation);
        }
        else if (observation.StoredMatches)
        {
            CameraSpawnShortcut stored = observation.Stored;
            int observations = Math.Min(100000, stored.MatchingObservations + 1);
            int averagedDrag = (int)Math.Round(
                (stored.DirectDragPixels * (double)stored.MatchingObservations + observation.EstimatedDragPixels) /
                observations);
            shortcut = stored with
            {
                DirectDragPixels = ClampDrag(averagedDrag, model, observation.MousePixelsPerArrowStep),
                MatchingObservations = observations,
                ConsecutiveFailures = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }
        else
        {
            return;
        }

        await SaveSafelyAsync(shortcut, progress, cancellationToken).ConfigureAwait(false);
        int remaining = Math.Max(0, RequiredMatchingObservations - shortcut.MatchingObservations);
        string message = remaining == 0
            ? "Spawn shortcut learned. The next matching load can try one direct mouse drag before atlas alignment."
            : $"Learning the repeatable spawn path: {shortcut.MatchingObservations}/{RequiredMatchingObservations} matching runs observed.";
        progress?.Report(new MacroProgress("Camera shortcut", 100, message));
    }

    private static CameraSpawnShortcut Create(CameraModel model, CameraSpawnShortcutObservation observation) => new()
    {
        CameraModelId = model.Manifest.Id,
        CameraModelCreatedAt = model.Manifest.CreatedAt,
        ClientWidth = model.Manifest.ClientWidth,
        ClientHeight = model.Manifest.ClientHeight,
        FingerprintWidth = observation.Fingerprint.Width,
        FingerprintHeight = observation.Fingerprint.Height,
        FingerprintPixels = (byte[])observation.Fingerprint.Pixels.Clone(),
        SpawnAtlasIndex = observation.AtlasIndex,
        DirectDragPixels = observation.EstimatedDragPixels,
        MousePixelsPerArrowStep = observation.MousePixelsPerArrowStep,
        MatchingObservations = 1,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static bool Compatible(CameraSpawnShortcut shortcut, CameraModel model) =>
        shortcut.CameraModelId == model.Manifest.Id &&
        shortcut.CameraModelCreatedAt == model.Manifest.CreatedAt &&
        shortcut.ClientWidth == model.Manifest.ClientWidth &&
        shortcut.ClientHeight == model.Manifest.ClientHeight &&
        shortcut.SpawnAtlasIndex < model.Manifest.FullYawSteps;

    private static int? EstimateMousePixelsPerArrowStep(CameraModel model)
    {
        (int Offset, double Score) right = BestFineMatch(model, model.YawAtlas[1], registered: false);
        (int Offset, double Score) left = BestFineMatch(model, model.YawAtlas[model.Manifest.FullYawSteps - 1], registered: false);
        List<int> estimates = [];
        if (right.Score >= StepCalibrationThreshold && right.Offset != 0) estimates.Add(right.Offset);
        if (left.Score >= StepCalibrationThreshold && left.Offset != 0) estimates.Add(-left.Offset);
        if (estimates.Count == 0 || estimates.Any(value => Math.Sign(value) != Math.Sign(estimates[0]))) return null;
        int result = (int)Math.Round(estimates.Average());
        return result == 0 ? null : result;
    }

    private static int ResidualCorrectionPixels(CameraModel model, ImageFrame current, int mousePixelsPerArrowStep)
    {
        (int Offset, double Score) fine = BestFineMatch(model, current);
        if (fine.Score >= FineResidualThreshold) return -fine.Offset;
        (int Index, _) = BestAtlasMatch(model.YawAtlas, current);
        return CorrectionPixels(Index, model.Manifest.FullYawSteps, mousePixelsPerArrowStep);
    }

    private static int CorrectionPixels(int atlasIndex, int fullTurn, int mousePixelsPerArrowStep) =>
        atlasIndex <= fullTurn / 2
            ? -atlasIndex * mousePixelsPerArrowStep
            : (fullTurn - atlasIndex) * mousePixelsPerArrowStep;

    private static int ClampDrag(int pixels, CameraModel model, int mousePixelsPerArrowStep)
    {
        int maximum = Math.Abs(mousePixelsPerArrowStep) * (model.Manifest.FullYawSteps / 2 + 1) + model.Manifest.FineSearchPixels * 2;
        return Math.Clamp(pixels, -maximum, maximum);
    }

    private static int CircularDistance(int left, int right, int fullTurn)
    {
        int difference = Math.Abs(left - right);
        return Math.Min(difference, fullTurn - difference);
    }

    private static (int Index, double Score) BestAtlasMatch(IReadOnlyList<ImageFrame> atlas, ImageFrame current)
    {
        int bestIndex = 0;
        double bestScore = double.NegativeInfinity;
        for (int index = 0; index < atlas.Count - 1; index++)
        {
            double score = CameraRegisteredScorer.Score(atlas[index], current).Score;
            if (score <= bestScore) continue;
            bestIndex = index;
            bestScore = score;
        }
        return (bestIndex, bestScore);
    }

    private static (int Offset, double Score) BestFineMatch(CameraModel model, ImageFrame current, bool registered = true)
    {
        int bestIndex = 0;
        double bestScore = double.NegativeInfinity;
        for (int index = 0; index < model.FineYawAtlas.Count; index++)
        {
            double score = registered
                ? CameraRegisteredScorer.Score(model.FineYawAtlas[index], current).Score
                : VisionScorer.RobustSimilarity(model.FineYawAtlas[index], current);
            if (score <= bestScore) continue;
            bestIndex = index;
            bestScore = score;
        }
        return (model.Manifest.FineYawOffsets[bestIndex], bestScore);
    }

    private async Task<ImageFrame> CurrentThumbnailAsync(
        RobloxWindow window,
        CameraModel model,
        int width,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < 2; index++)
        {
            frames.Add(CameraRegionAnalyzer.BuildComposite(automation.CaptureClient(window), model.Manifest.Regions));
            if (index == 0) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        return VisionScorer.MakeThumbnail(VisionScorer.Median(frames), width);
    }

    private async Task<CameraSpawnShortcut?> LoadSafelyAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            return await repository.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return null;
        }
    }

    private async Task SaveSafelyAsync(
        CameraSpawnShortcut shortcut,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveAsync(shortcut, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            progress?.Report(new MacroProgress("Camera shortcut", 100, $"Could not save the optional spawn shortcut: {error.Message}"));
        }
    }

    private async Task DeleteSafelyAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A stale optional shortcut must never prevent normal alignment.
        }
    }
}
